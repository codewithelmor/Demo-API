"""Builds the architecture demo MP4 from rendered frames and narration audio.

usage: python assemble.py <work_dir> <out_mp4> <captions_dir>
<work_dir> holds frames/<id>.png (render.mjs) and audio/<id>.mp3 + audio/durations.json (tts.py).
Each segment is its image with its audio (0.35 s lead, 0.6 s tail); clips are joined, loudness is
normalised to about -16 LUFS, captions are muxed as a soft subtitle track, and chapters.txt lists timestamps for the Loom description.
Needs ffmpeg and ffprobe on PATH.
"""

import json
import pathlib
import re
import subprocess
import sys

LEAD = 0.35
TAIL = 0.6
END_TAIL = 1.6

CHAPTERS = {
    "s01": "Introduction",
    "s02a": "The big picture",
    "s03a": "How it is built (AI-assisted)",
    "s04a": "API: structure, data, security",
    "s08a": "BFF: sessions, refresh, proxy",
    "s12a": "React front end",
    "s15a": "Quality and pipeline",
    "s16a": "What you get",
}

CAPTION_FIXES = [
    ("E F Core", "EF Core"),
    ("I D", "ID"),
    ("I P", "IP"),
    ("dot net ten", ".NET 10"),
    ("dot net", ".NET"),
    ("Next dot J S", "Next.js"),
    ("X CSRF", "X-CSRF"),
    ("HttpOnly", "HttpOnly"),
]


def run(cmd: list[str]) -> None:
    subprocess.run(
        cmd, check=True, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL
    )


def stamp(t: float, sep: str) -> str:
    ms = round(t * 1000)
    h, ms = divmod(ms, 3600000)
    m, ms = divmod(ms, 60000)
    s, ms = divmod(ms, 1000)
    return f"{h:02}:{m:02}:{s:02}{sep}{ms:03}"


def caption_text(text: str) -> str:
    for a, b in CAPTION_FIXES:
        text = text.replace(a, b)
    return text


def main() -> None:
    work = pathlib.Path(sys.argv[1])
    out = pathlib.Path(sys.argv[2])
    cap_dir = pathlib.Path(sys.argv[3])
    segs = json.loads((work / "audio" / "durations.json").read_text(encoding="utf-8"))
    clips = work / "clips"
    clips.mkdir(exist_ok=True)

    t = 0.0
    cues: list[tuple[float, float, str]] = []
    marks: list[tuple[float, str]] = []
    lines: list[str] = []
    for i, s in enumerate(segs):
        tail = END_TAIL if i == len(segs) - 1 else TAIL
        length = LEAD + s["dur"] + tail
        clip = clips / f"{s['id']}.mp4"
        run(
            [
                "ffmpeg",
                "-y",
                "-loop",
                "1",
                "-framerate",
                "30",
                "-i",
                str(work / "frames" / f"{s['id']}.png"),
                "-i",
                str(work / "audio" / f"{s['id']}.mp3"),
                "-filter_complex",
                f"[1:a]adelay={int(LEAD * 1000)}|{int(LEAD * 1000)},apad=pad_dur={tail}[a]",
                "-map",
                "0:v",
                "-map",
                "[a]",
                "-t",
                f"{length:.3f}",
                "-c:v",
                "libx264",
                "-preset",
                "medium",
                "-crf",
                "18",
                "-pix_fmt",
                "yuv420p",
                "-r",
                "30",
                "-c:a",
                "aac",
                "-ar",
                "48000",
                "-ac",
                "2",
                "-b:a",
                "192k",
                str(clip),
            ]
        )
        lines.append(f"file '{clip.as_posix()}'")
        if s["id"] in CHAPTERS:
            marks.append((t, CHAPTERS[s["id"]]))
        sentences = [
            x for x in re.split(r"(?<=[.!?])\s+", caption_text(s["text"])) if x
        ]
        total_chars = sum(len(x) for x in sentences)
        cur = t + LEAD
        for sent in sentences:
            d = s["dur"] * len(sent) / total_chars
            cues.append((cur, cur + d, sent))
            cur += d
        t += length
        print(s["id"], round(t, 1))

    (work / "list.txt").write_text("\n".join(lines), encoding="utf-8")
    joined = work / "joined.mp4"
    run(
        [
            "ffmpeg",
            "-y",
            "-f",
            "concat",
            "-safe",
            "0",
            "-i",
            str(work / "list.txt"),
            "-c",
            "copy",
            str(joined),
        ]
    )

    cap_dir.mkdir(parents=True, exist_ok=True)
    srt = cap_dir / "orders-architecture-demo.srt"
    vtt = cap_dir / "orders-architecture-demo.vtt"
    srt.write_text(
        "\n".join(
            f"{n}\n{stamp(a, ',')} --> {stamp(b, ',')}\n{x}\n"
            for n, (a, b, x) in enumerate(cues, 1)
        ),
        encoding="utf-8",
    )
    vtt.write_text(
        "WEBVTT\n\n"
        + "\n".join(f"{stamp(a, '.')} --> {stamp(b, '.')}\n{x}\n" for a, b, x in cues),
        encoding="utf-8",
    )

    chapters = [f"{int(s // 60)}:{int(s % 60):02} {title}" for s, title in marks]
    (pathlib.Path(__file__).parent / "chapters.txt").write_text(
        "\n".join(chapters) + "\n", encoding="utf-8"
    )

    out.parent.mkdir(parents=True, exist_ok=True)
    run(
        [
            "ffmpeg",
            "-y",
            "-i",
            str(joined),
            "-i",
            str(srt),
            "-map",
            "0:v",
            "-map",
            "0:a",
            "-map",
            "1:0",
            "-map_chapters",
            "-1",
            "-metadata",
            "title=Orders Shop: API, BFF and React architecture tour",
            "-c:v",
            "copy",
            "-af",
            "loudnorm=I=-16:TP=-1.5:LRA=11",
            "-c:a",
            "aac",
            "-ar",
            "48000",
            "-b:a",
            "192k",
            "-c:s",
            "mov_text",
            "-metadata:s:s:0",
            "language=eng",
            "-metadata:s:a:0",
            "language=eng",
            "-movflags",
            "+faststart",
            str(out),
        ]
    )
    print("done", out, round(t, 1), "seconds")


main()
