# COPY THIS ENTIRE PROMPT INTO CODEX

You are the implementation engineer for **AI Personal Work Assistant**, a Vietnamese-first portable Windows x64 ASP.NET Core MVC application. **Build a working V0.1 now. Do not just design or describe it.**

Read root `AGENTS.md` and all eight relevant documents in `docs/`, and inspect the Excel seed in `sample-data/9_projects_nov_2026.xlsx`. Obey architecture and current scope, especially: .NET 10 MVC, EF Core SQLite, 127.0.0.1:5088, self-contained local Windows publish, first-run authenticated account, Workstream/Project/Task CRUD, rules-based Home, auditable changes, import with preview/confirmation and idempotency. AI functionality is **not live** in V0.1 and must not be faked.

Implement in this repository with an organized solution, committed migrations, `publish-win-x64.bat`, published `Start.bat`, README, tests and an `IMPLEMENTATION_REPORT.md`. Use the sample workbook for the import workflow. If you encounter ambiguous worksheet fields, create an explicit mapping and display warnings rather than guessing. Do not mark tentative dates as committed.

WORKFLOW:
1. Inspect environment and read specs; write a concise implementation checklist.
2. Create solution and database entities/migrations.
3. Build secure first-run setup/login + MVC shell.
4. Implement functional Project, Workstream, Task screens and counts/history.
5. Implement workbook preview and confirmation importer with deduplication.
6. Implement self-contained publish/start scripts and backup guidance.
7. Run restore/build/test/smoke tests and fix issues. Do not claim success without actual commands.
8. Summarize evidence, remaining limitations and the exact step-by-step way the user starts the app on Windows.

Priority: a reliably runnable V0.1 is more important than polishing or scaffolding future AI features. Don't require user interaction unless absolutely blocked by unavailable SDK/environment. If blocked, still generate code and give exact unexecuted verification steps.
