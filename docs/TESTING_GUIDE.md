# Hướng dẫn kiểm thử — RAG Chatbot System

Tóm tắt cách chạy test tự động và kiểm tra thủ công. Tài liệu tham khảo, **không ảnh hưởng** đến build hay runtime.

---

## Test tự động (.NET)

Project: `RagChatbotSystem.Tests`

Chạy toàn bộ test trong solution:

```bash
dotnet test RagChatbotSystem.sln
```

Chỉ chạy project test:

```bash
dotnet test RagChatbotSystem.Tests/RagChatbotSystem.Tests.csproj
```

### Phạm vi hiện tại

`DocumentProcessingTests` kiểm tra logic xử lý tài liệu trong `DocumentService`:

- **SplitTextSegments_OverlapsAdjacentChunks** — chunk overlap đúng giữa các đoạn liền kề.
- **SplitTextSegments_UsesDominantPageWhenChunkCrossesPages** — gán số trang dominant khi chunk trải nhiều trang PDF.
- Các test trích xuất / xử lý DOCX, PDF (nếu có trong file).

Các test này **không cần** PostgreSQL hay RAG API — chạy offline, phù hợp CI.

---

## Test RAG API (Python)

File: `RAG-Retrieval-Indexing-API/test_api.py`

1. Khởi động FastAPI (mặc định port 8000; script mẫu có thể dùng 8001 — chỉnh `BASE_URL` cho khớp).
2. Chạy:

```bash
cd RAG-Retrieval-Indexing-API
python test_api.py
```

Script gửi mẫu document tới `POST /index`, sau đó `POST /retrieve` và in kết quả — dùng smoke test nhanh cho hybrid search.

Swagger UI khi API đang chạy: `http://localhost:8000/docs`

---

## Kiểm tra tích hợp qua Web App

### TestRagController (dev)

Controller `TestRag` có endpoint `GET .../run` để thử luồng gọi RAG từ phía .NET (cần web app + RAG API + DB đang chạy).

### Checklist thủ công E2E

1. Đăng ký / đăng nhập (admin duyệt nếu cần).
2. Tạo dataset, upload file `.txt` nhỏ.
3. Chờ indexing hoàn tất.
4. Mở chat, hỏi nội dung có trong file — kiểm tra citation (tên file, đoạn trích).
5. Thử xóa document — xác nhận không còn retrieve được nội dung đó.

---

## Chạy test trong Docker

Sau `docker compose up`:

```bash
# Health RAG API
curl http://localhost:8000/health

# Test .NET (từ máy host, cần SDK)
dotnet test RagChatbotSystem.sln
```

Integration test đầy đủ thường chạy trên môi trường dev có DB và API key LLM hợp lệ.

---

## Gợi ý mở rộng test (tùy chọn)

- Thêm unit test cho `PasswordHasherHelper`, DTO mapping.
- Integration test với Testcontainers (Postgres) — chưa bắt buộc trong scope hiện tại.
- pytest cho `service.py` / `hybrid_retrieve` nếu tách logic thuần Python.

---

## Tài liệu liên quan

- [README.md](../README.md) — mục Automated Tests
- [API_OVERVIEW.md](./API_OVERVIEW.md)
- [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)
