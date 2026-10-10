"""Builds the README demo in demo/: lazycat-demo.mp4, lazycat-demo.gif and the player lazycat-demo.html.

1. Renders the frames with the app's own renderer: LazyCatCompanion.exe --render-demo <folder>.
2. Synthesizes a soft soundtrack from the cues.json it writes (a music-box lullaby plus little sounds
   for clicks, hearts, purring, the leap and the landing).
3. Encodes the MP4 (1280x720, with sound), the GIF (800 px wide, silent) and embeds the MP4 in the player.
4. Encodes the portfolio card: the whole demo as a looping animated WebP, 640x360 at 15 fps. It belongs to the
   website, not this repository, so it goes to artifacts/demo/lazycat-card.webp unless --card names the
   website's copy (assets/img/lazycatcompanion.webp in RaisulSohanSite).

Usage:  python demo/make-demo.py [--exe path\\to\\LazyCatCompanion.exe] [--card path\\to\\card.webp]
Needs numpy and ffmpeg on PATH. Work files go to artifacts/demo (ignored by git).
"""

import argparse
import base64
import json
import re
import subprocess
import wave
from pathlib import Path

import numpy as np

ROOT = Path(__file__).resolve().parent.parent
DEMO = ROOT / "demo"
SR = 44100


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--exe", type=Path, default=ROOT / "bin" / "Debug" / "net10.0-windows" / "LazyCatCompanion.exe")
    parser.add_argument("--gif-fps", type=int, default=15)
    parser.add_argument("--card", type=Path, help="where to write the portfolio card (default: artifacts/demo/lazycat-card.webp)")
    args = parser.parse_args()
    if not args.exe.exists():
        raise SystemExit(f"{args.exe} not found. Build the app first (run.bat or dotnet build).")

    work = ROOT / "artifacts" / "demo"
    frames = work / "frames"
    frames.mkdir(parents=True, exist_ok=True)
    for old in frames.glob("frame-*.png"):
        old.unlink()

    print("Rendering frames...")
    subprocess.run([str(args.exe), "--render-demo", str(frames)], check=True)
    cues = json.loads((frames / "cues.json").read_text(encoding="utf-8"))

    print("Synthesizing the soundtrack...")
    mix = work / "mix.wav"
    write_wav(mix, soundtrack(cues))

    print("Encoding MP4...")
    mp4 = DEMO / "lazycat-demo.mp4"
    ffmpeg("-framerate", "30", "-i", frames / "frame-%04d.png", "-i", mix,
           "-c:v", "libx264", "-preset", "slow", "-crf", "21", "-pix_fmt", "yuv420p", "-g", "15",
           "-c:a", "aac", "-b:a", "128k", "-shortest", "-movflags", "+faststart", mp4)

    print("Encoding GIF...")
    gif = DEMO / "lazycat-demo.gif"
    palette = work / "palette.png"
    scale = f"fps={args.gif_fps},scale=800:-1:flags=lanczos"
    ffmpeg("-framerate", "30", "-i", frames / "frame-%04d.png", "-vf", f"{scale},palettegen=max_colors=160:stats_mode=full", palette)
    ffmpeg("-framerate", "30", "-i", frames / "frame-%04d.png", "-i", palette, "-lavfi",
           f"{scale}[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=4:diff_mode=rectangle", gif)

    print("Embedding the video in the player...")
    html = DEMO / "lazycat-demo.html"
    page = html.read_text(encoding="utf-8")
    data = base64.b64encode(mp4.read_bytes()).decode("ascii")
    page, count = re.subn(r'const VIDEO_B64 = "[^"]*";', f'const VIDEO_B64 = "{data}";', page)
    if count != 1:
        raise SystemExit("VIDEO_B64 marker not found in lazycat-demo.html")
    html.write_text(page, encoding="utf-8", newline="\n")

    print("Encoding the portfolio card...")
    card = args.card or work / "lazycat-card.webp"
    card.parent.mkdir(parents=True, exist_ok=True)
    ffmpeg("-framerate", "30", "-i", frames / "frame-%04d.png", "-vf", "fps=15,scale=640:360:flags=lanczos",
           "-c:v", "libwebp_anim", "-loop", "0", "-quality", "80", "-compression_level", "6", "-preset", "drawing", card)

    for f in (mp4, gif, html, card):
        print(f"{f.name}: {f.stat().st_size / 1e6:.2f} MB")


def ffmpeg(*args):
    subprocess.run(["ffmpeg", "-v", "error", "-y", *map(str, args)], check=True)


# ----- Soundtrack -----

def soundtrack(cues):
    duration = cues["duration"]
    n = int(duration * SR)
    out = np.zeros((2, n))

    def place(sig, t, pan=0.5, gain=1.0):
        i = int(round(t * SR))
        if i >= n or i + len(sig) <= 0:
            return
        seg = sig[: n - i]
        angle = np.clip(0.5 + (pan - 0.5) * 1.1, 0, 1) * np.pi / 2
        out[0, i:i + len(seg)] += seg * gain * np.cos(angle)
        out[1, i:i + len(seg)] += seg * gain * np.sin(angle)

    events = cues["cues"]
    title = next(c["t"] for c in events if c["kind"] == "title")
    music(place, title, duration)

    hearts = [c for c in events if c["kind"] == "heart"]
    if hearts:
        place(purr(hearts[0]["t"] - 0.35, hearts[-1]["t"] + 0.55), hearts[0]["t"] - 0.35, hearts[0]["x"], 0.3)

    heart_notes = [88, 91, 93, 96, 98]
    for i, c in enumerate(hearts):
        place(bloop(midi(heart_notes[min(i, len(heart_notes) - 1)])), c["t"], c["x"], 0.32)

    for c in events:
        t, x, kind = c["t"], c["x"], c["kind"]
        if kind == "click":
            place(tick(), t, x, 0.32)
        elif kind == "wake":
            place(mew(520, 760, 0.26), t + 0.45, x, 0.22)
        elif kind == "yawn":
            place(mew(760, 430, 0.7, breathy=0.5), t, x, 0.2)
        elif kind == "takeoff":
            place(whoosh(0.32), t - 0.05, x, 0.22)
            place(boing(), t, x, 0.2)
        elif kind == "touchdown":
            place(puff(), t, x, 0.42)
        elif kind == "lick":
            place(blip(2300), t, x, 0.07)
        elif kind == "grab":
            place(mew(560, 900, 0.3), t + 0.04, x, 0.22)
        elif kind == "drop":
            place(puff(), t + 0.06, x, 0.4)
        elif kind == "title":
            for k, note in enumerate([84, 88, 91, 96]):
                place(bell(midi(note), 1.6, 1.2), t + 0.08 + k * 0.075, 0.45 + 0.05 * k, 0.16)

    peak = np.abs(out).max()
    out *= 0.84 / peak
    fade = int(0.35 * SR)
    out[:, -fade:] *= np.linspace(1, 0, fade)
    return out


def music(place, title, duration):
    """A slow music-box lullaby; the title lands on the downbeat of bar six."""
    beat = title / 20
    melody = [
        [(0, 76), (1, 79), (2, 81), (2.5, 79), (3, 76)],
        [(0, 74), (1, 76), (2, 72), (3, 69)],
        [(0, 72), (1, 74), (2, 76), (2.5, 77), (3, 81)],
        [(0, 79), (1, 74), (2.5, 71), (3, 74)],
        [(0, 76), (1, 79), (2, 84), (3, 81)],
        [(0, 84)],
    ]
    chords = [[48, 55, 64], [45, 52, 60], [41, 48, 57], [43, 50, 59], [48, 55, 64], [48, 55, 64, 72]]
    for bar, (notes, chord) in enumerate(zip(melody, chords)):
        start = bar * 4 * beat
        for b, note in notes:
            t = start + b * beat
            gain = 0.11 * min(1, 0.45 + t / 2.5)   # the cat is asleep at first: start softly
            place(bell(midi(note), 2.2), t, 0.55, gain)
        length = min(4 * beat, duration - start) + 0.6
        for note in chord:
            place(pad(midi(note), length), start, 0.5, 0.024 * min(1, 0.35 + start / 3))


def midi(note):
    return 440 * 2 ** ((note - 69) / 12)


def timeline(seconds):
    return np.arange(int(seconds * SR)) / SR


def bell(f, dur=1.5, bright=1.0):
    t = timeline(dur)
    s = (np.sin(2 * np.pi * f * t) * np.exp(-t * 2.6)
         + 0.32 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-t * 5)
         + 0.14 * bright * np.sin(2 * np.pi * 4.17 * f * t) * np.exp(-t * 11))
    return s * np.minimum(1, t / 0.003)


def pad(f, dur):
    t = timeline(dur)
    s = np.sin(2 * np.pi * f * t) + 0.25 * np.sin(2 * np.pi * 2.003 * f * t)
    env = np.minimum(1, t / 0.6) * np.minimum(1, (dur - t) / 0.6)
    return s * env


def tick():
    t = timeline(0.03)
    return (np.sin(2 * np.pi * 1900 * t) * np.exp(-t * 260) + 0.6 * noise(len(t), 2000, 9000, 1) * np.exp(-t * 600))


def bloop(f):
    t = timeline(0.5)
    rise = np.minimum(1, t / 0.05)
    phase = 2 * np.pi * np.cumsum(f * (0.6 + 0.4 * rise)) / SR
    return (np.sin(phase) + 0.2 * np.sin(2 * phase)) * np.exp(-t * 7) * np.minimum(1, t / 0.004)


def mew(f0, f1, dur, breathy=0.15):
    """A small cartoon mew: a gliding voice with a few harmonics and a soft vibrato."""
    t = timeline(dur)
    shape = np.sin(np.pi * np.minimum(1, t / dur * 1.15) / 2)
    f = f0 + (f1 - f0) * shape + 12 * np.sin(2 * np.pi * 7 * t)
    phase = 2 * np.pi * np.cumsum(f) / SR
    voice = np.sin(phase) + 0.45 * np.sin(2 * phase) + 0.2 * np.sin(3 * phase) + 0.08 * np.sin(4 * phase)
    voice += breathy * noise(len(t), 600, 3500, 2)
    return voice * np.sin(np.pi * t / dur) ** 1.4


def purr(start, end):
    dur = max(0.5, end - start)
    t = timeline(dur)
    rumble = noise(len(t), 40, 260, 3) * (0.55 + 0.45 * np.sin(2 * np.pi * 24 * t)) ** 2
    rumble += 0.5 * np.sin(2 * np.pi * 50 * t) * (0.5 + 0.5 * np.sin(2 * np.pi * 24 * t))
    return rumble * np.minimum(1, t / 0.35) * np.minimum(1, (dur - t) / 0.45)


def whoosh(dur):
    t = timeline(dur)
    return noise(len(t), 500, 4000, 4) * np.sin(np.pi * t / dur) ** 2


def boing():
    t = timeline(0.32)
    f = 240 + 520 * (t / 0.32) ** 0.6 + 18 * np.sin(2 * np.pi * 26 * t)
    phase = 2 * np.pi * np.cumsum(f) / SR
    return np.sin(phase) * np.exp(-t * 7) * np.minimum(1, t / 0.01)


def puff():
    t = timeline(0.22)
    return (np.sin(2 * np.pi * (150 - 60 * t / 0.22) * t) * np.exp(-t * 26)
            + 0.5 * noise(len(t), 80, 900, 5) * np.exp(-t * 30)) * np.minimum(1, t / 0.003)


def blip(f):
    t = timeline(0.05)
    return np.sin(2 * np.pi * f * t) * np.exp(-t * 90) * np.minimum(1, t / 0.002)


def noise(count, low, high, seed):
    """White noise kept to one band, by zeroing the rest of its spectrum."""
    x = np.random.default_rng(seed).standard_normal(count)
    spectrum = np.fft.rfft(x)
    freqs = np.fft.rfftfreq(count, 1 / SR)
    spectrum[(freqs < low) | (freqs > high)] = 0
    y = np.fft.irfft(spectrum, count)
    return y / (np.abs(y).max() + 1e-9)


def write_wav(path, stereo):
    pcm = (np.clip(stereo.T, -1, 1) * 32767).astype("<i2")
    with wave.open(str(path), "wb") as w:
        w.setnchannels(2)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(pcm.tobytes())


if __name__ == "__main__":
    main()
