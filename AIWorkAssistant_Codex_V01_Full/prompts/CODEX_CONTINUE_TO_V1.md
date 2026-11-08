# START PROMPT — Continue existing V0.1 repository through V1.0

You are the implementation engineer responsible for taking the EXISTING runnable **AI Personal Work Assistant V0.1** repository to **V1.0** without re-scaffolding it or losing user data. The product owner has approved routine engineering decisions and asked you to continue through versions unattended to the extent environment permits.

**Read in order:** root `AGENTS.md`; root `AGENTS_V1_EXTENSION.md`; `codex-v1-spec/README_START_HERE.md`; all files in `codex-v1-spec/docs/` and `codex-v1-spec/checklists/`; then inspect source, migrations, tests and sample Excel. If relative folder differs, locate those exact documents.

**Execute, don't merely plan.** Work in the current repo and implement **V0.2 → V0.3 → V0.4 → V0.5 → V1.0**, sequentially. For each version implement usable functionality, add integration/unit tests, build/run/test, record checkpoints. Automatically proceed to the next version if its acceptance criteria pass. When a feature needs real OpenAI API or SMTP keys, implement the actual production integration and a distinctly labeled test/offline mode; do not assume a key exists; never fake a real external call. Run safe mocked integration tests using deterministic in-process fakes. No user confirmation required for routine refactors, code generation, new source files or routine test fixtures.

**Mandatory:** Preserve all V0.1 user data and migrations. Before modifying schema, create a consistent backup, validate an upgrade using a disposable copy and migrate production only if safe. No destructive DB reset. Default loopback-only. No hidden remote exposure. Do not run tests that send real emails or sensitive company documents externally. Do not bypass security or claim simulated tests verify actual external integrations.

**Version stages:**
- V0.2: Issues, commitments, instructions, decisions, sources and links, dependency graph, statuses, meaningful UI.
- V0.3: Real OpenAI Responses API chat with stored conversations, grounded read tools, provider abstraction, token/cost stats, offline experience.
- V0.4: Typed controlled AI write tools, policy engine, dedupe, proposals for high-risk changes, idempotency, audit and testable undo.
- V0.5: Manual Inbox (email/meeting/instruction/notes), attachments, extraction/review, temporal facts, source-linked retrieval and contradictions.
- V1.0: Adaptive morning/evening briefing, reminders, in-app notifications, SMTP optional, backup/restore (safe and verified), portable Windows x64 publish and robust diagnostics.

**At each gate:** run and report exact commands/results; record PASS/FAIL/BLOCKED in `TEST_REPORT_V1.md`; fix failed tests and bugs before proceeding. Maintain `IMPLEMENTATION_REPORT_V1.md` and `CHANGELOG.md`. At final deliver `publish/AIWorkAssistant/` with `Start.bat`, app executable, data folder convention, configuration guide and `V1_RELEASE_NOTES.md`. If unable to publish win-x64 on current OS, document and provide repeatable scripts, do not say publish passed. All functional screens must contain real actions/data, not static mock cards.

**Release completeness target:** All `codex-v1-spec/checklists/V1_ACCEPTANCE_MATRIX.md` critical checks pass with supporting evidence. If blocked by missing local secrets or hardware, finish all independent work, mark affected external integration `UNVERIFIED`, and tell the user how to validate it locally later. Do not promise background completion after session ends.

Start by printing a short audit of the existing V0.1 repo and writing an incremental implementation plan, then immediately implement it.
