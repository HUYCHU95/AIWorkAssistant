# UI-first V1 screens and quality criteria

## Navigation
Sidebar: Home, AI Chat, Inbox, Projects, Tasks, Planner, Memory/History, Settings. Global Quick Capture and Ask AI context button. On laptop desktop, left nav approx 230px; mobile collapsible. Vietnamese copy (English technical labels allowed).

## Reuse patterns
- `ProjectPicker` typeahead and explicit All Work context.
- Action Result Card: Completed, Pending approval, NeedsReview or Failed, with timestamp, entity link, source links, undo if safe.
- Source Viewer: original unmodified source text, timestamps, sender if known, linked extracted facts, version changes.
- Project Detail tabs: Overview, Tasks, Timeline, Issues, Documents, History; data from DB, no inert controls.
- Task Details right drawer: owner vs followup, priority/status, type of due date, next action, waiting for, blocker, evidence, source and audit.
- Empty states explain why nothing is there and one useful actionable button.

## Progressive release
V0.2: task and project workflows; issues/commitments/instructions/decisions/source links.
V0.3: chat history, contextual grounded Q&A, source citation interactions, API configuration status.
V0.4: action receipts and approval queue, retry/undo feedback.
V0.5: inbox input and review cards, history/source search, file attachment preview.
V1.0: daily brief, notification center, snooze and email settings, backup/restore/diagnostics.

## Accessibility and correctness
Forms require labels, keyboard focus, validation feedback, and clear timezone. No fake or decorative submit buttons. Display unknown as 'Chưa xác nhận', not zero or 'Done'. Show date type planned/committed/suggested and last verified date. Avoid unreadable low-contrast badges. Tests at 1366px and mobile <= 430px where possible.
