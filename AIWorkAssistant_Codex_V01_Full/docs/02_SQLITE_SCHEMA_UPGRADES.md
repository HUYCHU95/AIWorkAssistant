# SQLite additive schema and migrations V0.2–V1.0

Use actual V0.1 schema first. Below is conceptual target: implement with EF Core migrations in version order, never rewrite old migrations or delete production DB. User data lives in `Data/AIWork.db`, not wwwroot; attachments in `Data/Attachments`. Use relative paths. Store UTC timestamps and local display Asia/Ho_Chi_Minh; DateOnly for true calendar deadlines.

## V0.2 entities
- Issues(Id, ProjectId?, TaskId?, Title, Description, Severity, Status, RootCause?, OwnerPersonId?, Remediation?, TargetDate?, ResolvedAtUtc?, VerificationStatus, SourceId?, RowVersion)
- Commitments(Id, ProjectId?, TaskId?, PromisedByPersonId?, PromisedByOrganizationId?, Deliverable, PromisedDate?, DueDateType, Status, EvidenceSourceId?, VerifiedAtUtc?, LastFollowUpAtUtc?, NextCheckAtUtc?)
- Instructions(Id, IssuerPersonId?, Title, OriginalContent, IssuedAtUtc?, RequiredDate?, Priority, Status, SourceId?, VerificationStatus)
- Decisions(Id, Title, DecisionText, Reason?, AlternativesJson?, DecidedByPersonId?, DecidedAtUtc?, SourceId?, Status, SupersedesDecisionId?)
- Sources(Id, Type [Chat/Email/Meeting/Note/File], Title, OriginalText?, SourceOccurredAtUtc?, CapturedAtUtc, CapturedByUserId, SourceStatus, OriginalSha256?, VerificationStatus)
- EntityLinks(Id, SourceEntityType, SourceEntityId, TargetEntityType, TargetEntityId, LinkType, CreatedAtUtc): indexed, server-validated polymorphic integrity; consider typed bridge tables where practical.
- TaskDependencies(Id, TaskId, DependsOnTaskId, Relationship): unique pair, detect cycles via service, no self-dependency.
- EntityActivity(Id, EntityType, EntityId, EventType, AtUtc, ActorType, ActorId?, SourceId?, DataJson), if existing Audit supports same purpose reuse it.

## V0.3
- Conversations(Id, Title, ContextType?, ContextId?, CreatedAtUtc, UpdatedAtUtc)
- Messages(Id, ConversationId, Role, Content, AtUtc, ProviderMessageId?, Model?, IsError?, UsageInputTokens?, UsageOutputTokens?)
- AIUsage(Id, TimeUtc, Model, Operation, InputTokens?, OutputTokens?, CostEstimateUsd?, CorrelationId, Outcome)

## V0.4
- AIActionLogs(Id, CorrelationId, ToolCallId?, ToolName, Actor, EntityType?, EntityId?, ArgsRedactedJson, ResultJson?, BeforeJson?, AfterJson?, Status, AtUtc, IdempotencyKey UNIQUE)
- AIProposals(Id, ConversationId?, Title, RiskLevel, Status [Pending/Approved/Rejected/Expired/Applied], OriginSourceId?, ProposedAtUtc, ReviewedAtUtc?, ReviewBy?, Summary)
- AIProposalItems(Id, ProposalId, ToolName, ArgumentsJson, ExpectedEntityVersion?, Status)
- Prefer audit cross-link to ActionId, and safe undo modeled as compensating action with history, not deleting audit records.

## V0.5
- InboxItems(Id, SourceId, Type, Title, Body, Status [New/NeedsReview/Processed/Archived], SuggestedProjectId?, ReviewReason?, CreatedAtUtc, ReviewedAtUtc?)
- Documents(Id, SourceId?, OriginalFileName, StoredRelativePath, ContentType, LengthBytes, Sha256, CreatedAtUtc, ExtractedText?, ExtractionStatus)
- MemoryFacts(Id, SubjectType, SubjectId, Predicate, ValueText, SourceId, EffectiveAtUtc?, RecordedAtUtc, VerificationStatus [Reported/Verified/Conflicted/Superseded], SupersededById?, IsCurrent, ConfidenceNotes?)
- MemoryRevisions(Id, MemoryFactId, OldValueJson, NewValueJson, ChangedAtUtc, Actor, Reason?, SourceId?)
- Outgoing drafts are separate from inbound source; never send from import.

## V1.0
- Reminders(Id, RelatedEntityType, RelatedEntityId, ScheduledAtUtc, TimezoneId, Status, ReminderType, LastAttemptAtUtc?, RetryCount, DedupeKey UNIQUE)
- Notifications(Id, RecipientUserId, Type, Title, Body, CreatedAtUtc, ReadAtUtc?, SourceEntityType?, SourceEntityId?)
- NotificationDeliveries(Id, NotificationId, Channel, AttemptAtUtc, Result, ErrorRedacted?, ProviderMessageId?)
- Settings records store non-secret preferences; encrypted secrets in local secret store/DPAPI or credential vault, re-entry after machine transfer.
- BackupManifests(Id, CreatedAtUtc, AppVersion, SchemaVersion, BackupRelativePath, FileHash, ValidationStatus, Notes?)

## Rules
- SQLite FK ON; busy timeout; WAL if suitable; no concurrent app instance using DB. Use server-side concurrency token/version or timestamps tested for collisions; EF Core SQLite rowversion semantics not SQL Server timestamp.
- Treat duplicate sources via SHA256+source type/date, but identical content from distinct events must be allowed with explicit identity. Import idempotency key based on source and stable row identity, not Title alone.
- Unique index on task dependency pair, source entity links; indexes on ProjectId, WorkstreamId, Status, DueDate, NextCheckAt, SourceOccurredAtUtc, RecordedAtUtc and entity audit IDs.
- Initialize migrator once at startup, backup before migration, test copy of older schema upgrade. Don't auto recreate missing/corrupt real DB if a previous DB existed; fail safe.
- Use SQLite FTS5 for notes/sources if available; keep FTS index rebuilt after migration; fallback to scoped LIKE.
- Time semantics: separate event/occurrence, captured, effective and stored timestamps; don't treat supplier claim as confirmed acceptance.
