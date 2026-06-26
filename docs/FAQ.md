# Câu hỏi thường gặp (FAQ) — RAG Chatbot System

Tài liệu trả lời nhanh các thắc mắc phổ biến khi làm việc với dự án. Chỉ mang tính tham khảo, **không ảnh hưởng** đến build hay runtime.

---

## Tổng quan

### Dự án này làm gì?

Hệ thống cho phép upload tài liệu (PDF, DOCX, TXT), lập chỉ mục vector, rồi chat hỏi đáp dựa trên nội dung tài liệu đó — kèm trích dẫn nguồn (citation).

### Có những thành phần chính nào?

- **ASP.NET Core MVC** — giao diện web và logic nghiệp vụ (.NET 9)
- **Python FastAPI** — indexing, hybrid search, reranking
- **PostgreSQL + pgvector** — lưu metadata và embedding

---

## Upload & Indexing

### Tại sao upload và indexing tách làm 2 bước?

Để người dùng thấy file đã tải lên ngay, trong khi bước embedding/index (tốn thời gian hơn) chạy nền qua RAG API.

### Hỗ trợ định dạng file nào?

`.txt`, `.pdf`, `.docx` — chi tiết xem [README.md](../README.md).

### Chunk size và overlap là bao nhiêu?

Mặc định **600** ký tự mỗi chunk, overlap **120** ký tự — giúp giữ ngữ cảnh giữa các đoạn cắt.

---

## Chat & RAG

### Câu trả lời lấy thông tin từ đâu?

Hệ thống truy vấn hybrid search (BM25 + FAISS), rerank kết quả, rồi đưa các đoạn liên quan vào prompt cho LLM.

### LLM nào được dùng?

Hỗ trợ **Gemini**, **Groq (Llama 3.3)** và **OpenAI**. Có cơ chế fallback khi dịch vụ chính lỗi.

### Citation hiển thị gì?

Tên file gốc, số trang (nếu có), và đoạn trích được dùng làm ngữ cảnh cho câu trả lời.

---

## Phân quyền

### Các vai trò trong hệ thống?

- **Admin** — duyệt tài khoản, gán quyền
- **Teacher** — quản lý dataset được phân công
- **Student** — chat trên dataset được cấp quyền

---

## Triển khai & môi trường

### Cần cài gì để chạy local?

.NET 9 SDK, Python 3.10+, PostgreSQL (có pgvector), và các API key LLM tương ứng. Xem hướng dẫn chi tiết trong [README.md](../README.md).

### File cấu hình quan trọng?

- `appsettings.json` — connection string, API key (.NET)
- `RAG-Retrieval-Indexing-API/.env` — cấu hình Python API (tham khảo `.env.example`)

---

## Tài liệu liên quan

- [GLOSSARY.md](./GLOSSARY.md) — từ điển thuật ngữ
- [System_Architecture_Summary.md](./System_Architecture_Summary.md) — tóm tắt kiến trúc
- [ERD_RAG_Chatbot_Explanation.md](./ERD_RAG_Chatbot_Explanation.md) — giải thích ERD
