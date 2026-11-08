# Manual Inbox, document knowledge and temporal memory

## Capture
Inputs: email pasted as text, meeting minutes pasted or uploaded, manager instruction, note, attachment. Capture original immutable source text, source sender (if known), occurredAt (email/meeting date), recordedAt, import channel, attachment checksum. Render original content in a safe escaped viewer. HTML mail sanitized; never render raw active HTML or execute document instructions.

## Extraction pipeline
1. Save raw source (with authenticated user action).
2. Classify into Email/Meeting/Instruction/Note; detect projects/persons with candidates and confidence explanations.
3. Extract potential issue, commitment, instruction, decision, task, deadlines; retain each quote or source offset/record link.
4. Check existing entities and duplicates. Map facts to statuses: `Reported` by sender, `Verified` by user/evidence, `Conflicted` if contradicting current committed fact.
5. Auto-create only low-risk explicitly requested tasks if relation unambiguous. Otherwise write NeedsReview entries/proposals. Review list shows exactly what would be applied.
6. On approval, persist structures, source links and audited actions. Do not delete raw source.

## Retrieval
- Search structured project/task/issues first, then exact/FTS source text and optionally semantic search later.
- Each answer should include link to app source ID and source date; no invented citations or fabricated project status.
- EffectiveAt != RecordedAt. On conflicting assertions, retain both facts, link them, mark uncertain, request verification. Superseding a fact never erases provenance.
- Conversation history stored per user; retrieval filters by project/role/time, capped by tokens. Do not push entire DB into model context.

## Chat persistence
Messages stored before and after provider response, status error/retry, correlation ID. User can search/reopen conversations. ChatGPT web account history is not accessible automatically; import only user-provided exports/documents.

## File security
Whitelist formats initially .txt,.pdf,.docx,.xlsx,.png,.jpg,.jpeg; cap per-file size and count; validate MIME/signature where feasible, random server filename, `Data/Attachments` outside wwwroot, no executable upload. Document parsers are separate best-effort optional modules; show "extraction unsupported" and keep file safely rather than pretending it was read. Don't OCR automatically. Large documents should be chunked and indexed with source references.

## Required sample cases
- NCC 'will deliver' != goods received; 'quality evaluation passed' != machine delivered.
- Missing date/person => NeedsReview.
- Two versions of frame drawing => revision history and current confirmed version.
- A meeting instruction with 3 actions => 3 linked tasks, same Source.
