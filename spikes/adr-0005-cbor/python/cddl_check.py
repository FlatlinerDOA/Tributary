"""Validates fixtures/cddl.json instances with pycddl."""
import importlib.metadata
import json
import sys
from pathlib import Path

import pycddl

ROOT = Path(__file__).resolve().parent.parent


def validate(schema: str, hex_: str) -> str:
    try:
        pycddl.Schema(schema).validate_cbor(bytes.fromhex(hex_))
        return "valid"
    except Exception as e:
        return f"invalid: {type(e).__name__}: {e}"


def main(out_dir: Path) -> None:
    cases = json.loads((ROOT / "fixtures/cddl.json").read_text())
    result = {
        "impl": "python pycddl",
        "version": importlib.metadata.version("pycddl"),
        "validate": {i["name"]: {s: validate(cddl, i["hex"]) for s, cddl in cases["schemas"].items()} for i in cases["instances"]},
        "probes": {p["name"]: validate(p["schema"], p["hex"]) for p in cases["probes"]},
    }
    (out_dir / "python-pycddl.json").write_text(json.dumps(result, indent=1))


if __name__ == "__main__":
    main(Path(sys.argv[1]))
