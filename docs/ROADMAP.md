# Roadmap — RAG Chatbot System

Định hướng phát triển gợi ý (không ràng buộc triển khai). Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## Đã hoàn thành (v1)

- Upload & index tài liệu TXT, PDF, DOCX
- Hybrid search + rerank + chat có citation
- Phân quyền Admin / Teacher / Student
- Docker Compose, deploy VPS + Nginx
- Tích hợp Gemini, Groq, OpenAI + fallback

---

## Ngắn hạn (gợi ý)

- [ ] Thêm unit test cho `ChatService`, `DatasetService`
- [ ] Export lịch sử chat (PDF/JSON)
- [ ] Tìm kiếm full-text trong danh sách tài liệu
- [ ] Dark mode UI

---

## Trung hạn (gợi ý)

- [ ] API key bảo vệ RAG service khi expose public
- [ ] Streaming response chat (SSE)
- [ ] Hỗ trợ thêm định dạng (Markdown, HTML)
- [ ] Dashboard thống kê usage theo dataset

---

## Dài hạn (gợi ý)

- [ ] Multi-tenant tách dataset theo tổ chức
- [ ] Fine-tune hoặc chọn embedding model theo dataset
- [ ] Tích hợp SSO (Google/Microsoft campus login)

---

*Tài liệu cập nhật tùy ý team — không sync tự động với issue tracker.*
