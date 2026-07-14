# So sánh mô hình AI (Model Comparison) — Kiến trúc & Cấu hình

## Mục tiêu

Cho phép Admin/Teacher chạy cùng một câu hỏi qua nhiều model AI khác nhau (Groq, Gemini) trên cùng một context truy xuất, để so sánh chất lượng câu trả lời, thời gian phản hồi và số token sử dụng.

## Kiến trúc

- `ILlmService` (đã có sẵn) là interface chung cho mọi provider LLM: `GroqService`, `LlmService` (Gemini), `OpenAiService`.
- `ChatService` (luồng chat chính) chỉ inject **1** `ILlmService` duy nhất qua DI (`AddHttpClient<ILlmService, GroqService>` trong `Program.cs`), nên không thể dùng chung DI đó để lấy nhiều provider cùng lúc.
- Giải pháp: `ModelComparisonService` tự tạo `HttpClient` theo tên thông qua `IHttpClientFactory` (`ModelComparison.Groq`, `ModelComparison.Gemini`), rồi `new` trực tiếp từng concrete class tương ứng. Cách này tách biệt hoàn toàn khỏi luồng chat sản xuất — không ảnh hưởng `ChatService` hay HttpClient đang dùng cho chat.

## Named HttpClient trong Program.cs

Các client `ModelComparison.*` được đăng ký riêng, tách khỏi HttpClient dùng cho `ChatService`, để timeout và base address độc lập với luồng chat chính.
