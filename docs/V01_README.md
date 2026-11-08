# AI Personal Work Assistant — V0.1

Ứng dụng cá nhân Windows x64, ASP.NET Core 10 MVC / EF Core SQLite. Hoạt động offline, không cần OpenAI API key. LAN tắt; chỉ lắng nghe http://127.0.0.1:5088.

## Khởi động bản portable
1. Mở thư mục `publish\AIWorkAssistant`.
2. Chạy `Start.bat` (Windows PowerShell đi kèm Windows). Giữ cửa sổ console để ứng dụng tiếp tục hoạt động; trình duyệt mở sau khi sẵn sàng.
3. Lần đầu tạo tài khoản: tên 3–64 ký tự Latin/số/dấu ._-; mật khẩu 12–128 ký tự, không có mật khẩu mặc định.
4. Tạo luồng, dự án và công việc; hoặc vào **Nhập dữ liệu Excel**, chọn `sample-data\9_projects_nov_2026.xlsx`, xem kỹ mapping/cảnh báo rồi xác nhận.
5. Dừng sạch bằng Ctrl+C. Không tắt nguồn khi đang ghi dữ liệu.

Nếu cổng bận, đóng ứng dụng đang dùng cổng hoặc sửa `LocalPort` trong `appsettings.json` (1024–65535), rồi chạy lại. Luôn dùng 127.0.0.1 với cổng đã chọn. Không cần SQL Server, IIS, Docker, Node hay .NET Runtime trên máy đích.

## Phát triển / Visual Studio
Cần .NET 10 SDK. Mở `AIWorkAssistant.sln` bằng Visual Studio hỗ trợ .NET 10.

```powershell
dotnet restore
dotnet build
dotnet test
dotnet run --project src/AIWork.Web
```

Trong development, Data nằm cạnh DLL trong `src/AIWork.Web/bin/Debug/net10.0/Data`; chạy Release dùng thư mục Release riêng. Chỉ dùng development cho dữ liệu thử nghiệm. Trong publish, Data nằm cạnh EXE. Không phụ thuộc current working directory. Cấu hình runtime `appsettings.json` cũng nằm cạnh EXE; để thay cấu hình development lâu dài, sửa file nguồn rồi build.

Chạy `publish-win-x64.bat` để build, test và xuất Windows x64 self-contained. Script không xóa `Data` hiện có. Khi nâng cấp, sao lưu và dừng bản đang chạy trước khi publish/chép tệp mới. Không chạy hai bản trỏ cùng Data; ứng dụng giữ exclusive file lock.

## Sử dụng
- Home: **Work Briefing (rules-based)**, ưu tiên tối đa 5 việc, đếm việc quá hạn/chờ/vướng mắc/cần bổ sung theo SQLite. AI Integration — planned for V0.3.
- Luồng công việc có cha tùy chọn; không cho phép vòng lặp.
- Dự án có mã duy nhất, luồng, PIC đã xác nhận, giai đoạn, trạng thái, sức khỏe, mốc kế hoạch/mục tiêu/thực tế riêng.
- Công việc có thể thuộc dự án, luồng độc lập, hoặc hoàn toàn độc lập. Công việc thuộc dự án lấy luồng từ dự án, không gán luồng khác.
- Ngày hạn có ba loại: Committed (cam kết), Planned (kế hoạch), Suggested (gợi ý cần xác nhận). Ngày và loại hạn phải cùng có hoặc cùng trống.
- Done ghi thời điểm hoàn thành; thiếu bằng chứng được ghi rõ. Done/Cancelled không tính quá hạn; ngày trống không quá hạn. Tiến độ bỏ công việc đã hủy, hiển thị N/A khi không có việc tính được.
- Tìm kiếm tên/từ khóa, lọc trạng thái/PIC/dự án/ngày và sắp xếp theo hạn/ưu tiên/cập nhật.
- Trang chi tiết hiển thị audit trước/sau, actor và giờ Việt Nam; database lưu UTC. Sửa cùng bản ghi ở hai cửa sổ sẽ báo xung đột thay vì ghi đè im lặng.
- Lưu trữ mềm giữ nguyên lịch sử. Lưu trữ dự án không tự lưu trữ công việc con. Xem lại bằng bộ lọc “Gồm lưu trữ”.

## Mapping Excel 9projects-v1
ClosedXML (MIT, https://www.nuget.org/packages/ClosedXML/0.105.1), không cần Excel cài đặt. Chỉ hỗ trợ mẫu sheet `Quan_ly_9_du_an`; không phải importer Excel tổng quát.

- Dự án dòng 9–17: A -> AUTO-01..09; B -> luồng; C -> tên. Mốc F, PIC dự kiến G, trạng thái I, ghi chú N được giữ trong nguồn, không tự chuyển thành cam kết/người phụ trách/thực tế hoàn thành.
- Công việc dòng 21–80: A -> ID ổn định; B -> liên kết dự án; C -> tiêu đề; D -> ngày date-only; G -> ưu tiên; N “Theo kế hoạch” -> Planned, còn lại Suggested. E/F/H/I/J/K được giữ trong nguồn để rà soát. PIC để trống, trạng thái nhập Todo; dự án Planned/Unknown.
- KPI và công thức không được nhập hoặc chạy. Công thức ở cột dữ liệu nguồn bị từ chối. Tối đa 5 MB nén / 20 MB giải nén, 200 ZIP entries; Razor encode nội dung.
- Preview không ghi dữ liệu nghiệp vụ; confirm kiểm tra lại SHA-256 và nhập trong một transaction với audit. 9 dự án + 60 việc = 69 bản ghi báo cáo, không bao gồm các luồng được tạo.
- Nhập lại bỏ qua mã dự án và fingerprint ID đã tồn tại, kể cả lưu trữ; giữ nguyên chỉnh sửa người dùng. Cùng mã nhưng khác tên dự án sẽ từ chối cả lô. Import không phải đồng bộ cập nhật workbook.

## Dữ liệu / sao lưu / phục hồi
`Data/AIWork.db`, `Data/Attachments`, `Data/Backups`, `Data/Logs`, `Data/Keys`, `Data/Imports` đều cạnh executable. Không có dữ liệu mutable trong wwwroot. Imports chứa tệp preview tạm, được xóa sau confirm; bản preview bị hủy có thể xóa khi ứng dụng đã dừng.

Vào **Cài đặt & sao lưu**, bấm tạo sao lưu: SQLite Online Backup API tạo database nhất quán, ZIP gồm database + attachments + metadata. ZIP lưu trong Backups và được tải về. Không chép một DB đang chạy bằng File Explorer: WAL có thể chứa giao dịch chưa checkpoint. Bản snapshot không chứa audit của chính thao tác backup vừa tạo, nhưng database đang dùng có audit đó.

Phục hồi an toàn:
1. Ctrl+C và đóng tất cả bản ứng dụng; giữ bản sao toàn bộ Data hiện tại để quay lại.
2. Giải nén ZIP vào thư mục tạm. Dùng cùng phiên bản hoặc mới hơn có migration tương thích. Không downgrade schema.
3. Đổi tên Data hiện tại thành thư mục lưu trữ ngoài thư mục ứng dụng; tạo Data mới, chép AIWork.db và Attachments từ ZIP vào. Không giữ các tệp -wal/-shm cũ.
4. Chạy Start.bat và đăng nhập tài khoản trong bản sao lưu. Kiểm tra dự án/công việc/lịch sử. Khóa cookie sẽ tạo lại, nên cần đăng nhập lại.

Chuyển máy: dừng ứng dụng rồi copy **toàn bộ thư mục publish**, gồm Data và cấu hình, sang ổ cục bộ Windows x64. Không đặt SQLite trên network share. Password hash không gắn Windows DPAPI; khóa cookie persist trong Data/Keys không mã hóa gắn máy để giữ portable. Chỉ cấp quyền đọc/ghi thư mục cho tài khoản Windows của bạn, không đưa Data lên Git hoặc chia sẻ công khai. V0.1 chưa có lấy lại/đổi mật khẩu trong UI; giữ mật khẩu an toàn. Các key này không phải OpenAI API key.

## Migration và xử lý lỗi
Migration nằm trong `src/AIWork.Infrastructure/Migrations`, tự chạy lần đầu. Không dùng EnsureDeleted/EnsureCreated, không reset DB. Trước migration nâng cấp khi đã có schema, startup tạo online snapshot trong Backups. Nếu migration lỗi, dừng startup, giữ nguyên DB, xem console + Data/Logs/startup.log; không xóa DB để thử lại. Kiểm tra quyền ghi, dung lượng và bản backup. Port/lock lỗi có thông báo console.

Tạo migration mới khi thay schema, không sửa migration đã giao:
```powershell
dotnet tool restore
dotnet ef migrations add TenThayDoi --project src/AIWork.Infrastructure --startup-project src/AIWork.Web
```
Chỉ chạy EF CLI khi app đã dừng. Tests sử dụng DB tạm cô lập, không dùng Data cá nhân. Backup trước nâng cấp; kiểm tra restore trên bản sao trước khi áp dụng dữ liệu quan trọng.

## Giới hạn phiên bản
Không chat AI giả, không gửi/đọc email, không reminder nền, không tự động ghi bằng AI. Attachments có thư mục và được backup nhưng chưa có UI upload (V0.5). People dùng PIC free-text đã xác nhận trong V0.1. V0.2: Issues, Commitments, Instructions, Decisions, dependencies và nguồn liên kết; AI thật dành cho V0.3 trở đi. LAN chưa hỗ trợ; khi phát triển LAN cần HTTPS với chứng chỉ tin cậy, secure cookie và cấu hình xác thực phù hợp, không chỉ đổi IP binding.
