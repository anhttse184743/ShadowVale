"""Rebuild only Hùng's remembered line from his existing Anh Khôi recording. No TTS/network."""
import array
import hashlib
import json
import math
from pathlib import Path
import sys
import wave

ROOT = Path(__file__).resolve().parents[3]
TARGET_ID = "m2v3_echo_promise"
SOURCE_ID = "m2v3_sacrifice_02"
RATE = 48000


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def save(path, data):
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resource(row):
    return ROOT / "Assets/Resources" / (row["resource"] + ".wav")


def main():
    catalog_path = ROOT / "Assets/Resources/Dialogue/voice-catalog.json"
    bank = load(catalog_path)
    assert bank == load(ROOT / "SourceArt/Dialogue/voice-catalog.json")
    lookup = {row["id"]: row for row in bank["entries"]}
    source, target = lookup[SOURCE_ID], lookup[TARGET_ID]
    cast = next(r for r in load(ROOT / "Assets/Resources/Dialogue/voice-cast.json")["roles"] if r["speaker"] == "Hùng")
    assert source["speaker"] == cast["speaker"] and source["voice"] == cast["voice"] == "Anh Khôi"
    assert source["provider"] == cast["provider"] and source["caption"] == target["caption"]
    source_path, target_path = resource(source), resource(target)
    assert sha(source_path) == source["sha256"]
    untouched = {r["id"]: sha(resource(r)) for r in bank["entries"] if r["id"] != TARGET_ID}
    before_hash = sha(target_path)
    with wave.open(str(source_path), "rb") as stream:
        assert (stream.getframerate(), stream.getnchannels(), stream.getsampwidth()) == (RATE, 1, 2)
        samples = array.array("h", stream.readframes(stream.getnframes()))
    if sys.byteorder != "little":
        samples.byteswap()
    # Keep the complete, exact-caption take. Never crop it at an old voice's word boundary.
    dry = [s / 32768 for s in samples]
    source_fade_samples = min(len(dry), int(.12 * RATE))
    for i in range(source_fade_samples):
        dry[len(dry) - source_fade_samples + i] *= 1 - i / (source_fade_samples - 1)
    # Preserve the original 4.65 s cue window, with room for both echoes and the fade-out.
    length = max(round(4.65 * RATE), len(dry) + round(.6 * RATE))
    dry.extend([0.] * (length - len(dry)))
    wet = dry.copy()
    for delay, gain in [(.19, .32), (.38, .16)]:
        offset = round(delay * RATE)
        for i in range(offset, length):
            wet[i] += dry[i - offset] * gain
    alpha, previous = math.exp(-2 * math.pi * 6000 / RATE), 0.
    for i, value in enumerate(wet):
        previous = (1 - alpha) * value + alpha * previous
        wet[i] = previous
    fade = round(.6 * RATE)
    for i in range(fade):
        wet[length - fade + i] *= 1 - i / (fade - 1)
    gain = 10 ** (-3 / 20) / max(abs(s) for s in wet)
    encoded = array.array("h", (round(s * gain * 32767) for s in wet))
    if sys.byteorder != "little":
        encoded.byteswap()
    with wave.open(str(target_path), "wb") as stream:
        stream.setnchannels(1)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(encoded.tobytes())
    measured_peak = max(abs(s) for s in wet) * gain
    measured_rms = math.sqrt(sum((s * gain) ** 2 for s in wet) / length)
    provenance = {"id": SOURCE_ID, "resource": source["resource"], "sha256": sha(source_path), "fullTake": True}
    target.update({key: source[key] for key in ["speaker", "text", "voice", "provider", "model", "license", "source", "voiceRevision", "castingApproved", "castingApproval"]})
    target.update(language="vi", radio=False, duration=length / RATE, sha256=sha(target_path),
                  sampleRate=RATE, channels=1, cost=0, userApproved=False,
                  status="derived_existing_approved_cast_pending_line_review", integrationRevision="HungEcho_20261101",
                  derivedFrom=provenance,
                  processing="Full Anh Khoi take; 0.12 s source fade; 0.19/0.38 s echoes at 0.32/0.16; 6 kHz lowpass; 0.6 s tail fade; -3 dBFS peak")
    save(catalog_path, bank)
    save(ROOT / "SourceArt/Dialogue/voice-catalog.json", bank)
    manifest_path = ROOT / "SourceArt/Dialogue/FullGameFree20261023/integrated-manifest.json"
    manifest = load(manifest_path)
    for row in manifest["lines"]:
        if row["id"] == TARGET_ID:
            row.clear()
            row.update(target, peak=measured_peak, rms=measured_rms)
    # Keep the active cross-map manifest complete when a later dialogue batch added a cue.
    manifest_ids = {r["id"] for r in manifest["lines"]}
    for row in bank["entries"]:
        if row["id"] not in manifest_ids:
            with wave.open(str(resource(row)), "rb") as stream:
                extra = array.array("h", stream.readframes(stream.getnframes()))
            if sys.byteorder != "little":
                extra.byteswap()
            manifest["lines"].append(dict(row, peak=max(abs(s) for s in extra) / 32768,
                                          rms=math.sqrt(sum((s / 32768) ** 2 for s in extra) / len(extra))))
    manifest["cues"] = len(manifest["lines"])
    save(manifest_path, manifest)
    legacy_path = ROOT / "SourceArt/Dialogue/Map02DayOneFree/voice-manifest.json"
    legacy = load(legacy_path)
    row = next(r for r in legacy["derived"] if r["id"] == TARGET_ID)
    row.update(target, file=str(target_path.relative_to(ROOT)).replace("\\", "/"))
    save(legacy_path, legacy)
    built_path = ROOT / "SourceArt/Audio/Map02_DayOne_20261025/built.json"
    built = load(built_path)
    next(r for r in built if r["file"] == str(target_path.relative_to(ROOT)).replace("\\", "/")).update(
        seconds=round(length / RATE, 3), sha256=target["sha256"])
    built_path.write_text(json.dumps(built, ensure_ascii=False, indent=1) + "\n", encoding="utf-8")
    assert all(sha(resource(lookup[id])) == digest for id, digest in untouched.items())
    receipt_path = Path(__file__).with_name("integration.json")
    original_hash = load(receipt_path).get("beforeSha256", before_hash) if receipt_path.exists() else before_hash
    save(receipt_path, {
        "id": TARGET_ID, "voice": target["voice"], "beforeSha256": original_hash,
        "afterSha256": target["sha256"], "derivedFrom": provenance, "duration": length / RATE,
        "unchangedOtherRecordings": len(untouched), "cost": 0, "userApproved": False})
    print("Rebuilt Hùng echo from Anh Khôi; 272 other recordings unchanged.")


if __name__ == "__main__":
    main()
