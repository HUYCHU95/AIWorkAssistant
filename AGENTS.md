# Instructions for Codex — AIWorkAssistant

## Mission
Implement a **working, runnable V0.1** of AI Personal Work Assistant, a portable single-user Windows desktop-hosted ASP.NET Core MVC website. Work incrementally and preserve user data. Do not stop at planning or mockups. Implement, build, test, publish if the environment supports it, and report evidence.

## Non-negotiables
- .NET 10 LTS, ASP.NET Core MVC, C#, Razor, EF Core SQLite. Portable Windows x64, Kestrel bound to loopback **127.0.0.1 only** by default. No SQL Server, IIS, Docker, cloud dependency or mandatory Node/npm runtime.
- Self-contained Windows x64 folder publication. Data directory must resolve relative to the application executable root, never to current working directory.
- DB at `Data/AIWork.db`; attachments under `Data/Attachments/`, backups under `Data/Backups/` and logs under `Data/Logs/`. Never store mutable data inside wwwroot.
- First run creates data directories, migrates SQLite, starts first-user setup with local password hashing. **No default admin password, hardcoded credentials or exposed API keys**. Provide anti-forgery on mutating web actions, login cookie auth, HTTPS strategy for LAN only (LAN off in V0.1).
- V0.1 must function **without any OpenAI key**. AI areas must be transparently labeled `AI Integration — planned for V0.3`, not simulated as a real model response.
- Every data-change endpoint validates server-side, uses anti-forgery and records an audit event (actor, action, entity, UTC timestamp and before/after for important edits). Soft-delete/archive where appropriate.
- Include EF Core migrations in source and apply on first startup with safe failure handling. Enable SQLite FK constraints and sane busy timeout. Treat concurrency intentionally.
- All times UTC in storage; UI Asia/Ho_Chi_Minh timezone. `DueDate` date-only when appropriate. Committed/planned/suggested deadlines must stay distinct.
- No fabricated completed statuses, invented deadlines or NPC ownership. Example Excel dates may be suggested; do not silently treat suggested dates as actual commitments.
- Respect user data and privacy: avoid writing secrets to logs; no external calls until enabled explicitly.
- Build `dotnet build`, run `dotnet test` where possible and attempt Windows self-contained publish. Do not claim commands succeeded if not executed. Report barriers.
- Keep code modular, straightforward, documented. Favor working features over placeholders and excessive abstractions. Do not add microservices, message broker, web scraping or unrelated libraries.

## Current delivery V0.1 — must ship
1. ASP.NET Core MVC app startable with `dotnet run` on development machine.
2. Portable publish script (`publish-win-x64.bat`) and startup script (`Start.bat`) inside published folder; optional Stop.bat documented but graceful shutdown can use Ctrl+C.
3. First-run account creation + local password login/logout and protected app routes.
4. App shell, AI-first Home (rules-based briefing, **no pretend AI**), Workstreams, Projects, Tasks CRUD, task states, filters, due dates, source-of-truth SQLite.
5. Import 9-project workbook `sample-data/9_projects_nov_2026.xlsx` with preview, row count, mapping, deduplication and explicit import confirmation; if the workbook layout is complex, implement a focused importer and document mapping. Prefer closed-source free NuGet library EPPlus licensing-safe alternative (e.g. ClosedXML) after checking compatibility; no Excel dependency at runtime.
6. Audit history for task/project changes, responsive clean UI, error pages, empty states.
7. Unit/integration tests for key domain rules, import idempotency, persistence across restart; use temporary isolated test DB.
8. README with exact Windows startup, backup and migration instructions.

## Strict scope boundaries
- No fake chat intelligence, no email reading/sending, no automatic AI writes, no vector DB and no background reminder delivery yet. Reserve interfaces/DB only as needed for future versions.
- Don't overbuild every future table into V0.1 if not used; make migration-friendly scaffolding.
- Avoid creating needless empty pages. If an item isn't functional, label it as planned or omit navigation.

## Working style
Read these specs in order: PROJECT_SPEC, ARCHITECTURE, DATABASE_DESIGN, UI_DESIGN, AI_BEHAVIOR, ROADMAP, ACCEPTANCE_TESTS. Create a short implementation checklist. Then code V0.1 in sensible batches and test. Never rewrite established migration history after users have data. Document deviations and known issues in `IMPLEMENTATION_REPORT.md`.
