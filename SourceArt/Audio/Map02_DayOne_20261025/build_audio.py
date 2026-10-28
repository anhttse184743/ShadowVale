# Map 2 Day 1 / Night 1 sound (2026-10-25, plan step 5 / P8): ambience per phase, the duck, ông Sáu's cough, Hùng's
# theme and laugh, the store crates, and the "Đợi tới lúc đó..." echo for Night 3. Every input is a CC0 recording
# (public HQ preview MP3s from Freesound, listed with hashes in sources.json) or the game's own approved TTS line.
# No AI-generated sound. Run: blender -b --factory-startup -P build_audio.py   (Blender's audaspace + numpy)
import aud, numpy as np, os, json, hashlib, wave
HERE = os.path.dirname(os.path.abspath(__file__)); SRC = os.path.join(HERE, "source")
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
OUT = os.path.join(REPO, "Assets", "Resources", "Map02DayOne"); VOICE = os.path.join(REPO, "Assets", "Resources", "Dialogue", "Map02DayOneFree")
RATE = 48000

def load(name, folder=SRC):
    s = aud.Sound(os.path.join(folder, name)).resample(RATE, False); d = np.asarray(s.data(), dtype=np.float32)
    return d if d.ndim == 2 else d[:, None]
def mono(d): return d.mean(axis=1, keepdims=True)
def stereo(d): return d if d.shape[1] == 2 else np.repeat(d, 2, axis=1)
def crop(d, a, b): return d[int(a * RATE):int(b * RATE)].copy()
def fade(d, fin=0.0, fout=0.0):
    n = len(d)
    if fin > 0: k = min(n, int(fin * RATE)); d[:k] *= np.linspace(0, 1, k)[:, None]
    if fout > 0: k = min(n, int(fout * RATE)); d[n - k:] *= np.linspace(1, 0, k)[:, None]
    return d
def peak(d, db): m = np.abs(d).max(); return d * (10 ** (db / 20) / m) if m > 0 else d
def rms(d, db): r = np.sqrt(np.mean(np.square(d))); return d * (10 ** (db / 20) / r) if r > 0 else d
def lowpass(d, hz):                                   # one-pole, enough to push a take into the distance
    a = np.exp(-2 * np.pi * hz / RATE); y = np.empty_like(d); acc = np.zeros(d.shape[1], np.float32)
    for i in range(len(d)): acc = (1 - a) * d[i] + a * acc; y[i] = acc
    return y
def loop(d, seconds, xfade):
    """A seamless loop `seconds` long: the tail is crossfaded (equal power) into the head."""
    n, x = int(seconds * RATE), int(xfade * RATE); body = d[:n + x].copy()
    t = np.linspace(0, 1, x)[:, None]; head = body[:x] * np.sin(t * np.pi / 2); tail = body[n:n + x] * np.cos(t * np.pi / 2)
    out = body[:n].copy(); out[:x] = head + tail; return out
def mix(length, *parts):
    out = np.zeros((int(length * RATE), max(p[0].shape[1] for p in parts)), np.float32)
    for d, at, gain in parts:
        d = stereo(d) if out.shape[1] == 2 else d; s = int(at * RATE); e = min(len(out), s + len(d)); out[s:e] += d[:e - s] * gain
    return out
def write(d, name, folder=OUT):
    d = np.clip(d, -1, 1).astype(np.float32); path = os.path.join(folder, name)
    with wave.open(path, "wb") as w:                  # PCM 16-bit; audaspace's own writer failed on some lengths
        w.setnchannels(d.shape[1]); w.setsampwidth(2); w.setframerate(RATE); w.writeframes((d * 32767).round().astype("<i2").tobytes())
    made.append(dict(file=os.path.relpath(path, REPO).replace("\\", "/"), seconds=round(len(d) / RATE, 3), channels=d.shape[1], sha256=hashlib.sha256(open(path, "rb").read()).hexdigest()))
    print("WROTE", made[-1])
made = []

# Night 1 (P8): frogs and crickets all round; a stove fire crackling in chị Lành's kitchen (placed in the world).
write(rms(loop(load("32655_hq.mp3"), 60, 3), -30), "n1_frogs_loop.wav")
write(rms(mono(loop(load("532191_hq.mp3"), 45, 2.5)), -24), "n1_fire_loop.wav")
# Day 1: bamboo creaking in the wind under everything; children near the houses and oars at the jetty (both placed);
# a rooster now and then, far off; one aeroplane very far away, once.
write(rms(loop(load("335891_hq.mp3"), 75, 4), -32), "d1_bamboo_loop.wav")
write(rms(mono(loop(load("406938_hq.mp3"), 30, 2.5)), -26), "d1_children_loop.wav")
write(rms(mono(loop(load("588308_hq.mp3"), 30, 2.5)), -24), "d1_oars_loop.wav")
write(peak(fade(lowpass(mono(load("435508_hq.mp3")), 2500), 0.02, 0.4), -6), "d1_rooster.wav")
write(peak(fade(lowpass(load("348595_hq.mp3"), 1800), 6, 10), -14), "d1_plane_far.wav")

# The duck in the bushes (P6): wings beat out of a bush, one quack.
flap = crop(mono(load("719107_hq.mp3")), 0.4, 2.0); rustle = peak(mono(load("178615_hq.mp3")), -9); quack = peak(crop(mono(load("418509_hq.mp3")), 0, .6), -6)
write(peak(fade(mix(1.9, (rustle, 0, 1), (flap, .05, 1), (quack, .3, 1)), 0.005, 0.25), -3), "duck.wav")
# Ông Sáu coughs from the inner room before he speaks (P5).
write(peak(fade(crop(mono(load("530127_hq.mp3")), 0, 2.4), 0.01, 0.35), -4), "cough.wav")
# Store crates (P4a): a crate set down on the earth floor; a light knock when a place is refused.
write(peak(fade(lowpass(crop(mono(load("648959_hq.mp3")), 1.45, 2.2), 2200), 0.003, 0.2), -4), "crate_place.wav")
write(peak(fade(mono(load("742356_hq.mp3")), 0.002, 0.08), -8), "crate_wrong.wav")
# Hùng's theme, solo, for "Lời hứa" only (doc): a huqin phrase; the dizi take is kept beside it for the listening choice.
write(peak(fade(crop(stereo(load("372072_hq.mp3")), 0, 17.6), 0.6, 2.5), -4), "hung_theme.wav")
write(peak(fade(crop(stereo(load("339357_hq.mp3")), 1.3, 21.5), 0.4, 3.0), -4), os.path.join("..", "..", "..", "SourceArt", "Audio", "Map02_DayOne_20261025", "review_hung_theme_dizi.wav"))
# Bé Tí giggles behind the porch post, then runs off (P3, D1_02).
write(peak(fade(mono(load("367493_hq.mp3")), 0.01, 0.3), -8), "child_giggle.wav")
# Hùng's laugh, its own file to reuse later (doc): a short warm laugh.
write(peak(fade(crop(mono(load("720133_hq.mp3")), 0.42, 2.3), 0.01, 0.4), -6), "hung_laugh.wav")

# "Đợi tới lúc đó, cậu ra nhà tôi. U tôi nấu canh cua rau đay…" for Night 3 (m2v3_echo_promise): the approved
# promise_05 take up to its third pause, remembered — a short double echo, the tail let ring.
line = crop(load("m2v3_promise_05.wav", VOICE), 0, 3.45); line = fade(line, 0, 0.12)
tail = np.zeros((int(1.2 * RATE), 1), np.float32); dry = np.concatenate([line, tail])
wet = dry.copy(); d1, d2 = int(.19 * RATE), int(.38 * RATE)
wet[d1:] += dry[:-d1] * .32; wet[d2:] += dry[:-d2] * .16
write(peak(fade(lowpass(wet, 6000), 0, 0.6), -3), "m2v3_echo_promise.wav", VOICE)

json.dump(made, open(os.path.join(HERE, "built.json"), "w"), indent=1)
