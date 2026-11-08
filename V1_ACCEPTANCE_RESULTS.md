# V1 acceptance results

Đối chiếu AIWorkAssistant_Codex_V01_Full/checklists/V1_ACCEPTANCE_MATRIX.md. PASS ở đây là bằng chứng local/automated/native được chỉ rõ, không phải xác minh live external.

| Phạm vi | Kết quả | Bằng chứng |
|---|---|---|
| V0.2 Issues/Commitments/Instructions/Decisions/Sources CRUD/link/audit | PASS | TrackingTests + HTTP screens/forms; source original bất biến, metadata/status cập nhật có audit |
| Independent tasks / dependency cycle / Waiting-Blocked | PASS | V0.1 regression + TrackingTests, Tasks/Dependencies/Tracking |
| Migration từ V0.1 hai lần, giữ ID/attachments/password hash | PASS | TrackingTests, MigrationProbe, actual 9/60 copied DB hashes in TEST_REPORT_V1 |
| V0.3 secret settings / offline / API errors / history/context/read tools | PASS local | AITests, SecretStoreTests, HTTP + native offline chat |
| Token / optional unknown cost | PASS | AIUsage fake metrics / explicit unknown UI |
| OpenAI live | UNVERIFIED | New secure key/consent required; no exposed key used |
| V0.4 typed writes / proposals / actual receipts / dedupe / undo | PASS | ActionTests, three real HTTP approvals, native EXE Inbox workflow |
| Runtime prompt-injection policy / no model approvals or mail tool | PASS | ActionTests; all model writes require local user review |
| V0.5 Inbox immutable source / 3 linked tasks / no duplicates | PASS | MemoryTests + actual HTTP/native capture/extract/review |
| Safe attachments / auth download / unsupported extraction | PASS | MemoryTests, HTTP auth/escaping, signature/path/type checks |
| Temporal conflicts / revisions / source retrieval | PASS | MemoryTests, ReadTools source confidentiality and scoped facts |
| V1 adaptive brief / next-check / catch-up / no duplicate delivery | PASS | PlannerTests, persisted reminders/notifications; rules-based label |
| In-app read/unread/snooze, optional SMTP fake | PASS local | PlannerTests + Planner UI/forms; real transport present |
| SMTP live | UNVERIFIED | Real send not performed; confirmed recipient and local config required |
| Backup/restore/hash/integrity/rollback/persistence | PASS | PlannerTests (including locked file rollback), native backup-mutate-restore-restart |
| First-run/auth/CSRF/loopback/instance lock | PASS | HttpTests, Start.bat/native binding, retained V0.1 mechanisms |
| Release full build/tests and self-contained EXE | PASS local | Pipeline TRX + runtimeconfig/coreclr + native/Start.bat smoke |
| Separate fresh Windows without .NET | UNVERIFIED | Not available in this session |
| Required reports/source/scripts | PASS | README, IMPLEMENTATION_REPORT_V1, TEST_REPORT_V1, V1_RELEASE_NOTES and source/migrations |

## Behavioral scenarios
- Chất lượng Cable báo OK nhưng còn vấn đề kỹ thuật: nguồn/report chỉ Reported, không có quy tắc tự Done dự án; ActionTests/MemoryTests giữ trạng thái.
- NCC hứa giao LM Guide: Commitment Waiting, nguồn chưa phải nghiệm thu; follow-up/reminder riêng.
- Chỉ thị thiếu người: NeedsReview; không sinh nhân viên hoặc deadline.
- Ngày đề xuất khác target: proposal version-checked, memory conflict giữ target; stale approval bị chặn.
- Tài liệu không hỗ trợ trích: giữ tệp, báo Unsupported.
- Không key/Internet: offline CRUD, AI unavailable/error thật.
- Máy/app tắt: không chạy worker; restart catch-up có dedupe.
- Tests không gửi SMTP thật.

## Quyết định có chủ ý
Tất cả model writes yêu cầu người dùng duyệt; conservative hơn tự ghi A1/A2. TrackingEntries dùng category typed chung; ActionReceipt gộp proposal + receipt + audit-linked workflow. Không dựng vector DB hoặc parser tài liệu giả. Live external chỉ có thể xác minh sau cấu hình an toàn trên máy của người dùng.
