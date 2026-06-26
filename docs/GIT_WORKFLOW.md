# Git Workflow — RAG Chatbot System

Quy ước làm việc với Git trong dự án. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## Nhánh

| Prefix | Dùng khi |
|--------|----------|
| `feature/` | Tính năng mới |
| `fix/` | Sửa lỗi |
| `docs/` | Chỉ thay đổi tài liệu |
| `refactor/` | Tái cấu trúc, không đổi hành vi |

Ví dụ: `feature/export-chat`, `docs/update-faq`.

---

## Commit message

Ngắn gọn, mô tả **why** hơn **what**:

```
feat: thêm export chat session
fix: sửa dominant page khi chunk PDF
docs: cập nhật ENV_REFERENCE
chore: nâng package EF Core
```

---

## Quy trình PR gợi ý

1. `git checkout main && git pull`
2. `git checkout -b feature/ten-tinh-nang`
3. Commit nhỏ, rõ ràng
4. Push và mở Pull Request
5. Review → merge vào `main`

---

## Không commit

- `.env`, `appsettings.json` (secrets)
- `google-credentials.json`
- `bin/`, `obj/`, `cache/`, `node_modules/` (đã gitignore)

---

## Sau merge

```bash
git checkout main
git pull
git branch -d feature/ten-tinh-nang
```

---

## Tài liệu liên quan

- [CONTRIBUTING.md](./CONTRIBUTING.md)
