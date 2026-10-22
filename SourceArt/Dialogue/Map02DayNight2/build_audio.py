"""Install real recordings after casting. Does not synthesize or borrow dialogue."""
from pathlib import Path
import hashlib
import json
import wave
import numpy as np
from prepare_manifest import HERE, ROOT, prepare

RATE = 48000

def write_wav(path, data):
    with wave.open(str(path), "wb") as output:
        output.setnchannels(1); output.setsampwidth(2); output.setframerate(RATE)
        output.writeframes((np.clip(data, -1, 1)*32767).astype("<i2").tobytes())

def decode(path, radio=False):
    with wave.open(str(path), "rb") as source:
        if source.getsampwidth() != 2 or source.getcomptype() != "NONE":
            raise ValueError(f"Supply a PCM16 WAV master: {path}")
        rate, channels = source.getframerate(), source.getnchannels()
        data = np.frombuffer(source.readframes(source.getnframes()), dtype="<i2").astype(np.float32)/32768
    data = data.reshape(-1, channels).mean(axis=1)
    if rate != RATE:
        data = np.interp(np.arange(round(len(data)*RATE/rate))*rate/RATE, np.arange(len(data)), data).astype(np.float32)
    if radio:
        frequencies = np.fft.rfftfreq(len(data), 1/RATE)
        response = 1/np.sqrt(1+(350/np.maximum(frequencies,1))**4)/np.sqrt(1+(frequencies/3000)**6)
        data = np.fft.irfft(np.fft.rfft(data)*response, n=len(data)).astype(np.float32)
    return data

def install():
    manifest = prepare()
    cast = {e["speaker"]: e for e in json.loads((HERE / "cast.json").read_text(encoding="utf-8"))["characters"]}
    used = {line["speaker"] for line in manifest["entries"]}
    voices = {}
    for name in used:
        actor = cast[name]
        if not actor.get("voice"):
            raise ValueError(f"Choose an actual distinct voice before recording: {name}")
        if name not in ("Nam", "Hùng") and actor.get("age") is None:
            raise ValueError(f"User must confirm age: {name}")
        if actor["voice"] in voices:
            raise ValueError(f"Voice reused by different characters: {name} / {voices[actor['voice']]}")
        voices[actor["voice"]] = name
        if name not in ("Nam", "Hùng", "Anh Thành") and actor["voice"] in ("Brodie", "Clint", "Tom", "Martin"):
            raise ValueError(f"Voice already belongs to a Map 1 character: {actor['voice']}")
    for line in manifest["entries"]:
        if not (HERE / line["rawFile"]).is_file():
            raise FileNotFoundError(HERE / line["rawFile"])
    def normalize(x):
        peak = float(np.max(np.abs(x))); rms = float(np.sqrt(np.mean(x*x)))
        if rms < .0001:
            raise ValueError("Recording is silent")
        x *= min(.105 / rms, .78 / max(peak, 1e-6))
        fade = min(720, len(x)//2)
        x[:fade] *= np.linspace(0, 1, fade); x[-fade:] *= np.linspace(1, 0, fade)
        return x
    catalog_path = ROOT / "Assets/Resources/Dialogue/voice-catalog.json"
    catalog = json.loads(catalog_path.read_text(encoding="utf-8"))
    out = ROOT / "Assets/Resources/Dialogue/VI"
    out.mkdir(parents=True, exist_ok=True)
    processed = []
    for line in manifest["entries"]:
        raw = HERE / line["rawFile"]
        dry = decode(raw)
        # Keep short breaths and pauses; trim only leading/trailing silence.
        blocks = len(dry)//480
        envelope = np.sqrt(np.mean(dry[:blocks*480].reshape(blocks, 480)**2, axis=1))
        active = np.flatnonzero(envelope > max(.0008, float(envelope.max())*.02))
        if not len(active):
            raise ValueError(f"Silent recording: {line['id']}")
        start = max(0, (int(active[0])-10)*480); end = min(len(dry), (int(active[-1])+17)*480)
        dry = normalize(dry[start:end])
        clean_name = line["id"] + "_clean"
        write_wav(out / (clean_name + ".wav"), dry)
        name = clean_name
        if line["radio"]:
            transmitted = normalize(decode(raw, True)[start:end])
            transmitted = np.tanh(transmitted*1.6)/1.6
            rng = np.random.default_rng(int(hashlib.sha256(line["id"].encode()).hexdigest()[:8],16))
            noise = rng.normal(0, .002, len(transmitted)).astype(np.float32)
            transmitted += noise
            # A short radio opening crackle; the character's voice stays the same.
            click = rng.normal(0, .012, int(RATE*.09)).astype(np.float32)
            click *= np.sin(np.linspace(0, np.pi, len(click)))
            transmitted = np.concatenate((click, np.zeros(2400,dtype=np.float32), transmitted))
            name = line["id"] + "_radio"
            write_wav(out / (name + ".wav"), transmitted)
        data, rate = decode(out / (name + ".wav")), RATE
        record = dict(line)
        record.update(language="vi", match="", resource="Dialogue/VI/"+name,
                      cleanResource="Dialogue/VI/"+clean_name, duration=round(len(data)/rate,5),
                      sampleRate=rate, channels=1, userApproved=False, status="recording_bytes_validated_awaiting_listening",
                      sourceSha256=hashlib.sha256(raw.read_bytes()).hexdigest(),
                      sha256=hashlib.sha256((out/(name+".wav")).read_bytes()).hexdigest())
        catalog["entries"] = [e for e in catalog["entries"] if e["id"] != line["id"]] + [record]
        processed.append(record)
    text = json.dumps(catalog,ensure_ascii=False,indent=2) + "\n"
    catalog_path.write_text(text,encoding="utf-8")
    (ROOT / "SourceArt/Dialogue/voice-catalog.json").write_text(text,encoding="utf-8")
    (HERE / "installed-recordings.json").write_text(json.dumps(processed,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(f"Installed {len(processed)} recordings. Listening review remains separate from byte validation.")

if __name__ == "__main__":
    install()
