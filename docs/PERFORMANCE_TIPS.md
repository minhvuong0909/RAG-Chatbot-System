# Gợi ý tối ưu hiệu năng — RAG Chatbot System

Mẹo vận hành và phát triển. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## RAG API (Python)

- **Warm-up**: FastAPI lifespan tải embedding + reranker lúc khởi động — lượt request đầu nhanh hơn.
- **Cache**: Giữ volume `rag-cache`; tránh `rebuild_cache=true` trừ khi cần.
- **RAM**: Model Sentence-Transformers + Cross-Encoder cần bộ nhớ đủ (khuyến nghị ≥ 4GB cho service).

---

## Database

- Index phù hợp trên foreign key (`DatasetId`, `DocumentId`, `UserId`).
- pgvector: giới hạn `top_k` hợp lý (mặc định 3) để giảm tải.
- Backup và `VACUUM` định kỳ trên production.

---

## Web App (.NET)

- HttpClient timeout RAG: 120s — tài liệu lớn cần kiên nhẫn hoặc tăng timeout có chủ đích.
- Tránh N+1 query: dùng `Include` / projection khi load chat history.
- Production: `ASPNETCORE_ENVIRONMENT=Production`, tắt Swagger nếu không cần public.

---

## Upload & chunking

- File PDF scan ảnh (không có text layer) trích xuất kém — ảnh hưởng chất lượng RAG, không phải bug hiệu năng thuần.
- Chia nhỏ tài liệu cực lớn thành nhiều file nếu index timeout.

---

## LLM

- Groq inference nhanh — phù hợp demo.
- Giảm độ dài context gửi LLM (ít chunk hơn) nếu latency cao.

---

## Docker / VPS

- Đặt `restart: always` (đã có trong compose).
- Nginx `client_max_body_size` đủ lớn cho upload (50M trong mẫu).
- Monitor `docker stats` khi nhiều user đồng thời.

---

## Tài liệu liên quan

- [RAG_PIPELINE.md](./RAG_PIPELINE.md)
- [DEPLOYMENT_NOTES.md](./DEPLOYMENT_NOTES.md)
