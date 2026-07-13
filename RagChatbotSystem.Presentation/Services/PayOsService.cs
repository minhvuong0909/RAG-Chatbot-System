using System.Globalization;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using RagChatbotSystem.Business.DTOs;
using RagChatbotSystem.Business.Interfaces;

namespace RagChatbotSystem.Presentation.Services
{
    public sealed class PayOsService : IPayOsService
    {
        private readonly HttpClient _httpClient;
        private readonly string _clientId;
        private readonly string _apiKey;
        private readonly string _checksumKey;

        public PayOsService(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            _clientId = configuration["PayOs:ClientId"] ?? string.Empty;
            _apiKey = configuration["PayOs:ApiKey"] ?? string.Empty;
            _checksumKey = configuration["PayOs:ChecksumKey"] ?? string.Empty;
        }

        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(_clientId) &&
            !string.IsNullOrWhiteSpace(_apiKey) &&
            !string.IsNullOrWhiteSpace(_checksumKey);

        public async Task<PayOsCheckoutResult> CreatePaymentLinkAsync(
            CreditPurchaseDto purchase,
            string description,
            string returnUrl,
            string cancelUrl,
            CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                throw new InvalidOperationException("PayOS chưa được cấu hình. Vui lòng bổ sung Client ID, API Key và Checksum Key.");
            }

            if (purchase.Amount != decimal.Truncate(purchase.Amount) || purchase.Amount <= 0 || purchase.Amount > int.MaxValue)
            {
                throw new InvalidOperationException("Giá gói Credit phải là số nguyên VND hợp lệ để thanh toán qua PayOS.");
            }

            var orderCode = CreateOrderCode(purchase.Id);
            var normalizedDescription = string.IsNullOrWhiteSpace(description)
                ? $"CREDIT {orderCode % 10_000_000_000L:D10}"
                : description.Trim();
            if (normalizedDescription.Length > 25) normalizedDescription = normalizedDescription[..25];

            var amount = decimal.ToInt32(purchase.Amount);
            var signatureData = $"amount={amount}&cancelUrl={cancelUrl}&description={normalizedDescription}&orderCode={orderCode}&returnUrl={returnUrl}";
            var request = new
            {
                orderCode,
                amount,
                description = normalizedDescription,
                returnUrl,
                cancelUrl,
                items = new[]
                {
                    new { name = $"Gói {purchase.TotalCredits} Credit", quantity = 1, price = amount }
                },
                signature = Sign(signatureData)
            };

            using var message = new HttpRequestMessage(HttpMethod.Post, "v2/payment-requests")
            {
                Content = JsonContent.Create(request)
            };
            message.Headers.Add("x-client-id", _clientId);
            message.Headers.Add("x-api-key", _apiKey);

            using var response = await _httpClient.SendAsync(message, cancellationToken);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);
            if (!response.IsSuccessStatusCode || !json.TryGetProperty("code", out var code) || code.GetString() != "00")
            {
                var detail = json.TryGetProperty("desc", out var desc) ? desc.GetString() : response.ReasonPhrase;
                throw new InvalidOperationException($"PayOS không thể tạo liên kết thanh toán: {detail ?? "lỗi không xác định"}.");
            }

            var data = json.GetProperty("data");
            return new PayOsCheckoutResult(
                orderCode,
                data.GetProperty("paymentLinkId").GetString() ?? throw new InvalidOperationException("PayOS không trả về mã liên kết thanh toán."),
                data.GetProperty("checkoutUrl").GetString() ?? throw new InvalidOperationException("PayOS không trả về địa chỉ thanh toán."),
                data.TryGetProperty("qrCode", out var qrCode) ? qrCode.GetString() ?? string.Empty : string.Empty);
        }

        public PayOsWebhookResult VerifyWebhook(JsonElement payload)
        {
            if (!IsConfigured)
            {
                return new PayOsWebhookResult(false, false, 0, 0, string.Empty, "PayOS chưa được cấu hình.");
            }

            if (!payload.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object ||
                !payload.TryGetProperty("signature", out var signatureElement))
            {
                return new PayOsWebhookResult(false, false, 0, 0, string.Empty, "Webhook PayOS thiếu dữ liệu hoặc chữ ký.");
            }

            var receivedSignature = signatureElement.GetString() ?? string.Empty;
            var canonicalData = string.Join("&", data.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => $"{property.Name}={FormatWebhookValue(property.Value)}"));
            var expectedSignature = Sign(canonicalData);
            var valid = FixedTimeEquals(receivedSignature, expectedSignature);

            var orderCode = data.TryGetProperty("orderCode", out var order) && order.TryGetInt64(out var parsedOrder) ? parsedOrder : 0;
            var amount = data.TryGetProperty("amount", out var amountElement) && amountElement.TryGetDecimal(out var parsedAmount) ? parsedAmount : 0;
            var reference = data.TryGetProperty("reference", out var referenceElement) ? referenceElement.GetString() ?? string.Empty : string.Empty;
            var successful = payload.TryGetProperty("success", out var success) && success.ValueKind == JsonValueKind.True &&
                data.TryGetProperty("code", out var dataCode) && dataCode.GetString() == "00";

            return new PayOsWebhookResult(valid, successful, orderCode, amount, reference,
                valid ? null : "Chữ ký webhook PayOS không hợp lệ.");
        }

        private string Sign(string data)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_checksumKey));
            return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(data))).ToLowerInvariant();
        }

        private static long CreateOrderCode(Guid purchaseId)
        {
            var hash = SHA256.HashData(purchaseId.ToByteArray());
            var positive = BitConverter.ToInt64(hash, 0) & long.MaxValue;
            return 100_000_000_000L + positive % 800_000_000_000L;
        }

        private static string FormatWebhookValue(JsonElement value)
        {
            var raw = value.ValueKind switch
            {
                JsonValueKind.Null or JsonValueKind.Undefined => string.Empty,
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                _ => value.GetRawText()
            };
            return raw;
        }

        private static bool FixedTimeEquals(string left, string right)
        {
            if (left.Length != right.Length) return false;
            return CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));
        }

        public async Task<PayOsPaymentStatusResult> GetPaymentStatusAsync(long orderCode, CancellationToken cancellationToken = default)
        {
            if (!IsConfigured)
            {
                return new PayOsPaymentStatusResult(false, string.Empty, orderCode, 0, null, "PayOS chưa được cấu hình.");
            }

            try
            {
                using var message = new HttpRequestMessage(HttpMethod.Get, $"v2/payment-requests/{orderCode}");
                message.Headers.Add("x-client-id", _clientId);
                message.Headers.Add("x-api-key", _apiKey);

                using var response = await _httpClient.SendAsync(message, cancellationToken);
                var json = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);

                if (!response.IsSuccessStatusCode
                    || !json.TryGetProperty("code", out var code)
                    || code.GetString() != "00")
                {
                    var detail = json.TryGetProperty("desc", out var desc) ? desc.GetString() : response.ReasonPhrase;
                    return new PayOsPaymentStatusResult(false, string.Empty, orderCode, 0, null, detail ?? "Lỗi không xác định từ PayOS.");
                }

                var data = json.GetProperty("data");
                var status = data.TryGetProperty("status", out var statusEl)
                    ? statusEl.GetString() ?? string.Empty
                    : string.Empty;
                var amountPaid = data.TryGetProperty("amountPaid", out var amtEl) && amtEl.TryGetDecimal(out var amt)
                    ? amt
                    : 0m;

                // Lấy reference từ transaction đầu tiên nếu có
                string? reference = null;
                if (data.TryGetProperty("transactions", out var txns) && txns.ValueKind == JsonValueKind.Array)
                {
                    foreach (var txn in txns.EnumerateArray())
                    {
                        if (txn.TryGetProperty("reference", out var refEl))
                        {
                            reference = refEl.GetString();
                            break;
                        }
                    }
                }

                return new PayOsPaymentStatusResult(true, status, orderCode, amountPaid, reference);
            }
            catch (Exception ex)
            {
                return new PayOsPaymentStatusResult(false, string.Empty, orderCode, 0, null, ex.Message);
            }
        }
    }
}
