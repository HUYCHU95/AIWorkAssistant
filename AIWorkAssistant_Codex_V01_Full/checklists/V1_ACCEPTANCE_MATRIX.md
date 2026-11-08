# V1.0 staged acceptance matrix — record PASS / FAIL / BLOCKED with evidence

## V0.2 — tracking depth
- [ ] CRUD Issues, Commitments, Instructions, Decisions, Sources; linked to tasks/projects.
- [ ] Independent tasks, TaskDependencies cycle prevention, actionable Waiting/Blocked views.
- [ ] Audit and source links; no duplicated task count, preserve V0.1 records.
- [ ] Migration upgrade on V0.1 DB copy and run twice without data loss.

## V0.3 — AI read
- [ ] Configure API key safely; no key works offline; AI API errors surfaced.
- [ ] Conversations persisted and reopened, project-context and global search.
- [ ] Read-only typed tools with IDs, source/date, grounded answer links.
- [ ] AI costs/token counts tracked if provider returns metrics; missing estimate marked unknown.
- [ ] Fake provider deterministic tests pass; live call UNVERIFIED unless actually tested with valid key.

## V0.4 — AI write
- [ ] Typed Tools for capture_note/create_task/update_task/create_issue/create_commitment/register_instruction/schedule_reminder.
- [ ] Policy A0-A4 enforced; ambiguous data to NeedsReview; important changes proposed.
- [ ] Tool idempotency prevents duplicate tasks, audit before/after captures result.
- [ ] Prompt injection test: pasted instruction cannot command tools/external sends.
- [ ] Action result cards report actual executed vs pending vs failed; safe undo test.

## V0.5 — inbox memory
- [ ] Manual email/meeting/instruction/note capture with immutable source record.
- [ ] Extract 3 tasks from example meeting, preserve source and avoid duplicates.
- [ ] Document attach/download authorized; safe filename/size validation.
- [ ] Temporal facts, current vs superseded, source citations and conflict queue.
- [ ] Search across project and source history; no fabricated statuses.

## V1.0 — proactive assistant and packaging
- [ ] Morning/evening adaptive briefing and scheduled follow-up with no repetition spam.
- [ ] Reminder worker survives restart, catches up safely, dedupes deliveries.
- [ ] In-app notification center read/unread/snooze; SMTP disabled works, fake SMTP test passes.
- [ ] Backup consistent SQLite+attachments, restore on isolated data, hash/integrity verification.
- [ ] First-run, user login, auth/CSRF, loopback-only, single-instance, data remains after restart.
- [ ] `dotnet build` and `dotnet test` run and results captured.
- [ ] `win-x64` self-contained publish built if SDK/runtime environment available; Start.bat and site actually launched in Windows if accessible.
- [ ] Clean V0.1 -> V1 migration (schema and workbook imported data) retains same project/task counts.
- [ ] `TEST_REPORT_V1.md`, `IMPLEMENTATION_REPORT_V1.md`, `V1_RELEASE_NOTES.md` truthful and complete.

## Required behavioral scenarios
- [ ] Cable quality confirmed but technical issues open -> project not Done.
- [ ] LM Guide NCC promise -> Waiting and next-check, not technical acceptance.
- [ ] New instruction with no PIC -> Inbox NeedsReview, no invented employee.
- [ ] Proposed delivery date different from committed target -> conflict, no silent overwrite.
- [ ] Unsupported document extraction -> captured safely, reported as unsupported.
- [ ] No OpenAI credentials/Internet -> offline task/project working, clear AI disabled status.
- [ ] PC off -> reminders cannot send; on restart deduped catch-up.
- [ ] No real SMTP sending in automated tests.
