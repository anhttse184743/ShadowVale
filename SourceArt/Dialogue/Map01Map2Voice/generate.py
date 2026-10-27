# P9 (plan 2026-10-23, decided: Map 2's voices): Map 1's lines for Nam, Hùng and the commander re-voiced with the same
# VieNeu-TTS v3 Turbo presets as Map 2 (Nam = Adam, Hùng = Minh Quân, the commander = Minh Triết as Anh Thành), the same
# words and captions. Radio lines get the same band, saturation and hiss as rebuild_voice_audio.py. Free, offline, CPU.
#   <venv with vieneu==3.8.0>/python generate.py            (writes Assets/Resources/Dialogue/Map01Map2Voice + manifest)
import json, hashlib, random, re, uuid
from pathlib import Path
import numpy as np, soundfile as sf
from scipy.signal import butter, sosfilt
from vieneu import Vieneu
HERE = Path(__file__).resolve().parent; ROOT = HERE.parents[2]
CATALOG = ROOT / "Assets/Resources/Dialogue/voice-catalog.json"; OUT = ROOT / "Assets/Resources/Dialogue/Map01Map2Voice"
RATE = 48000
VOICE = {"Nam": "Adam", "Hùng": "Minh Quân", "Hùng (thì thào)": "Minh Quân", "Chỉ huy": "Minh Triết", "Chỉ huy (bộ đàm)": "Minh Triết"}
def speaker(e): return e["caption"].split(":", 1)[0].strip()
def trim(x):
    blocks = len(x) // 480; rms = np.sqrt(np.mean(x[:blocks * 480].reshape(blocks, 480) ** 2, axis=1))
    active = np.flatnonzero(rms > max(.0008, float(rms.max()) * .02)); start = max(0, (active[0] - 10) * 480); end = min(len(x), (active[-1] + 17) * 480)
    return x[start:end].copy()
def normalize(x, rms=.105):
    x = x * min(rms / max(float(np.sqrt(np.mean(x * x))), 1e-6), .78 / max(float(np.max(np.abs(x))), 1e-6))
    fade = min(720, len(x) // 2); x[:fade] *= np.linspace(0, 1, fade); x[-fade:] *= np.linspace(1, 0, fade); return x.astype(np.float32)
def radio(x):
    band = sosfilt(butter(2, 350, "highpass", fs=RATE, output="sos"), x); band = sosfilt(butter(2, 3000, "lowpass", fs=RATE, output="sos"), band)
    band = normalize(band.astype(np.float32)); band = np.tanh(band * 1.6) / 1.6
    noise = np.random.default_rng(1401).normal(0, .0026, len(band)).astype(np.float32); energy = np.convolve(np.abs(band), np.ones(480) / 480, mode="same")
    y = band + noise * np.clip(.2 + energy * 6, .2, 1); y[:720] *= np.linspace(0, 1, 720); y[-720:] *= np.linspace(1, 0, 720); return y.astype(np.float32)
def main():
    # the lines are chosen from the catalog as it was before this re-voice (git HEAD), so a re-run finds the same set
    import subprocess
    catalog = json.loads(subprocess.check_output(["git", "show", "HEAD:Assets/Resources/Dialogue/voice-catalog.json"], cwd=ROOT).decode("utf-8-sig"))
    # aliases (an exact-text reuse, e.g. Map 2's "Rõ.") take their original's new take instead of their own
    lines = sorted((e for e in catalog["entries"] if e.get("resource", "").startswith("Dialogue/VI/") and speaker(e) in VOICE and not e.get("reusedFrom")), key=lambda e: e["id"])
    OUT.mkdir(parents=True, exist_ok=True); tts = Vieneu(backend="onnx", precision="fp32", threads=4); made = []
    for i, e in enumerate(lines):
        who = speaker(e); text = e["caption"].split(":", 1)[1].strip(); seed = 3000 + i
        random.seed(seed); np.random.seed(seed)
        audio = np.asarray(tts.infer(text, voice=VOICE[who], temperature=.8), dtype=np.float32)
        if audio.size == 0 or float(np.sqrt(np.mean(audio * audio))) < .002: raise RuntimeError("silent: " + e["id"])
        dry = trim(audio); clean = normalize(dry.copy(), .06 if "thì thào" in who else .105)
        cpath = OUT / (e["id"] + "_clean.wav"); sf.write(cpath, clean, RATE, subtype="PCM_16"); path = cpath   # Nam and nearby people are heard clean
        if e.get("radio"): x = radio(dry); path = OUT / (e["id"] + "_radio.wav"); sf.write(path, x, RATE, subtype="PCM_16")
        else: x = clean
        made.append(dict(id=e["id"], speaker=who, voice=VOICE[who], seed=seed, text=text, radio=bool(e.get("radio")), caption=e["caption"],
                         file=str(path.relative_to(ROOT)).replace("\\", "/"), resource="Dialogue/Map01Map2Voice/" + path.stem, cleanResource="Dialogue/Map01Map2Voice/" + cpath.stem,
                         previous=e["resource"], previousClean=e.get("cleanResource"), sha256=hashlib.sha256(path.read_bytes()).hexdigest(), cleanSha256=hashlib.sha256(cpath.read_bytes()).hexdigest(), duration=round(len(x) / RATE, 2), status="generated_20261025_awaiting_listen"))
        print("VOICE", e["id"], who, VOICE[who], made[-1]["duration"], flush=True)
    (HERE / "voice-manifest.json").write_text(json.dumps(dict(engine="VieNeu-TTS v3 Turbo", package="vieneu 3.8.0", backend="onnx fp32", cost=0,
        source="https://huggingface.co/pnnbao-ump/VieNeu-TTS-v3-Turbo", license="Apache-2.0", decision="P9 2026-10-23: Map 1 voices follow Map 2",
        lines=made), ensure_ascii=False, indent=2), encoding="utf-8")
    print("DONE", len(made))
if __name__ == "__main__": main()
