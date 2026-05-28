using Microsoft.EntityFrameworkCore;
using RagChatbotSystem.DataAccess.Data;
using Pgvector;
using RagChatbotSystem.Business.Interfaces;
using RagChatbotSystem.Business.Services;

namespace RagChatbotSystem.Presentation
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddControllersWithViews();

            // Cấu hình kết nối PostgreSQL với pgvector
            builder.Services.AddDbContext<AppDbContext>(options =>
                     options.UseNpgsql(
                         builder.Configuration.GetConnectionString("DefaultConnection"),
                         o => o.UseVector()));

            // Cấu hình HttpClient cho Python RAG API (timeout 120s cho các thao tác nặng)
            builder.Services.AddHttpClient<IRagApiClient, RagApiClient>(client =>
            {
                var baseUrl = builder.Configuration["RagApi:BaseUrl"];
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
            builder.Services.AddScoped<IDocumentService, DocumentService>();
            builder.Services.AddScoped<IChatService, ChatService>();

            var app = builder.Build();

            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseStaticFiles();

            app.UseRouting();

            app.UseAuthorization();

            app.MapControllers();

            app.MapControllerRoute(
                name: "default",
                pattern: "{controller=Home}/{action=Index}/{id?}");

            app.Run();
        }
    }
}