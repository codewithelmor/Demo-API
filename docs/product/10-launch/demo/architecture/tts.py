"""Generates one MP3 per narration segment with a neural Edge voice and stores the durations.

usage: python tts.py <audio_dir> [voice] [rate]
Needs: pip install edge-tts, ffprobe on PATH. No API key is used (Edge read-aloud service, free).
The voice is a stock Microsoft neural voice. It is not a clone of any person. Disclose "AI voice" on publish.
"""

import asyncio
import json
import pathlib
import subprocess
import sys

import edge_tts


def duration(path: pathlib.Path) -> float:
    cmd = [
        "ffprobe",
        "-v",
        "error",
        "-show_entries",
        "format=duration",
        "-of",
        "csv=p=0",
        str(path),
    ]
    return float(subprocess.check_output(cmd).decode().strip())


def main() -> None:
    here = pathlib.Path(__file__).parent
    out = pathlib.Path(sys.argv[1])
    out.mkdir(parents=True, exist_ok=True)
    voice = sys.argv[2] if len(sys.argv) > 2 else "en-US-AndrewNeural"
    rate = sys.argv[3] if len(sys.argv) > 3 else "+0%"
    segs = json.loads((here / "narration.json").read_text(encoding="utf-8"))
    words = 0
    total = 0.0
    for s in segs:
        f = out / f"{s['id']}.mp3"
        for attempt in range(4):
            try:
                asyncio.run(
                    edge_tts.Communicate(s["text"], voice, rate=rate).save(str(f))
                )
                break
            except edge_tts.exceptions.NoAudioReceived:
                if attempt == 3:
                    raise
                print("retry", s["id"])
        s["dur"] = duration(f)
        words += len(s["text"].split())
        total += s["dur"]
        print(s["id"], round(s["dur"], 2))
    (out / "durations.json").write_text(json.dumps(segs, indent=1), encoding="utf-8")
    print(
        "words",
        words,
        "audio seconds",
        round(total, 1),
        "wpm",
        round(words / total * 60),
    )


main()
