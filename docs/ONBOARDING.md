# Onboarding thành viên mới — RAG Chatbot System

Checklist làm quen dự án trong 1–2 ngày. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## Ngày 1 — Cài đặt & chạy được

- [ ] Clone repo, đọc [README.md](../README.md)
- [ ] Cài .NET 9 SDK, Python 3.10+, Docker Desktop
- [ ] Copy `.env.example` → `.env`, điền `DB_PASSWORD` + ít nhất 1 LLM key
- [ ] `docker compose up --build -d` **hoặc** chạy thủ công 3 bước (DB, RAG API, Web)
- [ ] Truy cập `http://localhost:5259`, đăng nhập admin (seed từ env)
- [ ] Upload file `.txt` nhỏ → index → chat thử

---

## Ngày 2 — Hiểu kiến trúc

- [ ] Đọc [PROJECT_STRUCTURE.md](./PROJECT_STRUCTURE.md)
- [ ] Đọc [System_Architecture_Summary.md](./System_Architecture_Summary.md)
- [ ] Xem Swagger RAG: `http://localhost:8000/docs`
- [ ] Chạy `dotnet test RagChatbotSystem.sln`
- [ ] Duyệt flow: `DocumentsController` → `DocumentService` → `RagApiClient`

---

## Tài liệu nên bookmark

| File | Nội dung |
|------|----------|
| [GLOSSARY.md](./GLOSSARY.md) | Thuật ngữ RAG |
| [API_OVERVIEW.md](./API_OVERVIEW.md) | Endpoint |
| [ENV_REFERENCE.md](./ENV_REFERENCE.md) | Biến môi trường |
| [TROUBLESHOOTING.md](./TROUBLESHOOTING.md) | Xử lý lỗi |
| [CONTRIBUTING.md](./CONTRIBUTING.md) | Quy ước đóng góp |

---

## Câu hỏi thường gặp khi mới vào

**Tại sao có cả C# và Python?**  
.NET xử lý web, auth, DB; Python gắn stack ML (FAISS, transformers) cho RAG.

**Sửa UI ở đâu?**  
`RagChatbotSystem.Presentation/Views/` và `wwwroot/`.

**Sửa logic chat ở đâu?**  
`RagChatbotSystem.Business/Services/ChatService.cs`.

---

## Liên hệ team

Thay dòng này bằng channel Slack/Discord/Zalo của nhóm khi có.
