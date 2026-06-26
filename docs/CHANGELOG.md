# Changelog — RAG Chatbot System

Ghi chú thay đổi theo phiên bản (tài liệu tham khảo). File này **không ảnh hưởng** đến build hay runtime.

Định dạng dựa trên [Keep a Changelog](https://keepachangelog.com/vi/1.0.0/).

---

## [Unreleased]

### Added
- Tài liệu bổ sung trong `docs/`: GLOSSARY, FAQ, TROUBLESHOOTING, CONTRIBUTING, CHANGELOG.

---

## [1.0.0] — 2026-06

### Added
- Hệ thống RAG Chatbot: upload tài liệu (TXT, PDF, DOCX), indexing, chat có citation.
- Kiến trúc phân tầng ASP.NET Core (.NET 9) + Python FastAPI.
- Hybrid search: BM25 + FAISS, RRF, Cross-Encoder reranking.
- Tích hợp LLM: Gemini, Groq (Llama 3.3), OpenAI với cơ chế fallback.
- PostgreSQL + pgvector cho embedding.
- Phân quyền: Admin, Teacher, Student; duyệt tài khoản và gán dataset.
- Smart chunking (600 ký tự, overlap 120).
- Reverse proxy Nginx, hỗ trợ triển khai Docker/VPS.

### Documentation
- README hướng dẫn cài đặt và API contract.
- `docs/System_Architecture_Summary.md`, `docs/ERD_RAG_Chatbot_Explanation.md`.

---

## Ghi chú

- Phiên bản và ngày tháng mang tính tóm tắt theo trạng thái dự án học thuật (PRN222).
- Cập nhật mục `[Unreleased]` khi có tính năng hoặc sửa lỗi đáng ghi nhận trước khi tag release.

[Unreleased]: https://github.com/compare/main...HEAD
[1.0.0]: https://github.com/
