# Khắc phục sự cố — RAG Chatbot System

Hướng dẫn xử lý các lỗi thường gặp khi phát triển hoặc chạy dự án. Tài liệu tham khảo, **không ảnh hưởng** đến cấu hình hay vận hành hệ thống.

---

## Kết nối & dịch vụ

### Không kết nối được PostgreSQL

- Kiểm tra connection string trong `appsettings.json` (host, port, user, password, database).
- Đảm bảo PostgreSQL đang chạy và extension `pgvector` đã được cài.
- Chạy migration nếu database mới: `dotnet ef database update` (từ project DataAccess hoặc Presentation tùy cấu hình solution).

### RAG API (Python) không phản hồi

- Xác nhận service FastAPI đã khởi động (thường port riêng, xem README).
- Kiểm tra file `.env` trong `RAG-Retrieval-Indexing-API/` (copy từ `.env.example`).
- Xem log terminal Python để biết lỗi import hoặc thiếu dependency (`uv sync` / `pip install`).

### Ứng dụng .NET không gọi được RAG API

- Kiểm tra URL base của RAG API trong cấu hình ứng dụng.
- Nếu chạy Docker/nginx, đảm bảo reverse proxy trỏ đúng upstream.

---

## Upload & Indexing

### Upload thành công nhưng không index được

- Kiểm tra RAG API có nhận request index không (log FastAPI).
- Xác nhận dataset ID và document ID hợp lệ trong database.
- File PDF/DOCX hỏng hoặc rỗng có thể khiến không sinh được chunk.

### Index chậm hoặc timeout

- Embedding và build FAISS/BM25 tốn tài nguyên — tài liệu lớn cần thời gian.
- Tăng timeout phía client nếu cấu hình cho phép.
- Kiểm tra RAM/CPU khi chạy local.

---

## Chat & LLM

### Chat trả lời lỗi hoặc trống

- Kiểm tra API key LLM (Gemini / Groq / OpenAI) trong cấu hình.
- Xem log Business layer — có thể đang fallback hoặc hết quota.
- Đảm bảo dataset đã index xong và user có quyền truy cập dataset.

### Không có citation trong câu trả lời

- Retrieval có thể không tìm thấy chunk phù hợp — thử câu hỏi cụ thể hơn.
- Kiểm tra dữ liệu index còn tồn tại (không bị xóa document/dataset).

---

## Xác thực & phân quyền

### Đăng ký xong không đăng nhập được

- Tài khoản mới có thể cần **Admin duyệt** (tùy luồng approval trong hệ thống).
- Kiểm tra trạng thái user trong database (`IsApproved` hoặc tương đương).

### Không thấy dataset hoặc không chat được

- Xác nhận role (Teacher/Student) và quyền `DatasetPermission` đã được gán.

---

## Docker & triển khai

### Container build fail

- Kiểm tra Dockerfile và phiên bản .NET/Python khớp với README.
- Build context phải bao gồm đủ project references.

### Nginx 502 Bad Gateway

- Backend (.NET hoặc Python) chưa sẵn sàng khi nginx proxy.
- Kiểm tra `nginx.conf` — upstream name và port.

---

## Tài liệu liên quan

- [FAQ.md](./FAQ.md)
- [GLOSSARY.md](./GLOSSARY.md)
- [README.md](../README.md)
