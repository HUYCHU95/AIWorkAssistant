# AI Personal Work Assistant V1.0

ASP.NET Core 10 MVC / EF Core 10 / SQLite, Windows x64 portable. Source V0.1 và migration gốc được giữ nguyên; V0.2–V1.0 nâng cấp cộng thêm. Không cần OpenAI/SMTP để quản lý công việc ngoại tuyến.

## Khởi động
1. Mở `publish\AIWorkAssistant\Start.bat` (hoặc Start.bat ở root khi đã publish).
2. Trình duyệt mở sau readiness tại `http://127.0.0.1:5088`. Chỉ loopback, không mở LAN. Đổi `LocalPort` trong appsettings.json nếu cổng bận.
3. Dùng tài khoản hiện có. Chỉ khi database chưa có tài khoản mới hiện setup; không có tài khoản/mật khẩu mặc định.
4. Ctrl+C để dừng sạch. Ứng dụng và PC phải đang chạy để xử lý reminders/email. Không hứa nhắc khi máy tắt.

Máy đích không cần .NET Runtime, SQL Server, IIS, Docker, Node hoặc Excel. Gói self-contained chứa runtime. Windows PowerShell có sẵn dùng cho Start.bat. Không chia sẻ Data hoặc đóng gói Data thật vào bản phân phối công khai.

## Chức năng
- Home có composer AI, briefing theo quy tắc được ghi nhãn, số liệu thật và Inbox/đề xuất/vấn đề/quyết định cần xử lý.
- Workstreams, Projects, Tasks: CRUD, lưu trữ mềm, tìm/lọc, trạng thái, PIC vs người theo dõi, chờ ai, blocker, next action/check, hạn Committed/Planned/Suggested; audit và optimistic concurrency. Lưu trữ dự án không làm mất liên kết công việc con.
- Theo dõi chuyên sâu: Issues/Commitments/Instructions/Decisions dùng typed category trong bảng TrackingEntries, ngày/mức xác minh/nguồn và liên kết task/project riêng. Supplier promise không đồng nghĩa nhận hàng/nghiệm thu, chất lượng OK không tự hoàn thành dự án.
- Sources giữ nguyên văn và checksum bất biến; Entity links xác thực phía server. Dependency không cho tự phụ thuộc hoặc vòng lặp.
- AI Workspace: lịch sử hội thoại trong SQLite, ngữ cảnh all/project/task, sáu read tools có ID/link/thời gian, token và chi phí tùy giá do người dùng cấu hình. Không có bảng giá thì chi phí “chưa xác định”. API lỗi có thông báo thật, không giả model.
- AI tools viết qua allowlist typed schema và hàng đợi đề xuất. Chính sách an toàn hiện tại: **mọi thay đổi do model đề xuất đều cần người dùng xem và xác nhận**. Model không có tool tự duyệt/gửi mail/SQL/shell. Canonical idempotency chống retry trùng; approval kiểm tra version và source; undo bằng cập nhật bù nếu chưa bị sửa tiếp.
- Inbox dán email/họp/chỉ thị/ghi chú, source timestamp riêng thời điểm ghi nhận. Trích checklist theo quy tắc cục bộ (“1.”, “- [ ]”, “TASK:”), giữ quote/dòng/nguồn; không giả làm NLP/AI. Mỗi đề xuất phải được duyệt; không tự đoán PIC/hạn.
- Attachments: tối đa 3 × 5 MB, whitelist txt/pdf/docx/xlsx/png/jpg/jpeg, signature/ZIP/path/size checks, tên server ngẫu nhiên; download có auth và kiểm SHA-256. Chỉ trích text UTF-8 cục bộ; các định dạng khác ghi rõ Unsupported, không giả vờ đã đọc/OCR. Không có antivirus scanner tích hợp; không mở file không tin cậy.
- Memory: sự kiện theo thời gian, Reported/Verified/Conflicted/Superseded, effective vs recorded date; cả bản cũ lẫn mâu thuẫn được giữ. User xác minh có lý do và revision; không tự đổi mốc/KPI.
- Planner: lịch nhắc SQLite, catch-up khi mở lại, notification đọc/bỏ qua/hoãn, sáng 08:00/chiều 17:00 và tổng kết thứ Sáu (cấu hình được). Hỏi tối đa 5 việc, xét risk/due/next-check/last-asked/version; tránh hỏi lại nội dung không thay đổi hằng ngày. Briefing luôn được ghi nhãn rules-based; hỏi AI trong chat nếu muốn diễn giải bằng model.
- SMTP tùy chọn, test email cần xác nhận chính xác recipient. Auto reminder email mặc định tắt, chỉ bật sau test thành công và user tick cho phép tự gửi đến recipient đó. Gửi Unknown/Failed không được báo Sent hoặc tự retry gây trùng.
- Đổi mật khẩu có mật khẩu hiện tại; thu hồi phiên đăng nhập. Chẩn đoán schema/DB/worker/delivery và backup/restore có kiểm chứng.

## OpenAI & bảo mật dữ liệu
Vào **Cài đặt → AI & SMTP**. Nhập **khóa mới**, Model ID mà tài khoản API của bạn hỗ trợ, rồi bật AI và **quyền xử lý dữ liệu bên ngoài riêng biệt** chỉ khi chính sách dữ liệu cho phép. Không paste key vào chat/source/email hoặc commit vào Git. Khóa đã lộ trong hội thoại phải thu hồi tại tài khoản API.

Ứng dụng gửi Responses API thật tới endpoint OpenAI cố định, `store=false`, không gửi key vào nội dung model. Tắt quyền dữ liệu hoặc thiếu key/model: không gọi provider, CRUD vẫn hoạt động. Nguồn mặc định Nội bộ và bị loại khỏi AI retrieval; chỉ nguồn bạn đánh dấu được phép mới có thể được gửi. Nội dung project/task theo context có thể rời máy sau khi bật quyền — cân nhắc chính sách công ty. Không chạy test với dữ liệu RRC thật gửi ra ngoài.

Secret lưu Windows CurrentUser DPAPI trong Data/Secrets, không hiển thị lại, không đưa vào backup. Copy sang máy/tài khoản Windows khác cần nhập lại OpenAI/SMTP secrets; app không khóa mất dữ liệu vì không đọc được secret. Password hash portable; cookie keys Data/Keys được giữ portable, cần bảo vệ toàn bộ thư mục bằng quyền Windows. Không có tính năng quên mật khẩu/recovery tự động.

Live OpenAI/SMTP chỉ được coi VERIFIED khi bạn thực sự thử an toàn trong môi trường được phép. Tests dùng fake in-process; không suy ra đã gửi thật từ kết quả mock. Tích hợp chưa live-verified không ngăn dùng offline.

## Import workbook
Dùng sample-data/9_projects_nov_2026.xlsx, preview rồi confirm. Mapping 9projects-v1 giữ từ V0.1: 9 dự án dòng 9–17, 60 việc dòng 21–80; mã ổn định/fingerprint. Nhập lại bỏ qua, không ghi đè chỉnh sửa. PIC dự kiến, trạng thái và mốc dự án giữ trong provenance; Todo/Planned chưa xác minh; “Theo kế hoạch” thành Planned, còn lại Suggested; không biến thành Committed. Không chạy công thức KPI.

## Dữ liệu và migration
Tất cả cạnh executable: Data/AIWork.db, Attachments, Backups, Logs, Keys, Secrets, Imports, RestoreQueue. Không phụ thuộc cwd. Development sử dụng Data cạnh DLL trong bin/Debug/net10.0; Release có Data riêng. Dùng publish cho dữ liệu thật, không dùng database trong artifacts.

Migration InitialV01 giữ nguyên; V02Tracking, V03Chat, V04Actions, V05InboxMemory, V10PlannerMaintenance cộng thêm. Startup có instance lock, FK/busy timeout/WAL. Khi nâng schema đã có dữ liệu: snapshot SQLite online, nâng thử bản sao và so checksum ID Project/Task + quick_check trước khi migrate DB đang dùng. Lỗi dừng an toàn, không xóa/reset DB. Có marker database.initialized để không lặng lẽ tạo lại DB bị thiếu. Không sửa migration đã giao hoặc downgrade schema.

## Backup / restore
**Backup** ở Cài đặt: SQLite Online Backup API + attachments + manifest SHA-256/schema, ghi log trong DB. Không chép riêng file DB sống trong WAL mode. Backup không chứa API/SMTP secret hay cookie keys. Snapshot không chứa audit của chính thao tác backup vừa tạo.

**Restore có UI**:
1. Đăng nhập → Bảo trì & chẩn đoán, tải ZIP V1 (tối đa 100 MB nén/500 MB giải nén).
2. Hệ thống kiểm manifest/path traversal/duplicate entries/hash/SQLite quick_check/FK/schema và tham chiếu attachment. Xem đúng phiên bản/tập file.
3. Gõ PHUC HOI để xác nhận. App tạo pre-restore backup, xếp request đã kiểm chứng, dừng sạch.
4. Chạy lại Start.bat. Chỉ lúc offline và giữ instance lock mới chuyển DB/attachments; bản trước nằm Data/Backups/pre-restore-files-* để quay lại. Nếu lỗi chuyển file, cố gắng rollback; không xóa DB thật.
5. Đăng nhập lại; quyền AI/SMTP bị tắt để bạn rà soát. Bí mật vẫn nằm ngoài backup, có thể cần nhập lại khi chuyển máy.

Backup V0.1 không có manifest bị từ chối bởi UI V1; dùng hướng dẫn offline cũ trong docs/V01_README.md trên một thư mục sao chép riêng, sau đó chạy app để migrate và tạo backup V1. Không chép đè DB đang mở. Nếu cần quay lại bản trước restore: đóng app, giữ bản Data hiện tại, chuyển DB/attachments từ pre-restore-files-* khi offline, gồm sidecars nếu có. Không downgrade binary/schema; giữ một bản source/binary tương ứng. Không dùng SQLite trên network share.

## Build / test / publish
Mở AIWorkAssistant.sln với Visual Studio hỗ trợ .NET 10. Từ root:
```powershell
dotnet restore
dotnet build --no-restore
dotnet test --no-build --no-restore
dotnet run --project src/AIWork.Web --no-build
```
Chỉ restore khi dependencies đổi hoặc thiếu packages. Khi sửa một nhóm, chạy filtered tests; trước nghiệm thu chạy full build/tests.

`publish-win-x64.bat` gọi PowerShell script có fingerprint dependencies/RID; chỉ restore khi cần, build Release, full tests và publish win-x64 self-contained. Giữ nguyên Data và appsettings đã có. Dừng app trước khi nâng binary, tạo backup và thử nâng bản sao trước. Source test dùng DB tạm; artifacts/TestResults có TRX.

Xem IMPLEMENTATION_REPORT_V1.md, TEST_REPORT_V1.md và V1_RELEASE_NOTES.md để biết bằng chứng thực chạy và hạng mục UNVERIFIED. Đọc docs/V01_README.md chỉ cho lịch sử V0.1; README này là hướng dẫn hiện tại.

### Ghi nhớ đăng nhập
Trang đăng nhập có lựa chọn Ghi nhớ đăng nhập trong 30 ngày, mặc định tắt. Chỉ cookie xác thực được giữ lại, không lưu mật khẩu rõ. Bỏ chọn dùng phiên 8 giờ với cookie phiên trình duyệt. Đăng xuất hoặc đổi mật khẩu thu hồi phiên như trước.

### Workbook mẫu trong bản source
File Excel được cung cấp cục bộ không đưa vào Git mặc định để tránh tải dữ liệu công ty lên GitHub. Đặt lại workbook của bạn tại sample-data/9_projects_nov_2026.xlsx để chạy nhóm kiểm thử importer; không thay bằng dữ liệu giả.


### Cấu hình tự động trên máy hiện tại
Khóa có thể được cấp sẵn trong kho DPAPI cục bộ Data/Secrets và model/quyền AI trong SQLite, không cần nhập lại ở mỗi lần chạy. Secret không có trong GitHub; clone trên máy khác mặc định vẫn cần cấu hình riêng. Công cụ maintenance hỗ trợ ai-status (chỉ hiển thị trạng thái), configure-ai (nhận JSON qua stdin, không truyền key qua command argument), verify-ai (chỉ gửi câu kiểm tra tổng hợp không có dữ liệu công việc). Không log key.


## V1.1 upgrade
See [V11_GUIDE.md](V11_GUIDE.md) for source-preserving startup, upload/AI entry, streaming chat and themes. The new publish is publish/AIWorkAssistant-1.1. Licensed DevExtreme runtime verification remains blocked without supplied licensed assets; see DEVEXTREME_SETUP.md.
