# Tích hợp LLM — RAG Chatbot System

Tóm tắt cách hệ thống gọi mô hình ngôn ngữ. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## Nhà cung cấp hỗ trợ

| Provider | Service | Ghi chú |
|----------|---------|---------|
| Groq | `GroqService` | Mặc định đăng ký HttpClient trong `Program.cs` |
| Gemini | `LlmService` / provider tương ứng | Cấu hình `Gemini:ApiKey` |
| OpenAI | `OpenAiService` | Cấu hình `OpenAi:ApiKey` |

Cần **ít nhất một** API key hợp lệ để chat hoạt động.

---

## Cơ chế fallback

`LlmService` (hoặc lớp điều phối) thử provider chính; nếu lỗi (timeout, 401, quota) chuyển sang provider dự phòng — tránh chat đứng hoàn toàn.

---

## Luồng prompt

1. Retrieve top-K chunks từ RAG API.
2. Ghép system prompt + context chunks + câu hỏi user.
3. Gọi API LLM, nhận completion.
4. Parse / lưu câu trả lời; map chunks thành `Citation`.

---

## Cấu hình

- Local: `appsettings.json` — section `Groq`, `Gemini`, `OpenAi`.
- Docker: biến `Groq__ApiKey`, `Gemini__ApiKey`, `OpenAi__ApiKey`.
- Model Groq mặc định: `llama-3.3-70b-versatile` (README).

---

## Lưu ý

- Nội dung tài liệu có thể gửi tới server LLM bên thứ ba.
- Timeout HttpClient Groq: 60s; RAG retrieve: 120s.

---

## Tài liệu liên quan

- [ENV_REFERENCE.md](./ENV_REFERENCE.md)
- [SECURITY_NOTES.md](./SECURITY_NOTES.md)
