# Envelope of the short takes, to choose the crops (run: blender -b --factory-startup -P analyse.py).
import aud, numpy as np, os
HERE = os.path.dirname(os.path.abspath(__file__)); SRC = os.path.join(HERE, "source")
REPO = os.path.abspath(os.path.join(HERE, "..", "..", ".."))
def load(path):
    s = aud.Sound(path); d = np.asarray(s.data(), dtype=np.float32); return d, s.specs[0]
for name in ("530127_hq.mp3", "823548_hq.mp3", "720133_hq.mp3", "648959_hq.mp3", "719107_hq.mp3", "418509_hq.mp3", "178615_hq.mp3", "372072_hq.mp3", "339357_hq.mp3"):
    d, rate = load(os.path.join(SRC, name)); m = np.abs(d).max(axis=1); hop = int(rate * 0.1)
    env = [float(np.sqrt(np.mean(np.square(d[i:i + hop])))) for i in range(0, len(d), hop)]
    print("##", name, rate, d.shape, "peak %.3f" % m.max(), "env:", " ".join("%02d" % min(99, int(e * 300)) for e in env))
p = os.path.join(REPO, "Assets/Resources/Dialogue/Map02DayOneFree/m2v3_promise_05.wav")
d, rate = load(p); hop = int(rate * 0.02); env = [float(np.sqrt(np.mean(np.square(d[i:i + hop])))) for i in range(0, len(d), hop)]
quiet = [i * 0.02 for i, e in enumerate(env) if e < 0.01]
gaps = []; start = None
for i, e in enumerate(env + [1]):
    if e < 0.012 and start is None: start = i
    if e >= 0.012 and start is not None:
        if i - start >= 4: gaps.append((round(start * 0.02, 2), round(i * 0.02, 2)))
        start = None
print("## promise_05", rate, d.shape, "gaps(>=80ms):", gaps)
