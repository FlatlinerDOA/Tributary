"""Encode the fixtures and run the decode cases through cbor2; also emit the reference encoder's results."""
import importlib.metadata
import json
import struct
import sys
from pathlib import Path

import cbor2

from ref_encoder import try_encode

ROOT = Path(__file__).resolve().parent.parent


def to_python(node: dict):
    kind = next(k for k in node if k != "item")
    value = node[kind]
    return {
        "int": lambda: int(value),
        "float": lambda: struct.unpack(">d", bytes.fromhex(value))[0],
        "text": lambda: value,
        "bytes": lambda: bytes.fromhex(value),
        "bool": lambda: value,
        "null": lambda: None,
        "array": lambda: [to_python(x) for x in value],
        "map": lambda: {to_python(k): to_python(v) for k, v in value},
        "tag": lambda: cbor2.CBORTag(int(value), to_python(node["item"])),
    }[kind]()


def attempt(f) -> str:
    try:
        return f()
    except Exception as e:  # recorded as data, not handled
        return f"error: {type(e).__name__}: {e}"


def decode(hex_: str, **options) -> str:
    try:
        cbor2.loads(bytes.fromhex(hex_), **options)
        return "accept"
    except Exception as e:
        return f"reject: {type(e).__name__}: {e}"


def main(out_dir: Path) -> None:
    fixtures = json.loads((ROOT / "fixtures/encode.json").read_text())["fixtures"]
    cases = json.loads((ROOT / "fixtures/decode.json").read_text())["cases"]
    cbor2_result = {
        "impl": "python cbor2",
        "version": importlib.metadata.version("cbor2"),
        "encode": {f["name"]: {
            "canonical=True": attempt(lambda: cbor2.dumps(to_python(f["value"]), canonical=True).hex()),
            "default": attempt(lambda: cbor2.dumps(to_python(f["value"])).hex()),
        } for f in fixtures},
        "decode": {c["name"]: {
            "loads": decode(c["hex"]),
            "loads strict": decode(c["hex"], allow_indefinite=False, allow_duplicate_keys=False),
        } for c in cases},
    }
    reference = {
        "impl": "reference encoder",
        "version": "ref_encoder.py",
        "encode": {f["name"]: {p: try_encode(f["value"], p) for p in ("cde", "dcbor")} for f in fixtures},
        "decode": {},
    }
    (out_dir / "python-cbor2.json").write_text(json.dumps(cbor2_result, indent=1))
    (out_dir / "reference.json").write_text(json.dumps(reference, indent=1))


if __name__ == "__main__":
    main(Path(sys.argv[1]))
