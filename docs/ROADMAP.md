# Delivery Roadmap

## V0.1 — Build now / acceptance gate
Foundation + runnable application, login, portable SQLite, MVC layout, rules-based briefing, Workstream/Project/Task CRUD, history, workbook importer and publish folder. Do not require OpenAI key. Must build/test. Clean `Start.bat` run.

## V0.2 — Work tracking depth
Issues, Commitments, Instructions, Decisions, linked sources, independent and cross-project tasks, dependency tracking, robust advanced filters and statuses. Still works offline.

## V0.3 — AI read assistant
OpenAI Responses API, stored conversations, search project/task/context, source links, answer grounded in SQLite. Token/cost monitoring; gracefully handle no network/key.

## V0.4 — AI write assistant
Typed function calls, create/update notes/task/issue/commitment, schema validation, approval queue for high impact, idempotent tool execution, audit and undo strategy.

## V0.5 — Inbox + memory
Manual email and meeting capture, file attachment links, source extraction, temporal MemoryFacts, conflict detection, retrieval across conversations, no fabricated status.

## V1.0 — Proactive assistant + release
Reminders, daily check-in, worker scheduling, in-app notifications and SMTP, backup/restore, health diagnostics, installer-free portable package and extensive tests.

## Development discipline
Each version: branch/commit, build, test, smoke run, test on sample data, migration check, update CHANGELOG and implementation report; never erase existing DB or regenerate migrations carelessly. Keep v0.1 scope working and finite.
