# 🏢 Enterprise RAG Chatbot System

[🌐 English](#english) | [🇻🇳 Tiếng Việt](#tiếng-việt)

---

## English

An enterprise-grade, high-performance **Retrieval-Augmented Generation (RAG) Chatbot System** designed for smart search and precise document answering. The system combines modern .NET and Python service-oriented architectures with hybrid retrieval search (lexical + semantic) and cross-encoder reranking.

### 🏗️ Architecture & Core Components

- **Presentation Layer** (`RagChatbotSystem.Presentation`): ASP.NET Core Razor Pages application managing sessions, user accounts, UI, SignalR progress notifications, and PayOS integration.
- **Business Layer** (`RagChatbotSystem.Business`): Decoupled business logic, chat processing coordination, credit ledger, and AI orchestration.
- **Data Access Layer** (`RagChatbotSystem.DataAccess`): Entity Framework Core mapping, PostgreSQL database storage, migrations, and schema definitions.
- **RAG API** (`RAG-Retrieval-Indexing-API`): High-performance Python FastAPI service responsible for tokenization, smart chunking, FAISS index management, BM25 indexing, hybrid search (RRF), and cross-encoder reranking.
- **Tests Suite** (`RagChatbotSystem.Tests`): xUnit testing suite covering core retrieval, credit, and message logic.

### 🚀 Getting Started

#### 1. Docker Compose Quickstart
Run the following command at the root directory:
```bash
docker-compose up --build -d
```
- **Web App**: http://localhost:5259
- **RAG API**: http://localhost:8000

#### 2. Manual Setup
1. **Start Database**: Start PostgreSQL with pgvector enabled (e.g. `ankane/pgvector`).
2. **Apply Migrations**: 
   ```bash
   dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation
   ```
3. **Start Python RAG API**: Set up python environment, run `pip install -r requirements.txt`, then run:
   ```bash
   uvicorn main:app --host 127.0.0.1 --port 8000 --reload
   ```
4. **Start C# Web App**: Run the C# application:
   ```bash
   dotnet run --project RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj --launch-profile http
   ```

### ⚙️ Environment Configuration

Create a `.env` file at the root directory:
```bash
DB_PASSWORD=your_db_password
GROQ_API_KEY=your_groq_api_key
GEMINI_API_KEY=your_gemini_api_key
OPENAI_API_KEY=your_openai_api_key
HF_TOKEN=your_huggingface_token
GOOGLE_DRIVE_FOLDER_ID=your_google_drive_folder_id
```

### 📚 Technical Documentation & Guides

Detailed system design, pipeline details, and reference guides are linked below:
- 🗺️ [Database Schema & ERD Explanation](docs/ERD_RAG_Chatbot_Explanation.md)
- 🔌 [API Endpoints Overview](docs/API_OVERVIEW.md)
- ⚙️ [RAG Retrieval & Indexing Pipeline](docs/RAG_PIPELINE.md)
- 🌐 [Deployment & Hosting Notes](docs/DEPLOYMENT_NOTES.md)
- 🛠️ [Troubleshooting & FAQ](docs/TROUBLESHOOTING.md)
- 📊 [Model Comparison Benchmark Suite](docs/model-comparison/)

---

## Tiếng Việt

Hệ thống **RAG Chatbot (Retrieval-Augmented Generation)** cấp doanh nghiệp, hiệu năng cao, được thiết kế cho mục đích tra cứu thông minh và hỏi đáp chính xác dựa trên tài liệu tùy chỉnh. Hệ thống kết hợp kiến trúc hướng dịch vụ hiện đại giữa .NET và Python, tích hợp tìm kiếm lai (hybrid search kết hợp từ khóa + ngữ nghĩa) và sắp xếp lại kết quả (cross-encoder reranking).

### 🏗️ Kiến trúc & Các thành phần chính

- **Presentation Layer** (`RagChatbotSystem.Presentation`): Ứng ứng ASP.NET Core Razor Pages quản lý phiên làm việc, tài khoản người dùng, giao diện UI, SignalR cập nhật tiến trình thực tế, và tích hợp thanh toán PayOS.
- **Business Layer** (`RagChatbotSystem.Business`): Xử lý nghiệp vụ tách biệt, điều phối hội thoại chat, quản lý số dư tín dụng (credit ledger) và điều phối các dịch vụ AI.
- **Data Access Layer** (`RagChatbotSystem.DataAccess`): Ánh xạ thực thể Entity Framework Core, lưu trữ PostgreSQL, quản lý migrations và định nghĩa schema cơ sở dữ liệu.
- **RAG API** (`RAG-Retrieval-Indexing-API`): Dịch vụ Python FastAPI hiệu năng cao đảm nhiệm phân tách câu, chia nhỏ chunk thông minh, quản lý chỉ mục FAISS, chỉ mục BM25, tìm kiếm lai (RRF), và tái sắp xếp độ liên quan (reranking).
- **Tests Suite** (`RagChatbotSystem.Tests`): Bộ kiểm thử tự động xUnit bao gồm các test case cho các chức năng cốt lõi.

### 🚀 Khởi chạy hệ thống

#### 1. Khởi chạy nhanh bằng Docker Compose
Tại thư mục gốc của dự án, chạy lệnh:
```bash
docker-compose up --build -d
```
- **Ứng dụng Web**: http://localhost:5259
- **RAG API**: http://localhost:8000

#### 2. Cấu hình thủ công
1. **Khởi chạy Database**: Chạy PostgreSQL hỗ trợ extension pgvector (khuyên dùng `ankane/pgvector`).
2. **Cập nhật Database Migrations**:
   ```bash
   dotnet ef database update --project RagChatbotSystem.DataAccess --startup-project RagChatbotSystem.Presentation
   ```
3. **Khởi động Python RAG API**: Cài đặt môi trường Python, thực thi `pip install -r requirements.txt`, sau đó chạy:
   ```bash
   uvicorn main:app --host 127.0.0.1 --port 8000 --reload
   ```
4. **Khởi động C# Web App**: Khởi chạy ứng dụng Web C#:
   ```bash
   dotnet run --project RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj --launch-profile http
   ```

### ⚙️ Cấu hình môi trường

Tạo tệp `.env` tại thư mục gốc của dự án:
```bash
DB_PASSWORD=your_db_password
GROQ_API_KEY=your_groq_api_key
GEMINI_API_KEY=your_gemini_api_key
OPENAI_API_KEY=your_openai_api_key
HF_TOKEN=your_huggingface_token
GOOGLE_DRIVE_FOLDER_ID=your_google_drive_folder_id
```

### 📚 Tài liệu hướng dẫn kỹ thuật

Thông tin chi tiết về thiết kế, API và triển khai vận hành được liên kết bên dưới:
- 🗺️ [Sơ đồ CSDL & Giải thích ERD](docs/ERD_RAG_Chatbot_Explanation.md)
- 🔌 [Tổng quan API Endpoints](docs/API_OVERVIEW.md)
- ⚙️ [Quy trình xử lý RAG Pipeline](docs/RAG_PIPELINE.md)
- 🌐 [Hướng dẫn triển khai (Deployment)](docs/DEPLOYMENT_NOTES.md)
- 🛠️ [Khắc phục sự cố & FAQs](docs/TROUBLESHOOTING.md)
- 📊 [Nền tảng so sánh đánh giá Model Benchmark](docs/model-comparison/)

---

> [!TIP]
> **PostgreSQL Vector Extension**:
> If you are setting up local PostgreSQL without the `ankane/pgvector` image, you must install the extension on the database server and run: `CREATE EXTENSION IF NOT EXISTS vector;` before applying migrations.
> *(Nếu tự cấu hình PostgreSQL cục bộ không qua Docker, bạn bắt buộc phải cài đặt extension này và chạy: `CREATE EXTENSION IF NOT EXISTS vector;` trước khi apply migrations).*

> [!WARNING]
> **RAG API Cache Cleaning**:
> The Python API stores search indexes in the `cache/` directory. Delete this folder before restarting the API if you want to rebuild indexes from scratch.
> *(Python API lưu bộ nhớ đệm chỉ mục tại thư mục `cache/`. Hãy xóa thư mục này trước khi khởi chạy lại service nếu muốn làm sạch và lập chỉ mục lại).*

> [!IMPORTANT]
> **SignalR WebSockets over Nginx**:
> When using Nginx, configure connection upgrade headers correctly to prevent websocket handshake errors. Please refer to [nginx.conf](nginx.conf) for details.
> *(Khi chạy sau proxy Nginx, vui lòng cấu hình đúng Upgrade headers để tránh lỗi kết nối WebSockets cho SignalR. Tham khảo chi tiết tại [nginx.conf](nginx.conf)).*
