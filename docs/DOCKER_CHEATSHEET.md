# Docker Cheatsheet — RAG Chatbot System

Lệnh Docker thường dùng. Tài liệu tham khảo, **không ảnh hưởng** build hay runtime.

---

## Khởi động / dừng

```bash
# Build và chạy nền
docker compose up --build -d

# Dừng container (giữ volume)
docker compose down

# Dừng và xóa volume (MẤT DỮ LIỆU DB + CACHE)
docker compose down -v
```

---

## Xem log

```bash
docker compose logs -f
docker compose logs -f web-app
docker compose logs -f rag-api
docker compose logs -f db
```

---

## Trạng thái

```bash
docker compose ps
docker stats
```

---

## Vào shell container

```bash
docker exec -it rag-web-app bash
docker exec -it rag-retrieval-api bash
docker exec -it rag-postgres-db psql -U postgres -d RagChatbotSystemDb
```

---

## Rebuild một service

```bash
docker compose up --build -d web-app
docker compose up --build -d rag-api
```

---

## Volume

| Volume | Nội dung |
|--------|----------|
| `pgdata` | Database PostgreSQL |
| `rag-cache` | FAISS + BM25 cache |

Xóa cache index: `docker volume rm <project>_rag-cache` (sau `compose down`).

---

## Port mặc định

- Web: `5259`
- RAG API: `8000`
- Postgres: `5432`

---

## Tài liệu liên quan

- [DEPLOYMENT_NOTES.md](./DEPLOYMENT_NOTES.md)
- [TROUBLESHOOTING.md](./TROUBLESHOOTING.md)
