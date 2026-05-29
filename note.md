# Ghi chú Docker / Database cho RAG Chatbot System

File này dùng để hướng dẫn team chạy Docker Compose, PostgreSQL + pgvector, C# Web App và ghi rõ phần Python API đang chờ team phụ trách bổ sung code/entrypoint.

## 0. Trạng thái Docker Compose task

Đã làm:

- Tạo `docker-compose.yml`.
- Chạy được PostgreSQL + pgvector.
- Chạy được pgAdmin web.
- Tạo Dockerfile cho C# Web App tại `RagChatbotSystem.Presentation/Dockerfile`.
- Build Docker image cho C# Web App thành công.
- Khai báo sẵn service `python-api` trong `docker-compose.yml` dưới profile `python-api`.

Chưa thể chạy full Python API ngay vì thư mục `RAG-Retrieval-Indexing-API` hiện chưa có code/Dockerfile/entrypoint. Đây là phần chờ teammate phụ trách Python API bổ sung.

### Currently missing

- `RAG-Retrieval-Indexing-API` chưa có source code và Dockerfile.
- `python-api` chỉ là profile stub để sau này bật khi có code.
- Chưa có endpoint Python API thực tế nên `web-app` hiện chỉ chạy phần backend auth và setup.

Hiện tại chạy được các service chính bằng một lệnh:

```powershell
docker-compose up -d --build
```

Sau khi Python API có Dockerfile, có thể chạy full stack bằng:

```powershell
docker-compose --profile python-api up -d --build
```

Nếu team muốn `python-api` luôn chạy mặc định, xóa phần này trong service `python-api`:

```yml
profiles:
  - python-api
```

## 1. Yêu cầu trước khi chạy

- Đã cài Docker Desktop.
- Đã cài .NET SDK phù hợp với dự án.
- Đang đứng ở thư mục gốc solution, ví dụ:

```powershell
cd D:\prn222
```

### Run order

1. `docker-compose up -d --build`
2. `dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation`
3. `dotnet build`
4. `dotnet test RagChatbotSystem.IntegrationTests\RagChatbotSystem.IntegrationTests.csproj`

Use the provided `.env.example` as a template and copy it to `.env` before starting Docker.

(Phần `python-api` chỉ bật khi team bổ sung code và chạy với `docker compose --profile python-api up -d --build`.)

## 2. Chạy PostgreSQL + pgvector bằng Docker

Chạy lệnh:

```powershell
docker-compose up -d --build
```

Database sẽ chạy với thông tin:

```text
Host: localhost
Port: 5434
Database: rag_chatbot
Username: rag_user
Password: 123456
```

Connection string cho C# Web App:

```text
Host=localhost;Port=5434;Database=rag_chatbot;Username=rag_user;Password=123456
```

Khi C# Web App chạy trong Docker Compose, connection string nội bộ là:

```text
Host=postgres;Port=5432;Database=rag_chatbot;Username=rag_user;Password=123456
```

C# Web App chạy tại:

```text
http://localhost:5259
```

pgAdmin web chạy tại:

```text
http://localhost:5050
```

## 2.1. Biến môi trường cần có khi chạy Docker

`docker-compose.yml` đang đọc các biến môi trường sau:

```text
GOOGLE_CLIENT_ID
GOOGLE_CLIENT_SECRET
GROQ_API_KEY
```

Có thể tạo file `.env` ở thư mục gốc solution:

```env
GOOGLE_CLIENT_ID=your-google-client-id
GOOGLE_CLIENT_SECRET=your-google-client-secret
GROQ_API_KEY=your-groq-api-key
ADMIN_EMAIL_0=votanthinhcri28@gmail.com
```

Không commit file `.env`.

Google OAuth local cần cấu hình:

```text
Authorized JavaScript origins:
http://localhost:5259

Authorized redirect URIs:
http://localhost:5259/signin-google
```

Admin mặc định cho môi trường local/Docker hiện tại:

```text
votanthinhcri28@gmail.com
```

Khi email này login Google, hệ thống sẽ gán role `admin`. Các email khác mặc định là role `user`.

## 3. Cấu hình appsettings.json cho Presentation

Khi chạy local bằng `dotnet run`, trong file `RagChatbotSystem.Presentation/appsettings.json` cần có:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Port=5434;Database=rag_chatbot;Username=rag_user;Password=123456"
  },
  "RagApi": {
    "BaseUrl": "http://localhost:8000"
  }
}
```

Google OAuth ClientId/ClientSecret không nên commit lên Git. Khi chạy local thì dùng User Secrets:

```powershell
dotnet user-secrets set "Authentication:Google:ClientId" "GOOGLE_CLIENT_ID" --project RagChatbotSystem.Presentation
dotnet user-secrets set "Authentication:Google:ClientSecret" "GOOGLE_CLIENT_SECRET" --project RagChatbotSystem.Presentation
```

## 4. Tạo schema database từ EF Migration

Sau khi Docker DB đã chạy, chạy:

```powershell
dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation
```

Nếu thấy cuối log có:

```text
Done.
```

thì database đã tạo schema thành công.

Schema được tạo theo Models và `AppDbContext`, gồm các bảng chính:

- `Users`
- `Datasets`
- `Documents`
- `Chunks`
- `VectorRecords`
- `ChatSessions`
- `ChatMessages`
- `Citations`

## 5. Kiểm tra DB bằng pgAdmin local

Nếu dùng pgAdmin 4 cài trên laptop:

```text
Host name/address: localhost
Port: 5434
Maintenance database: rag_chatbot
Username: rag_user
Password: 123456
```

Sau khi connect:

```text
Servers
-> rag_chatbot
-> Databases
-> rag_chatbot
-> Schemas
-> public
-> Tables
```

## 6. Kiểm tra DB bằng pgAdmin web trong Docker

Docker compose cũng có pgAdmin web:

```text
URL: http://localhost:5050
Email: admin@example.com
Password: admin
```

Khi register server trong pgAdmin web, dùng:

```text
Host name/address: postgres
Port: 5432
Maintenance database: rag_chatbot
Username: rag_user
Password: 123456
```

Lưu ý: trong pgAdmin web chạy cùng Docker network thì host là `postgres`, không phải `localhost`.

## 7. Nếu bị lỗi password hoặc không thấy table

Kiểm tra container đang chạy:

```powershell
docker ps
```

Kiểm tra kết nối trực tiếp vào DB:

```powershell
docker exec rag-postgres psql -U rag_user -d rag_chatbot -c "select current_user, current_database();"
```

Nếu connect được nhưng không thấy bảng, chạy lại migration:

```powershell
dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation
```

Nếu đổi user/password trong `docker-compose.yml` nhưng Docker vẫn nhận mật khẩu cũ, nguyên nhân thường là volume cũ còn giữ data. Chỉ dùng lệnh dưới đây khi chấp nhận xóa sạch database local:

```powershell
docker-compose down -v
docker-compose up -d --build
dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation
```

## 8. Ghi chú về schema

Database hiện tại khớp với Models trong dự án, không nhất thiết giống 100% cách đặt tên trong ERD PlantUML.

Ví dụ:

- ERD viết `user_id`, nhưng EF tạo cột `UserId`.
- `Chunks` có thêm `DatasetId` vì model `Chunk` đang có field này.
- `VectorRecords` có `DatasetId`, `DocumentId`, `ChunkId` vì model `VectorRecord` đang có các field này.
- `VectorRecords.ChunkId` là unique vì `AppDbContext` cấu hình quan hệ 1-1 giữa `Chunk` và `VectorRecord`.

Vì vậy khi kiểm tra DB, ưu tiên đối chiếu với Models và `AppDbContext`.

## 9. Phần Python API cần teammate bổ sung

Service `python-api` đã được khai báo sẵn trong `docker-compose.yml`, nhưng thư mục `RAG-Retrieval-Indexing-API` hiện cần thêm tối thiểu:

- `Dockerfile`
- file dependency, ví dụ `requirements.txt` hoặc `pyproject.toml`
- app entrypoint, ví dụ FastAPI/Flask chạy port `8000`
- endpoint đúng với `RagApiClient` bên C# đang gọi

Khi chạy trong Docker network, C# Web App sẽ gọi Python API qua:

```text
http://python-api:8000
```

Python API nên đọc database bằng biến môi trường:

```text
DATABASE_URL=postgresql://rag_user:123456@postgres:5432/rag_chatbot
```

Sau khi teammate hoàn tất Python API, test full stack:

```powershell
docker-compose --profile python-api up -d --build
```

## 10. Integration Testing

Đã tạo project integration test chính thức:

```text
RagChatbotSystem.IntegrationTests
```

Project này nằm trong solution và dùng PostgreSQL thật qua connection string mặc định:

```text
Host=localhost;Port=5434;Database=rag_chatbot;Username=rag_user;Password=123456
```

Chạy test:

```powershell
dotnet test RagChatbotSystem.IntegrationTests/RagChatbotSystem.IntegrationTests.csproj
```

Nếu muốn dùng DB khác:

```powershell
$env:RAG_TEST_CONNECTION_STRING="Host=localhost;Port=5434;Database=rag_chatbot;Username=rag_user;Password=123456"
dotnet test RagChatbotSystem.IntegrationTests/RagChatbotSystem.IntegrationTests.csproj
```

Kết quả test gần nhất:

```text
Passed: 2
Failed: 0
Total: 2
```

Những gì test hiện tại kiểm tra:

- Docker Compose build và chạy được C# Web App.
- Web App trả `200 OK` tại `http://localhost:5259`.
- PostgreSQL pgvector chạy ở `localhost:5434`.
- Email admin config nhận role `admin`.
- Flow RAG ở tầng Business + DataAccess chạy được:
  - tạo user
  - tạo dataset
  - xử lý document
  - cắt chunk
  - lưu document/chunk/vector vào DB
  - tạo chat session
  - gửi câu hỏi
  - lưu user message và assistant message
  - tạo citation cho câu trả lời

Checklist test end-to-end sau này:

- Chạy Docker full stack:

```powershell
docker-compose --profile python-api up -d --build
```

- Apply migration:

```powershell
dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation
```

- Test flow:
  - login Google hoặc seed user test
  - upload file
  - Python API cắt chunk/index embedding
  - C# lưu `Documents`, `Chunks`, `VectorRecords`
  - tạo chat session
  - gửi câu hỏi
  - lưu `ChatMessages`
  - tạo `Citations`
  - response trả answer + citations/source

Khi Python API thật đã có, integration test nên gọi HTTP thật thay vì fake RAG API/LLM.
