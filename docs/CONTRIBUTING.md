# Hướng dẫn đóng góp — RAG Chatbot System

Tài liệu mô tả quy ước làm việc khi tham gia phát triển dự án. Chỉ mang tính tham khảo, **không ảnh hưởng** đến build hay runtime.

---

## Cấu trúc solution

```
RAG-Chatbot-System/
├── RagChatbotSystem.Presentation/   # MVC, Controllers, Views
├── RagChatbotSystem.Business/       # Services, DTOs, Interfaces
├── RagChatbotSystem.DataAccess/     # EF Core, Models, Migrations
├── RAG-Retrieval-Indexing-API/    # Python FastAPI (RAG)
└── docs/                            # Tài liệu dự án
```

Khi sửa tính năng, ưu tiên giữ đúng phân tầng: Presentation → Business → DataAccess; gọi RAG API qua `IRagApiClient`.

---

## Quy ước code

### C# (.NET)

- Đặt tên class/interface theo PascalCase, method async kết thúc bằng `Async` nếu dự án đang dùng pattern đó.
- Logic nghiệp vụ đặt trong `Business`, không nhét trực tiếp vào Controller.
- DTO dùng cho trao đổi giữa tầng hoặc với RAG API.

### Python (RAG API)

- Endpoint và schema định nghĩa rõ trong `main.py`, `schemas.py`.
- Cấu hình qua `config.py` và biến môi trường (`.env`), không hard-code secret.

### Database

- Thay đổi model → tạo migration EF Core, đặt tên migration mô tả rõ thay đổi.
- Không commit file `.env` chứa API key thật.

---

## Nhánh & commit

- Tạo nhánh từ `main` (hoặc nhánh phát triển chính của team): `feature/...`, `fix/...`, `docs/...`.
- Commit message ngắn gọn, tiếng Anh hoặc tiếng Việt tùy team — ví dụ:
  - `feat: thêm export chat session`
  - `fix: sửa lỗi index PDF nhiều trang`
  - `docs: cập nhật FAQ`

---

## Kiểm tra trước khi mở PR

1. Build solution .NET không lỗi.
2. RAG API chạy được và test cơ bản (nếu đụng indexing/retrieval).
3. Migration apply được trên DB dev.
4. Không đẩy secret, connection string production.

---

## Tài liệu tham khảo

- [README.md](../README.md) — cài đặt và chạy dự án
- [System_Architecture_Summary.md](./System_Architecture_Summary.md) — kiến trúc
- [GLOSSARY.md](./GLOSSARY.md) — thuật ngữ
- [FAQ.md](./FAQ.md) — câu hỏi thường gặp
- [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) — xử lý sự cố
