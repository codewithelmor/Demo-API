"""Edits the Orders Shop web demo: title card + customer recording + admin recording + end card, voice-over from
the narration segments, soft captions, chapters, loudness normalisation. Writes captions/*.srt and *.vtt.

usage: python web-assemble.py <scratch_dir> <demo_dir> <version_line>
Inputs (scratch): rec/customer.webm, rec/admin.webm, rec/marks.json, audio/<id>.mp3, audio/durations.json
Outputs: <demo_dir>/captions/orders-shop-web-demo.{srt,vtt}, <scratch>/final.mp4
"""

import json
import pathlib
import re
import subprocess
import sys

scratch = pathlib.Path(sys.argv[1])
demo = pathlib.Path(sys.argv[2])
version_line = sys.argv[3]
FONT_B = "/Windows/Fonts/segoeuib.ttf"
FONT = "/Windows/Fonts/segoeui.ttf"
OFFSET = (
    0.25  # seconds: Playwright's video starts a little after the clock the marks use
)
GAP = 0.35  # silence between the voice and the cut

segs = {
    s["id"]: s
    for s in json.loads(
        (scratch / "audio" / "durations.json").read_text(encoding="utf-8")
    )
}
marks = json.loads((scratch / "rec" / "marks.json").read_text(encoding="utf-8"))


def run(cmd: list[str]) -> None:
    subprocess.run(cmd, check=True, cwd=scratch)


def probe(path: pathlib.Path) -> float:
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


def card(name: str, lines: list[tuple[str, str, int, str, int]], dur: float) -> None:
    """lines: (text, fontfile, size, colour, y)."""
    filters = []
    for i, (text, font, size, colour, y) in enumerate(lines):
        (scratch / f"{name}{i}.txt").write_text(text, encoding="utf-8")
        filters.append(
            f"drawtext=fontfile={font}:textfile={name}{i}.txt:fontsize={size}:fontcolor={colour}:x=(w-text_w)/2:y={y}"
        )
    vf = (
        ",".join(filters)
        + f",fade=t=in:d=0.5,fade=t=out:st={dur - 0.5}:d=0.5,format=yuv420p"
    )
    run(
        ["ffmpeg", "-y", "-loglevel", "error", "-f", "lavfi", "-i", f"color=c=0x0f172a:s=1920x1080:r=30:d={dur}",
         "-vf", vf, "-c:v", "libx264", "-crf", "18", f"{name}.mp4"]
    )  # fmt: skip


title_dur = segs["intro"]["dur"] + 1.2
end_dur = segs["outro"]["dur"] + 2.2
card("title", [
    ("Orders Shop", FONT_B, 120, "white", 360),
    ("Web front end for the Orders API: customer portal and back office", FONT, 46, "0x93c5fd", 520),
    (version_line, FONT, 32, "0x94a3b8", 640),
    ("Local demo build, demo data only", FONT, 32, "0x94a3b8", 700),
], title_dur)  # fmt: skip
card("end", [
    ("Orders Shop web front end", FONT_B, 80, "white", 330),
    ("Try it locally:  docker compose up -d   then open  http://localhost:8081", FONT, 44, "0x93c5fd", 480),
    (version_line, FONT, 30, "0x94a3b8", 580),
    ("Demo data only. Voice-over is an AI-generated voice (Microsoft Edge neural voice).", FONT, 30, "0x94a3b8", 640),
], end_dur)  # fmt: skip

cust_dur = probe(scratch / "rec" / "customer.webm")
adm_dur = probe(scratch / "rec" / "admin.webm")
t_cust = title_dur
t_adm = t_cust + cust_dur
t_end = t_adm + adm_dur
total = t_end + end_dur

# Voice timeline: (segment id, absolute start second in the final video)
timeline: list[tuple[str, float]] = [("intro", 0.4)]
timeline += [(i, t_cust + t + OFFSET) for i, t in marks["customer"].items()]
timeline += [(i, t_adm + t + OFFSET) for i, t in marks["admin"].items()]
timeline.append(("outro", t_end + 0.6))


# Captions: split each segment into cues of at most ~84 characters, timed by character count.
def cues(text: str, start: float, dur: float) -> list[tuple[float, float, str]]:
    sentences = re.findall(r"[^.!?]+[.!?]?", text)
    chunks: list[str] = []
    for sentence in (s.strip() for s in sentences if s.strip()):
        parts = [sentence]
        while len(parts[-1]) > 84:
            cut = parts[-1].rfind(",", 0, 84)
            cut = cut if cut > 30 else parts[-1].rfind(" ", 0, 84)
            parts = [
                *parts[:-1],
                parts[-1][: cut + 1].strip(),
                parts[-1][cut + 1 :].strip(),
            ]
        chunks += parts
    total_chars = sum(len(c) for c in chunks)
    out, t = [], start
    for c in chunks:
        d = dur * len(c) / total_chars
        out.append((t, t + d, c))
        t += d
    return out


def wrap(text: str) -> str:
    if len(text) <= 42:
        return text
    mid = text.rfind(" ", 0, len(text) // 2 + 8)
    return text[:mid] + "\n" + text[mid + 1 :]


def stamp(sec: float, sep: str) -> str:
    ms = round(sec * 1000)
    return f"{ms // 3600000:02}:{ms // 60000 % 60:02}:{ms // 1000 % 60:02}{sep}{ms % 1000:03}"


all_cues = [
    c
    for sid, start in timeline
    for c in cues(segs[sid]["text"], start, segs[sid]["dur"])
]
(demo / "captions").mkdir(parents=True, exist_ok=True)
srt = "\n".join(
    f"{n}\n{stamp(a, ',')} --> {stamp(b, ',')}\n{wrap(t)}\n"
    for n, (a, b, t) in enumerate(all_cues, 1)
)
vtt = "WEBVTT\n\n" + "\n".join(
    f"{stamp(a, '.')} --> {stamp(b, '.')}\n{wrap(t)}\n" for a, b, t in all_cues
)
(demo / "captions" / "orders-shop-web-demo.srt").write_text(srt, encoding="utf-8")
(demo / "captions" / "orders-shop-web-demo.vtt").write_text(vtt, encoding="utf-8")
(scratch / "captions.srt").write_text(srt, encoding="utf-8")

# Chapters
chapters = [
    ("Introduction", 0),
    ("Create an account", t_cust + marks["customer"]["c1_open"]),
    ("Browse the catalog", t_cust + marks["customer"]["c2_catalog"]),
    ("Cart and orders", t_cust + marks["customer"]["c3_cart"]),
    ("Account and security", t_cust + marks["customer"]["c4_profile"]),
    ("Back office", t_adm + marks["admin"]["e_signin"]),
    ("Wrap-up", t_end),
]
meta = ";FFMETADATA1\ntitle=Orders Shop web front end demo\n"
for i, (name, start) in enumerate(chapters):
    end = chapters[i + 1][1] if i + 1 < len(chapters) else total
    meta += f"[CHAPTER]\nTIMEBASE=1/1000\nSTART={round(start * 1000)}\nEND={round(end * 1000)}\ntitle={name}\n"
(scratch / "chapters.txt").write_text(meta, encoding="utf-8")

# Final mix
inputs = [
    "-i",
    "title.mp4",
    "-i",
    "rec/customer.webm",
    "-i",
    "rec/admin.webm",
    "-i",
    "end.mp4",
    "-i",
    "captions.srt",
    "-i",
    "chapters.txt",
]
for sid, _ in timeline:
    inputs += ["-i", f"audio/{sid}.mp3"]
base = 6
vf = (
    "[0:v]fps=30,scale=1920:1080:flags=lanczos,setsar=1[v0];[1:v]fps=30,scale=1920:1080:flags=lanczos,setsar=1[v1];"
    "[2:v]fps=30,scale=1920:1080:flags=lanczos,setsar=1[v2];[3:v]fps=30,scale=1920:1080:flags=lanczos,setsar=1[v3];"
    "[v0][v1][v2][v3]concat=n=4:v=1:a=0[vout];"
)
af = "".join(
    f"[{base + k}:a]adelay={round(t * 1000)}|{round(t * 1000)}[a{k}];"
    for k, (_, t) in enumerate(timeline)
)
af += "".join(f"[a{k}]" for k in range(len(timeline)))
af += f"amix=inputs={len(timeline)}:normalize=0,loudnorm=I=-16:TP=-1.5:LRA=11,apad=whole_dur={total:.2f}[aout]"
run(["ffmpeg", "-y", "-loglevel", "error", *inputs, "-filter_complex", vf + af,
     "-map", "[vout]", "-map", "[aout]", "-map", "4:s", "-map_metadata", "5",
     "-c:v", "libx264", "-preset", "slow", "-crf", "20", "-pix_fmt", "yuv420p", "-r", "30",
     "-c:a", "aac", "-b:a", "160k", "-ar", "48000", "-c:s", "mov_text", "-metadata:s:s:0", "language=eng",
     "-disposition:s:0", "default", "-t", f"{total:.2f}", "-movflags", "+faststart", "final.mp4"])  # fmt: skip
print(
    "total",
    round(total, 1),
    "title",
    round(title_dur, 1),
    "customer",
    round(cust_dur, 1),
    "admin",
    round(adm_dur, 1),
)
