# Ghi chú triển khai — RAG Chatbot System

Tóm tắt các bước deploy lên server hoặc chạy bằng Docker. Tài liệu tham khảo, **không ảnh hưởng** đến build hay runtime.

---

## Triển khai nhanh (Docker Compose)

Phù hợp local hoặc VPS đã có Docker.

```bash
# Tạo .env từ .env.example và điền biến môi trường
docker-compose up --build -d
```

### Các service

| Service | Container | Port | Vai trò |
|---------|-----------|------|---------|
| `db` | `rag-postgres-db` | 5432 | PostgreSQL + pgvector |
| `rag-api` | `rag-retrieval-api` | 8000 | FastAPI indexing/retrieval |
| `web-app` | `rag-web-app` | 5259 | ASP.NET Core MVC |

### Volume

- `pgdata` — dữ liệu PostgreSQL.
- `rag-cache` — cache FAISS/BM25 của RAG API.

### Biến môi trường quan trọng

- `DB_PASSWORD` — mật khẩu Postgres.
- `GEMINI_API_KEY`, `GROQ_API_KEY`, `OPENAI_API_KEY` — ít nhất một key LLM.
- `HF_TOKEN` — tùy chọn, tải model HuggingFace bị giới hạn.
- `GOOGLE_DRIVE_FOLDER_ID` — tùy chọn, đồng bộ/lưu file Drive.
- `ADMIN_EMAIL`, `ADMIN_PASSWORD` — tài khoản admin khởi tạo.

Web app kết nối RAG API nội bộ qua `http://rag-api:8000` trong mạng Docker.

---

## Chuẩn bị VPS (Ubuntu/Debian)

Script `setup_vps.sh` ở thư mục gốc cài:

1. Cập nhật hệ thống
2. Docker + Docker Compose plugin
3. Nginx + Certbot (SSL)
4. Thư mục `/var/www/rag-chatbot-system`

```bash
chmod +x setup_vps.sh
./setup_vps.sh
```

Sau khi cài, clone repo vào `/var/www/rag-chatbot-system`, cấu hình `.env`, rồi `docker compose up -d`.

---

## Nginx reverse proxy

File mẫu `nginx.conf` proxy HTTP tới web app tại `127.0.0.1:5259`.

- `client_max_body_size 50M` — cho phép upload file lớn.
- Header `X-Forwarded-*` — ASP.NET nhận đúng scheme/host phía sau proxy.

Triển khai SSL (ví dụ Let's Encrypt):

```bash
sudo certbot --nginx -d your-domain.example
```

Chỉnh `server_name` trong config Nginx cho khớp domain thực tế.

---

## Migration database

Lần đầu deploy hoặc sau khi pull migration mới:

```bash
dotnet ef database update \
  --project RagChatbotSystem.DataAccess \
  --startup-project RagChatbotSystem.Presentation
```

Với Docker, có thể chạy lệnh tương tự trong container `rag-web-app` hoặc từ máy dev trỏ connection string tới DB production (cẩn thận môi trường).

---

## Kiểm tra sau deploy

1. `curl http://localhost:8000/health` — RAG API sống.
2. Truy cập web app qua port 5259 hoặc domain Nginx.
3. Đăng nhập admin, thử upload + index + chat một tài liệu nhỏ.
4. Xem log: `docker compose logs -f web-app rag-api`.

---

## Lưu ý vận hành

- Xóa cache index: xóa volume `rag-cache` hoặc thư mục `cache/` trong RAG API (xem README).
- Không commit `.env`, `appsettings.json` production, `google-credentials.json`.
- Backup volume `pgdata` định kỳ nếu chạy production.

---

## Tài liệu liên quan

- [README.md](../README.md)
- [API_OVERVIEW.md](./API_OVERVIEW.md)
- [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)
