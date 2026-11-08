# UI / UX — AI-first, Vietnamese-first

## Visual direction
Desktop-first responsive layout, clean enterprise dashboard, neutral colors, restrained blue accents, generous white space but compact working tables. Vietnamese labels; English internal naming. Accessible labels, keyboard navigation and meaningful empty states. No decorative charts with fabricated data.

## Navigation V0.1
- Home (`/`): headline `Trợ lý công việc`; rules-based morning briefing (clearly labeled not OpenAI), counts for projects/tasks, critical/high, overdue, waiting, blocked, recent changes; quick jump to task/project.
- Projects (`/Projects`): DataGrid/table filter by status/phase/owner, create/edit/archive, project detail header with plan/current target, tabs Overview, Tasks, History. Avoid showing blank later-stage tabs as working.
- Workstreams (`/Workstreams`): grid + detail/list assigned projects and independent tasks.
- Tasks (`/Tasks`): editable table with task name, project, owner, deadline, due type, priority, status, next action, last update. Filter all/today/overdue/waiting/blocked. Clicking opens side panel or detail page, shows history.
- Import (`/Import`): upload `.xlsx`, review detected projects/tasks, per-row validation warnings, preview count, confirm import, summary and idempotency. Protect from formula injection and large input.
- Settings: profile, backup and optional app info. In V0.1 no fake working SMTP/AI controls.
- AI Chat entry: **disabled or visibly `Coming in V0.3`**. Avoid faux functional chat.

## IA sketch
Desktop left nav (Home, Projects, Tasks, Workstreams, Import, Settings), top search + user. Main home: first card `Ưu tiên hôm nay` with <= 5 grounded items, then counts and lists; project/task management below. Mobile: sidebar collapse.

## Essential screens / interactions
1. First launch: create first local account with strong password, protected route after.
2. Login/logout with clear error, no hint/default credentials.
3. Create Project with title, workstream optional, targets, phase, status. Validate server side.
4. Add Task from Project detail or generic Task Center; optional Project/Workstream. `Suggested` dates visually distinguished.
5. Change Task from Todo to Waiting/Blocked/Done; status progression handled; audit timeline visible.
6. Import preview project/task rows and warnings; explicit confirmation; repeat import should not duplicate.
7. Search by keyword; do not require AI to find records.
8. Copy/paste notes optional: if implemented, store raw text as note, don't pretend AI parsed it.

## Future (do not implement in V0.1)
- AI Chat with source cards/tool-result cards and confirmation for sensitive edits.
- AI Inbox (notes, manually pasted email, meeting minutes) with extracted tasks.
- Daily Planner interactive AI check-in; Memory/History search across sources; web/email reminder engine.

## Example briefing behavior
- `Việc quá hạn`: tasks with DueDate before local date excluding Done/Cancelled.
- `Cần theo dõi`: Waiting and Blocked tasks.
- `Việc đến hạn tuần`: due in next seven local calendar days.
- `Thiếu thông tin`: Tasks with no owner or no due date, marked for review (not automatically invalid).
- No AI-generated claims until V0.3 integrated and verified.
