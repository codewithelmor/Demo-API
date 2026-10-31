# Demo plan: Orders Shop web front end (Phase 2)

| Field | Value |
|---|---|
| Date | 2026-10-31 |
| App and version shown | Orders Shop web front end `demoapi-web` 0.1.0 (React 19 SPA + ASP.NET Core BFF), branch `feature/build-orders-app`, commit `c85f560`. Orders API behind it unchanged. Stack run with `docker compose up -d --build web` (web image rebuilt from this commit; the image that was running was older and still showed "Back office coming soon") |
| Type | Full walkthrough of the whole front end (a "full tutorial" without a tutorial's tips section) |
| Target length | About 6 to 7 minutes (the plan skill suggests 5-15 min for a full tutorial; the product has two roles and about 12 features, so shorter is not honest) |
| Audience | Colleagues and the product owner (internal proof of concept) |
| What they should do after | Review and approve the front end build, try it locally, give feedback on the next slices (Admin customers, orders, reports, users, audit logs) |
| Shared where | Not decided. Nothing is uploaded. Next step is `market-reviewer`, then `market-publisher` after the user approves |
| Voice-over | Local-free option: Microsoft Edge neural voice `en-US-AndrewNeural` through the `edge-tts` package (no API key; the text is sent to Microsoft's read-aloud service to be synthesised). Natural American male, not SAPI. Not a clone of a real person. Speed set to -14% so it measures 147 words per minute |
| Branding | None yet (design profile is only a plan). Dark slate title and end cards, plain text |
| Format | 1920x1080, 30 fps, H.264 + AAC, 16:9. Captions as `.srt`, `.vtt` and a soft subtitle track in the MP4. Chapters in the MP4. No vertical cut (not asked) |

## Story

- **Problem:** The Orders API could only be used through developer tools. Customers could not register or shop, admins used Scalar.
- **Solution:** A web front end. Customers register, browse, order and manage their account. Admins manage products.
- **Result:** One continuous, real session: a new customer is created, orders, cancels and deletes the account; an admin creates, edits, deletes and restores a product.
- **Call to action:** Run `docker compose up -d` and open `http://localhost:8081`; review and say what Admin screen comes next.

## Chapters and targets

| # | Chapter | Journeys shown | Narration segments | Target s |
|---|---|---|---|---|
| 0 | Introduction (title card) | | intro | 14 |
| 1 | Create an account | Register (with validation), same-answer "check your email", sign in before confirming + resend, confirm from the email link (read from the API log), sign in | c1_* | 85 |
| 2 | Browse the catalog | List, search (2-char rule), sort + price filter + in stock, product detail, add to cart | c2_* | 70 |
| 3 | Cart and orders | Edit quantity, remove, place order, order detail with history, My orders filter, cancel with reason | c3_* | 65 |
| 4 | Account and security | Profile edit, change password, sign out everywhere, forgot + reset password, not-allowed page for `/admin`, delete account | c4_* | 100 |
| 5 | Back office (Admin) | Sign in as Admin, dashboard, product list search and sort, create (with validation), edit stock, delete, include deleted, restore | e_* | 85 |
| 6 | Wrap-up (end card) | What is not built yet, how to run it | outro | 22 |

## Data and safety

- Generated customer `maya@demo-<id>.example.test` (new for every run, deleted in the video). Seeded demo catalogue (8 products, `Seed-DevData.ps1`). Seeded dev Admin `admin@demoapi.local`.
- The Admin password is read from the api container into memory, typed into a masked field, and never printed, written or shown. Email links (one-time tokens) are read from the API log and only opened in the browser; the browser address bar is not part of the recording.
- No real email, payment or external service is involved (the e-mail sender is the fake one that logs).
- The recording does not show the host name, user name or private paths.

## Not shown, on purpose

Admin screens for customers, orders, reports, users and audit logs, the Admin idle timeout, 412 conflict and 429 rate limit messages, the HTTPS profile. They are not built, or cannot be triggered honestly in a short demo. The end card says what is missing.

## Approval

This plan was written and executed in one run because the request asked for the finished video. Nothing is published.
