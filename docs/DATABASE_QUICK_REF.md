# Tham chiếu nhanh Database — RAG Chatbot System

Bảng và quan hệ chính (tóm tắt). Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

Chi tiết đầy đủ: [ERD_RAG_Chatbot_Explanation.md](./ERD_RAG_Chatbot_Explanation.md).

---

## Bảng chính

| Bảng | Mô tả ngắn |
|------|-------------|
| `Users` | Tài khoản, role, approval, đổi mật khẩu |
| `Datasets` | Bộ tài liệu / môn học |
| `Documents` | File đã upload, trạng thái index |
| `Chunks` | Đoạn text sau khi cắt |
| `VectorRecords` | Embedding vector (pgvector) |
| `ChatSessions` | Phiên hội thoại |
| `ChatMessages` | Tin User / Assistant |
| `Citations` | Nguồn trích dẫn gắn message |
| `DatasetPermissions` | User ↔ Dataset (đọc/ghi) |
| `TeacherSubjectAssignments` | Gán Teacher ↔ Dataset |

---

## Luồng dữ liệu

```
Dataset 1─* Document 1─* Chunk 1─1 VectorRecord
User   1─* ChatSession 1─* ChatMessage 1─* Citation
Dataset *─* User (qua DatasetPermissions)
```

---

## Migration (EF Core)

Thư mục: `RagChatbotSystem.DataAccess/Migrations/`

| Migration | Nội dung gợi ý |
|-----------|----------------|
| `InitialCreate` | Schema ban đầu |
| `AddAuthAndPermissions` | Auth + phân quyền dataset |
| `AddUserApproval` | Duyệt tài khoản |
| `AdminProvisioningAndTeacherAssignments` | Admin seed, gán teacher |

Apply:

```bash
dotnet ef database update \
  --project RagChatbotSystem.DataAccess \
  --startup-project RagChatbotSystem.Presentation
```

---

## pgvector

- Extension PostgreSQL lưu kiểu `vector`.
- `AppDbContext` cấu hình `UseVector()` trong `Program.cs`.

---

## Kết nối mẫu (local)

```
Host=localhost;Port=5432;Database=RagChatbotSystemDb;Username=postgres;Password=***
```

Docker service `db`: host = `db` (trong mạng compose).

---

## Tài liệu liên quan

- [ENV_REFERENCE.md](./ENV_REFERENCE.md)
- [DEPLOYMENT_NOTES.md](./DEPLOYMENT_NOTES.md)
