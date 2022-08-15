using System.Collections.Generic;

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
    internal bool ExplicitlyDoNotPublicizeAssembly { get; set; } = false;
    internal HashSet<string> PublicizeMemberPatterns { get; } = new HashSet<string>();
    internal HashSet<string> DoNotPublicizeMemberPatterns { get; } = new HashSet<string>();
}
