using System.Text.RegularExpressions;

namespace Publicizer;

internal sealed class PublicizerAssemblyContext
{
    internal PublicizerAssemblyContext(string assemblyName)
    {
        AssemblyName = assemblyName;
    }

    internal string AssemblyName { get; }
    internal bool ExplicitlyPublicizeAssembly { get; set; } = false;
    internal bool IncludeCompilerGeneratedMembers { get; set; } = true;
    internal bool IncludeVirtualMembers { get; set; } = true;
    internal bool ExplicitlyDoNotPublicizeAssembly { get; set; } = false;
    internal HashSet<string> PublicizeMemberPatterns { get; } = [];
    internal Regex? PublicizeMemberRegexPattern { get; set; }
    internal HashSet<string> DoNotPublicizeMemberPatterns { get; } = [];

    /// <summary>
    /// Namespace and type scopes from the structured item form. The assembly-wide scope is not one
    /// of these — it stays in the fields above, where its last-wins metadata semantics live.
    /// </summary>
    internal List<PublicizeScope> Scopes { get; } = [];
}
