using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.DataAccess.Data;
using Pgvector;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.Presentation.Services;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            // Đăng ký Cookie Authentication
            builder.Services.AddAuthentication(Microsoft.AspNetCore.Authentication.Cookies.CookieAuthenticationDefaults.AuthenticationScheme)
                .AddCookie(options =>
                {
                    options.LoginPath = "/Account/Login";
                    options.AccessDeniedPath = "/Account/AccessDenied";
                    options.ExpireTimeSpan = TimeSpan.FromDays(7);
                });


            // Đăng ký Swagger Services
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo 
                { 
                    Title = "RAG Chatbot System API", 
                    Version = "v1",
                    Description = "API endpoints for RAG (Retrieval-Augmented Generation) Chatbot System."
                });
            });

            // Cấu hình kết nối PostgreSQL với pgvector
            builder.Services.AddDbContext<AppDbContext>(options =>
                     options.UseNpgsql(
                         builder.Configuration.GetConnectionString("DefaultConnection"),
                         o => o.UseVector()));

            // Cấu hình HttpClient cho Python RAG API (timeout 120s cho các thao tác nặng)
            builder.Services.AddHttpClient<IRagApiClient, RagApiClient>(client =>
            {
                var baseUrl = builder.Configuration["RagApi:BaseUrl"]
                    ?? throw new InvalidOperationException("RagApi:BaseUrl is not configured.");
                client.BaseAddress = new Uri(baseUrl);
                client.Timeout = TimeSpan.FromSeconds(120);
            });

            // Cấu hình HttpClient cho Groq LLM API (timeout 60s, cấu hình sẵn BaseAddress và Header)
            builder.Services.AddHttpClient<ILlmService, GroqService>(client =>
            {
                client.BaseAddress = new Uri("https://api.groq.com/openai/v1/");
                client.Timeout = TimeSpan.FromSeconds(60);

                var apiKey = builder.Configuration["Groq:ApiKey"];
                if (!string.IsNullOrWhiteSpace(apiKey))
                {
                    client.DefaultRequestHeaders.Authorization =
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }
            });

            // Đăng ký các dịch vụ Data Access Layer
            builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();

            // Đăng ký các dịch vụ Business Layer
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IDatasetService, DatasetService>();
            builder.Services.AddScoped<IFileStorageService, GoogleDriveStorageService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IChatSessionService, ChatSessionService>();

            var app = builder.Build();

            // === Auto-migrate database & seed admin account từ biến môi trường ===
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var startupLogger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

                try
                {
                    startupLogger.LogInformation("Applying database migrations...");
                    db.Database.Migrate();
                    startupLogger.LogInformation("Database migrations applied successfully.");

                    // Seed admin account nếu chưa có Admin nào trong hệ thống
                    // Đọc thông tin từ biến môi trường (cấu hình trong .env trên VPS)
                    var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL");
                    var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD");

                    if (!string.IsNullOrWhiteSpace(adminEmail)
                        && !string.IsNullOrWhiteSpace(adminPassword)
                        && !db.Users.Any(u => u.Role == "Admin"))
                    {
                        var admin = new RagChatbotSystem.DataAccess.Models.User
                        {
                            UserId = Guid.NewGuid(),
                            FullName = "System Admin",
                            Email = adminEmail,
                            PasswordHash = RagChatbotSystem.Business.Helpers.PasswordHasherHelper.HashPassword(adminPassword),
                            Role = "Admin",
                            IsApproved = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.Users.Add(admin);
                        db.SaveChanges();
                        startupLogger.LogInformation("Admin account seeded successfully for {Email}.", adminEmail);
                    }
                }
                catch (Exception ex)
                {
                    startupLogger.LogError(ex, "Error during database migration or seeding.");
                }
            }

            if (app.Environment.IsDevelopment())
            {
                app.UseSwagger();
                app.UseSwaggerUI(c =>
                {
                    c.SwaggerEndpoint("/swagger/v1/swagger.json", "RAG Chatbot System API v1");
                    // Cho phép hiển thị Swagger làm trang mặc định hoặc tại /swagger
                    c.RoutePrefix = "swagger"; 
                });
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            // Hỗ trợ reverse proxy (Nginx) để nhận đúng IP client và scheme
            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthentication();

            app.UseAuthorization();

            app.MapControllers();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
