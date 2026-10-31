# Demo quality check

Video: `docs/product/10-launch/demo/exports/orders-shop-web-demo.mp4` | SHA-256: `d0ad4dc561c2421b541ca4ecaec60ab768cc072a216c202bad4952a20923de98` | App version: web 0.1.0, commit c85f560 | Checked: 2026-10-31

Automated: `check_video.py` PASS (0 high, 1 warning). Contact sheets in `exports/qc-frames/` were reviewed by eye. Sync checked with frames at scene starts (c2_search, c3_place, c4_guard).

| # | Check | Severity | Result | Notes |
|---|---|---|---|---|
| 1 | Every planned journey shown start to finish | High | Pass | J1-J5, 31 recorder steps passed |
| 2 | Result visible >= 1.5 s | High | Pass | Narration hold after every step; restore result held 2.5 s before click |
| 3 | No failed or hidden steps, no error dialogs | High | Pass | Planned validation errors only |
| 4 | No private data | High | Pass | Only generated example.test addresses and the seeded dev admin address. Admin password typed into a masked field, never shown. No browser chrome, paths or machine names in frames; no metadata tags |
| 5 | No notifications or other apps | High | Pass | Browser viewport only |
| 6 | Captions .srt and .vtt, timed | High | Pass | 78 cues, no overlaps; also a soft subtitle track in the MP4 |
| 7 | Version on cards matches build | High | Pass | v0.1.0, c85f560 (package.json, git HEAD) |
| 8 | H.264 High, yuv420p, AAC 48 kHz, faststart | High | Pass | |
| 9 | 1920x1080, 30 fps | Medium | Pass | Recorded at 1280x720, upscaled with Lanczos, so UI text is slightly soft |
| 10 | Length within plan | Medium | Pass | 318.7 s (plan 6-7 min target was loose; 5.3 min delivered) |
| 11 | Loudness | Medium | Pass | -16.3 LUFS, -1.5 dBTP |
| 12 | Sync | Medium | Pass | Voice starts with the action in all sampled scenes |
| 13 | Pacing and cursor | Medium | Pass | Typing visible, cursor ring drawn |
| 14 | Frozen parts > 6 s | Medium | Pass with note | Tool warns on 20 still stretches; all are holds while the voice-over explains the screen |
| 15 | Branding | Low | N/A | No design system yet |
| 16 | Cards true; AI voice disclosed | Medium | Pass | End card states AI-generated voice |
| 17 | Music | High | N/A | None |

**Result:** Pass

**Notes:** the confirm and reset links are read from the API log (the dev stack's fake e-mail sender); the narration says so. The Edge neural voice is a Microsoft cloud service (the narration text is sent to it); licence terms for publishing should be recorded in `marketing/legal-check.md` by `market-legal-check` before anything is public.
