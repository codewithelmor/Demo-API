# Orders API Scalar demo video: steps taken

Reconstructed on 2026-10-28 from the session transcript of 2026-10-27/28. Final file: `C:\Users\elmorc\Desktop\orders-api-scalar-demo-v2.mp4` (1600x900, 30 fps, H.264 + AAC, 102.8 s, about 7 MB).

**Status: the video is made but not uploaded.** Every Loom upload attempt failed (see section 6). The file is only on the Desktop.

## 1. Request

"Create a demo video with voice over (woman American accent, not robotic) then upload to my Loom account."

## 2. Tools used

| Purpose | Tool |
|---|---|
| Voice-over | `edge-tts` (Python), voice `en-US-AvaNeural`, rate `-4%` |
| Browser automation and screen recording | Playwright 1.64.1, Chromium, `recordVideo` at 1600x900 |
| Audio mix and encode | FFmpeg (`adelay`, `amix`, `loudnorm`, `libx264`, `aac`) |
| Upload preview | `market-publisher` agent (preview step only) |
| Upload | Atlassian MCP (`createLoomObjectUpload`), failed |

Scripts are saved in `demo/automation/`: `narration.json`, `tts.py`, `record-scalar.mjs`, `assemble.py`.

## 3. Steps

### 3.1 Check the environment
1. Confirmed `docker`, `ffmpeg`, `python`, `node` and `dotnet` are available and the compose stack is running (`docker compose ps`).
2. Read the endpoint catalog for the health, auth, products, customers and orders routes.
3. Got the dev Admin login: email `admin@demoapi.local`; the password comes from `api-tests/http/http-client.private.env.json` (`local.adminPassword`, git-ignored). `docker compose exec api cat /run/secrets/dev/SeedAdmin__Password` gives the same value.
4. Called `POST /api/v1/auth/login` once with PowerShell to confirm the credentials work.

### 3.2 Install tooling (in the scratchpad, not the repo)
```bash
pip install edge-tts
npm init -y && npm i playwright@1.64.1
npx playwright install chromium
```

### 3.3 First attempt (v1, discarded)
1. Wrote a narration script and generated the voice with `edge-tts`.
2. Recorded a styled local `console.html` page that called the API, then built `orders-api-demo.mp4` (95 s, 1280x720).
3. `market-publisher` returned an upload preview and I asked for approval.
4. You asked for the real Scalar UI instead of a mock page, so v1 was dropped.

### 3.4 Explore Scalar
Opened `http://localhost:8080/scalar/` with Playwright (`explore.mjs`, several rounds) to find:
- the operation anchors, for example `v1/tag/health/GET/health/ready`
- the **Test Request** button on each operation
- the **Send** button selector (`button.scalar-button:has-text("Send"):visible`)
- the editor for the login body (`.cm-content`)
- the bearer **Token** field

### 3.5 Write the narration (`narration.json`)
Eight scenes, each a short paragraph: `intro`, `ready`, `live`, `login`, `products`, `customers`, `orders`, `outro`.
Rules used: plain spoken English, contractions, no jargon, the password and tokens are never read out.

### 3.6 Generate the voice-over (`tts.py`)
```bash
python tts.py
```
For each scene it writes `<id>.mp3` with `en-US-AvaNeural` at `-4%` and stores the measured length as `dur` in `narration.json`. The scene length drives the recording pace in the next step.

### 3.7 Record the screen (`record-scalar.mjs`)
```bash
node record-scalar.mjs
```
- Launches headless Chromium with video recording at 1600x900 and opens Scalar.
- Each scene runs its UI actions, takes a screenshot, then waits until the scene lasts `dur + 0.7 s`. Audio and video stay aligned this way.
- Scenes:
  1. `ready` and `live`: open the operation, **Test Request**, **Send**; 200 with Postgres and Valkey healthy.
  2. `login`: type the admin JSON body and send. A page script blurs any editor line containing `password` or `refreshToken"` (30 ms interval), so the password and refresh token never show.
  3. The `accessToken` is captured from the login response by a `page.on('response')` handler.
  4. `products`: paste the captured token into the bearer field, then `GET /api/v1/products`.
  5. `customers` and `orders`: same token, `GET` the lists.
- Writes `marks.json` (start time of each scene) and `vid2/*.webm`.
- Several iterations were needed to fix selectors and timing. Failures were captured in `shots/FAIL.png`.
- A second pass hardened the blur so the password was not visible at any frame; checked by extracting frames with FFmpeg (`h1.png`, `h52.png`).

### 3.8 Mix and encode (`assemble.py`)
```bash
python assemble.py
```
- Cuts the recording to start 0.5 s before the first scene.
- Delays each scene's MP3 to its `marks.json` start time (`adelay`), mixes them (`amix`, `normalize=0`) and normalises loudness (`loudnorm I=-16 TP=-1.5`).
- Encodes H.264 `yuv420p` CRF 20, 30 fps, AAC 160 kbps.
- Output: `orders-api-scalar-demo-v2.mp4`.

### 3.9 Check the result
Extracted frames at 1 s and 52 s and looked at them to confirm the Scalar UI is shown and the password and refresh token are blurred. Checked duration and size with `ffprobe`. Copied the file to `C:\Users\elmorc\Desktop\`.

## 4. Scene timeline (approximate)

| Scene | Voice length | What is on screen |
|---|---|---|
| intro | 13.7 s | Scalar landing page |
| ready | 12.6 s | `GET /health/ready`, 200 |
| live | 6.8 s | `GET /health/live`, 200 |
| login | 15.3 s | `POST /api/v1/auth/login`, tokens returned, secrets blurred |
| products | 14.6 s | `GET /api/v1/products`, bearer token pasted |
| customers | 11.6 s | `GET /api/v1/customers` (Admin only) |
| orders | 8.2 s | `GET /api/v1/orders` |
| outro | 10.0 s | Wrap-up |

## 5. Re-run

From a folder that holds the four scripts (needs the stack running via `docker compose up -d`, plus `pip install edge-tts` and `npm i playwright`):
```bash
python tts.py && node record-scalar.mjs && python assemble.py
```
`record-scalar.mjs` reads the Admin password from `api-tests/http/http-client.private.env.json`. Update the path inside it if the repo is elsewhere.

## 6. Loom upload: what happened

| Attempt | Result |
|---|---|
| `market-publisher` preview of v1 | Preview returned, then superseded by v2 |
| MCP `createLoomObjectUpload` (v1 and v2) | Failed. Atlassian MCP returned `siteId does not resolve to a Loom workspace you are a member of` |
| Re-authentication via `/mcp`, several times | Same account and site (`codewithelmor.atlassian.net`), same error |
| Plain `listLoomObjects` | Same error, so it is not an upload-size or file problem |
| Jira `getJiraCurrentUser` | `Site temporarily unavailable` (404) |
| Opening Loom in Brave to upload by hand | Not done; you asked for the MCP route |

Open points:
- The earlier successful upload (2026-10-24, YouTube-Downloader-App) no longer resolves: "No Loom artifact matches".
- Cause is not confirmed. It is either the MCP account is not a member of the Loom workspace `codewithelmor`, or the Atlassian site is unhealthy.
- Proposed title if uploaded by hand: "Orders API Demo: Health Probes, Admin Login and List Endpoints". Visibility: Only me. Description should say the voice is AI-generated.
- When it is uploaded, record the link in `docs/product/10-launch/published-links.md`.
