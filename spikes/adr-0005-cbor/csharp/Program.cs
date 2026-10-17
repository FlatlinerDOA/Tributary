using System.Formats.Cbor;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;

var root = args.Length > 0 ? args[0] : "..";
var fixtures = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "fixtures/encode.json")))!["fixtures"]!.AsArray();
var cases = JsonNode.Parse(File.ReadAllText(Path.Combine(root, "fixtures/decode.json")))!["cases"]!.AsArray();

var conformanceModes = new[] { CborConformanceMode.Lax, CborConformanceMode.Strict, CborConformanceMode.Canonical, CborConformanceMode.Ctap2Canonical };
var encodeModes = conformanceModes
    .SelectMany(m => new[] { new EncodeMode($"{m}, WriteDouble", m, FloatStyle.AlwaysDouble), new EncodeMode($"{m}, shortest float", m, FloatStyle.Shortest) })
    .ToArray();
var decodeModes = conformanceModes;

var assembly = typeof(CborWriter).Assembly;
var result = new JsonObject
{
    ["impl"] = "dotnet System.Formats.Cbor",
    ["version"] = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? assembly.GetName().Version!.ToString(),
    ["encode"] = new JsonObject(fixtures.Select(f => KeyValuePair.Create(
        (string)f!["name"]!,
        (JsonNode?)new JsonObject(encodeModes.Select(m => KeyValuePair.Create(m.Name, (JsonNode?)Encode(f["value"]!, m))))))),
    ["decode"] = new JsonObject(cases.Select(c => KeyValuePair.Create(
        (string)c!["name"]!,
        (JsonNode?)new JsonObject(decodeModes.Select(m => KeyValuePair.Create(m.ToString(), (JsonNode?)Decode(Convert.FromHexString((string)c["hex"]!), m))))))),
};
File.WriteAllText(Path.Combine(root, "results/dotnet-system-formats-cbor.json"), result.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

static string Encode(JsonNode node, EncodeMode mode)
{
    try
    {
        var writer = new CborWriter(mode.Conformance, convertIndefiniteLengthEncodings: false);
        Write(writer, node, mode.Floats);
        return Convert.ToHexStringLower(writer.Encode());
    }
    catch (Exception e)
    {
        return $"error: {e.GetType().Name}: {e.Message}";
    }
}

static void Write(CborWriter w, JsonNode node, FloatStyle floats)
{
    var (kind, value) = node.AsObject().Where(p => p.Key != "item").Select(p => (p.Key, p.Value)).Single();
    switch (kind)
    {
        case "int": WriteInteger(w, BigInteger.Parse((string)value!)); break;
        case "float": WriteFloat(w, BitConverter.Int64BitsToDouble((long)Convert.ToUInt64((string)value!, 16)), floats); break;
        case "text": w.WriteTextString((string)value!); break;
        case "bytes": w.WriteByteString(Convert.FromHexString((string)value!)); break;
        case "bool": w.WriteBoolean((bool)value!); break;
        case "null": w.WriteNull(); break;
        case "array":
            var items = value!.AsArray();
            w.WriteStartArray(items.Count);
            foreach (var item in items) Write(w, item!, floats);
            w.WriteEndArray();
            break;
        case "map":
            var entries = value!.AsArray();
            w.WriteStartMap(entries.Count);
            foreach (var entry in entries) { Write(w, entry![0]!, floats); Write(w, entry[1]!, floats); }
            w.WriteEndMap();
            break;
        case "tag":
            w.WriteTag((CborTag)ulong.Parse((string)value!));
            Write(w, node["item"]!, floats);
            break;
        default: throw new InvalidDataException(kind);
    }
}

static void WriteInteger(CborWriter w, BigInteger v)
{
    if (v >= 0) w.WriteUInt64((ulong)v);
    else if (v >= long.MinValue) w.WriteInt64((long)v);
    else w.WriteCborNegativeIntegerRepresentation((ulong)(-1 - v));
}

static void WriteFloat(CborWriter w, double x, FloatStyle style)
{
    var bits = BitConverter.DoubleToInt64Bits(x);
    if (style == FloatStyle.Shortest && BitConverter.DoubleToInt64Bits((double)(Half)x) == bits) w.WriteHalf((Half)x);
    else if (style == FloatStyle.Shortest && BitConverter.DoubleToInt64Bits((float)x) == bits) w.WriteSingle((float)x);
    else w.WriteDouble(x);
}

static string Decode(byte[] data, CborConformanceMode mode)
{
    try
    {
        var reader = new CborReader(data, mode);
        Read(reader);
        return reader.BytesRemaining == 0 ? "accept" : $"reject: {reader.BytesRemaining} trailing bytes";
    }
    catch (Exception e)
    {
        return $"reject: {e.GetType().Name}: {e.Message}";
    }
}

static void Read(CborReader r)
{
    switch (r.PeekState())
    {
        case CborReaderState.UnsignedInteger: r.ReadUInt64(); break;
        case CborReaderState.NegativeInteger: r.ReadCborNegativeIntegerRepresentation(); break;
        case CborReaderState.ByteString or CborReaderState.StartIndefiniteLengthByteString: r.ReadByteString(); break;
        case CborReaderState.TextString or CborReaderState.StartIndefiniteLengthTextString: r.ReadTextString(); break;
        case CborReaderState.StartArray:
            r.ReadStartArray();
            while (r.PeekState() != CborReaderState.EndArray) Read(r);
            r.ReadEndArray();
            break;
        case CborReaderState.StartMap:
            r.ReadStartMap();
            while (r.PeekState() != CborReaderState.EndMap) { Read(r); Read(r); }
            r.ReadEndMap();
            break;
        case CborReaderState.Tag: r.ReadTag(); Read(r); break;
        case CborReaderState.HalfPrecisionFloat or CborReaderState.SinglePrecisionFloat or CborReaderState.DoublePrecisionFloat: r.ReadDouble(); break;
        case CborReaderState.Boolean: r.ReadBoolean(); break;
        case CborReaderState.Null: r.ReadNull(); break;
        case CborReaderState.SimpleValue: r.ReadSimpleValue(); break;
        case var s: throw new InvalidDataException($"unexpected state {s}");
    }
}

enum FloatStyle { AlwaysDouble, Shortest }

sealed record EncodeMode(string Name, CborConformanceMode Conformance, FloatStyle Floats);
