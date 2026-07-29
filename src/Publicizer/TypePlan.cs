namespace Publicizer;

/// <summary>
/// The publicization rules that can apply inside one type, with all per-type work already hoisted
/// out of the member loops. Holds the single copy of the decision ladder that
/// <see cref="PublicizeAssemblies.PublicizeAssembly"/> used to repeat once per member kind.
/// </summary>
internal sealed class TypePlan
{
    private readonly SweepSettings settings;
    private readonly string typeReflectionFullName;
    private readonly HashSet<string>? allowedMembers;
    private readonly HashSet<string>? deniedMembers;
    private readonly bool allowedAsType;
    private readonly bool deniedAsType;

    internal TypePlan(
        SweepSettings settings,
        string typeReflectionFullName,
        HashSet<string>? allowedMembers,
        HashSet<string>? deniedMembers,
        bool allowedAsType,
        bool deniedAsType)
    {
        this.settings = settings;
        this.typeReflectionFullName = typeReflectionFullName;
        this.allowedMembers = allowedMembers;
        this.deniedMembers = deniedMembers;
        this.allowedAsType = allowedAsType;
        this.deniedAsType = deniedAsType;
    }

    internal bool NeedsCompilerGeneratedCheck => settings.NeedsCompilerGeneratedCheck;

    internal bool IncludeVirtualMembers => settings.IncludeVirtualMembers;

    /// <summary>
    /// The name a diagnostic or the <c>MemberPattern</c> regex needs. Built on demand only, since
    /// deciding a member no longer requires it.
    /// </summary>
    internal string FullNameOf(string memberName) => typeReflectionFullName + "." + memberName;

    /// <summary>
    /// Answers for every member at once when no rule depends on the member, letting the walk skip
    /// name lookups entirely. This is the common whole-assembly case.
    /// </summary>
    internal bool TryDecideAllMembers(out PublicizeDecision decision)
    {
        decision = PublicizeDecision.Skip;

        if (allowedMembers is not null || deniedMembers is not null)
        {
            return false;
        }

        if (deniedAsType || !settings.Publicize)
        {
            return true;
        }

        if (settings.MemberPattern is not null || settings.NeedsCompilerGeneratedCheck)
        {
            return false;
        }

        decision = PublicizeDecision.BySweep;
        return true;
    }

    internal PublicizeDecision DecideMember(string memberName, bool isCompilerGenerated)
    {
        if (deniedMembers is not null && deniedMembers.Contains(memberName))
        {
            return PublicizeDecision.DeniedExplicitly;
        }

        if (allowedMembers is not null && allowedMembers.Contains(memberName))
        {
            return PublicizeDecision.Explicit;
        }

        if (deniedAsType)
        {
            return PublicizeDecision.Skip;
        }

        return DecideBySweep(memberName, isCompilerGenerated);
    }

    internal PublicizeDecision DecideType(bool isCompilerGenerated)
    {
        if (deniedAsType)
        {
            return PublicizeDecision.DeniedExplicitly;
        }

        if (allowedAsType)
        {
            return PublicizeDecision.Explicit;
        }

        return DecideBySweep(memberName: null, isCompilerGenerated);
    }

    /// <summary>
    /// The sweep in force here — assembly-wide, or narrowed by a namespace or type scope — and its
    /// filters. <paramref name="memberName"/> is <see langword="null"/> when deciding the type itself.
    /// </summary>
    private PublicizeDecision DecideBySweep(string? memberName, bool isCompilerGenerated)
    {
        if (!settings.Publicize)
        {
            return PublicizeDecision.Skip;
        }

        if (isCompilerGenerated && settings.NeedsCompilerGeneratedCheck)
        {
            return PublicizeDecision.Skip;
        }

        if (settings.MemberPattern is not null)
        {
            // The only place the flat name is still needed, and only when a regex was configured.
            string matchedName = memberName is null ? typeReflectionFullName : FullNameOf(memberName);
            if (!settings.MemberPattern.IsMatch(matchedName))
            {
                return PublicizeDecision.Skip;
            }
        }

        return PublicizeDecision.BySweep;
    }
}
