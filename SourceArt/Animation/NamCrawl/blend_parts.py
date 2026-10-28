"""Store large Blender sources in Git without LFS. Run restore after checkout."""
import argparse
import hashlib
import json
from pathlib import Path
import tempfile

ROOT = Path(__file__).resolve().parent
PARTS = ROOT / "BlendParts"
FILES = ("Nam_Crawl_Mixamo_Review_v4.blend", "Nam_Crawl_Mixamo_Review_v4.blend1")
CHUNK = 50 * 1024 * 1024


def digest(path):
    h = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            h.update(block)
    return h.hexdigest()


def split():
    PARTS.mkdir(exist_ok=True)
    for name in FILES:
        source = ROOT / name
        entries = []
        with source.open("rb") as stream:
            index = 1
            for block in iter(lambda: stream.read(CHUNK), b""):
                part = PARTS / f"{name}.part{index:03d}"
                part.write_bytes(block)
                entries.append({"name": part.name, "size": len(block),
                                "sha256": hashlib.sha256(block).hexdigest()})
                index += 1
        manifest = {"file": name, "size": source.stat().st_size,
                    "sha256": digest(source), "parts": entries}
        (PARTS / f"{name}.json").write_text(
            json.dumps(manifest, indent=2) + "\n", encoding="utf-8")
        print(f"Split {name}: {len(entries)} parts")


def restore(verify_only):
    for name in FILES:
        manifest = json.loads((PARTS / f"{name}.json").read_text(encoding="utf-8"))
        # Assemble beside the target, then replace atomically only after verification.
        with tempfile.NamedTemporaryFile(dir=ROOT, suffix=".assembling", delete=False) as out:
            temporary = Path(out.name)
            try:
                for entry in manifest["parts"]:
                    part = PARTS / entry["name"]
                    if part.parent != PARTS or part.stat().st_size != entry["size"]:
                        raise ValueError(f"Invalid part: {part}")
                    if digest(part) != entry["sha256"]:
                        raise ValueError(f"Corrupt part: {part}")
                    with part.open("rb") as stream:
                        for block in iter(lambda: stream.read(1024 * 1024), b""):
                            out.write(block)
            except BaseException:
                out.close()
                temporary.unlink(missing_ok=True)
                raise
        try:
            if temporary.stat().st_size != manifest["size"] or digest(temporary) != manifest["sha256"]:
                raise ValueError(f"Reassembled file failed verification: {name}")
            target = ROOT / name
            if verify_only:
                if target.exists() and digest(target) != manifest["sha256"]:
                    raise ValueError(f"Local source differs: {name}")
                print(f"Verified {name}: all parts reconstruct the original SHA-256")
            elif target.exists():
                if digest(target) != manifest["sha256"]:
                    raise ValueError(f"Refusing to overwrite modified {name}; move it aside first")
                print(f"Already restored: {name}")
            else:
                temporary.replace(target)
                print(f"Restored {name}")
        finally:
            temporary.unlink(missing_ok=True)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=("split", "restore", "verify"))
    args = parser.parse_args()
    if args.action == "split":
        split()
    else:
        restore(args.action == "verify")
