using System.Security.Cryptography;
using System.Text;

namespace Publicizer;

/// <summary>
/// Helper class for various hash related functions.
/// </summary>
internal static class Hasher
{
    internal static string ComputeHash(byte[] bytes)
    {
        using var algorithm = MD5.Create();

        var computedHash = algorithm.ComputeHash(bytes);
        var sb = new StringBuilder();
        foreach (var b in computedHash)
        {
            sb.Append($"{b:X2}");
        }
        var hexadecimalHash = sb.ToString();

        return hexadecimalHash;
    }
}
