# Cấu trúc dự án — RAG Chatbot System

Mô tả thư mục và trách nhiệm từng phần mã nguồn. Tài liệu tham khảo, **không ảnh hưởng** đến build hay runtime.

---

## Cây thư mục gốc

```
RAG-Chatbot-System/
├── RagChatbotSystem.Presentation/     # Web MVC, Controllers, Views
├── RagChatbotSystem.Business/         # Logic nghiệp vụ, LLM, RAG client
├── RagChatbotSystem.DataAccess/       # EF Core, Models, Migrations
├── RagChatbotSystem.Tests/            # Unit tests (.NET)
├── RAG-Retrieval-Indexing-API/        # Python FastAPI (index + retrieve)
├── docs/                              # Tài liệu dự án
├── docker-compose.yml
├── nginx.conf
├── setup_vps.sh
└── README.md
```

---

## RagChatbotSystem.Presentation

| Thư mục / file | Mô tả |
|----------------|--------|
| `Controllers/` | Account, Admin, Datasets, Documents, ChatSessions, Home, TestRag |
| `Views/` | Razor views (Account, Admin, Documents, Home, Shared) |
| `Models/` | ViewModel cho UI |
| `Services/` | `SmtpEmailService` — gửi email |
| `wwwroot/` | CSS, JS, thư viện Bootstrap/jQuery |
| `Program.cs` | DI, auth, middleware, routing |
| `Dockerfile` | Image web app |

Điểm vào HTTP từ người dùng; gọi xuống Business qua interface đã đăng ký DI.

---

## RagChatbotSystem.Business

| Thư mục | Mô tả |
|---------|--------|
| `Services/` | `ChatService`, `DocumentService`, `DatasetService`, `UserService`, `LlmService`, `RagApiClient`, `GroqService`, `OpenAiService`, `GoogleDriveStorageService`, … |
| `Interfaces/` | Hợp đồng cho từng service |
| `DTOs/` | Request/response trao đổi giữa tầng hoặc với RAG API |
| `Helpers/` | `PasswordHasherHelper` |

Luồng chat: `ChatService` → `RagApiClient` (retrieve) → `LlmService` (sinh câu trả lời) → lưu citation.

Luồng document: `DocumentService` (trích xuất, chunk) → `RagApiClient` (index).

---

## RagChatbotSystem.DataAccess

| Thư mục | Mô tả |
|---------|--------|
| `Models/` | User, Dataset, Document, Chunk, VectorRecord, ChatSession, ChatMessage, Citation, DatasetPermission, TeacherSubjectAssignment |
| `Data/AppDbContext.cs` | DbContext EF Core |
| `Repositories/` | GenericRepository, UnitOfWork |
| `Migrations/` | Lịch sử schema (InitialCreate, Auth, Approval, Admin provisioning) |

PostgreSQL + pgvector lưu metadata, embedding vector, lịch sử chat.

---

## RagChatbotSystem.Tests

- `DocumentProcessingTests.cs` — test chunking, overlap, dominant page PDF/DOCX.

Chạy: `dotnet test RagChatbotSystem.sln`

---

## RAG-Retrieval-Indexing-API (Python)

| File | Mô tả |
|------|--------|
| `main.py` | FastAPI app, routes `/health`, `/index`, `/retrieve`, DELETE document |
| `service.py` | Hybrid retrieve, FAISS, BM25, reranker |
| `schemas.py` | Pydantic models |
| `config.py` | Cấu hình từ env |
| `test_api.py` | Smoke test HTTP |
| `Dockerfile` | Image RAG API |

Cache index: thư mục `cache/` (FAISS, BM25 pickle) — volume `rag-cache` trong Docker.

---

## docs/

Tài liệu bổ sung: kiến trúc, ERD, glossary, FAQ, troubleshooting, contributing, changelog, API, deployment, testing, security.

---

## Phụ thuộc giữa các tầng

```
Presentation  →  Business  →  DataAccess  →  PostgreSQL
                    ↓
              RAG API (Python)
                    ↓
              cache/ (FAISS, BM25)
```

Presentation **không** gọi trực tiếp DataAccess repository — đi qua Business services.

---

## Tài liệu liên quan

- [System_Architecture_Summary.md](./System_Architecture_Summary.md)
- [CONTRIBUTING.md](./CONTRIBUTING.md)
- [API_OVERVIEW.md](./API_OVERVIEW.md)
