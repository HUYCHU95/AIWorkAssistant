# AI Personal Work Assistant — HƯỚNG DẪN GỬI CODEX

**Mục đích:** Bộ đặc tả để Codex xây dựng **một website V0.1 có thể chạy ngay** trên Windows, rồi từng bước tích hợp OpenAI ở phiên bản tiếp theo. Đây là tài liệu, **chưa phải mã nguồn website**.

## Bước bắt đầu
1. Giải nén ZIP vào thư mục làm việc, ví dụ `D:\AIWorkAssistant`.
2. Mở thư mục đó bằng Codex/VS Code. Đọc `AGENTS.md` trước.
3. Gửi nội dung `prompts/CODEX_V01_START.md` cho Codex, yêu cầu **thực thi xây dựng chứ không chỉ lập kế hoạch**.
4. Codex tạo solution, mã nguồn, SQLite migration, scripts và build/test. Theo dõi log build. Nếu thiếu .NET 10 SDK, cài .NET SDK rồi chạy lại.
5. Chạy `dotnet run --project src/AIWork.Web` trong development; để chạy gói phân phối thì dùng `publish-win-x64.bat` và sau đó `Start.bat` trong thư mục publish.
6. Đăng nhập tài khoản cá nhân tạo trong lần chạy đầu, nhập thử 9 dự án từ `sample-data/9_projects_nov_2026.xlsx` (chỉ nếu import đã được hoàn thiện) hoặc sử dụng tính năng quản lý để tạo thử.

## Phạm vi quan trọng
- **V0.1 chạy được ngay, không cần OpenAI API**, không giả làm AI hoạt động thật.
- V0.1 gồm nền tảng portable, giao diện AI-first, Project / Workstream / Task, SQLite, setup và dữ liệu, import Excel, dashboard và tìm kiếm cơ bản.
- AI Chat thật, Function Calling, Knowledge/Memory nâng cao, email reminders sẽ thực hiện trong V0.3–V1.0.
- Source code không đính kèm trong ZIP này vì Codex sẽ tạo dựa trên đặc tả.

## Thứ tự đọc
1. `AGENTS.md`
2. `docs/PROJECT_SPEC.md`
3. `docs/ARCHITECTURE.md`
4. `docs/DATABASE_DESIGN.md`
5. `docs/UI_DESIGN.md`
6. `docs/AI_BEHAVIOR.md`
7. `docs/ROADMAP.md`
8. `docs/ACCEPTANCE_TESTS.md`

Khi báo hoàn thành, Codex phải nêu **lệnh build/test đã chạy thực tế**, file publish thực tế và những giới hạn chưa triển khai.
