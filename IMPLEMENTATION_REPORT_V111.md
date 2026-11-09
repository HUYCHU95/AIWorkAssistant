# V1.1.1 — sửa bản triển khai và nghiệm thu lại theo yêu cầu UI/AI

## Nguyên nhân người dùng vẫn thấy giao diện cũ
Source và thư mục publish mới đã có các chức năng V1.1, nhưng tiến trình cũ ở thư mục publish/AIWorkAssistant vẫn phục vụ cổng 5088. Sửa Start trước đó chỉ mở lại process cũ nên người dùng không nhận được bản nâng cấp. Đây là lỗi triển khai; không thể coi cập nhật source là đã cập nhật website đang dùng.

## Sửa trong lần này
- Bộ cài cập nhật code vào chính thư mục publish/AIWorkAssistant, giữ nguyên Data và appsettings. Không sao chép dữ liệu nghiệp vụ sang nhánh runtime khác. Backup DB/code trước thay mã; manifest ghi sau cùng. Khởi động lại bộ cài không lặp backup nếu DLL/JS/CSS/version đã khớp.
- Không âm thầm mở lại phiên bản cũ. Launcher đối chiếu /System/Version; footer hiển thị V1.1.1. Nếu process cũ còn giữ DB, cần dừng phiên đó trước khi thay mã.
- AI entry đặt ở đầu cả 7 nhóm form, có nút AI Analyze / Auto Fill, nhiều file/preview, chế độ tự đọc sau upload. Nguồn nội bộ chỉ trích cục bộ, không gửi AI. Giữ chỉnh sửa kết quả trước khi điền/lưu.
- Timeline có dữ liệu phụ thuộc: sửa lỗi JavaScript tham chiếu phần tử, kiểm thử với một quan hệ thật trong fixture thay vì chỉ danh sách rỗng.
- Chat Action Cards lấy trạng thái receipt từ SQLite. Tool create_project/create_workstream bổ sung trường code trong schema để tạo đề xuất hợp lệ, vẫn phải duyệt trước khi ghi.
- Giữ grouped menu, dashboard SQLite, chat SSE/Stop/phím tắt, theme và các chức năng quản lý cũ. Không thêm migration mới trong lần sửa này; tiếp tục dùng additive V11Experience.

## Bằng chứng đã chạy
- Full Release build: 0 warning, 0 error; 32 xUnit/HTTP integration test PASS. HTTP kiểm tra version và AI entry hiện đúng một lần trên cả 7 nhóm form; test project chat tool có code, chưa tạo Project trước khi duyệt.
- JavaScript: parse 5 file; test Markdown chống raw HTML/script/javascript URL, code block và table trên fixture cô lập PASS. Script kiểm thử tools/check-web-js.mjs chỉ dùng cho phát triển, không yêu cầu Node khi chạy ứng dụng.
- EXE self-contained: smoke setup/auth/import 9 dự án 60 task, Inbox + 3 task duyệt, dependency Dashboard, version 1.1.1, backup/restore/password/63 task PASS.
- Installer fixture: giữ nguyên hash DB, appsettings, attachment; backup code cũ; chạy hai lần không sao lưu lặp PASS.
- Browser fixture: 8 KPI, 12 dòng/page, một quan hệ dependency, loading kết thúc, không console error. Form Task có AI entry trước manual editor. Upload TXT + PNG thật, tự lưu nguồn, trích TXT cục bộ, ảnh báo cần vision — không giả đã OCR. Bài kiểm tra OpenAI thật chỉ dùng file tổng hợp, không dùng tài liệu công ty.
- Snapshot dữ liệu thật trước deploy: 0 project, 0 task, 37 audit, 6 migrations, quick_check ok. Snapshot artifacts/redesign-backup/before-deploy-v111.db. Không đưa DB/keys/attachments/publish vào Git.

## Phạm vi còn giới hạn
DevExtreme runtime chưa có license/assets do người dùng cung cấp; charts/grid tiêu chuẩn hoạt động theo quyền dùng phương án tương đương trong mục 3 của yêu cầu. Không báo DevExtreme PASS. Không gửi email thật hoặc tài liệu công ty để thử. Các kiểm tra live vision trước đó dùng PNG/TXT tổng hợp; PDF/DOCX/XLSX có kiểm thử trích xuất cục bộ. Xem báo cáo V11 và DEVEXTREME_SETUP.md.

Trạng thái triển khai website thật được cập nhật sau khi dừng process cũ/cài bản mới; không coi trang fixture là website production. Hướng dẫn mới: V11_GUIDE.md.

Browser live AI acceptance: uploaded PNG + TXT synthetic files through the actual chooser; local auto-extraction completed. With synthetic-source permission enabled, real OpenAI returned Ready and one task suggestion plus ambiguity warnings. Cleared unconfirmed OwnerText, applied three fields, clicked Save. Persisted task Review pump drawing, PIC/deadline unknown, source link and audit visible in Details. This was isolated fixture data, not production. Full HD screenshot: artifacts/v111-dashboard.png; AI form: artifacts/v111-ai-entry.png.
