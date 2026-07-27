using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace Publicizer;

/// <summary>
/// Helper class for various hash related functions.
/// </summary>
internal static class Hasher
{
    // Includes the commit hash via SourceLink, so it changes on every build.
    // Feeding it into the cache key invalidates assemblies publicized by an
    // older Publicizer whose publicization logic may have differed.
    private static readonly string publicizerVersion =
        typeof(Hasher).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion
        ?? "unknown";

    internal static string ComputeHash(string assemblyPath, PublicizerAssemblyContext assemblyContext)
    {
        var sb = new StringBuilder();
        sb.Append(publicizerVersion);
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
        // Scope order is deliberately significant: equally specific scopes are resolved by item
        // order, so reordering two items really can change what gets publicized.
        foreach (PublicizeScope scope in assemblyContext.Scopes)
        {
            // Delimited, so that Namespace="AB" cannot hash the same as Namespace="A" Type="B".
            _ = sb.Append("|scope|").Append(scope.Deny)
                .Append('|').Append(scope.Namespace)
                .Append('|').Append(scope.TypeReflectionName)
                .Append('|').Append(scope.IncludeVirtualMembers)
                .Append('|').Append(scope.IncludeCompilerGeneratedMembers)
                .Append('|').Append(scope.MemberPattern);
        }

        byte[] patternBytes = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        byte[] allBytes = [.. assemblyBytes, .. patternBytes];

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
