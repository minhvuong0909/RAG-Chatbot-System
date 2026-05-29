using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.DataAccess.Data;
using Pgvector;
using RagChatbotSystem.Business.Constants;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;
using RagChatbotSystem.Presentation.Authorization;
using RagChatbotSystem.Presentation.Services;

namespace RagChatbotSystem.Presentation
{
    public class Program
    {
        private const string FrontendCorsPolicy = "FrontendCorsPolicy";

        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            builder.Services.AddCors(options =>
            {
                options.AddPolicy(FrontendCorsPolicy, policy =>
                {
                    policy
                        .WithOrigins(
                            "http://localhost:3000",
                            "http://127.0.0.1:3000",
                            "http://localhost:4200",
                            "http://127.0.0.1:4200",
                            "http://localhost:5173",
                            "http://127.0.0.1:5173",
                            "http://localhost:5174",
                            "http://127.0.0.1:5174",
                            "http://localhost:5500",
                            "http://127.0.0.1:5500",
                            "http://localhost:8080",
                            "http://127.0.0.1:8080")
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                });
            });

            var googleClientId = builder.Configuration["Authentication:Google:ClientId"];
            var googleClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
            var hasGoogleAuthentication = !string.IsNullOrWhiteSpace(googleClientId)
                && !string.IsNullOrWhiteSpace(googleClientSecret);

            var authenticationBuilder = builder.Services
                .AddAuthentication(options =>
                {
                    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
                    if (hasGoogleAuthentication)
                    {
                        options.DefaultChallengeScheme = "Google";
                    }
                })
                .AddCookie(options =>
                {
                    options.LoginPath = "/api/auth/google-login";
                    options.AccessDeniedPath = "/api/auth/access-denied";
                    options.ExpireTimeSpan = TimeSpan.FromHours(8);
                    options.SlidingExpiration = true;
                });

            if (hasGoogleAuthentication)
            {
                authenticationBuilder.AddGoogle("Google", options =>
                {
                    options.ClientId = googleClientId!;
                    options.ClientSecret = googleClientSecret!;
                    options.SaveTokens = false;
                });
            }

            builder.Services.AddAuthorization(options =>
            {
                options.AddPolicy(AuthPolicies.AdminOnly, policy =>
                    policy.RequireRole(UserRoles.Admin));

                options.AddPolicy(AuthPolicies.UserOnly, policy =>
                    policy.RequireRole(UserRoles.User));

                options.AddPolicy(AuthPolicies.AdminOrUser, policy =>
                    policy.RequireRole(UserRoles.Admin, UserRoles.User));
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

            // Đăng ký các dịch vụ Business Layer
            builder.Services.AddScoped<IAccountService, AccountService>();
            builder.Services.AddScoped<IUserService, UserService>();
            builder.Services.AddScoped<IDatasetService, DatasetService>();
            builder.Services.AddScoped<IFileStorageService, LocalFileStorageService>();
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();
            builder.Services.AddScoped<IChatSessionService, ChatSessionService>();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                using var scope = app.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                db.Database.Migrate();
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            if (!app.Environment.IsDevelopment())
            {
                app.UseHttpsRedirection();
            }

            app.UseStaticFiles();

            app.UseRouting();

            app.UseCors(FrontendCorsPolicy);

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
