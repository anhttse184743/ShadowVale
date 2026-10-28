"""Prepare exact chapter lines without generating audio or changing gameplay text."""
from pathlib import Path
import json

HERE = Path(__file__).resolve().parent
ROOT = HERE.parents[2]

def prepare():
    chapter = json.loads((ROOT / "Assets/_Project/Map01/Resources/Story/Map02Chapter.json").read_text(encoding="utf-8"))
    cast = json.loads((HERE / "cast.json").read_text(encoding="utf-8"))
    actors = {e["speaker"]: e for e in cast["characters"]}
    selected = {"m2v3_" + t["scene"] for t in chapter["tasks"] if t["phase"] in (2, 3) and t.get("scene")}
    catalog = json.loads((ROOT / "Assets/Resources/Dialogue/voice-catalog.json").read_text(encoding="utf-8"))
    known = {e["id"]: e for e in catalog["entries"]}
    entries = []
    for line in chapter["entries"]:
        if line["conversation"] not in selected:
            continue
        actor = actors[line["speaker"]]
        row = dict(line)
        row.update(age=actor.get("age"), voice=actor.get("voice"),
                   accentPreference=actor.get("accentPreference", actor.get("accent")),
                   caption=line["speaker"] + (" (bộ đàm): " if line.get("radio") else ": ") + line["text"],
                   rawFile="Raw/" + line["id"] + ".wav",
                   status="existing_recording" if line["id"] in known else "not_generated")
        entries.append(row)
    assert len({e["id"] for e in entries}) == len(entries)
    manifest = {"scope": "Map 2 Day 2 / Night 2", "language": "vi", "entries": entries}
    (HERE / "voice-manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(f"Prepared {len(entries)} lines; {sum(e['radio'] for e in entries)} radio lines. No audio generated.")
    return manifest

if __name__ == "__main__":
    prepare()
