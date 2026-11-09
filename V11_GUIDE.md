# Khởi động AIWork V1.1.1 và nâng cấp từ bản cũ

Chạy **Start.bat tại thư mục source**. Bộ cài lấy bản đã publish ở `publish/AIWorkAssistant-1.1` và cập nhật mã vào **thư mục đang dùng `publish/AIWorkAssistant`**. Database, attachments, API key mã hóa, cookie keys và cấu hình vẫn ở chỗ cũ; không tạo database nghiệp vụ thứ hai.

Nếu bản cũ còn chạy, hãy đóng đúng ứng dụng AIWork trước. Start sẽ báo rõ cần cập nhật, không âm thầm mở lại giao diện cũ. Không tự kill ứng dụng. Sau khi đóng, chạy Start lại: mã cũ và SQLite được sao lưu vào `Data/Backups/pre-deploy-*`, rồi mới cài mã mới. Startup sao lưu/probe migration như trước.

Website mới có:
- Trang chủ **Dashboard công việc**, 8 KPI, biểu đồ, timeline và bảng công việc.
- Sidebar nhóm AI Workspace / Work Management / Planning & Reports / Settings.
- Khối **AI Analyze / Auto Fill ở đầu form** Project, Task, Workstream, hồ sơ theo dõi, Inbox, Source và Memory.
- Dòng phiên bản **AIWork V1.1.1** ở cuối trang. Endpoint `/System/Version` giúp launcher đối chiếu đúng mã đang chạy.

**Nhập liệu AI:** chọn nhiều PDF/DOCX/XLSX/TXT/ảnh, kéo thả hoặc dán ảnh. Có preview, kích thước và nút bỏ file. Tùy chọn tự đọc sau upload mặc định bật. Nguồn Nội bộ chỉ trích xuất cục bộ; chỉ bỏ chọn khi được phép gửi chính nguồn đó đến OpenAI và global consent đã bật. Nhấn AI Analyze để phân tích; chỉnh các trường rồi chọn Điền vào form, cuối cùng Lưu. Các mục hàng loạt tạo phiếu để duyệt; không tự thay mốc cam kết hoặc tự xác nhận nghiệm thu. File gốc và liên kết được giữ.

**Chat:** Enter gửi, Ctrl/Shift+Enter xuống dòng; có chế độ Ctrl+Enter gửi trong Appearance. Chat không reload, có streaming, Stop, retry cùng mã chống gửi trùng, lịch sử SQLite, Markdown an toàn và Action Cards từ receipt thực tế. Muốn thử lại một lượt đã Failed/Cancelled bằng một lần gọi AI mới, gửi tin nhắn mới.

**Appearance:** Light / Dark / System, accent từng nhóm, một màu thống nhất hoặc JSON màu riêng từng controller. Lưu trong SQLite.

**Dashboard:** dữ liệu từ SQLite; Actual theo số task Done; Planned thiếu hạn Planned/Committed hiển thị N/A; Unknown khác OnTrack. Bảng có search/group/sort/page/edit và kiểm tra version. Timeline chỉ dùng ngày thật đã lưu. DevExtreme chỉ kích hoạt khi có thư viện và giấy phép hợp lệ tại Data/LicensedUI; nếu chưa có, dùng charts/grid tiêu chuẩn được phép theo điều khoản phương án tương đương trong yêu cầu. Không đóng gói bản trial hoặc giả license. Xem DEVEXTREME_SETUP.md.

**Giới hạn file:** 8 file/lần, 5 MB/file, tổng 25 MB. PDF tối đa 20 trang; Excel 8 sheet/3000 cell; Word tối đa 3 ảnh nhúng cho vision. Không chạy macro/formula. Ảnh và bản scan cần model vision; không giả OCR offline.

**Chuyển máy:** đóng app, copy toàn bộ `publish/AIWorkAssistant` gồm Data sang máy mới, chạy Start.bat trong đó. Không cần .NET/SQL Server. Nhập lại API key/SMTP vì DPAPI gắn với tài khoản Windows cũ. Không copy fixture trong artifacts.

**Build lại:** `powershell -NoProfile -ExecutionPolicy Bypass -File publish-win-x64.ps1 -OutputFolder publish/AIWorkAssistant-1.1`. Sau publish, dùng Start.bat tại source để cài vào thư mục chính, khi ứng dụng cũ đã đóng.
