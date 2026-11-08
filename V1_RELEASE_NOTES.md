# AIWorkAssistant V1.0 — release notes

Mở publish/AIWorkAssistant/Start.bat. Dữ liệu/tài khoản V0.1 giữ nguyên; schema nâng cộng thêm, snapshot và nâng thử bản sao trước khi mở app. Nếu thiếu key/consent, AI báo unavailable; toàn bộ quản lý offline tiếp tục dùng được.

V0.2 tracking/sources/dependencies; V0.3 Responses chat/history/read tools/usage; V0.4 reviewed write tools/receipts/idempotency/undo; V0.5 Inbox/attachments/temporal facts; V1.0 reminders/notifications/SMTP optional/verified backup/queued offline restore/diagnostics/password change.

Quyết định an toàn: tất cả model writes cần review tại runtime. Trích checklist Inbox là rules-based, không giả AI. PDF/Office/image extraction unsupported, chỉ lưu an toàn. AI/SMTP secrets dùng Windows DPAPI, nhập lại sau chuyển máy; ngoại vi mặc định tắt. Không dùng khóa đã paste trong chat.

Live OpenAI và SMTP UNVERIFIED trong phiên triển khai; fake tests không phải live evidence. Chưa có máy Windows thứ hai sạch không .NET để kiểm chứng; gói self-contained đã build sạch, 22/22 tests PASS và native EXE/Start.bat smoke/restore/restart PASS, xem TEST_REPORT_V1. Không có tiến trình làm tiếp sau khi chat kết thúc.

Dừng Ctrl+C trước copy Data. Restore: upload backup V1 có manifest, review, PHUC HOI, app dừng; chạy Start.bat để swap offline và giữ bản cũ. Không xóa Data để sửa migration.


### Ghi nhớ đăng nhập — bản cập nhật
Checkbox tùy chọn nhớ đăng nhập 30 ngày đã được thêm, mặc định tắt; không lưu mật khẩu rõ. Do bản đang dùng khóa DLL khi publish, bản cập nhật nằm tại publish/AIWorkAssistant-1.0.1. Đóng ứng dụng trước khi thay các file chương trình trong thư mục đang dùng; giữ nguyên toàn bộ Data. Không khởi động thư mục mới với Data trống nếu muốn tiếp tục dữ liệu hiện tại.

