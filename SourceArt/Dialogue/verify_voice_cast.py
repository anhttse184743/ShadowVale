"""Read-only cross-map voice gate. Run from any directory with standard Python."""
from pathlib import Path
import hashlib
import json
import re
import wave

ROOT = Path(__file__).resolve().parents[2]


def load(path):
    return json.loads((ROOT / path).read_text(encoding="utf-8-sig"))


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def verify():
    policy = {}
    for role in load("Assets/Resources/Dialogue/voice-cast.json")["roles"]:
        for name in [role["speaker"], *role.get("aliases", [])]:
            assert name not in policy, "Duplicate role alias: " + name
            policy[name] = role
    bank = load("Assets/Resources/Dialogue/voice-catalog.json")
    assert bank == load("SourceArt/Dialogue/voice-catalog.json"), "Source/runtime catalogs differ"
    rows = bank["entries"]
    by_id = {r["id"]: r for r in rows}
    assert len(by_id) == len(rows), "Duplicate voice ids"
    manifest = load("SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json")
    manifest_rows = {r["id"]: r for r in manifest["lines"]}
    assert set(manifest_rows) == set(by_id), "Active manifest and catalogs contain different dialogue ids"
    assert manifest["cues"] == len(rows)
    root = ROOT / "Assets/Resources"

    def path(row):
        return root / (row["resource"] + ".wav")

    for row in rows:
        for field in ["speaker", "voice", "provider", "resource", "sha256", "duration", "radio"]:
            assert manifest_rows[row["id"]].get(field) == row.get(field), row["id"] + ": active manifest drift: " + field
        role = policy.get(row.get("speaker"))
        assert role is not None, row["id"] + ": unknown or missing speaker"
        assert (row.get("voice"), row.get("provider")) == (role["voice"], role["provider"]), row["id"] + ": wrong cast"
        assert row["sha256"] == sha(path(row)), row["id"] + ": audio changed without verified metadata"
        with wave.open(str(path(row))) as stream:
            assert (stream.getframerate(), stream.getnchannels()) == (48000, 1), row["id"]
            assert abs(stream.getnframes() / 48000 - row["duration"]) < .035, row["id"]
        source = row.get("derivedFrom")
        if isinstance(source, dict) and source.get("id") in by_id:
            parent = by_id[source["id"]]
            assert source["sha256"] == parent["sha256"], row["id"] + ": rebuild after changing its source"
            assert policy[parent["speaker"]] is role, row["id"] + ": derived from another character"

    spoken = 0
    for script in ["Assets/_Project/Map01/Resources/Story/Map02Chapter.json", "Assets/_Project/Map01/Resources/Dialogue/Map02Story/script.json"]:
        for line in load(script)["entries"]:
            if not line.get("text", "").strip():
                continue
            recorded = by_id[line.get("recording") or line["id"]]
            caption = line["speaker"] + (" (bộ đàm): " if line.get("radio") else ": ") + line["text"]
            assert caption == recorded["caption"] and bool(line.get("radio")) == bool(recorded.get("radio")), line["id"]
            assert policy[line["speaker"]] is policy[recorded["speaker"]], line["id"]
            spoken += 1

    for direct, id in [("OperationsAudio/night2_patrol_warning", "m1_ops_enemy_spotted"),
                       ("OperationsAudio/night2_patrol_fire_order", "m1_ops_enemy_order"),
                       ("Map02DayOne/hung_invitation", "m2v3_promise_05"),
                       ("Dialogue/VI/m1_open_reply", "free_literal_7be0a5941d33")]:
        assert sha(root / (direct + ".wav")) == sha(path(by_id[id])), direct + ": stale direct audio copy"

    # The opening is a direct prefab clip, concatenated from three catalog commander lines.
    source = (ROOT / "Assets/_Project/Map01/Runtime/Map01OpeningCutscene.cs").read_text(encoding="utf-8-sig")
    captions = list(dict.fromkeys(re.findall(r'"(Chỉ huy: [^"\n]+)"', source)))
    assert len(captions) == 3
    parts = []
    for caption in captions:
        row = next(r for r in rows if r["caption"] == caption)
        with wave.open(str(path(row))) as stream:
            parts.extend([stream.readframes(stream.getnframes()), b"\x00\x00" * 12000])
    with wave.open(str(root / "Dialogue/VI/m1_open_commander.wav")) as stream:
        assert stream.readframes(stream.getnframes()) == b"".join(parts), "Stale opening commander voice"
    return {"recordings": len(rows), "scriptLines": spoken, "rolesIncludingAliases": len(policy), "result": "Passed"}


if __name__ == "__main__":
    print(json.dumps(verify(), indent=2))
