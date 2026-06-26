# Luồng RAG Pipeline — RAG Chatbot System

Mô tả pipeline indexing và retrieval. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## 1. Indexing (Data Ingestion)

```
Upload file → Trích xuất text (PdfPig / OpenXml)
           → Smart chunking (600 chars, overlap 120)
           → Gán metadata (file, page, document_id)
           → POST /index (RAG API)
           → Embedding + FAISS + BM25 cache
           → Lưu VectorRecord vào PostgreSQL (pgvector)
```

### Điểm quan trọng

- Upload và index tách bước — UX phản hồi nhanh hơn.
- `rebuild_cache` buộc build lại chỉ mục khi cần.
- Cache nằm tại `cache/` (volume Docker `rag-cache`).

---

## 2. Retrieval (Query)

```
Câu hỏi user → POST /retrieve
            → BM25 (lexical) + FAISS (semantic)
            → RRF hợp nhất thứ hạng
            → Cross-Encoder rerank (tùy enable_rerank)
            → Top-K chunks + scores + trace
```

Tham số mặc định: `semantic_weight=0.7`, `lexical_weight=0.3`, `top_k=3`.

---

## 3. Generation

```
Chunks + câu hỏi → Prompt LLM
                 → Gemini / Groq / OpenAI (fallback nếu lỗi)
                 → Câu trả lời + Citations lưu DB
```

`ChatService` điều phối retrieve → LLM → persist `ChatMessage` và `Citation`.

---

## 4. Xóa tài liệu

```
DELETE document → Xóa khỏi RAG index (DELETE /documents/{id})
               → Xóa metadata DB (chunks, vectors)
```

---

## Tài liệu liên quan

- [GLOSSARY.md](./GLOSSARY.md)
- [API_OVERVIEW.md](./API_OVERVIEW.md)
