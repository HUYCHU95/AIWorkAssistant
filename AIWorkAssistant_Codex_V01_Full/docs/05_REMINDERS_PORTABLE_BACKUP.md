# Daily Assistant, notifications, local deployment, backup

## Scheduling
- `BackgroundService` in app plus persisted SQLite jobs/reminders, bounded interval, async cancellation and startup catch-up. Don't double send reminders; dedupe via scheduled job key and delivery log.
- Suggested default Asia/Ho_Chi_Minh 08:00 morning, 17:00 end-of-day, Friday weekly summary; configurable. Windows Task Scheduler autostart optional and disabled by default.
- Prioritize due/overdue, Blocked, Waiting commitments, NeedMyDecision, NextCheckAt, last asked and new updates. Maximum 3–5 top asks; snooze/dismiss with audit, no daily repetitive nags if next-check future.
- AI-generated brief requires configured provider and approved data transfer; otherwise show deterministic rules-based briefing accurately labeled, never fake AI.

## Notifications
- In-app persisted Notifications + SignalR optional for immediate update.
- Outbound SMTP optional; user enters server/port/TLS/user/credential + recipient in Settings; require test-email confirmation to recipient configured by user. Do not send any real email in CI; use fake SMTP sink. Store secrets encrypted or local machine-protected; re-enter after machine transfer as needed. Never hardcode company's server/addresses.
- If PC/app off, no outgoing mail; on restart perform safe catch-up and consolidate duplicates. Show last successful worker run and pending/failed deliveries.

## Local portable runtime
- Publish win-x64 `--self-contained true`, Kestrel only loopback `127.0.0.1:5088`, cookie login, CSRF, persistent Data directory, no cloud DB, no mandatory installer or runtime.
- `Start.bat` launches executable with stable base directory, clear port conflict handling and browser opening only after readiness. `Stop.bat` graceful shutdown guidance (do not abruptly kill writer during backup).
- Single-instance lock; SQLite WAL/FK/busy timeout; avoid network-share DB.
- Secrets and user data are separate from replaceable binaries so in-place upgrade can preserve `Data/`.

## Backup & Restore
- Backup database online consistently via SQLite backup API or database checkpoint/stop, **not raw live `AIWork.db` only**. Backup all attachments, manifest, integrity hashes; exclude/logically separate secrets and explain re-entry.
- Restore only from authenticated admin setting into offline/maintenance mode after creating pre-restore backup. Verify archive path traversal protection, hashes, compatible schema, attachment count, DB quick_check, rollback on failure. Never overwrite active DB files in place during writes.
- Test backup -> mutate test records -> restore -> verify values and attachments in disposable environment, plus reboot persistence.
- Before schema migration create verified backup and test migration on copy. Never delete real data to fix failed migration.

## Monitoring
Settings/System Health: app/schema versions, DB/attachment size, last backup, worker last run, AI provider configured/disabled, SMTP configured/disabled, last errors redacted. Never expose tokens/keys in diagnostics. README includes data transfer policy warning.
