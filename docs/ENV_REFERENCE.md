# Tham chiếu biến môi trường — RAG Chatbot System

Bảng tra cứu các biến cấu hình dùng khi chạy local hoặc Docker. Tài liệu tham khảo, **không ảnh hưởng** đến build hay runtime.

---

## File `.env` (thư mục gốc)

Dùng cho `docker-compose.yml`. Copy từ `.env.example`.

| Biến | Bắt buộc | Mô tả |
|------|----------|--------|
| `DB_PASSWORD` | Có (Docker) | Mật khẩu PostgreSQL |
| `GROQ_API_KEY` | Một trong các LLM | API key Groq (Llama 3.3) |
| `GEMINI_API_KEY` | Một trong các LLM | API key Google Gemini |
| `OPENAI_API_KEY` | Một trong các LLM | API key OpenAI |
| `HF_TOKEN` | Khuyến nghị | Token HuggingFace — tải model embedding/reranker |
| `GOOGLE_DRIVE_FOLDER_ID` | Không | ID thư mục Drive lưu file upload |
| `ADMIN_EMAIL` | Khuyến nghị (prod) | Email admin seed lần đầu |
| `ADMIN_PASSWORD` | Khuyến nghị (prod) | Mật khẩu admin seed |

Docker Compose map thêm vào container `web-app`:

- `ConnectionStrings__DefaultConnection`
- `RagApi__BaseUrl=http://rag-api:8000`
- `Gemini__ApiKey`, `Groq__ApiKey`, `OpenAi__ApiKey`

---

## `appsettings.json` (Web App — local dev)

File thường **không commit** (gitignore). Tham khảo README.

| Key | Mô tả |
|-----|--------|
| `ConnectionStrings:DefaultConnection` | Chuỗi kết nối PostgreSQL |
| `RagApi:BaseUrl` | URL RAG API, ví dụ `http://localhost:8000` |
| `Groq:ApiKey` | Key Groq |
| `Groq:Model` | Model Groq, mặc định `llama-3.3-70b-versatile` |
| `Gemini:ApiKey` | Key Gemini (nếu dùng) |
| `OpenAi:ApiKey` | Key OpenAI (nếu dùng) |
| `AdminSeed:Email` | Email admin (thay cho `ADMIN_EMAIL`) |
| `AdminSeed:Password` | Mật khẩu admin seed |
| `AdminSeed:Username` | Username admin, mặc định `admin` |
| `AdminSeed:FullName` | Tên hiển thị admin |

ASP.NET Core tự map biến môi trường dạng `Section__Key` (ví dụ `Groq__ApiKey`).

---

## RAG API — `.env` trong `RAG-Retrieval-Indexing-API/`

Copy từ `RAG-Retrieval-Indexing-API/.env.example`.

| Biến | Bắt buộc | Mặc định | Mô tả |
|------|----------|----------|--------|
| `HF_TOKEN` | Khuyến nghị | (rỗng) | HuggingFace token |
| `EMBEDDING_MODEL` | Không | `sentence-transformers/all-MiniLM-L6-v2` | Model embedding |
| `CROSS_ENCODER_MODEL` | Không | `cross-encoder/ms-marco-MiniLM-L-6-v2` | Model reranker |
| `API_HOST` | Không | `0.0.0.0` | Host uvicorn |
| `API_PORT` | Không | `8000` | Port uvicorn |

Giá trị mặc định RAG (trong `config.py`, không cần env):

- `SEMANTIC_WEIGHT=0.7`, `LEXICAL_WEIGHT=0.3`, `TOP_K=3`
- Cache: `cache/faiss_index`, `cache/bm25.pkl`

---

## Biến runtime ASP.NET

| Biến | Mô tả |
|------|--------|
| `ASPNETCORE_ENVIRONMENT` | `Development` / `Production` |
| `ASPNETCORE_URLS` | URL bind, ví dụ `http://+:5259` |

---

## Google Drive (tùy chọn)

- File `google-credentials.json` mount vào container (`docker-compose`).
- `GoogleDrive:CredentialJsonPath` — đường dẫn credential trong container.
- `GoogleDrive:TargetFolderId` — từ `GOOGLE_DRIVE_FOLDER_ID`.

---

## Lưu ý bảo mật

- Không commit `.env`, `appsettings.json` production, `google-credentials.json`.
- Ít nhất **một** LLM API key phải hợp lệ để chat hoạt động.
- Đổi `ADMIN_PASSWORD` mặc định ngay sau deploy.

---

## Tài liệu liên quan

- [README.md](../README.md) — mục Cấu hình môi trường
- [DEPLOYMENT_NOTES.md](./DEPLOYMENT_NOTES.md)
- [SECURITY_NOTES.md](./SECURITY_NOTES.md)
