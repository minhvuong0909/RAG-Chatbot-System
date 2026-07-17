using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.DataAccess.Data;
using Pgvector;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.Presentation.Services;
using RagChatbotSystem.Presentation.Hubs;
using RagChatbotSystem.Presentation.Realtime;
using RagChatbotSystem.DataAccess.Repositories;

namespace RagChatbotSystem.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            var groqApiKey = builder.Configuration["Groq:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GROQ_API_KEY");
            if (string.IsNullOrWhiteSpace(groqApiKey) && builder.Environment.IsDevelopment())
            {
                var envFile = new[]
                {
                    Path.Combine(builder.Environment.ContentRootPath, ".env"),
                    Path.GetFullPath(Path.Combine(builder.Environment.ContentRootPath, "..", ".env"))
                }.FirstOrDefault(File.Exists);

                var keyLine = envFile == null
                    ? null
                    : File.ReadLines(envFile).FirstOrDefault(line => line.StartsWith("GROQ_API_KEY=", StringComparison.Ordinal));
                groqApiKey = keyLine?["GROQ_API_KEY=".Length..].Trim().Trim('"', '\'');
            }
            if (!string.IsNullOrWhiteSpace(groqApiKey))
            {
                builder.Configuration["Groq:ApiKey"] = groqApiKey;
            }

            var geminiApiKey = builder.Configuration["Gemini:ApiKey"]
                ?? Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (string.IsNullOrWhiteSpace(geminiApiKey) && builder.Environment.IsDevelopment())
            {
                geminiApiKey = ReadLocalEnvValue(builder.Environment.ContentRootPath, "GEMINI_API_KEY");
            }
            if (!string.IsNullOrWhiteSpace(geminiApiKey))
            {
                builder.Configuration["Gemini:ApiKey"] = geminiApiKey;
            }

            var payOsSettings = new[]
            {
                (ConfigKey: "PayOs:ClientId", EnvironmentKey: "PAYOS_CLIENT_ID"),
                (ConfigKey: "PayOs:ApiKey", EnvironmentKey: "PAYOS_API_KEY"),
                (ConfigKey: "PayOs:ChecksumKey", EnvironmentKey: "PAYOS_CHECKSUM_KEY"),
                (ConfigKey: "PayOs:PublicBaseUrl", EnvironmentKey: "PAYOS_PUBLIC_BASE_URL")
            };
            foreach (var setting in payOsSettings)
            {
                if (!string.IsNullOrWhiteSpace(builder.Configuration[setting.ConfigKey])) continue;
                var value = Environment.GetEnvironmentVariable(setting.EnvironmentKey);
                if (string.IsNullOrWhiteSpace(value) && builder.Environment.IsDevelopment())
                {
                    value = ReadLocalEnvValue(builder.Environment.ContentRootPath, setting.EnvironmentKey);
                }
                if (!string.IsNullOrWhiteSpace(value)) builder.Configuration[setting.ConfigKey] = value;
            }

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

            // Named HttpClients dùng riêng cho tính năng so sánh model (Admin/ModelComparison),
            // tách khỏi HttpClient đã đăng ký cho ILlmService để không ảnh hưởng luồng chat chính.
            builder.Services.AddHttpClient("ModelComparison.Groq", client =>
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

            builder.Services.AddHttpClient("ModelComparison.Gemini", client =>
            {
                client.Timeout = TimeSpan.FromSeconds(60);
            });

            builder.Services.AddHttpClient<IPayOsService, PayOsService>(client =>
            {
                client.BaseAddress = new Uri("https://api-merchant.payos.vn/");
                client.Timeout = TimeSpan.FromSeconds(30);
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
            builder.Services.AddScoped<IQuestionSuggestionService, QuestionSuggestionService>();
            builder.Services.AddScoped<ISystemSettingService, SystemSettingService>();
            builder.Services.AddScoped<ITokenUsageService, TokenUsageService>();
            builder.Services.AddScoped<ICreditService, CreditService>();
            builder.Services.AddScoped<ICreditPurchaseService, CreditPurchaseService>();
            builder.Services.AddScoped<IStatisticsService, StatisticsService>();
            builder.Services.AddScoped<IModelComparisonService, ModelComparisonService>();
            builder.Services.AddScoped<IBenchmarkEvaluationService, BenchmarkEvaluationService>();
            builder.Services.AddScoped<IRealtimeService, RealtimeService>();
            builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();
            builder.Services.AddScoped<IDocumentProgressNotifier, DocumentProgressNotifier>();
            builder.Services.AddHostedService<RagIndexRehydrationService>();
            builder.Services.AddSingleton<BatchComparisonService>();

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
                    var adminEmail = Environment.GetEnvironmentVariable("ADMIN_EMAIL")
                        ?? builder.Configuration["AdminSeed:Email"];
                    var adminPassword = Environment.GetEnvironmentVariable("ADMIN_PASSWORD")
                        ?? builder.Configuration["AdminSeed:Password"];
                    var adminUsername = builder.Configuration["AdminSeed:Username"] ?? "admin";
                    var adminFullName = builder.Configuration["AdminSeed:FullName"] ?? "System Admin";

                    if (!string.IsNullOrWhiteSpace(adminEmail)
                        && !string.IsNullOrWhiteSpace(adminPassword))
                    {
                        adminEmail = adminEmail.Trim().ToLowerInvariant();
                        adminUsername = adminUsername.Trim().ToLowerInvariant();

                        var adminByEmail = db.Users.FirstOrDefault(u => u.Email.ToLower() == adminEmail);
                        var adminByUsername = db.Users.FirstOrDefault(u => u.Username.ToLower() == adminUsername);
                        var admin = adminByEmail ?? adminByUsername;

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
                        admin.Username = ResolveAvailableUsername(db, adminUsername, admin.UserId);
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
                            Username = ResolveAvailableUsername(db, "vuongdev-admin", Guid.Empty),
                            PasswordHash = RagChatbotSystem.Business.Helpers.PasswordHasherHelper.HashPassword("Vv123456!"),
                            Role = "Admin",
                            IsApproved = true,
                            CreatedAt = DateTime.UtcNow
                        };
                        db.Users.Add(myAdmin);
                        db.SaveChanges();
                        startupLogger.LogInformation("Admin account seeded successfully for {Email}.", myAdminEmail);
                    }

                    var seedUsers = new[]
                    {
                        new { Email = "s1@test.com", Username = "s1", Role = "Student", FullName = "Student 1" },
                        new { Email = "s2@test.com", Username = "s2", Role = "Student", FullName = "Student 2" },
                        new { Email = "t1@test.com", Username = "t1", Role = "Teacher", FullName = "Teacher 1" },
                        new { Email = "t2@test.com", Username = "t2", Role = "Teacher", FullName = "Teacher 2" }
                    };

                    foreach (var u in seedUsers)
                    {
                        if (!db.Users.Any(x => x.Email == u.Email))
                        {
                            var newUser = new RagChatbotSystem.DataAccess.Models.User
                            {
                                UserId = Guid.NewGuid(),
                                FullName = u.FullName,
                                Email = u.Email,
                                Username = ResolveAvailableUsername(db, u.Username, Guid.Empty),
                                PasswordHash = RagChatbotSystem.Business.Helpers.PasswordHasherHelper.HashPassword("123"),
                                Role = u.Role,
                                IsApproved = true,
                                CreatedAt = DateTime.UtcNow
                            };
                            db.Users.Add(newUser);
                            startupLogger.LogInformation("{Role} account seeded successfully for {Email}.", u.Role, u.Email);
                        }
                    }
                    db.SaveChanges();
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
                app.UseExceptionHandler("/Error");
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

            app.MapRazorPages();
            app.MapHub<ChatHub>("/hubs/chat");
            app.MapHub<DocumentHub>("/hubs/document");
            app.MapHub<NotificationHub>("/hubs/notifications");

            app.Run();
        }

        private static string ResolveAvailableUsername(AppDbContext db, string desiredUsername, Guid currentUserId)
        {
            var baseUsername = string.IsNullOrWhiteSpace(desiredUsername)
                ? "admin"
                : desiredUsername.Trim().ToLowerInvariant();

            var username = baseUsername;
            var suffix = 2;

            while (db.Users.Any(u => u.UserId != currentUserId && u.Username.ToLower() == username))
            {
                username = $"{baseUsername}{suffix}";
                suffix++;
            }

            return username;
        }

        private static string? ReadLocalEnvValue(string contentRootPath, string key)
        {
            var envFile = new[]
            {
                Path.Combine(contentRootPath, ".env"),
                Path.GetFullPath(Path.Combine(contentRootPath, "..", ".env"))
            }.FirstOrDefault(File.Exists);
            if (envFile == null) return null;

            var prefix = key + "=";
            var line = File.ReadLines(envFile)
                .FirstOrDefault(candidate => candidate.StartsWith(prefix, StringComparison.Ordinal));
            return line?[prefix.Length..].Trim().Trim('"', '\'');
        }
    }
}
