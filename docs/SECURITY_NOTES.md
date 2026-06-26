# Ghi chú bảo mật — RAG Chatbot System

Khuyến nghị bảo mật khi phát triển và triển khai. Tài liệu tham khảo, **không ảnh hưởng** đến build hay runtime.

---

## Xác thực & phân quyền

### Cookie authentication

Web app dùng đăng nhập cookie ASP.NET Core. Các controller nhạy cảm gắn `[Authorize]`; thao tác dataset/document thường yêu cầu role **Teacher** hoặc **Admin**.

### Duyệt tài khoản

User mới đăng ký có thể cần **Admin duyệt** (`IsApproved`) trước khi sử dụng đầy đủ tính năng.

### Phân quyền dataset

Truy cập chat/upload gắn với `DatasetPermission` — kiểm tra quyền ở tầng Business (`CanManageDatasetAsync`, v.v.), không chỉ ẩn nút trên UI.

### Đổi mật khẩu bắt buộc

Một số tài khoản (ví dụ do Admin tạo) có cờ `MustChangePassword` — buộc đổi mật khẩu lần đầu đăng nhập.

---

## Bảo vệ secret

### Không commit

- `.env`, `appsettings.json` (production)
- `google-credentials.json`
- API key LLM (Gemini, Groq, OpenAI), `HF_TOKEN`, `DB_PASSWORD`

File mẫu: `.env.example` — chỉ placeholder, không giá trị thật.

### Production

- Dùng biến môi trường hoặc secret manager (Docker env, GitHub Secrets, vault).
- Rotate key định kỳ nếu bị lộ.

---

## Mật khẩu

- Hash mật khẩu qua `PasswordHasherHelper` (không lưu plain text).
- Admin tạo user có thể gửi mật khẩu tạm qua email (SMTP) — đảm bảo cấu hình SMTP an toàn.

---

## RAG API & mạng nội bộ

- Trong Docker Compose, RAG API (`:8000`) nên **không expose** ra internet nếu không cần — chỉ web app gọi nội bộ `http://rag-api:8000`.
- Nếu mở port 8000 ra ngoài, cân nhắc thêm API key hoặc firewall (hiện tại endpoint chưa có auth riêng — phù hợp mạng tin cậy).

---

## Upload file

- Giới hạn kích thước upload (Nginx: `client_max_body_size 50M`).
- Chỉ chấp nhận định dạng đã hỗ trợ (TXT, PDF, DOCX) — tránh thực thi file tùy ý trên server.
- Lưu file qua `IFileStorageService` / Google Drive — kiểm tra quyền trước khi đọc.

---

## HTTPS

- Production: bật SSL qua Nginx + Certbot (xem [DEPLOYMENT_NOTES.md](./DEPLOYMENT_NOTES.md)).
- Header `X-Forwarded-Proto` giúp app biết request HTTPS phía sau proxy.

---

## LLM & dữ liệu

- Nội dung tài liệu và câu hỏi có thể gửi tới nhà cung cấp LLM bên thứ ba — tuân thủ chính sách dữ liệu của tổ chức.
- Không đưa PII nhạy cảm vào dataset demo công khai.

---

## Checklist nhanh trước go-live

- [ ] Secret không nằm trong git
- [ ] `ASPNETCORE_ENVIRONMENT=Production`, tắt chi tiết lỗi debug
- [ ] Postgres không bind `0.0.0.0:5432` ra internet không cần thiết
- [ ] Admin mặc định đổi mật khẩu sau lần deploy đầu
- [ ] Backup DB và giới hạn quyền volume Docker

---

## Tài liệu liên quan

- [DEPLOYMENT_NOTES.md](./DEPLOYMENT_NOTES.md)
- [CONTRIBUTING.md](./CONTRIBUTING.md)
- [FAQ.md](./FAQ.md)
