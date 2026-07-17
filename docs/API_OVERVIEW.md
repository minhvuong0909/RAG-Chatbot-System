# Tổng quan API — RAG Chatbot System

Tóm tắt các endpoint và nhóm chức năng chính. Tài liệu tham khảo, **không ảnh hưởng** đến build hay runtime.

Chi tiết đầy đủ của RAG API: Swagger tại `http://localhost:8000/docs` khi chạy FastAPI.

---

## Python RAG API (FastAPI)

Base URL mặc định: `http://localhost:8000` (cấu hình trong `RagApi:BaseUrl`).

| Method | Path | Mô tả |
|--------|------|--------|
| `GET` | `/health` | Kiểm tra trạng thái service và cấu hình |
| `POST` | `/index` | Lập/cập nhật chỉ mục từ danh sách document (chunk + embedding) |
| `POST` | `/retrieve` | Hybrid search + rerank, trả về documents và scores |
| `DELETE` | `/documents/{document_id}` | Xóa document khỏi chỉ mục RAG |

### `/index` (POST)

- Body: danh sách document (`page_content`, `metadata`).
- Tùy chọn `rebuild_cache` để build lại FAISS/BM25 cache.
- Response: số lượng document đã index và embeddings (dùng lưu pgvector phía .NET).

### `/retrieve` (POST)

- Body: `query`, `top_k`, `semantic_weight`, `lexical_weight`, `enable_rerank`.
- Response: documents khớp, điểm số, và `trace` (debug luồng retrieval).

---

## ASP.NET Core MVC (Web App)

Base URL mặc định: `http://localhost:5259`. Ứng dụng chủ yếu phục vụ giao diện web; các controller xử lý form/AJAX.

### Account

- Đăng ký, đăng nhập, đăng xuất.
- Quản lý phiên người dùng (cookie authentication).

### Datasets

- Tạo và quản lý dataset (tập tài liệu).
- Gán quyền truy cập theo vai trò.

### Documents

- Upload file (TXT, PDF, DOCX).
- Kích hoạt indexing qua RAG API sau khi trích xuất và chunk.
- Liệt kê / xem tài liệu trong dataset.

### ChatSessions

- Tạo phiên chat, gửi tin nhắn.
- Luồng: retrieve context → gọi LLM → lưu message + citations.

### Admin

- Duyệt tài khoản người dùng.
- Phân quyền Teacher/Student, gán dataset.

### Home

- Trang chủ và các view công khai (Privacy, v.v.).

### TestRag (dev)

- Endpoint kiểm thử nhanh tích hợp RAG (`GET .../run`).

---

## Luồng gọi API điển hình

```
User → Web App (MVC)
         → Business (ChatService / DocumentService)
              → RAG API: POST /retrieve
              → LLM (Gemini / Groq / OpenAI)
         → DataAccess: lưu ChatMessage, Citation
```

```
Upload file → DocumentService (chunk) → RAG API: POST /index
                                      → lưu embedding vào PostgreSQL (pgvector)
```

---

## Tài liệu liên quan

- [README.md](../README.md) — cấu hình và khởi chạy
- [GLOSSARY.md](./GLOSSARY.md) — thuật ngữ RAG
- [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) — xử lý lỗi kết nối API
