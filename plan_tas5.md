# Kế hoạch thực hiện TV5 - QA / DevOps & Authentication Developer

## 1. Kết luận sau khi đọc ERD và dự án

Dự án hiện tại nên đi theo mô hình MVC 3 lớp trước:

```text
Presentation layer: Controller/API endpoint cho web
Business layer: Service xử lý nghiệp vụ
DataAccess layer: DbContext, Entity, Migration
```

Phần Razor/View có thể làm sau. Vì vậy plan này ưu tiên làm đúng authentication, authorization, docker và integration test ở tầng controller/service trước. ERD của nhóm đã chốt bảng `Users` như sau:

```text
Users
- user_id : UUID
- full_name : VARCHAR
- email : VARCHAR
- role : VARCHAR
- created_at : TIMESTAMP
```

ERD không có `password`, `password_hash`, `refresh_token`, hay bảng external login. Vì vậy cách tốt nhất cho task Authentication hiện tại là:

```text
Google OAuth trực tiếp + Cookie Authentication
```

Không chọn email/password vì không có chỗ lưu password theo ERD. Không chọn Supabase vì dự án hiện tại chưa dùng Supabase. Không chọn JWT vì bạn đang làm web theo mô hình MVC, Cookie Authentication phù hợp hơn và task cho phép chọn Cookie hoặc JWT.

Ý nghĩa triển khai:

```text
Đăng ký = lần đầu user đăng nhập Google, hệ thống tự tạo record trong bảng Users.
Đăng nhập = user đăng nhập Google, hệ thống tìm lại Users theo email.
Phân quyền = dùng Users.role với giá trị admin hoặc user.
Phiên đăng nhập = lưu bằng Cookie Authentication.
```

## 2. Hiện trạng code liên quan

Các phần đã có:

- `RagChatbotSystem.DataAccess/Models/User.cs`: đúng với ERD, không có password.
- `RagChatbotSystem.DataAccess/Data/AppDbContext.cs`: đã cấu hình các bảng chính và pgvector.
- `RagChatbotSystem.Business/Services/DocumentService.cs`: đã tạo document, cắt chunk, gọi Python API `/index`, lưu `Chunks`, lưu `VectorRecords`.
- `RagChatbotSystem.Business/Services/ChatService.cs`: đã gọi Python API `/retrieve`, gọi LLM, lưu `ChatMessages`, lưu `Citations`.
- `RagChatbotSystem.Business/DTOs/DocumentModelDto.cs`: DTO gửi chunk sang Python API, gồm `page_content` và `metadata`.
- `RagChatbotSystem.Presentation/Controllers/TestRagController.cs`: đang có endpoint test thủ công `/api/TestRag/run`.
- `RagChatbotSystem.Presentation/Program.cs`: đã cấu hình DB, Python RAG API, Groq LLM, nhưng chưa có authentication.

Các phần còn thiếu:

- Chưa có đăng nhập Google.
- Chưa có cookie authentication.
- Chưa có trang tài khoản.
- Chưa có phân quyền bằng `[Authorize]`.
- Chưa có Dockerfile và `docker-compose.yml`.
- Chưa có integration test project.
- Thư mục `RAG-Retrieval-Indexing-API` hiện chưa có đủ source để build Docker.

## 3. Phạm vi TV5 phải làm

TV5 chịu trách nhiệm 3 nhóm việc:

1. Authentication & Authorization.
2. Docker Compose / Infrastructure.
3. Integration Testing.

Luồng integration test bắt buộc theo task:

```text
Tải file -> Cắt chunk -> Lưu DB -> Chat trả lời -> Trích dẫn nguồn
```

Mapping với code hiện tại:

```text
DocumentService.ProcessAndIndexDocumentAsync
-> Documents
-> Chunks
-> VectorRecords
-> ChatService.SendChatMessageAsync
-> ChatMessages
-> Citations
```

## 4. Authentication & Authorization

### 4.1. Chọn giải pháp

Chọn:

```text
Google OAuth trực tiếp + Cookie Authentication
```

Lý do:

- Không cần sửa ERD.
- Không cần thêm password vào `User`.
- Phù hợp với ASP.NET Core MVC Web App theo mô hình 3 lớp.
- Đáp ứng yêu cầu "Cookie hoặc JWT" vì ta chọn Cookie.
- Google xử lý xác thực danh tính, app chỉ lưu user nội bộ theo ERD.

### 4.2. Luồng đăng ký và đăng nhập

Luồng hoạt động:

```text
User bấm Đăng nhập Google
-> App redirect sang Google
-> Google xác thực
-> Google trả về email và full_name
-> App tìm Users theo email
-> Nếu chưa có thì tạo user mới với role = user
-> Nếu đã có thì dùng lại user cũ
-> App tạo cookie đăng nhập
-> User được vào hệ thống
```

Tạo user mới theo đúng ERD:

```csharp
var user = new User
{
    UserId = Guid.NewGuid(),
    FullName = fullName,
    Email = email,
    Role = "user",
    CreatedAt = DateTime.UtcNow
};
```

### 4.3. Package cần cài

```bash
dotnet add RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj package Microsoft.AspNetCore.Authentication.Google
```

### 4.4. Cấu hình Google OAuth

Trên Google Cloud Console:

1. Tạo OAuth consent screen.
2. Tạo OAuth Client ID loại Web Application.
3. Thêm redirect URI local:

```text
https://localhost:<port>/signin-google
```

4. Thêm redirect URI Docker:

```text
http://localhost:8080/signin-google
```

Không commit `ClientId` và `ClientSecret` vào git.

Local dùng user secrets:

```bash
dotnet user-secrets init --project RagChatbotSystem.Presentation
dotnet user-secrets set "Authentication:Google:ClientId" "<client-id>" --project RagChatbotSystem.Presentation
dotnet user-secrets set "Authentication:Google:ClientSecret" "<client-secret>" --project RagChatbotSystem.Presentation
```

### 4.5. Sửa `Program.cs`

File:

```text
RagChatbotSystem.Presentation/Program.cs
```

Thêm:

```csharp
using Microsoft.AspNetCore.Authentication.Cookies;
```

Thêm cấu hình service sau `builder.Services.AddControllersWithViews();`:

```csharp
builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = "Google";
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    })
    .AddGoogle("Google", options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"]!;
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"]!;
        options.SaveTokens = false;
    });

builder.Services.AddAuthorization();
```

Sửa middleware đúng thứ tự:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

Hiện tại code mới có `UseAuthorization()`, nên cần thêm `UseAuthentication()` trước nó.

### 4.6. Tạo `AccountController`

File cần tạo:

```text
RagChatbotSystem.Presentation/Controllers/AccountController.cs
```

Các action cần có:

- `Login`: redirect sang Google.
- `GoogleCallback`: nhận kết quả từ Google, tạo/tìm user, tạo cookie.
- `Logout`: xóa cookie.
- `Profile`: trả về thông tin tài khoản. Giai đoạn đầu có thể trả JSON hoặc view đơn giản, Razor UI làm sau.
- `AccessDenied`: trang báo không đủ quyền.

Claims cần lưu trong cookie:

```text
ClaimTypes.NameIdentifier = UserId
ClaimTypes.Name = FullName
ClaimTypes.Email = Email
ClaimTypes.Role = Role
```

### 4.7. Giao diện tài khoản để sau, ưu tiên endpoint trước

Ở giai đoạn MVC 3 lớp trước, chỉ cần hoàn thành controller/action để flow chạy được:

```text
/Account/Login
/Account/Logout
/Account/Profile
/Account/AccessDenied
```

Nếu chưa làm Razor, có thể:

- `Login` redirect thẳng sang Google.
- `Logout` redirect về `Home/Index`.
- `Profile` trả JSON hoặc `Content(...)` tạm thời gồm họ tên, email, role.
- `AccessDenied` trả `Forbid()` hoặc `Content("Access denied")` tạm thời.

Khi chuyển sang làm Razor/View sau, tạo thêm:

```text
RagChatbotSystem.Presentation/Views/Account/Login.cshtml
RagChatbotSystem.Presentation/Views/Account/Profile.cshtml
RagChatbotSystem.Presentation/Views/Account/AccessDenied.cshtml
RagChatbotSystem.Presentation/Views/Shared/_Layout.cshtml
```

Lúc đó mới cập nhật navbar:

```text
Chưa đăng nhập: Đăng nhập
Đã đăng nhập: Tên người dùng | Tài khoản | Đăng xuất
```

### 4.8. Phân quyền

Dùng role theo ERD:

```text
admin
user
```

Route cần đăng nhập:

```csharp
[Authorize]
```

Route chỉ admin:

```csharp
[Authorize(Roles = "admin")]
```

Khi tạo dataset/document/chat session, lấy user hiện tại từ claim:

```csharp
var currentUserId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
```

Sau đó dùng:

```text
Datasets.CreatedBy = currentUserId
Documents.UploadedBy = currentUserId
ChatSessions.UserId = currentUserId
```

Không dùng hard-code test user trong code production.

## 5. Docker Compose / Infrastructure

### 5.1. Mục tiêu

Chạy toàn bộ hệ thống bằng một lệnh:

```bash
docker compose up --build
```

Các service cần có:

```text
postgres: PostgreSQL + pgvector
rag-api: Python Retrieval/Indexing API
web: C# ASP.NET Core MVC Web App
```

### 5.2. Dockerfile cho C# Web App

File cần tạo:

```text
RagChatbotSystem.Presentation/Dockerfile
```

Nội dung đề xuất:

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY RagChatbotSystem.sln ./
COPY RagChatbotSystem.DataAccess/RagChatbotSystem.DataAccess.csproj RagChatbotSystem.DataAccess/
COPY RagChatbotSystem.Business/RagChatbotSystem.Business.csproj RagChatbotSystem.Business/
COPY RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj RagChatbotSystem.Presentation/
RUN dotnet restore RagChatbotSystem.sln

COPY . .
RUN dotnet publish RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
EXPOSE 8080
ENTRYPOINT ["dotnet", "RagChatbotSystem.Presentation.dll"]
```

### 5.3. Dockerfile cho Python API

Thư mục:

```text
RAG-Retrieval-Indexing-API
```

Cần có:

- `Dockerfile`
- `requirements.txt`
- FastAPI app, ví dụ `main.py`

Python API phải khớp với `RagApiClient.cs`:

```text
POST /index
POST /retrieve
DELETE /documents/{documentId}
GET /health
```

Response `/index` phải trả về số embedding bằng số chunk C# gửi sang.

Response `/retrieve` phải trả về documents có metadata:

```text
id
document_id
dataset_id
```

Metadata này rất quan trọng vì `ChatService` dùng nó để tạo `Citations`.

### 5.4. Tạo `docker-compose.yml`

File cần tạo ở root:

```text
docker-compose.yml
```

Nội dung đề xuất:

```yaml
services:
  postgres:
    image: pgvector/pgvector:pg16
    container_name: rag-postgres
    environment:
      POSTGRES_DB: rag_chatbot
      POSTGRES_USER: postgres
      POSTGRES_PASSWORD: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U postgres -d rag_chatbot"]
      interval: 5s
      timeout: 5s
      retries: 10

  rag-api:
    build:
      context: ./RAG-Retrieval-Indexing-API
    container_name: rag-api
    ports:
      - "8000:8000"
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8000/health"]
      interval: 10s
      timeout: 5s
      retries: 10

  web:
    build:
      context: .
      dockerfile: RagChatbotSystem.Presentation/Dockerfile
    container_name: rag-web
    depends_on:
      postgres:
        condition: service_healthy
      rag-api:
        condition: service_healthy
    environment:
      ASPNETCORE_URLS: http://+:8080
      ASPNETCORE_ENVIRONMENT: Development
      ConnectionStrings__DefaultConnection: Host=postgres;Port=5432;Database=rag_chatbot;Username=postgres;Password=postgres
      RagApi__BaseUrl: http://rag-api:8000
      Authentication__Google__ClientId: ${GOOGLE_CLIENT_ID}
      Authentication__Google__ClientSecret: ${GOOGLE_CLIENT_SECRET}
      Groq__ApiKey: ${GROQ_API_KEY}
    ports:
      - "8080:8080"

volumes:
  postgres_data:
```

### 5.5. Tạo `.env.example`

File:

```text
.env.example
```

Nội dung:

```env
GOOGLE_CLIENT_ID=
GOOGLE_CLIENT_SECRET=
GROQ_API_KEY=
```

Khi chạy thật, copy thành `.env` và điền secret.

### 5.6. Migration khi chạy Docker

Project đã có migration `InitialCreate`. Để demo Docker dễ chạy, thêm auto migrate trong môi trường Development.

Trong `Program.cs`, sau `var app = builder.Build();`:

```csharp
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
```

Cách này giúp container web chạy lên là tự tạo bảng trong PostgreSQL.

## 6. Integration Testing

### 6.1. Mục tiêu test

Test phải chứng minh luồng này chạy thông:

```text
Tải file/text
-> Cắt chunk
-> Lưu Documents, Chunks, VectorRecords
-> Tạo ChatSession
-> Gửi câu hỏi
-> Lưu ChatMessages
-> Lưu Citations
```

### 6.2. Tạo test project

```bash
dotnet new xunit -n RagChatbotSystem.IntegrationTests
dotnet sln add RagChatbotSystem.IntegrationTests/RagChatbotSystem.IntegrationTests.csproj
dotnet add RagChatbotSystem.IntegrationTests/RagChatbotSystem.IntegrationTests.csproj reference RagChatbotSystem.Presentation/RagChatbotSystem.Presentation.csproj
dotnet add RagChatbotSystem.IntegrationTests/RagChatbotSystem.IntegrationTests.csproj package Microsoft.AspNetCore.Mvc.Testing
dotnet add RagChatbotSystem.IntegrationTests/RagChatbotSystem.IntegrationTests.csproj package Microsoft.EntityFrameworkCore.InMemory
```

### 6.3. Fake dependency bên ngoài

Không gọi Google, Groq hoặc Python API thật trong test mặc định.

Fake:

```text
IRagApiClient
ILlmService
Authentication handler
```

Fake `IRagApiClient.IndexDocumentsAsync`:

- Nhận danh sách chunk.
- Trả về embeddings 384 chiều.
- Số embedding bằng số chunk.

Fake `IRagApiClient.RetrieveAsync`:

- Trả về chunk có metadata:

```text
id = chunk_id
document_id = document_id
dataset_id = dataset_id
```

Fake `ILlmService.GenerateAnswerAsync`:

```text
Burj Khalifa nằm ở Dubai, United Arab Emirates.
```

### 6.4. Test flow RAG chính

Tên test đề xuất:

```text
RagFlow_Should_CreateDocumentChunksVectorsAnswerAndCitations
```

Arrange:

1. Tạo user test.
2. Tạo dataset thuộc user test.
3. Chuẩn bị text:

```text
The Burj Khalifa is located in Dubai, United Arab Emirates.
```

Act:

1. Gọi `DocumentService.ProcessAndIndexDocumentAsync(...)`.
2. Tạo `ChatSession`.
3. Gọi `ChatService.SendChatMessageAsync(...)`.

Assert:

- Có record trong `Documents`.
- `Documents.Status == "Completed"`.
- Có ít nhất 1 record trong `Chunks`.
- Số `VectorRecords` bằng số `Chunks`.
- Có `ChatMessages` role `User` hoặc `user`, tùy code thống nhất.
- Có `ChatMessages` role `Assistant` hoặc `assistant`, tùy code thống nhất.
- Assistant answer không rỗng.
- Có ít nhất 1 `Citation`.
- Citation trỏ đúng `ChunkId`.
- Citation trỏ đúng `DocumentId`.

Lưu ý: ERD ví dụ role message là `user`, `assistant`, `system`, nhưng code hiện tại đang dùng `User`, `Assistant`. Nên thống nhất lại một kiểu để tránh test và UI bị lệch.

### 6.5. Test Authentication & Authorization

Các test cần có:

1. User chưa đăng nhập truy cập route `[Authorize]` thì bị redirect tới `/Account/Login`.
2. User role `user` không truy cập được route `[Authorize(Roles = "admin")]`.
3. User role `admin` truy cập được route admin.
4. Lần login Google đầu tạo record trong bảng `Users`.
5. Lần login Google sau dùng lại user cũ theo email.

Không test Google thật trong integration test. Nếu cần test logic tạo user từ Google claim, tách phần đó thành service, ví dụ:

```text
IAccountService.FindOrCreateGoogleUserAsync(...)
```

Sau đó test service này bằng DB test.

## 7. Smoke test bằng Docker Compose

Chạy:

```bash
docker compose up --build
```

Kiểm tra Python API:

```bash
curl http://localhost:8000/health
```

Kiểm tra C# Web App:

```bash
curl http://localhost:8080
```

Kiểm tra flow RAG tạm bằng endpoint hiện có:

```bash
curl "http://localhost:8080/api/TestRag/run?question=Where%20is%20the%20Burj%20Khalifa"
```

Đạt yêu cầu nếu response có:

```text
Status = Success
Answer có nội dung
Citations có ít nhất 1 item
```

Lưu ý: `TestRagController` chỉ nên dùng trong Development/Test. Sau này nên gắn `[Authorize(Roles = "admin")]` hoặc tắt bằng environment.

## 8. Checklist hoàn thành TV5

### Authentication & Authorization

- Đăng nhập Google trực tiếp hoạt động.
- Lần đăng nhập đầu tự tạo user trong bảng `Users`.
- Lần đăng nhập sau dùng lại user theo email.
- App tạo cookie đăng nhập.
- Có logout.
- Có profile/account page.
- Có `[Authorize]` cho route cần đăng nhập.
- Có `[Authorize(Roles = "admin")]` cho route admin.
- Không sửa model `User` trái ERD.

### Docker Compose

- Có Dockerfile cho C# Web App.
- Có Dockerfile cho Python API.
- Có `docker-compose.yml`.
- Có `.env.example`.
- `docker compose up --build` chạy được.
- PostgreSQL dùng image có pgvector.
- Web app kết nối DB qua `postgres`.
- Web app gọi Python API qua `rag-api`.

### Integration Testing

- Có project `RagChatbotSystem.IntegrationTests`.
- Có fake `IRagApiClient`.
- Có fake `ILlmService`.
- Có fake authentication.
- Test được flow `Documents -> Chunks -> VectorRecords -> ChatMessages -> Citations`.
- Test được authorization cơ bản.
- `dotnet test` pass.

## 9. Thứ tự làm đề xuất

1. Cài package Google Authentication.
2. Cấu hình Cookie + Google trong `Program.cs`.
3. Tạo `AccountController`.
4. Tạo các endpoint `Login`, `Logout`, `Profile`, `AccessDenied`. Razor UI làm sau.
5. Gắn `[Authorize]` và `[Authorize(Roles = "admin")]`.
6. Đảm bảo controller nghiệp vụ lấy `UserId` từ claims.
7. Tạo Dockerfile cho C# Web App.
8. Chuẩn bị Dockerfile/source cho Python API.
9. Tạo `docker-compose.yml`.
10. Tạo `.env.example`.
11. Thêm auto migration trong Development.
12. Tạo integration test project.
13. Viết fake `IRagApiClient`, fake `ILlmService`, fake authentication handler.
14. Viết test flow RAG.
15. Viết test authorization.
16. Chạy:

```bash
dotnet build
dotnet test
docker compose up --build
```

## 10. Điểm cần báo nhóm

- Với ERD hiện tại, register/login tốt nhất là Google OAuth trực tiếp + Cookie.
- Không làm email/password nếu không sửa ERD thêm `PasswordHash`.
- Không cần Supabase vì dự án hiện tại chưa dùng Supabase.
- Không cần JWT vì project hiện là web theo mô hình MVC và task cho phép Cookie.
- Python API folder hiện chưa đủ source để Docker build, cần bổ sung hoặc tạo stub tạm.
- Không commit Google Client Secret hoặc Groq API Key.
- Nên thống nhất role message là lowercase theo ERD hoặc cập nhật test theo code hiện tại.
