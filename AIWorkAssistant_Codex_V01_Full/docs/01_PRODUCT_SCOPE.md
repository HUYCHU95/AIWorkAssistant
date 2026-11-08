# Product scope and user flows, v1.0

## Fixed decisions
- Single local user. ASP.NET Core MVC .NET 10, Razor, EF Core SQLite, modular monolith, Kestrel loopback `127.0.0.1:5088` (configurable), Windows x64 portable self-contained folder. No IIS/SQL Server mandatory.
- AI-first home; all project/task data is owned by website SQLite, not by ChatGPT conversations. Chat can read/write via validated C# tools. Manual inbound email; optional outbound SMTP reminders.
- Flexible Work Management: Workstream (ongoing responsibility), Project (bounded goal), Task (may be project-linked, workstream-linked, or independent). A Task has one primary project, secondary links via source/entity links. Do not duplicate task counts.
- AI links new text to existing project/workstream if confidently matched; propose new groups. Incomplete or ambiguous entries land in Inbox NeedsReview. Adaptive check-ins according to due date, risk, commitments, next check and last asked.

## Primary screens and readiness
1. **AI Home:** text-first Daily Briefing, top 3–5 priorities, task metrics, overdue, waiting, decisions, quick capture, chat composer. With no AI: deterministic rules-generated briefing with transparent label.
2. **AI Workspace:** conversation picker, context scope (all/project/task), stream responses if feasible, source citations opening records, real action receipts, proposal approvals, error/retry handling, usage summary.
3. **Inbox:** paste email/meeting/instruction/note, source date + received date, attachments with scanning/type limits, preview extraction, NeedsReview queue, target entity links; no automatic mailbox read.
4. **Projects:** portfolio filters/sort; detail Overview, Tasks, Milestones, Issues, Sources, History, Ask AI. Distinguish health, completion and freshness.
5. **Task Center:** status filters Todo/Doing/Waiting/Blocked/Done/Cancelled, missing PIC/due, WaitingFor, NextAction, linked source and audit; inline edits authorized with server validation.
6. **Daily Planner:** upcoming work, overdue, next-check, check-in dismissal, reminders; don't re-ask stale same item every day without new trigger.
7. **Memory/History:** source search, decisions/changes timeline, current/superseded facts, original source viewer and facts provenance.
8. **Settings:** user credentials, OpenAI configuration, model, AI data transfer policy, SMTP settings, backup and diagnostics. No secrets rendered after save.

## Vertical user stories
- I paste "NCC sent updated LM Guide frame drawing, check Friday". System stores original note, links known project, creates follow-up task (if user explicitly requested), does not claim dimension is accepted, schedules reminder using date of note if unambiguous.
- I say "Cable quality OK, 2 technical issues remain, delivery still Nov 15". AI records report of quality and two unresolved issues if enough details; may instead create NeedsReview asking for issue descriptions. It does not mark project Done.
- I paste chairman request to report manpower savings. Instruction is saved, work linked to Management/KPI; if assignee unknown, leave PIC blank and track owner reviewing.
- I ask "Why was AMR inspection not fully deployed?". AI retrieves test notes/decisions by source with dates, describes limitations and uncertainty.
- Offline: I can still edit Project/Task/Inbox and view last stored briefs; AI clearly reports unavailable; queued actions cannot falsely claim success.

## Status semantics
- Task = Inbox (untriaged only), Todo, InProgress, Waiting, Blocked, Done, Cancelled. Overdue calculated, not persisted as status. Done must have explicit user statement or acceptable completion evidence.
- Project stage Build, Order, Setup, Apply; separate project state/health OnTrack/AtRisk/Delayed/Blocked/Completed as derived/manual verified. Do not infer stage from checklist counts alone.
- DeadlineType = Committed, Planned, Suggested; baseline vs current project targets retained with history.
- Source states Captured, Classified, Linked, Verified, Actioned, Archived; a source may be fully processed while commitments remain open.

## Data seeding
- Preserve original workbook `sample-data/9_projects_nov_2026.xlsx` in existing repo. Import with preview/dedupe/idempotency, do not infer completion, do not convert suggested checklist target into confirmed commitment. Never reset existing records to reseed.
