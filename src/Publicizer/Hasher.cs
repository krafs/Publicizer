using System.Security.Cryptography;
using System.Text;

namespace Publicizer
{
    /// <summary>
    /// Helper class for various hash related functions.
    /// </summary>
    public static class Hasher
    {
        public static string ComputeHash(byte[] bytes)
        {
#pragma warning disable CA5351 // Do Not Use Broken Cryptographic Algorithms
            using MD5 algorithm = MD5.Create();
#pragma warning restore CA5351 // Do Not Use Broken Cryptographic Algorithms

            byte[] computedHash = algorithm.ComputeHash(bytes);
            StringBuilder sb = new StringBuilder();
            foreach (byte b in computedHash)
            {
                sb.Append($"{b:X2}");
            }
            string hexadecimalHash = sb.ToString();

            return hexadecimalHash;
        }
    }
}
