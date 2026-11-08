# Báo cáo triển khai AIWork V0.1

Ngày kiểm chứng: 09/11/2026 (giờ Việt Nam). Source đã triển khai và bản Windows x64 self-contained đã publish/chạy thật trên máy hiện tại.

## Kết quả đã thực hiện
- Solution modular monolith: Domain, Application, Infrastructure, Web MVC, Tests; net10.0, EF Core SQLite 10.0.14, ClosedXML 0.105.1 (MIT), SQLitePCLRaw 3.0.2. Không cần Node, Excel, SQL Server, IIS hoặc OpenAI key lúc chạy.
- First-run setup tài khoản duy nhất; Identity PasswordHasher, cookie HttpOnly/SameSite Strict, CSRF toàn bộ POST, giới hạn đăng nhập sai 5 lần/5 phút; không có mật khẩu mặc định. Setup race được chặn bởi PK singleton trong transaction lưu.
- Workstreams CRUD, parent cycle validation; Projects CRUD, phase/status/health/targets/PIC; Tasks CRUD, independent/project/workstream, six states, priority, owner, date type, next action, completion time/evidence.
- Lưu trữ mềm, tìm kiếm và bộ lọc; audit before/after + actor + UTC trong cùng SaveChanges; optimistic Version concurrency.
- Rules-based Home tối đa 5 ưu tiên, số liệu truy vấn thật. Ghi rõ AI Integration — planned for V0.3; không có chat/model giả.
- Focused importer 9projects-v1: preview 9 dự án/60 việc, mapping, cảnh báo, confirm anti-CSRF, kiểm tra SHA-256, transaction, stable fingerprint, import history. Nhập lại không trùng và không ghi đè chỉnh sửa thủ công. PIC dự kiến/trạng thái nguồn/mốc tháng giữ nguyên văn để xác minh; không bịa thành cam kết hoặc hoàn thành.
- EF migration InitialV01 + snapshot trong source; tự migrate, SQLite FK + timeout + WAL, exclusive file lock; online snapshot trước nâng cấp schema; migration lỗi không reset dữ liệu.
- Tất cả Data và config cạnh executable; Data/Attachments, Backups, Logs, Keys, Imports ngoài wwwroot. Backup ZIP qua SQLite Online Backup API, kèm attachments/metadata. Hướng dẫn phục hồi offline và chuyển máy trong README.
- UI tiếng Việt, assets cục bộ, responsive; empty states, trang lỗi, lịch sử thay đổi. Scripts publish và Start mở browser sau readiness, giữ console cho Ctrl+C. Publish giữ Data và bảo toàn appsettings hiện có qua appsettings.pre-publish.json.

## Lệnh và bằng chứng thực tế
1. `dotnet --info`: SDK 10.0.403, runtime 10.0.14, win-x64. Lần đầu sandbox chặn Service Control Manager/cache; chạy lại với quyền đã được cấp thành công.
2. `dotnet build`: thành công, 0 warnings / 0 errors sau khi sửa lỗi Razor/controller mẫu.
3. `dotnet ef migrations add InitialV01 --project src/AIWork.Infrastructure --startup-project src/AIWork.Web --output-dir Migrations`: thành công.
4. `dotnet test --logger "console;verbosity=normal"`: 4/4 tests qua. Đây là 4 test cases, gồm nhiều assertion và nhiều bước HTTP, không phải chỉ 4 thao tác.
5. `publish-win-x64.bat`: restore, Release build, 4/4 Release tests, self-contained publish thành công. Output `publish/AIWorkAssistant/AIWork.Web.exe`, `coreclr.dll`, `AIWork.Web.runtimeconfig.json` với includedFrameworks 10.0.14, Start.bat/Start.ps1/README/sample workbook.
6. `dotnet run --project src/AIWork.Web --no-build --no-launch-profile`: app chạy trên 127.0.0.1:5088; HTTP setup=200, netstat xác nhận TCP 127.0.0.1:5088 LISTENING, CSS=200.
7. Copy publish sang `artifacts/PortableSmoke`, chạy EXE với cwd ở root repository: content root và SQLite đúng cạnh EXE. Setup/login bằng tài khoản kiểm thử ngẫu nhiên. Trình duyệt thực tế upload workbook, preview 9/60, confirm báo tạo 69 và bỏ qua 0; Home hiển thị 9 dự án, 60 việc, 0 hoàn thành, không gán PIC dự kiến.
8. Dừng EXE, copy toàn bộ bản thử gồm Data sang `artifacts/PortableMoved`, chạy lại từ cwd khác: login mật khẩu cũ HTTP=200; 9 dự án và 60 việc vẫn hiện đầy đủ. Không sửa DB bản publish bàn giao để seed test.
9. Quan sát responsive ở viewport 1440x1000 và 390x844: không tràn ngang toàn trang. Ảnh `artifacts/dashboard-desktop.png`, `artifacts/dashboard-mobile.png`. Kiểm tra hình ảnh thực tế trên trình duyệt, không phải mockup.

10. Chạy trực tiếp `Start.bat` trong bản thử: Kestrel khởi động, readiness HTTP=200. Kết thúc các tiến trình kiểm thử trước bàn giao.

## Những gì tests kiểm tra
- Date-only overdue theo ngày Việt Nam; thiếu ngày không quá hạn; Done/Cancelled không quá hạn; completion timestamp và reopen; progress N/A và không tính hủy; cặp ngày/loại hạn và assignment không xung đột.
- Migration DB tạm; importer đọc đúng workbook; 9 dự án/60 Todo, không owner mặc định, không Committed; import 2 lần: 69 mới rồi 0 mới/69 bỏ qua; giữ title đã sửa; audit update before/after; mở DbContext mới giữ dữ liệu; SQLite backup restore integrity_check=ok; hai context ghi stale gây concurrency exception; workbook lỗi bị từ chối.
- HTTP thật qua ASP.NET TestServer: first-run redirect; POST thiếu CSRF bị 400; setup/login/logout; protected routes; list/detail; tạo workstream/project/task/independent task; sửa trạng thái; stale update không ghi đè; ngày thiếu loại bị reject; archive; multipart preview/confirm; backup ZIP; kiểm tra DB/audit sau request.
- Regression lưu trữ dự án rồi sửa trạng thái công việc con: liên kết dự án được giữ lại, không bị rơi khỏi dropdown.
- Cảnh báo TestServer không hỗ trợ IHttpMaxRequestBodySizeFeature chỉ thuộc test server. Kestrel thật áp dụng giới hạn; controller tiếp tục kiểm tra file length và zip expanded size.

## Lỗi đã tìm và khắc phục
- Trùng HomeController của template và thiếu ký tự đóng thẻ Razor: đã sửa và build lại.
- Thứ tự middleware khiến first-run đi login trước setup: đã chuyển kiểm tra setup sau authentication/trước authorization.
- Backup file bị connection pool giữ khóa: tắt pooling cho destination snapshot, test tạo ZIP và restore đã qua.
- NuGet cảnh báo SQLite native cũ khi dùng EF 10.0.0: cập nhật 10.0.14/SQLitePCLRaw 3.0.2; build cuối không còn cảnh báo đó.

## Khác biệt có chủ ý / giới hạn
- People chưa tạo bảng/UI vì V0.1 cho phép PIC free-text; PIC dự kiến Excel giữ trong provenance, chỉ người dùng xác nhận mới gán OwnerText.
- Mapping chỉ hỗ trợ đúng workbook mẫu, không import Excel tùy ý. Status nguồn không đủ bằng chứng nên mặc định Planned/Todo với cảnh báo rõ; mốc dự án không đưa vào ngày mục tiêu thực tế. Import không đồng bộ cập nhật từ Excel.
- UI attachments, account recovery/change password, restore online và quick note chưa có; không tạo navigation giả. Thư mục attachments và backup sẵn sàng, restore thủ công offline được hướng dẫn.
- Lưu trữ chưa có nút khôi phục trong UI; dữ liệu/history vẫn còn và có bộ lọc xem bản lưu trữ. Lưu trữ dự án không tự thay đổi công việc con.
- Keys cookie không mã hóa gắn máy để portable; quyền Windows phải bảo vệ toàn bộ Data. Không có API key. Chưa hỗ trợ LAN/HTTPS, không mở bind public.
- Chưa kiểm tra trên máy Windows x64 thứ hai hoàn toàn không cài .NET. Đã kiểm tra gói included runtime, chạy EXE, chuyển thư mục/cwd, tài khoản và DB tồn tại sau restart trên máy hiện tại.
- Chưa fault-injection mất điện giữa migration hoặc kiểm thử dung lượng đĩa đầy; không tuyên bố đã kiểm chứng các tình huống đó. Startup giữ DB khi lỗi và có pre-migration snapshot khi nâng cấp.
- Không tạo commit vì repository ban đầu không có Git metadata. Mã nguồn/migration được giữ đầy đủ trong thư mục dự án.

## V0.2 tiếp theo
Issues, Commitments, Instructions, Decisions, dependency tracking và liên kết nguồn sâu hơn; bộ lọc nâng cao. AI Responses API/read assistant dành V0.3; email/reminder/background worker ngoài V0.1.

## Cách bắt đầu
Mở `publish/AIWorkAssistant/Start.bat`, tạo tài khoản của bạn, vào Nhập Excel và chọn `sample-data/9_projects_nov_2026.xlsx`. Review rồi confirm. Không sử dụng tài khoản hoặc Data trong artifacts để làm dữ liệu thật. Source solution `AIWorkAssistant.sln`; hướng dẫn đầy đủ trong README.md.


## Kiểm chứng binary cuối
Sau regression giữ liên kết lưu trữ, pipeline Release chạy lại thành công: 0 warnings/errors, 4/4 tests. Chạy binary mới nhất trong artifacts/FinalPortable từ bản copy dữ liệu thử: HTTP login=200, 9 projects, 60 tasks. Runtime coreclr.dll có trong gói. Bản publish chính không chứa Data/tài khoản kiểm thử. Đã dừng tiến trình thử.

