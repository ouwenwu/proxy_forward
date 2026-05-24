using System.Security.Cryptography;
using System.Text;

namespace ProxyForward;

public static class SecretProtector
{
    private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("ProxyForward.v1");

    public static string Protect(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
        {
            return "";
        }

        var bytes = Encoding.UTF8.GetBytes(plainText);
        var encrypted = ProtectedData.Protect(bytes, Entropy, DataProtectionScope.CurrentUser);
        return Convert.ToBase64String(encrypted);
    }

    public static string Unprotect(string protectedText)
    {
        if (string.IsNullOrWhiteSpace(protectedText))
        {
            return "";
        }

        var encrypted = Convert.FromBase64String(protectedText);
        var bytes = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
        return Encoding.UTF8.GetString(bytes);
    }
}
