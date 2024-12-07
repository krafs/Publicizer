using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Publicizer;

/// <summary>
/// Helper class for various hash related functions.
/// </summary>
internal static class Hasher
{
    internal static string ComputeHash(string assemblyPath, PublicizerAssemblyContext assemblyContext)
    {
        var sb = new StringBuilder();
        sb.Append(assemblyContext.AssemblyName);
        sb.Append(assemblyContext.IncludeCompilerGeneratedMembers);
        sb.Append(assemblyContext.IncludeVirtualMembers);
        sb.Append(assemblyContext.ExplicitlyPublicizeAssembly);
        sb.Append(assemblyContext.ExplicitlyDoNotPublicizeAssembly);
        foreach (string publicizePattern in assemblyContext.PublicizeMemberPatterns)
        {
            sb.Append(publicizePattern);
        }
        foreach (string doNotPublicizePattern in assemblyContext.DoNotPublicizeMemberPatterns)
        {
            sb.Append(doNotPublicizePattern);
        }
        if (assemblyContext.PublicizeMemberRegexPattern is not null)
        {
            sb.Append(assemblyContext.PublicizeMemberRegexPattern.ToString());
        }

        byte[] patternBytes = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        byte[] allBytes = assemblyBytes.Concat(patternBytes).ToArray();

        return ComputeHash(allBytes);
    }

    private static string ComputeHash(byte[] bytes)
    {
        using var algorithm = MD5.Create();

        byte[] computedHash = algorithm.ComputeHash(bytes);
        var sb = new StringBuilder();
        foreach (byte b in computedHash)
        {
            sb.Append($"{b:X2}");
        }
        string hexadecimalHash = sb.ToString();

        return hexadecimalHash;
    }
}
