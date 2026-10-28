"""Original temporary instrumental motif, not speech or a borrowed recording.
Night 1 introduces six soft plucked notes; Night 2 recalls three and leaves them open.
"""
import math
import struct
import wave
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
RATE = 24000
NOTES = [220.0, 261.625565, 329.627557, 293.664768, 261.625565, 220.0]

def render(name, count):
    duration = count * .95 + 1.8
    data = [0.0] * int(duration * RATE)
    for note, frequency in enumerate(NOTES[:count]):
        start = int((.25 + note * .95) * RATE)
        for index in range(min(int(2 * RATE), len(data) - start)):
            t = index / RATE
            envelope = min(1.0, t / .018) * math.exp(-t * 3.0)
            tone = sum(math.sin(math.tau * frequency * harmonic * t) / harmonic**2.5 for harmonic in range(1, 5))
            data[start + index] += tone * envelope * .18
    path = ROOT / 'Assets/Resources/OperationsAudio' / (name + '.wav')
    with wave.open(str(path), 'wb') as output:
        output.setparams((1, 2, RATE, len(data), 'NONE', 'not compressed'))
        output.writeframes(b''.join(struct.pack('<h', max(-32767, min(32767, int(v * 32767)))) for v in data))
    print(path.name, round(duration, 2), 'seconds')

if __name__ == '__main__':
    render('hung_theme_night1', 6)
    render('hung_theme_night2', 3)
