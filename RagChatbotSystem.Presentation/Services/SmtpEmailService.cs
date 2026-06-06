using System;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Services
{
    public class SmtpEmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public SmtpEmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendAccountCreatedEmailAsync(
            string email,
            string fullName,
            string username,
            string temporaryPassword,
            CancellationToken cancellationToken = default)
        {
            var host = _configuration["Smtp:Host"];
            var usernameConfig = _configuration["Smtp:Username"];
            var password = _configuration["Smtp:Password"];
            var fromEmail = _configuration["Smtp:FromEmail"] ?? usernameConfig;
            var fromName = _configuration["Smtp:FromName"] ?? "RAG Chatbot System";

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(usernameConfig) ||
                string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(fromEmail))
            {
                throw new InvalidOperationException("SMTP email is not configured. Please set Smtp:Host, Smtp:Username, Smtp:Password and Smtp:FromEmail.");
            }

            var port = int.TryParse(_configuration["Smtp:Port"], out var configuredPort)
                ? configuredPort
                : 587;

            using var message = new MailMessage
            {
                From = new MailAddress(fromEmail, fromName),
                Subject = "Tài khoản RAG Chatbot của bạn",
                Body = BuildBody(fullName, username, temporaryPassword),
                IsBodyHtml = false
            };
            message.BodyEncoding = System.Text.Encoding.UTF8;
            message.SubjectEncoding = System.Text.Encoding.UTF8;
            message.HeadersEncoding = System.Text.Encoding.UTF8;
            message.To.Add(new MailAddress(email, fullName));

            using var smtp = new SmtpClient(host, port)
            {
                EnableSsl = true,
                Credentials = new NetworkCredential(usernameConfig, password)
            };

            await smtp.SendMailAsync(message, cancellationToken);
        }

        private static string BuildBody(string fullName, string username, string temporaryPassword)
        {
            return
                $"Xin chào {fullName},\n\n" +
                "Tài khoản RAG Chatbot của bạn đã được tạo.\n\n" +
                $"Username: {username}\n" +
                $"Mật khẩu tạm: {temporaryPassword}\n\n" +
                "Vui lòng đăng nhập và đổi mật khẩu ngay trong lần đầu sử dụng.\n\n" +
                "Nếu bạn không yêu cầu tài khoản này, hãy liên hệ Admin của hệ thống.";
        }
    }
}
