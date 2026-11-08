# TEST REPORT V1 — 09/11/2026

## Environment and scope
Windows x64; .NET SDK 10.0.403 / runtime 10.0.14. Existing AIWorkAssistant.sln retained. All tests use temporary isolated SQLite or named artifacts fixtures. No real SMTP/AI transmission. No chat-exposed key used. Source/local data only, loopback binding. No Git metadata: source ZIP checkpoint plus database snapshots and staged reports.

| Gate | Result | Commands and evidence |
|---|---|---|
| V0.1 baseline | PASS | dotnet test: 4/4; online v01-publish.db/v01-development.db, quick_check=ok |
| V0.2 | PASS | dotnet restore (initial graph), dotnet build clean, dotnet test 6/6; new authenticated HTTP screens; legacy upgrade twice, dependencies, immutable source |
| V0.3 | PASS local / live UNVERIFIED | full build clean; 9/9; consent/offline/provider errors, bounded scope, confidential exclusion, Responses wire format, persistence and usage |
| V0.4 | PASS | full build clean; 13/13; typed write schemas, proposal approval, canonical idempotency, prompt injection, stale target protection, audited undo |
| V0.5 | PASS | full build clean; 16/16; three action meeting, retry dedupe, attachment signature/path/size, temporal conflicts and supersession |
| V1.0 | PASS local; final gate recorded below | Release publish/test plus real EXE/Start.bat smoke, restore/restart, responsive browser; external restrictions below |

## Final Release gate
Final gate PASS: full Release build 0 warnings / 0 errors; 22/22 tests passed, 0 failed, TRX parsed and confirmed. This includes locked-attachment restore rollback, source metadata preservation, password change/session revocation and all earlier stages. Final binary was smoked again after the last compiled change with the complete native backup-mutate-restore-restart workflow: PASS.

Commands:
```powershell
.\publish-win-x64.bat
# invokes dependency-fingerprint restore only when changed/missing, then:
dotnet build AIWorkAssistant.sln -c Release --no-restore
dotnet test AIWorkAssistant.sln -c Release --no-build --no-restore --logger "trx;LogFileName=v1-release.trx" --results-directory artifacts/TestResults
dotnet publish src/AIWork.Web/AIWork.Web.csproj -c Release -r win-x64 --self-contained true --no-restore -o publish/AIWorkAssistant
```

Result artifact: `artifacts/TestResults/v1-release.trx`. NuGet restored when DPAPI dependency graph/RID changed; later code-only release pipeline skipped restore. No cache/bin/obj clearing.

## Native executable / Start.bat evidence
`tools/smoke-v1.ps1 -KeepRunning` ran against a **copy excluding production Data**, random free loopback port, cwd repository root, with random test-only account. Actual flow:
- Start native self-contained EXE, first setup + cookie login; 13 protected screens HTTP 200.
- Real multipart workbook upload, preview, confirmation: 9 projects / 60 tasks.
- Multipart Inbox + UTF-8 attachment, extract 3 reviewed proposals, user approvals -> 63 tasks. No owner/date fabricated.
- AI chat with no key/consent: persisted error/unavailable, no simulated answer or external call.
- Download verified backup ZIP; modify a test task; upload/verify backup; confirm PHUC HOI; app StopApplication and process exits cleanly.
- Restart native EXE: offline restore applied; old password still logs in; exactly 63 tasks restored; changed-after-backup sentinel gone; original DB/attachments kept under pre-restore-files-*; restored audit records RestoreApplied.
- Updated binary launched through Start.bat, readiness/site verified, data survived. Included runtime files coreclr.dll and runtimeconfig includedFrameworks .NET/ASP.NET 10.0.14 confirmed.

Readiness script failures were diagnosed: first-run /Account/Login redirects to Setup (302 is ready); then a fixed port collided with an earlier isolated test process. Harness now accepts the setup redirect, selects a free loopback port, and checks process exit. Those failed attempts are not counted as successful smoke; the subsequent full flow above passed.

## Migration and preservation
Actual workbook V0.1 DB copy, online snapshot then upgrade twice:
- Projects 9 -> 9 -> 9; Tasks 60 -> 60 -> 60; AuditEvents 80 -> 80 -> 80.
- Migration history count 1 -> 6 -> 6; quick_check=ok each run.
- Project ID SHA256: EB02E48592A7C145BDD8478228CD8191FD4D282D11F10A9CD876F465692503E1 unchanged.
- Task ID SHA256: B87911284F2E4FC0CC07FFB1EAFCB33F208FF3FA841494CDAA7CA453CF7D4F97 unchanged.
- Temp migration tests also retain original password hash, project/task IDs and attachment file.
- MigrationSafety preflights an online snapshot copy and fingerprints IDs before applying any live schema upgrade. Original InitialV01 migration retained.
- Existing published Data is preserved by packaging; no production reset, reseed, restore or credential replacement was performed. User's first V1 start will upgrade safely from retained V0.1 schema.

## Security and correctness coverage
- Auth redirect, anti-CSRF 400, output escaping, protected attachment download; real HTTP workflow for old CRUD + Inbox/actions/memory/reminder + password change/session revocation.
- DTO server validation; FK constraints; manual version concurrency; no arbitrary SQL/shell/URL/mail tools; model cannot approve itself.
- Dependency cycle rejection; source content immutability; metadata/status update audited; invalid uploads/executables/path traversal/signature spoof rejected.
- Secrets DPAPI ciphertext tested not plaintext; unknown secret names rejected; no API/SMTP secrets in audit/content sent to provider. Config secrets never echoed.
- Backup SHA-256/schema/FK/quick_check/attachment-reference validation; tampered ZIP and ../ entries rejected before swap; Windows locked attachment triggers restore failure and rollback preserves current DB and file.
- Worker catch-up, restart dedupe, snooze and unchanged-version no-nag; Done/Cancelled excluded from overdue; no inferred delivery/quality acceptance.
- SMTP fake sink requires exact recipient, dedupes, surfaces Unknown instead of claiming Sent on transport failure. No real emails sent in tests.

## Browser QA
V1 Home checked in actual in-app browser at 1366x1000 and 390x844. No document horizontal overflow (scrollWidth <= innerWidth). Mobile navigation collapsed initially and toggles aria-expanded/open links. Screenshots: artifacts/v1-home-desktop.png, artifacts/v1-home-mobile.png. Test data displayed: 9 projects and 63 tasks after reviewed meeting tasks.

## External integrations / unverified items
- OpenAI LIVE: **UNVERIFIED**. Production Responses HTTP integration exists, test fake and wire-format parser verified. Requires a fresh local API key/model and separate external-processing consent; chat-exposed key not saved or used. No company data sent.
- SMTP LIVE: **UNVERIFIED**. Production TLS SMTP transport exists; fake sink verified. Requires approved host/credentials/recipient and explicit confirmed test/send. Auto reminder mail off until successful test plus local opt-in.
- Separate clean Windows x64 machine without installed .NET: **UNVERIFIED**. Self-contained publish and EXE were exercised on this Windows machine with bundled runtime present.
- Real PC power-loss during migration: not fault-injected; snapshots/copy preflight/no-reset safeguards implemented. Restore file-lock failure rollback tested; archive corruption/path attacks tested.
- No antivirus/OCR/Office/PDF text parser integrated. Unsupported extraction is shown honestly; UTF-8 text and pasted checklist parsing work locally.

Final integrity: Original InitialV01 migration SHA256 equals V0.1 checkpoint (True). Existing published Data inspected read-only: Projects=0, Tasks=0, AuditEvents=1, migration history=1, quick_check=ok — unchanged baseline retained for user's safe first V1 startup. Test workbook Data stayed separate. Published root contains 372 files (~130 MB excluding assets/Data); native runtime included. Test processes stopped after evidence capture.


## 09/11/2026 — Remembered-login patch
Authenticated HTTP regression verified: RememberMe=true issues HttpOnly/SameSite Strict auth cookie expiring approximately 30 days later; normal login omits cookie expiry; password-change/session revocation tests still pass. Full Release build: 0 warnings/errors; full tests: 22/22 passed. Original publish folder was locked by a running application (MSB3027), so a separate self-contained package was successfully produced at publish/AIWorkAssistant-1.0.1. Existing Data and active process were preserved. No schema migration needed for this patch.


## 09/11/2026 — Explicit local OpenAI setup
At the user's explicit request, an API key was saved only to the local Windows DPAPI store, with AI enabled and external-processing setting enabled. No key was written to source or Git. Maintenance tool build: 0 errors/warnings. Production Responses provider LIVE BASIC VERIFIED with model gpt-5.4-mini: synthetic prompt only, response exactly OK, 19 input / 5 output tokens. This supersedes earlier UNVERIFIED status for a basic OpenAI connection only; business-context/tool-action scenarios remain locally/mock tested and SMTP live remains UNVERIFIED. No company records were transmitted during the live test.

