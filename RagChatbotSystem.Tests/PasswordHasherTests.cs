using System.Security.Cryptography;
using RagChatbotSystem.Business.Helpers;

namespace RagChatbotSystem.Tests;

public sealed class PasswordHasherTests
{
    [Fact]
    public void HashPassword_CreatesVersionedHashThatCanBeVerified()
    {
        var hash = PasswordHasherHelper.HashPassword("Strong-password-123!");

        Assert.StartsWith("pbkdf2-sha256$", hash);
        Assert.True(PasswordHasherHelper.VerifyPassword("Strong-password-123!", hash));
        Assert.False(PasswordHasherHelper.VerifyPassword("wrong-password", hash));
    }

    [Fact]
    public void VerifyPassword_AcceptsLegacyHashDuringMigration()
    {
        const string password = "Legacy-password-123!";
        var salt = Enumerable.Range(1, 16).Select(value => (byte)value).ToArray();
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, 10_000, HashAlgorithmName.SHA256, 32);
        var legacyBytes = salt.Concat(key).ToArray();

        Assert.True(PasswordHasherHelper.VerifyPassword(password, Convert.ToBase64String(legacyBytes)));
        Assert.False(PasswordHasherHelper.VerifyPassword("wrong-password", Convert.ToBase64String(legacyBytes)));
    }
}
