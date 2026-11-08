# Product Requirements — V1 direction, V0.1 implementation

## Vision
A Vietnamese-first AI personal work executive assistant for Automation & Kaizen work at RRC. Its eventual job is to organize projects, tasks, issues, instructions, supplier commitments, decisions, meeting notes, manually pasted emails, and reminders; use sources and historical context to help decide and follow up. Interaction is AI-first but structured database remains the source of truth.

## Previously agreed requirements
- Personal single user initially; flexible model: task may belong to project, or workstream, or exist independently.
- AI auto-links new items to existing project/workstream; suggest new groups only when appropriate.
- Ambiguous new content (missing PIC/deadline/context) goes to Inbox/Needs Review; no invented deadlines/PIC.
- Daily follow-up adaptive to due date, risk and latest update, rather than asking about every task every day.
- AI later: automatically add low-risk notes/clear tasks, record history, require confirmation for changing committed targets, important approvals and external email.
- Website portable on Windows, SQLite local file, self-contained .NET 10 x64, Kestrel 127.0.0.1; manually paste emails; later web/SMTP notification.

## V0.1 deliverables
- First-run configuration, username/password login, logout and protected app routes.
- AI-first homepage: TODAY briefing derived from deterministic DB queries (due soon, overdue, waiting, blocked, recently changed). Label as `Work Briefing (rules-based)`, no claim this is AI output.
- Project portfolio/detail CRUD: name, code, workstream, owner (free-text or Person), stage, planned target, current target, status, risk/issue summary; project view with tasks and audit history.
- Workstream CRUD (name, description, status, parent optional).
- Task CRUD & status with `Todo`, `InProgress`, `Waiting`, `Blocked`, `Done`, `Cancelled`, due date type `Committed`, `Planned`, `Suggested`; task links to optional project/workstream and can be independent.
- Task center sorting/filtering by project/status/PIC/due date, reliable progress counts and overdue rules.
- Basic responsive navigation and global quick capture note if feasible; do not misrepresent as AI parsing.
- Data import from provided sample workbook via preview+confirm; use stable external IDs/content fingerprints and idempotent behavior. Explicitly mark field provenance / deadline type.
- SQLite local, folder-relative paths, audit logs, backup script or safe app backup action, published Windows folder and running docs.
- Search by project/task name and keyword.

## V0.1 non-goals
OpenAI actual chat, AI memory semantic retrieval, email auto sync, arbitrary PDF extraction, notification scheduler/email, advanced KPI exports. Those are V0.3+.

## Person/source data
Do not assume actual personal owners from provisional suggestions. Existing workbook is a seed from a prior planning discussion, not an authoritative report of completion. Import statuses and dates carefully, with a preview showing ambiguous or suggested values. No made-up completion evidence.

## Quality
Vietnamese UI primary, English identifiers in code, consistent date formatting dd/MM/yyyy, local timezone. User may copy published folder to another Windows x64 machine; password and data remain accessible without external DB. Secrets that use OS-bound encryption must have explicit transfer/re-entry policy.
