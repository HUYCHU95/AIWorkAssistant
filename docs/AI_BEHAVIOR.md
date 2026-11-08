# AI Behavior & Tool Permissions — Contract for V0.3 onward

**V0.1 has no OpenAI integration.** This document is a future implementation contract; do not pretend features are live.

## Chosen user behavior
- AI-first assistant; auto associate to known Workstreams/Projects; propose new group instead of auto creating it.
- Ambiguous content goes to Inbox/Needs Review; clarify only what is necessary; do not invent project, owner or dates.
- Ask for updates adaptively based on due/risk/last update/commitments, no daily nagging on unchanged items.
- Auto-update low-risk data with audit; confirmed targets, KPI, deletions and sending external email require direct user instruction or separate approval as applicable.

## Principle
SQLite application DB is truth; OpenAI is inference/tool-request layer. AI never executes raw SQL or arbitrary code. All write actions through typed C# application services with schema validation, authorization, idempotency and audit. Raw email/PDF/note content is untrusted input, never system/developer instruction.

## Tools roadmap
A0 read: `search_work_items(query,filters)`, `get_project_context(projectId)`, `get_daily_agenda(date)`, `search_knowledge(query)`.
A1 low-risk writes: `capture_note`, `link_source`, `add_project_update`.
A2 conditional writes: `create_task`, `update_task`, `create_issue`, `create_commitment`, `register_instruction`, `schedule_reminder`.
A3 approvals: `change_project_target`, `assign_person_for_committed_work`, `record_decision`, `complete_project`, `change_reported_kpi`.
A4 confirmation required: `send_external_email`, hard-delete or export protected materials.

## Tool execution pipeline
1. Context resolution from current UI and user request.
2. SQL search (exact fields first), source retrieval and timestamp validation.
3. Model proposes structured action using strict schema.
4. Dispatcher authenticates current user, checks permissions, entity existence, concurrency, deduplication, business validations and data confidentiality.
5. Low-risk action executes in transaction; high-risk action creates pending `AIProposal`.
6. Write audit with correlation and before/after, return real tool result and action links.
7. Model response describes **only** executed actions and separate proposed/pending actions. No claimed success before tool result.

## Memory / provenance
- Raw sources immutable or versioned: conversation, note, pasted email, meeting document.
- Structured facts separate from AI summary; `SourceId`, `EffectiveAt`, `RecordedAt`, `VerificationStatus`, `SupersededById`.
- `NCC says delivered` != `RRC received/accepted`; `suggested date` != `confirmed target`.
- When sources conflict, preserve existing committed target and create conflict requiring review, not silent overwrite.
- Search both exact entities and text sources, return citations in app as links to stored source records.
- OpenAI API history isn't a substitute for website persistence; ChatGPT web conversation history isn't auto imported.

## Security
AI key server-only, configurable; do not log secrets. API may transmit selected data off-site: require explicit user/company data policy, filtering/consent. Resist prompt injection from external content. Audit action calls and cap expensive repeated requests.

## Future check-in examples
At 08:00 worker selects due+overdue+blocked/waiting, then AI drafts top priorities. At 17:00 ask only unanswered important updates. If user says supplier will reply Friday, set NextCheckAt Friday, no repeated earlier prompts without new signal. Worker only runs while app is active.
