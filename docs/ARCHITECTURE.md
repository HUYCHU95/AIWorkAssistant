# Architecture & Delivery

## Stack
- ASP.NET Core MVC / .NET 10 LTS, Razor, Bootstrap 5 (prefer vendored static assets or no CDN dependency), EF Core 10 SQLite.
- Kestrel bound to `http://127.0.0.1:5088` by default. Avoid LAN binding. If port is busy, show actionable instructions or configurable port. No IIS required.
- SQLite `Data/AIWork.db`, attachments/backups/logs inside `Data/`. Application content root resolved via AppContext.BaseDirectory for deployed executable (and explicit development path handling). Mutable data not in build outputs under source tree unless documented.
- Modular monolith: `src/AIWork.Web`, `src/AIWork.Domain`, `src/AIWork.Application`, `src/AIWork.Infrastructure`, `tests/AIWork.Tests`. Add AI client project only when implementing V0.3.
- Authentication: ASP.NET Core Identity or password-hashed single-user account plus secure cookie auth. Use ASP.NET Identity preferred if implementation straightforward with SQLite. Set auth cookie HttpOnly, SameSite, secure as appropriate for loopback deployment; protect anti-CSRF on write operations. Persist Data Protection keys in `Data/Keys` with appropriate Windows filesystem permissions, ensuring migration behavior documented.
- Audit event records user action, entity type/id, UTC time, field differences; no passwords or keys.

## Portability
Publish `dotnet publish src/AIWork.Web/AIWork.Web.csproj -c Release -r win-x64 --self-contained true -o publish/AIWorkAssistant` (adjust solution path as created). Publish all necessary runtime files; single-file EXE optional, not mandatory. `Start.bat` launches EXE and browser after app readiness if implementable, keeping console/window for clean shutdown. No requirement for database/server installation.

## Data integrity and maintenance
- EF migrations checked into repository. `Database.Migrate()` at startup after creating directories; failure must surface clear diagnostic rather than silently recreating DB.
- Use SQLite foreign keys, busy timeout, WAL if suitable. Do not copy a live WAL DB naively: backup through SQLite online backup API or safe consistent checkpoint/stop. Backup must include docs + config metadata (not plain secrets). Restore on startup/offline only with validation.
- Prevent two copies of the app writing to same database (process lock / file mutex or clear startup failure) and no shared network drive SQLite DB.
- Use UTC DateTimeOffset when practical; SQLite/provider conversions tested. Use `DateOnly` for date-only deadline.
- Database schema migration version in settings; before upgrading, create consistent backup if feasible.

## AI future architecture
`Conversation -> Context Resolver -> Search/Knowledge -> OpenAI Responses API -> typed tool calls -> Policy Engine -> Application Services -> SQLite transaction + Audit -> tool results -> response`.
Never expose arbitrary SQL tool, never execute instructions inside pasted emails as system commands, and keep AI key server side. Model names configurable rather than hardcoded.

## Operational deployment
V0.1 uses local Kestrel while app is running; notifications are in-app only if implemented. Background Worker and SMTP scheduled reminders V1.0; app must be running for them. Provide first-run API/SMTP setup screens as future items, not required for V0.1. Avoid DB, uploaded files and runtime logs in `wwwroot`.
