# SQLite Domain Data Model — detailed V0.1 baseline

## Shared conventions
Primary keys GUID (`TEXT` in SQLite) or stable generated ID, consistent across tables. `CreatedAtUtc`, `UpdatedAtUtc`, archived timestamp optional. Foreign keys ON; unique business codes where applicable. Use indices on status, dates, project and workstream FKs. Store `DateOnly` deadlines as consistent ISO date. Use concurrency version integer/manual optimistic conflict handling where necessary. Avoid database schema complexity beyond functional need.

## V0.1 entities

### Workstreams
`Id PK`, `Code UNIQUE NOT NULL`, `Name NOT NULL`, `Description`, `ParentId nullable FK -> Workstreams`, `Status {Active,Paused,Archived}`, `CreatedAtUtc`, `UpdatedAtUtc`, `ArchivedAtUtc?`. Parent self-reference must not cycle. Primary workstream can have projects + independent tasks.

### People
`Id PK`, `DisplayName NOT NULL`, `StaffCode nullable`, `Department nullable`, `OrganizationId nullable`, `Email nullable`, `IsActive`, timestamps. Do not imply user must grant login to a person.

### Projects
`Id PK`, `Code UNIQUE`, `Name NOT NULL`, `Objective`, `Scope`, `WorkstreamId nullable FK`, `OwnerPersonId nullable FK`, `Phase {Concept,Build,Order,Setup,Apply,Other}`, `Status {Planned,Active,Paused,Completed,Cancelled}`, `Health {Unknown,OnTrack,AtRisk,Delayed,Blocked}`, `BaselineTargetDate nullable`, `CurrentTargetDate nullable`, `ActualApplyDate nullable`, `StatusReason`, `LastVerifiedAtUtc nullable`, timestamps, `ArchivedAtUtc nullable`. Milestones separate if implemented.

### Tasks
`Id PK`, `Title NOT NULL`, `Description`, `ProjectId nullable FK`, `WorkstreamId nullable FK`, `OwnerPersonId nullable FK`, `OwnerText nullable` (for temporary unknown/import names), `FollowUpPersonId nullable FK`, `Status {Todo,InProgress,Waiting,Blocked,Done,Cancelled}`, `Priority {Low,Normal,High,Critical}`, `StartDate nullable`, `DueDate nullable`, `DueDateType nullable {Committed,Planned,Suggested}`, `NextAction`, `NextCheckAtUtc nullable`, `LastUpdatedAtUtc nullable`, `CompletedAtUtc nullable`, `CompletionEvidence nullable`, `SourceLabel nullable`, `ImportFingerprint nullable`, timestamps, `ArchivedAtUtc nullable`.
Constraint/validation: project and independent workstream assignment supported. If ProjectId exists, workstream displayed from project; avoid conflicting duplicate value. Done requires completion timestamp and optional evidence; lack of evidence clearly marked. Overdue is computed not stored; Done/Cancelled not overdue.

### TaskDependencies (may implement in V0.2 if time)
`TaskId FK`, `DependsOnTaskId FK`, composite PK, no self dependency/no cycles (application validation).

### AuditEvents
`Id PK`, `OccurredAtUtc`, `ActorType {User,System,AI}`, `Action`, `EntityType`, `EntityId`, `BeforeJson nullable`, `AfterJson nullable`, `CorrelationId nullable`, `Details nullable`. Exclude credentials, secrets and plaintext passwords. Capture create/update/archive and import batches.

### ImportBatches
`Id PK`, `SourceFileName`, `FileSha256`, `ImportedAtUtc`, `CreatedCount`, `UpdatedCount`, `SkippedCount`, `WarningsJson`, `MappingVersion`. Import preview before writing and idempotency reimport.

### Users / Identity
One local account at V0.1, hashed password / ASP.NET Core Identity tables; login name and key management locally stored, no hardcoded credentials.

## Future entities (documented only; add migrations with corresponding features)
- `Issues`: problem, severity, project, blocker, remediation, verification.
- `Commitments`: promised by, promised date, deliverable, linked project/task, evidence.
- `Instructions`: issuer, issued time, original content, source, due and fulfillment.
- `Decisions`: decision, rationale, alternatives, decided by, source, status.
- `InboxItems`, `Sources`, `Documents`, `EntityLinks`: source lineage and manually captured email/meetings.
- `Conversations`, `Messages`, `MemoryFacts`, `MemoryRevisions`: AI/knowledge memories.
- `AIActionLogs`, `AIProposals`, `AIProposalItems`: tool execution and approvals.
- `Reminders`, `Notifications`, `NotificationDeliveries`: scheduled notifications.

## Data rules
- `Overdue = DueDate < local_today && Status not in {Done,Cancelled}`; exclude unknown DueDate. Date-only comparison relative to Vietnam timezone.
- Project progress is completed required tasks / total scoped tasks; count planned/skipped appropriately and show `N/A` when no tasks.
- A task may be independent, inside a workstream or under a project. Avoid double counting linked project tasks.
- Distinguish objective facts from `Planned` and `Suggested` dates; imported work may be a planning suggestion.
- In future, raw source date (`EffectiveAt`) differs from receipt date (`RecordedAt`); do not overwrite history.

## Migration + backup
EF migrations maintained and never reset user DB. Backup safely before schema changes. Test restart, clean install, import twice, bad input, restore, interrupted migration.
