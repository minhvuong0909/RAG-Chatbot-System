# RAG Chatbot System

Hệ thống chatbot hỏi đáp thông minh dựa trên kiến thức tùy chỉnh, được xây dựng bằng cách kết hợp công nghệ Retrieval-Augmented Generation (RAG) với các thành phần backend hiện đại. Dự án này hướng tới việc cung cấp trải nghiệm tra cứu thông minh, chính xác và có thể mở rộng cho các ứng dụng doanh nghiệp.

## 🎯 Mục tiêu dự án

- Cung cấp một chatbot có thể trả lời dựa trên tài liệu riêng của doanh nghiệp hoặc tổ chức.
- Hỗ trợ xử lý tài liệu đa định dạng như PDF, DOCX và TXT.
- Tích hợp tìm kiếm ngữ nghĩa, tìm kiếm từ khóa và sắp xếp lại kết quả để nâng cao chất lượng phản hồi.
- Cho phép triển khai linh hoạt trên môi trường phát triển cục bộ hoặc production.

## ✨ Tính năng chính

- Tải lên và xử lý tài liệu từ nhiều định dạng khác nhau.
- Tạo chỉ mục và chia nhỏ tài liệu thông minh để tối ưu hóa truy xuất.
- Hỗ trợ tìm kiếm lai (hybrid search) kết hợp lexical và semantic retrieval.
- Sử dụng reranking để cải thiện độ liên quan của kết quả.
- Cung cấp câu trả lời có trích dẫn nguồn để người dùng dễ kiểm chứng.
- Hỗ trợ tích hợp với nhiều mô hình LLM và cơ chế fallback khi dịch vụ chính gặp sự cố.

## 🏗️ Kiến trúc hệ thống

Dự án được tổ chức theo mô hình phân tầng với các thành phần chính:

- Presentation Layer: ứng dụng ASP.NET Core web interface.
- Business Layer: xử lý nghiệp vụ, dịch vụ chatbot và logic ứng dụng.
- Data Access Layer: quản lý dữ liệu, repository và mapping với cơ sở dữ liệu.
- RAG API: service Python FastAPI chịu trách nhiệm indexing, retrieval và ranking.

## 2. Cấu trúc thư mục dự án

- Backend chính: C# / ASP.NET Core
- RAG API: Python / FastAPI
- Cơ sở dữ liệu: PostgreSQL với pgvector
- Tìm kiếm và embedding: FAISS, BM25, sentence-transformers
- Containerization: Docker, Docker Compose
- Xác thực và quản lý dự án: .NET SDK, Git, EF Core

## 💻 Yêu cầu hệ thống

Trước khi bắt đầu, hãy đảm bảo hệ thống đã cài đặt:

- .NET 9 SDK
- Python 3.10+
- Docker Desktop (khuyến nghị)
- PostgreSQL hoặc Docker để chạy database

## 🚀 Cài đặt và khởi chạy nhanh

### 1. Sử dụng Docker Compose

Tại thư mục gốc của dự án, chạy:

```bash
docker-compose up --build -d
```

Sau khi khởi động thành công, hệ thống sẽ sẵn sàng tại các địa chỉ:

- Web application: http://localhost:5259
- FastAPI service: http://localhost:8000

### 2. Chạy thủ công cho môi trường phát triển

#### Bước 1: Khởi động database

```bash
docker run --name rag-postgres-db -e POSTGRES_PASSWORD=your_secure_password -e POSTGRES_DB=RagChatbotSystemDb -p 5432:5432 -d ankane/pgvector:latest
```

#### Bước 2: Cập nhật cơ sở dữ liệu

```bash
dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation
```

#### Bước 3: Khởi động Python RAG API

```bash
cd RAG-Retrieval-Indexing-API
pip install -r requirements.txt
uvicorn main:app --host 127.0.0.1 --port 8000 --reload
```

#### Bước 4: Khởi động ứng dụng C#

```bash
dotnet run --project RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj --launch-profile http
```

## ⚙️ Cấu hình môi trường

Tạo file .env ở thư mục gốc với các biến môi trường cần thiết như:

```bash
DB_PASSWORD=your_db_password
GROQ_API_KEY=
GEMINI_API_KEY=
OPENAI_API_KEY=
HF_TOKEN=
GOOGLE_DRIVE_FOLDER_ID=your_google_drive_folder_id
```

Ngoài ra, cấu hình kết nối và API trong file appsettings.json của ứng dụng Presentation.

## 🧪 Chạy tests

Để chạy bộ kiểm thử tự động của dự án:

```bash
dotnet test RagChatbotSystem.sln
```

## 📁 Cấu trúc thư mục chính

- RagChatbotSystem.Presentation: ứng dụng web và controller
- RagChatbotSystem.Business: business logic và service interface
- RagChatbotSystem.DataAccess: model, repository và migration
- RAG-Retrieval-Indexing-API: service Python phục vụ indexing và retrieval
- RagChatbotSystem.Tests: test cases cho các chức năng cốt lõi

## 📚 Tài liệu tham khảo

Thông tin chi tiết về kiến trúc, quy trình xử lý tài liệu, deployment và troubleshooting có thể tìm thấy trong thư mục docs.

## 📝 Ghi chú

- Nếu sử dụng môi trường phát triển cục bộ, hãy đảm bảo PostgreSQL đã được cấu hình đúng và mở rộng vector đã được kích hoạt.
- Nếu muốn làm mới chỉ mục tìm kiếm, có thể xóa thư mục cache trước khi khởi động lại API.

> [!TIP]
> **Kích hoạt Extension Vector trong PostgreSQL**:
> Nếu tự cài đặt PostgreSQL cục bộ mà không sử dụng Docker image `ankane/pgvector`, bạn bắt buộc phải cài đặt extension này trên máy chủ và chạy câu lệnh SQL sau trước khi áp dụng migrations:
> ```sql
> CREATE EXTENSION IF NOT EXISTS vector;
> ```

> [!WARNING]
> **Xóa Cache của RAG API**:
> Python API lưu bộ nhớ đệm chỉ mục tìm kiếm FAISS & BM25 tại thư mục `cache/`. Khi muốn làm sạch dữ liệu để lập chỉ mục lại hoàn toàn, hãy xóa thư mục này trước khi chạy lại service Python.

> [!IMPORTANT]
> **Vấn đề kết nối SignalR WebSockets qua Nginx**:
> Khi triển khai qua Nginx, kết nối WebSocket có thể bị lỗi hoặc tự động hạ cấp xuống Long Polling nếu các headers Upgrade không được cấu hình chính xác. Hãy tham khảo mẫu cấu hình tại [nginx.conf](nginx.conf). Để đảm bảo kết nối hoạt động ổn định, bạn nên thay đổi dòng `proxy_set_header Connection keep-alive;` trong Nginx cấu hình thực tế thành `proxy_set_header Connection $http_connection;`.
