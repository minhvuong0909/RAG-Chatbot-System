# Định dạng tài liệu hỗ trợ — RAG Chatbot System

Chi tiết xử lý file upload. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## Tổng quan

| Định dạng | Thư viện | Ghi chú |
|-----------|----------|---------|
| `.txt` | Đọc text thuần | UTF-8 khuyến nghị |
| `.pdf` | PdfPig | Trích text theo trang |
| `.docx` | OpenXml | Word Open XML |

---

## PDF

- Text được gom theo trang → `ExtractedTextSegment(pageNumber)`.
- Chunk cross-page dùng thuật toán **dominant page** (`ResolveDominantPage`).
- PDF scan (chỉ ảnh) không có text layer → nội dung trích xuất rỗng hoặc kém.

---

## DOCX

- Đọc paragraph qua DocumentFormat.OpenXml.
- Không giữ số trang như PDF — metadata page có thể mặc định.

---

## TXT

- Đọc toàn bộ file một segment.
- Phù hợp test nhanh pipeline RAG.

---

## Chunking (chung)

- Kích thước: **600** ký tự.
- Overlap: **120** ký tự.
- Mục tiêu: giữ ngữ cảnh giữa các đoạn liền kề.

---

## Metadata chunk (ví dụ)

- `document_id`, `source` (tên file)
- `page` (PDF)
- Dùng khi hiển thị **Citation** trong chat.

---

## Giới hạn upload

- Nginx: `client_max_body_size 50M` (production).
- Kiểm tra giới hạn ASP.NET `FormOptions` nếu tùy chỉnh.

---

## Tài liệu liên quan

- [RAG_PIPELINE.md](./RAG_PIPELINE.md)
- [TESTING_GUIDE.md](./TESTING_GUIDE.md)
