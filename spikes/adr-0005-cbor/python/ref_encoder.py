"""Reference encoder for the fixture format: CDE (draft-ietf-cbor-cde-09) and dCBOR
(draft-mcnally-deterministic-cbor-12), written directly from the drafts as a test oracle."""
import math
import struct
import unicodedata


class EncodeError(Exception):
    pass


def head(major: int, arg: int) -> bytes:
    if arg < 24:
        return bytes([major << 5 | arg])
    for ai, size in ((24, 1), (25, 2), (26, 4), (27, 8)):
        if arg < 1 << (8 * size):
            return bytes([major << 5 | ai]) + arg.to_bytes(size, "big")
    raise EncodeError("argument exceeds 64 bits")


def encode_int(v: int, profile: str) -> bytes:
    if profile == "dcbor" and v < -(1 << 63):
        raise EncodeError("dCBOR: negative integer below -2^63")
    if not -(1 << 64) <= v < 1 << 64:
        raise EncodeError("integer outside the 65-bit CBOR range")
    return head(0, v) if v >= 0 else head(1, -1 - v)


def encode_nan(bits: int) -> bytes:
    sign, mant = bits >> 63, bits & ((1 << 52) - 1)
    if mant & ((1 << 42) - 1) == 0:
        return b"\xf9" + (sign << 15 | 0x7C00 | mant >> 42).to_bytes(2, "big")
    if mant & ((1 << 29) - 1) == 0:
        return b"\xfa" + (sign << 31 | 0x7F800000 | mant >> 29).to_bytes(4, "big")
    return b"\xfb" + bits.to_bytes(8, "big")


def encode_float(bits: int, profile: str) -> bytes:
    x = struct.unpack(">d", bits.to_bytes(8, "big"))[0]
    if x != x:
        return b"\xf9\x7e\x00" if profile == "dcbor" else encode_nan(bits)
    if profile == "dcbor" and math.isfinite(x) and x == int(x) and -(1 << 63) <= int(x) <= (1 << 64) - 1:
        return encode_int(int(x), profile)
    for prefix, fmt in ((b"\xf9", ">e"), (b"\xfa", ">f")):
        try:
            packed = struct.pack(fmt, x)
        except (OverflowError, struct.error):
            continue
        if struct.pack(">d", struct.unpack(fmt, packed)[0]) == bits.to_bytes(8, "big"):
            return prefix + packed
    return b"\xfb" + bits.to_bytes(8, "big")


def encode(node: dict, profile: str) -> bytes:
    (kind, value), = [(k, v) for k, v in node.items() if k != "item"] or [("tag", None)]
    if kind == "int":
        return encode_int(int(value), profile)
    if kind == "float":
        return encode_float(int(value, 16), profile)
    if kind == "text":
        if profile == "dcbor" and not unicodedata.is_normalized("NFC", value):
            raise EncodeError("dCBOR: text is not NFC")
        b = value.encode("utf-8")
        return head(3, len(b)) + b
    if kind == "bytes":
        b = bytes.fromhex(value)
        return head(2, len(b)) + b
    if kind == "bool":
        return b"\xf5" if value else b"\xf4"
    if kind == "null":
        return b"\xf6"
    if kind == "array":
        return head(4, len(value)) + b"".join(encode(x, profile) for x in value)
    if kind == "map":
        entries = sorted((encode(k, profile), encode(v, profile)) for k, v in value)
        if len({k for k, _ in entries}) != len(entries):
            raise EncodeError("duplicate map key")
        return head(5, len(entries)) + b"".join(k + v for k, v in entries)
    if kind == "tag":
        return head(6, int(node["tag"])) + encode(node["item"], profile)
    raise EncodeError(f"unknown node kind {kind}")


def try_encode(node: dict, profile: str) -> str:
    try:
        return encode(node, profile).hex()
    except EncodeError as e:
        return f"error: {e}"
