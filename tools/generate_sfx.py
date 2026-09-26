"""Generate Birdsong's original effects using only the Python standard library.

No recordings, samples, external services or third-party sound assets are used.
Run from any directory: python tools/generate_sfx.py
"""
from pathlib import Path
import math
import struct
import wave

RATE = 44100
OUTPUT = Path(__file__).resolve().parents[1] / "Assets" / "Birdsong" / "Audio"


def render(name, duration, notes):
    samples = [0.0] * int(RATE * duration)
    # Each note: start, duration, start frequency, end frequency, amplitude.
    for start, length, low, high, amplitude in notes:
        phase = 0.0
        for index in range(int(length * RATE)):
            time = index / RATE
            progress = time / length
            phase += 2 * math.pi * (low + (high - low) * progress) / RATE
            attack = min(1.0, time / 0.009)
            release = min(1.0, (length - time) / 0.045)
            envelope = attack * release * math.exp(-2.4 * progress)
            tone = math.sin(phase) + 0.18 * math.sin(phase * 2) + 0.06 * math.sin(phase * 3)
            position = int(start * RATE) + index
            if position < len(samples):
                samples[position] += amplitude * envelope * tone
    peak = max(abs(value) for value in samples)
    if peak > 0.7:
        samples = [value * 0.7 / peak for value in samples]
    assert peak > 0.01, f"Silent effect: {name}"
    pcm = b"".join(struct.pack("<h", round(value * 32767)) for value in samples)
    with wave.open(str(OUTPUT / f"{name}.wav"), "wb") as output:
        output.setnchannels(1)
        output.setsampwidth(2)
        output.setframerate(RATE)
        output.writeframes(pcm)
    rms = math.sqrt(sum(value * value for value in samples) / len(samples))
    print(f"{name:8} {duration:.2f}s  peak={max(map(abs, samples)):.3f}  rms={rms:.3f}")


if __name__ == "__main__":
    OUTPUT.mkdir(parents=True, exist_ok=True)
    render("select", 0.12, [(0, 0.10, 820, 1140, 0.26)])
    render("button", 0.11, [(0, 0.08, 620, 780, 0.24)])
    render("swap", 0.22, [(0, 0.13, 430, 850, 0.24), (0.07, 0.12, 660, 1060, 0.16)])
    render("invalid", 0.29, [(0, 0.12, 370, 310, 0.27), (0.11, 0.15, 310, 240, 0.24)])
    render("match", 0.40, [(0, 0.22, 784, 805, 0.30), (0.07, 0.24, 988, 1008, 0.24),
                            (0.14, 0.24, 1175, 1195, 0.22)])
    render("cascade", 0.56, [(0, 0.22, 784, 810, 0.26), (0.09, 0.24, 988, 1020, 0.24),
                              (0.18, 0.26, 1175, 1210, 0.24), (0.27, 0.27, 1568, 1600, 0.22)])
    render("win", 1.10, [(0, 0.25, 523, 523, 0.29), (0.13, 0.26, 659, 659, 0.27),
                          (0.26, 0.28, 784, 784, 0.26), (0.43, 0.62, 1047, 1047, 0.30),
                          (0.43, 0.59, 784, 784, 0.13), (0.43, 0.58, 659, 659, 0.11)])
    render("lose", 0.74, [(0, 0.26, 523, 494, 0.26), (0.19, 0.28, 440, 392, 0.24),
                           (0.39, 0.32, 349, 330, 0.22)])
