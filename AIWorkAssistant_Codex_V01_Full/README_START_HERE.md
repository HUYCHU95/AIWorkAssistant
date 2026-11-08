# AI Personal Work Assistant — V0.2 to V1.0 unattended delivery pack

**Purpose:** Use this supplement AFTER Codex finishes V0.1, within the SAME existing repository and SQLite database. This ZIP contains specifications and prompts, **not** an implementation or promise that V1.0 is finished.

## How to use
1. Finish V0.1. Save/commit its working source and take a consistent backup of the SQLite database and attachments.
2. Extract this ZIP. Copy `docs/`, `checklists/`, `templates/` and `prompts/` into repository under a subfolder `codex-v1-spec/` (or keep paths intact). Copy `overrides/AGENTS_V1_EXTENSION.md` to repo root as `AGENTS_V1_EXTENSION.md`. **Do not overwrite the existing `AGENTS.md`.**
3. Open Codex at the existing V0.1 repository root. Give it the prompt text in `codex-v1-spec/prompts/CODEX_CONTINUE_TO_V1.md` (adjust path if needed).
4. Allow Codex to write code and run build/test/publish in a trusted development environment. Use a disposable test SQLite file for migrations, AI actions, mail and restore tests.
5. User secrets/API keys are provided only via secure local settings when available; no keys embedded in source, prompt, screenshot, data export or logs.
6. Inspect its final `IMPLEMENTATION_REPORT_V1.md`, `CHANGELOG.md`, `TEST_REPORT_V1.md`, deployment folder, and actual smoke test results.

## Existing V0.1 source takes precedence as a working baseline
- Preserve source, Git history, existing migrations and **real user data**. Merge, not replace.
- If V0.1 differs from architecture, inspect first and adapt incrementally. If risky migration is unavoidable, stop that one action and document.
- `AGENTS.md` still applies, but its V0.1 scope boundary no longer blocks V0.2–V1.0 implementation; this supplement explicitly extends scope.

## Definitions of success
- V1.0 passes each version's automated tests, can start locally with `Start.bat`, uses persistent local SQLite and offers real Chat AI features *when credentials/network exist*.
- When no OpenAI key, no Internet, or no SMTP config: app still works offline and clearly labels AI/email as unavailable; mock provider is test-only.
- Cannot guarantee wall-clock completion, external service connectivity, automated mail delivery when PC is off, or functioning API calls without configuration.
- Only report success supported by actual command and test evidence.
