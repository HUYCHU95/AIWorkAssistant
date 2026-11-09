# V1.1 — khởi động và sử dụng

1. Đóng cửa sổ ứng dụng cũ bằng Ctrl+C trước khi chuyển bản. Không chạy hai bản dùng hai bản sao dữ liệu đồng thời.
2. Chạy `Start.bat` tại thư mục source. Script chọn bản `publish/AIWorkAssistant-1.1`, tự sao chép Data + cấu hình từ bản cũ duy nhất lần đầu khi ứng dụng cũ đã đóng. Bản cũ vẫn nguyên vẹn. Nếu bản cũ đang chạy, Start mở lại website của bản cũ và hướng dẫn đóng bằng Ctrl+C trước khi nâng cấp; không kill process.
3. Ứng dụng kiểm tra migration trên bản sao, sao lưu DB, rồi thêm 3 bảng V11Experience. Đăng nhập bằng tài khoản hiện có; API key cục bộ tiếp tục dùng trên cùng tài khoản Windows.
4. Khi chuyển máy: đóng app, copy toàn bộ thư mục publish/AIWorkAssistant-1.1 gồm Data. Chạy Start.bat bên trong. Không cần .NET/SQL Server. Nhập lại API key/SMTP trong Integrations vì DPAPI gắn với tài khoản Windows cũ. Không copy dữ liệu thử trong artifacts.

Dashboard: KPI, biểu đồ sức khỏe, tiến độ theo số task hoàn thành; kế hoạch thiếu hạn Planned/Committed sẽ là N/A. Timeline chỉ hiển thị ngày đã lưu; hạn Suggested khác màu/nét. Bảng có tìm/lọc/nhóm/phân trang và sửa inline, chống ghi đè version.

Nhập liệu: mở form Project/Workstream/Task/Tracking/Inbox/Source/Memory, kéo file hoặc dán ảnh vào AI Assisted Entry. File gốc lưu trước, có checksum. Giữ Nội bộ để xử lý cục bộ; chỉ bỏ chọn khi được phép gửi tài liệu đó ra OpenAI và đã bật AI/consent trong Integrations. Chỉnh các trường đề xuất rồi chọn điền vào form và bấm Lưu. Các mục trích xuất hàng loạt tạo phiếu, từng phiếu cần xác nhận. Không tự nhận email là bằng chứng nghiệm thu.

Chat: Enter gửi, Ctrl/Shift+Enter xuống dòng; có thể đổi Ctrl+Enter gửi trong Appearance. Tin nhắn lưu trước khi gọi AI; delta streaming, tool results riêng, Dừng giữ phần đã nhận; retry cùng mã không tạo lượt trùng. Muốn hỏi lại một lượt Failed/Cancelled, gửi một tin nhắn mới. Markdown không render HTML thô. File đính kèm giữ link nguồn trong lịch sử.

Giới hạn: 8 file/lần, 5 MB/file, tổng 25 MB; PDF 20 trang, Excel 8 sheet/3000 cell, Word 3 ảnh nhúng. Text dài bị giới hạn 60k; không chạy macro/formula. OCR dùng model vision, không giả vờ có OCR offline. DevExtreme cần thư viện/giấy phép của người dùng: xem DEVEXTREME_SETUP.md. Các charts/grid tiêu chuẩn luôn chạy offline.

Kiểm thử: xem IMPLEMENTATION_REPORT_V11.md. Để build/publish lại: `powershell -NoProfile -ExecutionPolicy Bypass -File publish-win-x64.ps1 -OutputFolder publish/AIWorkAssistant-1.1`. Nếu bản đích đang chạy, đóng đúng bản đó trước; script không xóa Data.
