# So sánh mô hình AI (Model Comparison) — Kiến trúc & Cấu hình

## Mục tiêu

Cho phép Admin/Teacher chạy cùng một câu hỏi qua nhiều model AI khác nhau (Groq, Gemini, Ollama) trên cùng một context truy xuất, để so sánh chất lượng câu trả lời, thời gian phản hồi và số token sử dụng.

## Kiến trúc

- `ILlmService` (đã có sẵn) là interface chung cho mọi provider LLM: `GroqService`, `LlmService` (Gemini), `OpenAiService`, và nay có thêm `OllamaService`.
- `ChatService` (luồng chat chính) chỉ inject **1** `ILlmService` duy nhất qua DI (`AddHttpClient<ILlmService, GroqService>` trong `Program.cs`), nên không thể dùng chung DI đó để lấy nhiều provider cùng lúc.
- Giải pháp: `ModelComparisonService` tự tạo `HttpClient` theo tên thông qua `IHttpClientFactory` (`ModelComparison.Groq`, `ModelComparison.Gemini`, `ModelComparison.Ollama`), rồi `new` trực tiếp từng concrete class tương ứng. Cách này tách biệt hoàn toàn khỏi luồng chat sản xuất — không ảnh hưởng `ChatService` hay HttpClient đang dùng cho chat.

## OllamaService

- Gọi REST API local: `POST {BaseUrl}api/chat`.
- Không cần API key — chạy hoàn toàn miễn phí trên máy có cài Ollama.
- Cấu hình qua `appsettings.json` (không commit vào repo):
  - `Ollama:BaseUrl` — mặc định `http://localhost:11434/`
  - `Ollama:Model` — mặc định `llama3`

## Yêu cầu để chạy được Ollama khi demo

1. Cài Ollama: https://ollama.com
2. Tải model: `ollama pull llama3`
3. Ollama tự chạy server ở `localhost:11434` sau khi cài, không cần khởi động thủ công.

Nếu máy demo không có Ollama, chỉ cần bỏ tick model "Ollama (Local)" trên trang so sánh — Groq và Gemini vẫn hoạt động bình thường vì gọi API ngoài.

## Named HttpClient trong Program.cs

Ba client `ModelComparison.*` được đăng ký riêng, tách khỏi HttpClient dùng cho `ChatService`, để timeout và base address độc lập — Ollama cần timeout dài hơn (180s) do chạy suy luận cục bộ trên CPU/GPU của máy chủ, trong khi Groq/Gemini gọi API cloud thường phản hồi nhanh hơn.
