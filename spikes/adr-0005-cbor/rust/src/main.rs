//! Encodes the fixtures and runs the decode cases through ciborium and dcbor.
use std::cmp::Ordering;
use std::panic::{catch_unwind, AssertUnwindSafe};

use ciborium::value::{CanonicalValue, Integer, Value};
use dcbor::prelude::*;
use serde_json::{json, Map as JsonMap, Value as Json};

fn node_kind(node: &Json) -> (&str, &Json) {
    node.as_object().unwrap().iter().find(|(k, _)| k.as_str() != "item").map(|(k, v)| (k.as_str(), v)).unwrap()
}

fn int_of(v: &Json) -> i128 { v.as_str().unwrap().parse().unwrap() }
fn float_of(v: &Json) -> f64 { f64::from_bits(u64::from_str_radix(v.as_str().unwrap(), 16).unwrap()) }

fn to_ciborium(node: &Json) -> Value {
    match node_kind(node) {
        ("int", v) => Value::Integer(Integer::try_from(int_of(v)).unwrap()),
        ("float", v) => Value::Float(float_of(v)),
        ("text", v) => Value::Text(v.as_str().unwrap().into()),
        ("bytes", v) => Value::Bytes(hex::decode(v.as_str().unwrap()).unwrap()),
        ("bool", v) => Value::Bool(v.as_bool().unwrap()),
        ("null", _) => Value::Null,
        ("array", v) => Value::Array(v.as_array().unwrap().iter().map(to_ciborium).collect()),
        ("map", v) => Value::Map(v.as_array().unwrap().iter().map(|e| (to_ciborium(&e[0]), to_ciborium(&e[1]))).collect()),
        ("tag", v) => Value::Tag(v.as_str().unwrap().parse().unwrap(), Box::new(to_ciborium(&node["item"]))),
        (k, _) => panic!("unknown kind {k}"),
    }
}

fn ciborium_bytes(v: &Value) -> Vec<u8> {
    let mut out = Vec::new();
    ciborium::ser::into_writer(v, &mut out).unwrap();
    out
}

fn sort_maps(v: Value, cmp: &dyn Fn(&Value, &Value) -> Ordering) -> Value {
    match v {
        Value::Array(xs) => Value::Array(xs.into_iter().map(|x| sort_maps(x, cmp)).collect()),
        Value::Tag(t, x) => Value::Tag(t, Box::new(sort_maps(*x, cmp))),
        Value::Map(entries) => {
            let mut entries: Vec<_> = entries.into_iter().map(|(k, v)| (sort_maps(k, cmp), sort_maps(v, cmp))).collect();
            entries.sort_by(|a, b| cmp(&a.0, &b.0));
            Value::Map(entries)
        }
        other => other,
    }
}

fn length_first(a: &Value, b: &Value) -> Ordering { CanonicalValue::from(a.clone()).cmp(&CanonicalValue::from(b.clone())) }
fn bytewise(a: &Value, b: &Value) -> Ordering { ciborium_bytes(a).cmp(&ciborium_bytes(b)) }

fn to_dcbor(node: &Json) -> CBOR {
    match node_kind(node) {
        ("int", v) => {
            let i = int_of(v);
            if i >= 0 { CBORCase::Unsigned(i as u64).into() } else { CBORCase::Negative((-1 - i) as u64).into() }
        }
        ("float", v) => CBOR::from(float_of(v)),
        ("text", v) => CBOR::from(v.as_str().unwrap()),
        ("bytes", v) => CBOR::to_byte_string(hex::decode(v.as_str().unwrap()).unwrap()),
        ("bool", v) => CBOR::from(v.as_bool().unwrap()),
        ("null", _) => CBOR::null(),
        ("array", v) => CBOR::from(v.as_array().unwrap().iter().map(to_dcbor).collect::<Vec<CBOR>>()),
        ("map", v) => {
            let mut map = dcbor::Map::new();
            for e in v.as_array().unwrap() {
                map.insert(to_dcbor(&e[0]), to_dcbor(&e[1]));
            }
            CBOR::from(map)
        }
        ("tag", v) => CBOR::to_tagged_value(v.as_str().unwrap().parse::<u64>().unwrap(), to_dcbor(&node["item"])),
        (k, _) => panic!("unknown kind {k}"),
    }
}

fn attempt(f: impl FnOnce() -> String) -> String {
    match catch_unwind(AssertUnwindSafe(f)) {
        Ok(s) => s,
        Err(e) => format!("error: panic: {}", e.downcast_ref::<String>().cloned().or_else(|| e.downcast_ref::<&str>().map(|s| s.to_string())).unwrap_or_default()),
    }
}

fn decode_ciborium(data: &[u8]) -> String {
    let mut rest = data;
    match ciborium::de::from_reader::<Value, _>(&mut rest) {
        Ok(_) if rest.is_empty() => "accept".into(),
        Ok(_) => format!("reject: {} trailing bytes", rest.len()),
        Err(e) => format!("reject: {e}"),
    }
}

fn decode_dcbor(data: &[u8]) -> String {
    match CBOR::try_from_data(data) {
        Ok(_) => "accept".into(),
        Err(e) => format!("reject: {e}"),
    }
}

fn main() {
    std::panic::set_hook(Box::new(|_| {}));
    let root = std::env::args().nth(1).unwrap_or("..".into());
    let read = |p: &str| -> Json { serde_json::from_str(&std::fs::read_to_string(format!("{root}/{p}")).unwrap()).unwrap() };
    let fixtures = read("fixtures/encode.json");
    let cases = read("fixtures/decode.json");

    let encode: JsonMap<String, Json> = fixtures["fixtures"].as_array().unwrap().iter().map(|f| {
        let v = &f["value"];
        (f["name"].as_str().unwrap().to_string(), json!({
            "ciborium into_writer": attempt(|| hex::encode(ciborium_bytes(&to_ciborium(v)))),
            "ciborium CanonicalValue sort": attempt(|| hex::encode(ciborium_bytes(&sort_maps(to_ciborium(v), &length_first)))),
            "ciborium bytewise sort": attempt(|| hex::encode(ciborium_bytes(&sort_maps(to_ciborium(v), &bytewise)))),
            "dcbor": attempt(|| hex::encode(to_dcbor(v).to_cbor_data())),
        }))
    }).collect();
    let decode: JsonMap<String, Json> = cases["cases"].as_array().unwrap().iter().map(|c| {
        let data = hex::decode(c["hex"].as_str().unwrap()).unwrap();
        (c["name"].as_str().unwrap().to_string(), json!({
            "ciborium from_reader": attempt(|| decode_ciborium(&data)),
            "dcbor try_from_data": attempt(|| decode_dcbor(&data)),
        }))
    }).collect();

    let cddl_cases = read("fixtures/cddl.json");
    let validate = |schema: &str, hex_: &str| attempt(|| match cddl::validate_cbor_from_slice(schema, &hex::decode(hex_).unwrap(), None) {
        Ok(()) => "valid".into(),
        Err(e) => format!("invalid: {e}"),
    });
    let schemas = cddl_cases["schemas"].as_object().unwrap();
    let cddl_result = json!({
        "impl": "rust cddl",
        "version": "",
        "validate": cddl_cases["instances"].as_array().unwrap().iter().map(|i| (i["name"].as_str().unwrap().to_string(),
            json!(schemas.iter().map(|(s, c)| (s.clone(), json!(validate(c.as_str().unwrap(), i["hex"].as_str().unwrap())))).collect::<JsonMap<_, _>>()))).collect::<JsonMap<_, _>>(),
        "probes": cddl_cases["probes"].as_array().unwrap().iter().map(|p| (p["name"].as_str().unwrap().to_string(),
            json!(validate(p["schema"].as_str().unwrap(), p["hex"].as_str().unwrap())))).collect::<JsonMap<_, _>>(),
    });

    let lock = std::fs::read_to_string(concat!(env!("CARGO_MANIFEST_DIR"), "/Cargo.lock")).unwrap_or_default();
    let version = |name: &str| lock.split("[[package]]").find(|p| p.contains(&format!("name = \"{name}\"\n"))).and_then(|p| p.lines().find(|l| l.starts_with("version"))).map(|l| l.split('"').nth(1).unwrap().to_string()).unwrap_or_default();
    let out = |impl_: &str, enc: &[&str], dec: &[&str]| json!({
        "impl": impl_,
        "version": version(impl_.split(' ').nth(1).unwrap()),
        "encode": encode.iter().map(|(k, v)| (k.clone(), json!(enc.iter().map(|m| (m.to_string(), v[*m].clone())).collect::<JsonMap<_, _>>()))).collect::<JsonMap<_, _>>(),
        "decode": decode.iter().map(|(k, v)| (k.clone(), json!(dec.iter().map(|m| (m.to_string(), v[*m].clone())).collect::<JsonMap<_, _>>()))).collect::<JsonMap<_, _>>(),
    });
    std::fs::write(format!("{root}/results/rust-ciborium.json"), serde_json::to_string_pretty(&out("rust ciborium", &["ciborium into_writer", "ciborium CanonicalValue sort", "ciborium bytewise sort"], &["ciborium from_reader"])).unwrap()).unwrap();
    let mut cddl_result = cddl_result;
    cddl_result["version"] = json!(version("cddl"));
    std::fs::write(format!("{root}/results/rust-cddl.json"), serde_json::to_string_pretty(&cddl_result).unwrap()).unwrap();
    std::fs::write(format!("{root}/results/rust-dcbor.json"), serde_json::to_string_pretty(&out("rust dcbor", &["dcbor"], &["dcbor try_from_data"])).unwrap()).unwrap();
}
