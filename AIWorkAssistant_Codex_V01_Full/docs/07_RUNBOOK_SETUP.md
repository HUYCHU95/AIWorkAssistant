# Development, deployment and security runbook

## Build
Check .NET 10 SDK and project compatibility. From repo root: `dotnet restore`, `dotnet build -c Release`, `dotnet test -c Release`. Use actual solution filenames found in repo. Don't regenerate solution if existing code compiles.

## Portable publish
`dotnet publish <web-csproj> -c Release -r win-x64 --self-contained true -o publish/AIWorkAssistant` (may require additional single-file setting; not required). Ensure Start.bat, README, appsettings template, migration assemblies and static assets included, and Data created on first run. Do not package sensitive real Data by default in distributable builds; create separate encrypted backup when transferring data.

## Local setup
1. Run Start.bat on Windows x64; set up first account. Bind loopback only.
2. Verify project/task CRUD, independent tasks, import status, no OpenAI key mode.
3. Configure OpenAI API key in secure settings; review whether company data may be transmitted externally; enable consent flag separately. Test with non-sensitive sample.
4. Configure SMTP credentials/recipient only if allowed; send test mail to explicit self-recipient if user chooses; no silent test email.
5. Enable configured morning/afternoon reminders, confirm app stays running while using reminders.

## Troubleshooting
- Missing .NET SDK (build machine): install SDK; portable release machine should not need runtime.
- Port busy: choose alternate loopback port via config; app startup should explain.
- SQLite locked: ensure no second instance, do not disable DB locks; check busy timeout.
- Provider missing key: AI disabled; do not block project/task CRUD.
- Network unavailable: offline features continue; provider action can't be claimed complete.
- Mail failure: persist failed attempt, cap retries and show error without credential leakage.
- DB corruption: stop app and restore verified backup; no automatic reset.
- Windows cross-machine: Data Protection/DPAPI credentials may need re-entry, handle graceful recovery.

## Release evidence
`IMPLEMENTATION_REPORT_V1.md`: files, schema diffs, features, failures, screenshots if possible.
`TEST_REPORT_V1.md`: exact commands and outcomes, version gates, any external services UNVERIFIED.
`V1_RELEASE_NOTES.md`: startup, settings, backup/restore, migration notes, known limitations.
