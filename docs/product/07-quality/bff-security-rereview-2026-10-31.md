# Security re-review: BFF delta (2026-10-31)

Reviewer: `dotnet-security-reviewer` (fresh context, read-only). Saved by the main session (agent cannot write files). Condensed from the agent's report.

Scope: `web/DemoApi.Bff` and `web/DemoApi.Bff.Tests`, delta since [bff-security-review-2026-10-30.md](bff-security-review-2026-10-30.md): security-event logging, OpenTelemetry, fixes L-1/2/3/4/6/8, CSP nonce. Checked against NFR-F15, F19, F36, F39, T-55, ADR-0015 (incl. Amendment 2). `dotnet package list --vulnerable --include-transitive` and `--deprecated`: none. BFF tests: 135 pass.

## Summary

| Severity | Count |
|---|---|
| Critical | 0 |
| High | 0 |
| Medium | 0 |
| Low | 4 |
| Info | 5 |

No exploitable defect. Only fixed reason codes are logged, session IDs are HMAC-hashed, IPs are masked, the redaction processor runs before the exporter. The four Lows can break the "no secret in logs" promise under outage or misconfiguration, or amplify log volume. Fix F-1 before sign-off.

## Findings

### F-1 Low (confidence Medium; Medium if confirmed): a session ID can reach logs through the Valkey exception chain
- Evidence: `Session/SessionStore.cs:114` builds the cache key as `KeyPrefix + id` (the raw session ID is the bearer cookie value); `SessionStore.cs:157-167` (`Io`) wraps the Redis exception as `new SessionStoreUnavailableException(ex)` keeping the inner exception; `Security/Middleware.cs:33` (`TraceMiddleware`) and `:245` (`BffExceptionHandler`) call `log.LogError(ex, ...)`; ASP.NET `ExceptionHandlerMiddleware` also logs at Error; `appsettings.json:5` only sets `Microsoft.AspNetCore` to Warning; `BffHost.cs:34` creates `RedisCache` with a plain connection string, leaving StackExchange.Redis `IncludeDetailInExceptions` on (messages normally include the command and key). `StoreDownTests` scans only log `Message`, not exception text.
- Impact: during a Valkey timeout or outage a log line with the exception can contain live session IDs; anyone with log read access could replay that cookie. Needs an outage plus log access; not confirmed by running.
- Fix: build `ConfigurationOptions.Parse(connection)` with `IncludeDetailInExceptions = false` and `IncludePerformanceCountersInExceptions = false` and pass it as `RedisCacheOptions.ConfigurationOptions`; in `Io` do not keep the raw inner exception (`new SessionStoreUnavailableException(ex.GetType().Name)`); optionally log only `ex.GetType().Name` for that exception in `TraceMiddleware`.
- Test: a `DownCache` throwing `new RedisConnectionException(..., "GET bff:session:" + id)`; log capture keeps `Exception.ToString()`; assert the signed-in session ID is in no entry including framework `ExceptionHandlerMiddleware` entries.
- Maps: OWASP A09/A04:2025; ASVS V16, V7.

### F-2 Low: security events are unbounded per source, so one IP can flood the log
- Evidence: `BffHost.cs:93-97` runs `PathGuardMiddleware` before `AnonymousRateLimitMiddleware`; `Middleware.cs:110,117,122` return 404/405 and `Problems.cs:60-67` logs `RouteProbe` for each, never counted by the limiter; `Middleware.cs:210` resolves the session before the limiter, so a well-formed random cookie logs `SessionEnded unknown-or-planted-cookie` (`SessionService.cs:119`) per request; `Middleware.cs:219` and `RateLimitPolicies.cs:22` log one Warning per rejected request.
- Impact: lines are small and carry no secret; cost and noise, and real T-55 events get hard to see.
- Fix: per-key counter (client key plus event ID, 1-minute window): log the first N (for example 5), drop the rest, emit one summary event ("suppressed n"); count everything in a metric with no unbounded tags; or move `RouteProbe` and planted-cookie events to Debug.
- Test: 1,000 requests to `/bff/x` and with a random cookie; assert at most N events plus one summary and that real events from another IP still log.
- Maps: OWASP A09/A06:2025; API4:2023; ASVS V16.

### F-3 Low (confidence Medium): `RedactingProcessor` is a block-list with gaps
- Evidence (`Security/Telemetry.cs:88-125`): strips `url.query`, `http.query`, request/response header tags, the query part of four URL tags, masks `client.address` and `network.peer.address`. Not handled: `Activity.StatusDescription`; `user_agent.original` / `http.user_agent` (attacker-controlled; the comment at `Telemetry.cs:6` says no header is captured); old names `net.peer.ip`, `http.client_ip`; `server.address` (from Host header, `AllowedHosts: "*"`); Activity events (`RecordException = false` today covers them). Metrics (lines 38-45) have no processor or view; tag set not verified at runtime.
- Impact: personal or attacker-controlled text can reach the trace backend; no token or password path found.
- Fix: allow-list in the processor (keep `http.request.method`, `http.response.status_code`, `url.path`, `url.scheme`, `http.route`, `error.type`, `network.protocol.version`, `server.port`, `peer.service`; mask or drop the rest); clear status description with `data.SetStatus(data.Status)`; drop `user_agent.original`; add a `MeterListener` test for metric tags.
- Test: tag `user_agent.original`, a status description with a fake token and an event; assert gone; random Host and path give a fixed metric tag set.

### F-4 Low: the "no query string in logs" guarantee depends on log-level configuration only
- Evidence: `appsettings.json:5-8` sets `Microsoft.AspNetCore.Hosting.Diagnostics`, `System.Net.Http.HttpClient`, `Yarp` to Warning; nothing in code enforces it. At Information, Hosting logs "Request starting ... path?query" and YARP logs the proxied URL; query strings on `/confirm-email` and `/reset-password` hold tokens (NFR-F39).
- Impact: `Logging__LogLevel__Default=Debug` or a compose override re-enables token leakage with no code change; hygiene tests run only with the shipped config.
- Fix: after config loads, `builder.Logging.AddFilter(...)` for `Microsoft.AspNetCore.Hosting.Diagnostics`, `Yarp`, `System.Net.Http.HttpClient` at Warning (check rule precedence); consider a startup warning if the default level is below Information.
- Test: factory with `Logging:LogLevel:Default=Trace`, `GET /api/v1/products?token=QUERYSECRET`, assert the secret is in no log entry.

## Info

- I-1 Nonce handling correct (CSPRNG 128 bit, per response, via `HttpContext.Items`, GET/HEAD/fallback/`/index.html`, error and non-HTML keep `style-src 'self'`, never logged). Optional: `no-store` instead of `no-cache` for the nonce page.
- I-2 IP masking correct (IPv4-mapped IPv6 converted first; IPv6 /48; scope ID dropped). /24 and /48 are still pseudonymous personal data: apply log retention.
- I-3 Session hash sound (HMAC-SHA256, 256-bit per-process key, 8 bytes). Breaks correlation across restarts and instances (ADR-0015 says one instance).
- I-4 Log injection and cardinality safe by construction (reason constants, hashes, masked IPs, ints, policy names; path never logged).
- I-5 OTLP over plain HTTP inside compose (`compose.yaml:157,211`); require https and an authenticated collector for any real deployment. Other `ActivityListener`s outside OTel would see raw tags before the processor.

## Checks asked

Redactor runs before every exporter (processor added before `AddOtlpExporter()`; keep that order, add a test); trace extraction disabled on both layers (`RootingPropagator`, `RootingTextMapPropagator`, tested); the global OTel propagator has no side effect on outgoing `traceparent` to the API; L-1/2/3/4/6/8 fixes sound; no regression found in refresh/logout/revocation; packages: no vulnerable or deprecated.
