"""
Rushhouse soundtrack, rendered locally with Stable Audio 3.

Nothing leaves the machine and nothing is paid for: Stability's Community
License covers commercial use under $1M annual revenue.

The game has four musical states, and they exist because the game already
changes state that way -- the menu is idle, the shift is busy, a rush day is
panic, and the day-end screen is over. One track each; the runtime crossfades.

  python music_gen.py                 # all four
  python music_gen.py --track rush    # just one
  python music_gen.py --model medium  # slower, richer

Output lands straight in Assets/Resources/Music as .wav (Unity imports wav
natively; it compresses to Vorbis on build, so there is no ffmpeg step).
"""
import argparse
import json
import os
import sys
import time

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = os.path.join(HERE, "..", "Assets", "Resources", "Music")
SA3 = r"D:\cowork\StableAudio"

# Kitchen-game music is a solved genre: acoustic, swung, comedic under pressure.
# The prompts name real instruments rather than adjectives, because the model
# renders instruments reliably and moods only sometimes.
TRACKS = {
    "menu": dict(
        bpm=96, key="F major", seconds=32,
        mood="warm cozy jazz cafe loop, soft brushed drums, gentle upright bass, "
             "mellow rhodes piano and vibraphone, relaxed and inviting, no vocals",
    ),
    "service": dict(
        bpm=124, key="C major", seconds=32,
        mood="bouncy upbeat kitchen jazz, light swing drums, walking upright bass, "
             "playful clarinet and vibraphone, busy and cheerful, no vocals",
    ),
    "rush": dict(
        bpm=156, key="D minor", seconds=32,
        mood="fast frantic gypsy jazz, driving swing drums, urgent walking bass, "
             "wild clarinet and muted trumpet, comedic panic, no vocals",
    ),
    "result": dict(
        bpm=88, key="Bb major", seconds=24,
        mood="warm satisfied jazz outro, soft brushed drums, mellow vibraphone and "
             "rhodes, calm and resolved, no vocals",
    ),
}


def prompt_for(name):
    t = TRACKS[name]
    return (f"{t['mood']}, {t['bpm']} BPM, {t['key']}, seamless loop, "
            f"instrumental video game music, clean mix")


def write(audio, path):
    import soundfile as sf
    import torch
    os.makedirs(os.path.dirname(path), exist_ok=True)
    a = audio[0] if isinstance(audio, (list, tuple)) else audio
    if isinstance(a, torch.Tensor):
        a = a.detach().float().cpu().numpy()
    while a.ndim > 2:
        a = a[0]
    if a.ndim == 2 and a.shape[0] <= 2:
        a = a.T
    # trim the tail click: a hard cut at an arbitrary sample pops on loop, so
    # fade the last 200 ms into the first 200 ms of the same file.
    import numpy as np
    n = int(.2 * 44100)
    if len(a) > 4 * n:
        ramp = np.linspace(0, 1, n)
        if a.ndim == 2:
            ramp = ramp[:, None]
        head, tail = a[:n].copy(), a[-n:].copy()
        a[:n] = head * ramp + tail * (1 - ramp)
        a = a[:-n]
    # peak-normalise to -1 dBFS. The raw renders came back anywhere from 0.51 to
    # 1.00 peak, which both clips the loud ones and makes the set inconsistent;
    # the runtime sets the actual level, so all it needs from here is headroom.
    peak = float(np.abs(a).max())
    if peak > 1e-6:
        a = a * (0.891 / peak)
    sf.write(path, a, 44100)
    return path


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--model", default="small-music")
    ap.add_argument("--track")
    ap.add_argument("--steps", type=int, default=12)
    ap.add_argument("--seed", type=int, default=11)
    args = ap.parse_args()

    sys.path.insert(0, os.path.join(SA3, "stable-audio-3"))
    from stable_audio_3 import StableAudioModel
    t0 = time.time()
    print(f"loading {args.model} ...", flush=True)
    model = StableAudioModel.from_pretrained(args.model)
    print(f"loaded in {time.time() - t0:.0f}s", flush=True)

    names = [args.track] if args.track else list(TRACKS)
    made = {}
    for i, name in enumerate(names):
        if name not in TRACKS:
            print("unknown track:", name); sys.exit(1)
        p = prompt_for(name)
        print(f"\n=== {name} ({TRACKS[name]['bpm']} BPM, {TRACKS[name]['key']}) ===", flush=True)
        t1 = time.time()
        a = model.generate(prompt=p, duration=TRACKS[name]["seconds"],
                           steps=args.steps, seed=args.seed + i)
        path = write(a, os.path.abspath(os.path.join(OUT, name + ".wav")))
        made[name] = path
        print(f"  {time.time() - t1:.0f}s -> {path}", flush=True)

    with open(os.path.abspath(os.path.join(OUT, "prompts.json")), "w", encoding="utf-8") as f:
        json.dump({"model": args.model, "steps": args.steps, "seed": args.seed,
                   "tracks": {n: {**TRACKS[n], "prompt": prompt_for(n)} for n in names}},
                  f, indent=2, ensure_ascii=False)
    print("\nMUSIC_DONE " + ",".join(made))


if __name__ == "__main__":
    main()
