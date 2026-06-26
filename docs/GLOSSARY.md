# Thuật ngữ & Từ điển dự án RAG Chatbot System

Tài liệu tham khảo nhanh các khái niệm kỹ thuật xuất hiện trong mã nguồn và README của dự án. File này chỉ mang tính mô tả, **không ảnh hưởng** đến cấu hình hay vận hành hệ thống.

---

## RAG & Tìm kiếm

| Thuật ngữ | Giải thích ngắn |
|-----------|-----------------|
| **RAG** | Retrieval-Augmented Generation — kỹ thuật bổ sung ngữ cảnh từ tài liệu vào prompt trước khi gọi LLM. |
| **Chunk** | Đoạn văn bản nhỏ được cắt từ tài liệu gốc để lập chỉ mục và truy vấn. |
| **Embedding** | Vector số học biểu diễn ngữ nghĩa của một đoạn văn bản. |
| **Hybrid Search** | Kết hợp tìm kiếm từ khóa (BM25) và tìm kiếm ngữ nghĩa (vector/FAISS). |
| **BM25** | Thuật toán tìm kiếm lexical dựa trên tần suất từ khóa. |
| **FAISS** | Thư viện Facebook AI Similarity Search — lưu trữ và truy vấn vector nhanh. |
| **RRF** | Reciprocal Rank Fusion — hợp nhất thứ hạng từ nhiều nguồn tìm kiếm. |
| **Reranker** | Mô hình Cross-Encoder sắp xếp lại kết quả truy vấn theo độ liên quan. |
| **Citation** | Trích dẫn nguồn (tên file, số trang, nội dung đoạn) đi kèm câu trả lời AI. |

---

## Thành phần hệ thống

| Thuật ngữ | Giải thích ngắn |
|-----------|-----------------|
| **Presentation** | Tầng ASP.NET Core MVC — giao diện web và API gateway cho người dùng. |
| **Business** | Tầng nghiệp vụ — xử lý chat, tài liệu, tích hợp LLM. |
| **DataAccess** | Tầng truy cập dữ liệu — Entity Framework Core, PostgreSQL. |
| **RAG API** | Dịch vụ Python FastAPI (`RAG-Retrieval-Indexing-API`) — indexing và retrieval. |
| **Dataset** | Tập tài liệu do người dùng/quản trị viên quản lý, mỗi dataset có index riêng. |
| **Index** | Chỉ mục vector + lexical được xây dựng sau khi upload và xử lý tài liệu. |

---

## LLM & Dịch vụ ngoài

| Thuật ngữ | Giải thích ngắn |
|-----------|-----------------|
| **LLM** | Large Language Model — mô hình sinh ngôn ngữ (Gemini, Groq, OpenAI). |
| **Fallback** | Cơ chế chuyển sang nhà cung cấp LLM dự phòng khi dịch vụ chính lỗi. |
| **Groq** | Nền tảng inference tốc độ cao, dự án dùng Llama 3.3 qua Groq API. |
| **pgvector** | Phần mở rộng PostgreSQL lưu trữ và tìm kiếm vector embedding. |

---

## Vai trò người dùng

| Thuật ngữ | Giải thích ngắn |
|-----------|-----------------|
| **Admin** | Quản trị viên — duyệt tài khoản, phân quyền dataset. |
| **Teacher** | Giảng viên — quản lý dataset và tài liệu được gán. |
| **Student** | Sinh viên — sử dụng chatbot trên dataset được phép truy cập. |

---

*Tài liệu bổ sung: xem thêm [README.md](../README.md) và [System_Architecture_Summary.md](./System_Architecture_Summary.md).*
