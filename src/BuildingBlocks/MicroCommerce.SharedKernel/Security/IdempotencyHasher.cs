using System.Security.Cryptography;
using System.Text;

namespace MicroCommerce.SharedKernel.Security;

public static class IdempotencyHasher
{
    public static string Compute(string rawValue)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(rawValue));
        return Convert.ToHexString(bytes);
    }
}
