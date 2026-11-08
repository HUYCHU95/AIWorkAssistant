# V0.1 Acceptance & Smoke Tests

## Build and launch
- [ ] `dotnet --info` shows usable .NET 10 SDK.
- [ ] `dotnet restore` and `dotnet build` succeed (report actual logs/outcomes).
- [ ] `dotnet test` passes actual tests, or explain environment blocker precisely.
- [ ] First run shows setup; creates `Data/AIWork.db`, directories, schema via migrations.
- [ ] Setup first local account with no default password; log out, log back in; unauthorized direct navigation redirects to login.
- [ ] Browser opens localhost only; app doesn't listen on public interfaces.
- [ ] Close and restart; added Project/Task still exist.

## CRUD
- [ ] Create a workstream; create project under it; create task under project.
- [ ] Create independent task without project/workstream.
- [ ] Edit task priority/owner/status/deadline; audit entry exists.
- [ ] Task with no due date not counted as overdue.
- [ ] `Done` and `Cancelled` excluded from overdue count; project stats correct.
- [ ] Archive project/task with history intact.
- [ ] Invalid input rejected server side; CSRF requests blocked.

## Import sample
- [ ] Preview `sample-data/9_projects_nov_2026.xlsx` showing detected projects/tasks, unresolved cells and dates.
- [ ] Confirm import creates records; report count and warnings.
- [ ] Import same workbook again, no duplicate items; nothing is marked completed from an unverified planning estimate.
- [ ] Export or inspect import batch record with file hash and summary.

## Portability
- [ ] `publish-win-x64.bat` produces standalone folder that includes executable and Start.bat.
- [ ] Test copy entire publish folder to a fresh Windows x64 location; run without installing SQL Server/IIS/.NET runtime. Note test limits if such a machine not available.
- [ ] Stop application before copying raw SQLite DB or use online backup; transfer data and attachments safely.
- [ ] Password works on destination or migration policy clearly requires recovery/re-entry (no silent lockout).

## UX
- [ ] Vietnamese labels, responsive viewport, empty states, functional navigation; no fake AI responses or buttons doing nothing.
- [ ] Briefing counts agree with Task data, show current data with source.

## Codex delivery report
Report changed/created paths, actual commands+results, how to start, known limitations, screenshots if possible, and next version recommended. Never claim build/publish worked without execution.
