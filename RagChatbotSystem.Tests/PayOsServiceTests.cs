using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Moq;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.DataAccess.Models;
using RagChatbotSystem.Presentation.Services;

namespace RagChatbotSystem.Tests;

public sealed class PayOsServiceTests
{
    private const string ChecksumKey = "test-checksum-key";

    [Fact]
    public async Task CreatePaymentLinkAsync_SignsRequiredFieldsAndSendsCredentials()
    {
        var handler = new CaptureHandler();
        var service = CreateService(handler);
        var purchase = new CreditPurchaseDto(
            Guid.Parse("708b8210-78b4-4b20-886b-9cd3f313b8ce"), Guid.NewGuid(), Guid.NewGuid(),
            100, 10, 110, 50_000, "VND", CreditPurchaseStatus.PENDING, "PAYOS", null,
            DateTime.UtcNow, null, null);

        var result = await service.CreatePaymentLinkAsync(
            purchase, string.Empty, "https://example.com/return", "https://example.com/cancel");

        Assert.Equal("client-id", handler.Request!.Headers.GetValues("x-client-id").Single());
        Assert.Equal("api-key", handler.Request.Headers.GetValues("x-api-key").Single());
        var payload = JsonDocument.Parse(handler.Body!).RootElement;
        var canonical = $"amount={payload.GetProperty("amount").GetInt32()}&cancelUrl={payload.GetProperty("cancelUrl").GetString()}&description={payload.GetProperty("description").GetString()}&orderCode={payload.GetProperty("orderCode").GetInt64()}&returnUrl={payload.GetProperty("returnUrl").GetString()}";
        Assert.Equal(Sign(canonical), payload.GetProperty("signature").GetString());
        Assert.Equal("https://pay.payos.vn/web/test-link", result.CheckoutUrl);
    }

    [Fact]
    public void VerifyWebhook_RejectsTamperingAndAcceptsValidPayload()
    {
        var service = CreateService(new CaptureHandler());
        const string data = "amount=50000&code=00&description=CREDIT&orderCode=123456&reference=TX123";
        var validPayload = JsonDocument.Parse($$"""
            {"success":true,"data":{"orderCode":123456,"amount":50000,"description":"CREDIT","reference":"TX123","code":"00"},"signature":"{{Sign(data)}}"}
            """).RootElement.Clone();

        var valid = service.VerifyWebhook(validPayload);
        Assert.True(valid.IsValid);
        Assert.True(valid.IsSuccessful);
        Assert.Equal(123456, valid.OrderCode);

        var tamperedPayload = JsonDocument.Parse($$"""
            {"success":true,"data":{"orderCode":123456,"amount":90000,"description":"CREDIT","reference":"TX123","code":"00"},"signature":"{{Sign(data)}}"}
            """).RootElement.Clone();
        Assert.False(service.VerifyWebhook(tamperedPayload).IsValid);
    }

    private static PayOsService CreateService(HttpMessageHandler handler)
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c["PayOs:ClientId"]).Returns("client-id");
        configuration.Setup(c => c["PayOs:ApiKey"]).Returns("api-key");
        configuration.Setup(c => c["PayOs:ChecksumKey"]).Returns(ChecksumKey);
        return new PayOsService(new HttpClient(handler) { BaseAddress = new Uri("https://api-merchant.payos.vn/") }, configuration.Object);
    }

    private static string Sign(string data)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(ChecksumKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
    }

    private sealed class CaptureHandler : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"code":"00","desc":"success","data":{"paymentLinkId":"test-link","checkoutUrl":"https://pay.payos.vn/web/test-link","qrCode":"qr"}}""", Encoding.UTF8, "application/json")
            };
        }
    }
}
