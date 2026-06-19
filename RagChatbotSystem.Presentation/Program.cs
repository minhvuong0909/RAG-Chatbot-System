using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.DataAccess.Data;
using Pgvector;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.Presentation.Services;
using RagChatbotSystem.Presentation.Realtime;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();
            builder.Services.AddRazorPages();
            builder.Services.AddSignalR();

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

            // Cấu hình HttpClient cho Python RAG API 
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
            builder.Services.AddScoped<IEmailService, SmtpEmailService>();
            builder.Services.AddScoped<IFileStorageService, GoogleDriveStorageService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IChatSessionService, ChatSessionService>();
            builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

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
                    var adminEmail = builder.Configuration["AdminSeed:Email"]
                        ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL");
                    var adminPassword = builder.Configuration["AdminSeed:Password"]
                        ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
                    var adminUsername = builder.Configuration["AdminSeed:Username"] ?? "admin";
                    var adminFullName = builder.Configuration["AdminSeed:FullName"] ?? "System Admin";

                    if (!string.IsNullOrWhiteSpace(adminEmail)
                        && !string.IsNullOrWhiteSpace(adminPassword))
                    {
                        adminEmail = adminEmail.Trim().ToLowerInvariant();
                        adminUsername = adminUsername.Trim().ToLowerInvariant();

                        var admin = db.Users.FirstOrDefault(u =>
                            u.Email.ToLower() == adminEmail ||
                            u.Username.ToLower() == adminUsername);

                        if (admin == null)
                        {
                            admin = new RagChatbotSystem.DataAccess.Models.User
                            {
                                UserId = Guid.NewGuid(),
                                CreatedAt = DateTime.UtcNow
                            };
                            db.Users.Add(admin);
                        }

                        admin.FullName = adminFullName;
                        admin.Email = adminEmail;
                        admin.Username = adminUsername;
                        admin.PasswordHash = RagChatbotSystem.Business.Helpers.PasswordHasherHelper.HashPassword(adminPassword);
                        admin.Role = "Admin";
                        admin.IsApproved = true;
                        admin.MustChangePassword = false;
                        admin.TemporaryPasswordExpiresAt = null;
                        admin.LastPasswordChangedAt = DateTime.UtcNow;
                        db.SaveChanges();
                        startupLogger.LogInformation("Admin account ensured successfully for {Email}.", adminEmail);
                    }

                    var myAdminEmail = "admin@vuongdev.top";
                    if (!db.Users.Any(u => u.Email == myAdminEmail))
                    {
                        var myAdmin = new RagChatbotSystem.DataAccess.Models.User
                        {
                            UserId = Guid.NewGuid(),
                            FullName = "Vuong Dev Admin",
                            Email = myAdminEmail,
                            PasswordHash = RagChatbotSystem.Business.Helpers.PasswordHasherHelper.HashPassword("Vv123456!"),
                            Role = "Admin",
                            IsApproved = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.Users.Add(myAdmin);
                        db.SaveChanges();
                        startupLogger.LogInformation("Admin account seeded successfully for {Email}.", myAdminEmail);
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
            app.MapRazorPages();
            app.MapHub<Hubs.NotificationHub>("/hubs/notifications");

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}
