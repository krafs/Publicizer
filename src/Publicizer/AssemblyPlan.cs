namespace Publicizer;

/// <summary>
/// A <see cref="PublicizerAssemblyContext"/> compiled into the structures the member walk needs.
/// </summary>
/// <remarks>
/// <para>
/// The colon-string item form stores targets as flat dotted strings that are compared against
/// <c>{TypeReflectionFullName}.{MemberName}</c>. Rebuilding that concatenation for every member of
/// every type is the dominant cost of matching, so instead each target is decomposed once, up front,
/// into the (type name, member name) pairs it could denote. The member walk then does a single
/// dictionary lookup per type and plain lookups per member, never concatenating.
/// </para>
/// <para>
/// A target is ambiguous by construction: <c>A.B.C</c> may name a type, or member <c>C</c> of type
/// <c>A.B</c>, and the syntax gives no way to tell. Rather than guess, a target is indexed under
/// <em>every</em> split point, plus as a type name in its own right. That is exactly equivalent to
/// the string comparison it replaces — including the doubled dot of <c>Fixture.Shapes..ctor</c>,
/// which a naive split at the last dot would get wrong. Targets are few and user-authored, so the
/// extra entries cost nothing. The structured item form exists precisely to avoid this guesswork,
/// and its targets arrive here already unambiguous, as <see cref="PublicizeScope"/>s.
/// </para>
/// </remarks>
internal sealed class AssemblyPlan
{
    private readonly Dictionary<string, HashSet<string>> allowedMembersByType;
    private readonly Dictionary<string, HashSet<string>> deniedMembersByType;
    private readonly HashSet<string> allowedTypeNames;
    private readonly HashSet<string> deniedTypeNames;
    private readonly List<PublicizeScope> scopes;
    private readonly SweepSettings[] scopeSettings;
    private readonly SweepSettings assemblySettings;

    private AssemblyPlan(
        Dictionary<string, HashSet<string>> allowedMembersByType,
        Dictionary<string, HashSet<string>> deniedMembersByType,
        HashSet<string> allowedTypeNames,
        HashSet<string> deniedTypeNames,
        List<PublicizeScope> scopes,
        SweepSettings assemblySettings,
        SweepSettings[] scopeSettings)
    {
        this.allowedMembersByType = allowedMembersByType;
        this.deniedMembersByType = deniedMembersByType;
        this.allowedTypeNames = allowedTypeNames;
        this.deniedTypeNames = deniedTypeNames;
        this.scopes = scopes;
        this.assemblySettings = assemblySettings;
        this.scopeSettings = scopeSettings;
    }

    internal static AssemblyPlan Compile(PublicizerAssemblyContext context)
    {
        var allowedMembersByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var deniedMembersByType = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        foreach (string target in context.PublicizeMemberPatterns)
        {
            IndexMemberSplits(target, allowedMembersByType);
        }

        foreach (string target in context.DoNotPublicizeMemberPatterns)
        {
            IndexMemberSplits(target, deniedMembersByType);
        }

        var allowedTypeNames = new HashSet<string>(context.PublicizeMemberPatterns, StringComparer.Ordinal);
        var deniedTypeNames = new HashSet<string>(context.DoNotPublicizeMemberPatterns, StringComparer.Ordinal);

        var assemblySettings = new SweepSettings
        {
            Publicize = context.ExplicitlyPublicizeAssembly && !context.ExplicitlyDoNotPublicizeAssembly,
            IncludeVirtualMembers = context.IncludeVirtualMembers,
            IncludeCompilerGeneratedMembers = context.IncludeCompilerGeneratedMembers,
            MemberPattern = context.PublicizeMemberRegexPattern,
        };

        // An assembly-wide DoNotPublicize vetoes every scope, the same precedence a colon-form
        // type-level deny gets. Dropping the scopes here is what enforces it: Resolve then falls
        // through to assemblySettings, which already says Publicize = false.
        List<PublicizeScope> scopes = context.ExplicitlyDoNotPublicizeAssembly ? [] : context.Scopes;

        // A scope's settings depend only on the scope, so they are resolved once here rather than
        // once per type the scope covers.
        var scopeSettings = new SweepSettings[scopes.Count];
        for (int i = 0; i < scopes.Count; i++)
        {
            scopeSettings[i] = assemblySettings.NarrowedBy(scopes[i]);
        }

        return new AssemblyPlan(allowedMembersByType, deniedMembersByType, allowedTypeNames, deniedTypeNames, scopes, assemblySettings, scopeSettings);
    }

    private static void IndexMemberSplits(string target, Dictionary<string, HashSet<string>> index)
    {
        for (int i = target.IndexOf('.'); i >= 0; i = target.IndexOf('.', i + 1))
        {
            string typeName = target.Substring(0, i);
            string memberName = target.Substring(i + 1);

            if (!index.TryGetValue(typeName, out HashSet<string>? memberNames))
            {
                memberNames = new HashSet<string>(StringComparer.Ordinal);
                index.Add(typeName, memberNames);
            }

            memberNames.Add(memberName);
        }
    }

    /// <summary>
    /// Whether a <c>DoNotPublicize</c> target names this type. Only consulted for the enclosing
    /// types the walk-up would otherwise publicize; deciding a type the walk reached on its own
    /// goes through <see cref="ForType"/>.
    /// </summary>
    internal bool IsDeniedType(string typeReflectionFullName) => deniedTypeNames.Contains(typeReflectionFullName);

    /// <summary>
    /// Returns the rules that can apply inside <paramref name="typeReflectionFullName"/>, or
    /// <see langword="null"/> when nothing in the type is reachable by any rule and the whole type
    /// can be skipped without inspecting a single member.
    /// </summary>
    /// <param name="typeReflectionFullName">dnlib's <c>ReflectionFullName</c> for the type.</param>
    /// <param name="typeNamespace">The namespace of the outermost enclosing type, which is the
    /// namespace a nested type belongs to.</param>
    internal TypePlan? ForType(string typeReflectionFullName, string typeNamespace)
    {
        _ = allowedMembersByType.TryGetValue(typeReflectionFullName, out HashSet<string>? allowedMembers);
        _ = deniedMembersByType.TryGetValue(typeReflectionFullName, out HashSet<string>? deniedMembers);
        bool allowedAsType = allowedTypeNames.Contains(typeReflectionFullName);
        bool deniedAsType = deniedTypeNames.Contains(typeReflectionFullName);

        SweepSettings settings = Resolve(typeReflectionFullName, typeNamespace);

        bool hasNamedTarget = allowedMembers is not null || deniedMembers is not null || allowedAsType || deniedAsType;
        if (!hasNamedTarget && !settings.Publicize)
        {
            return null;
        }

        return new TypePlan(settings, typeReflectionFullName, allowedMembers, deniedMembers, allowedAsType, deniedAsType);
    }

    /// <summary>
    /// The settings of the tightest scope covering this type, or the assembly-wide settings when no
    /// scope does. Scopes are few and user-authored, so a linear scan costs less than any index would.
    /// </summary>
    private SweepSettings Resolve(string typeReflectionFullName, string typeNamespace)
    {
        int winner = -1;
        for (int i = 0; i < scopes.Count; i++)
        {
            if (scopes[i].Covers(typeReflectionFullName, typeNamespace) && (winner < 0 || Beats(scopes[i], scopes[winner])))
            {
                winner = i;
            }
        }

        return winner < 0 ? assemblySettings : scopeSettings[winner];
    }

    /// <summary>
    /// A tighter scope always wins. Between equally tight scopes, <c>DoNotPublicize</c> wins over
    /// <c>Publicize</c> — matching the colon form, where naming a member in both excludes it — and
    /// otherwise the later item wins.
    /// </summary>
    private static bool Beats(PublicizeScope candidate, PublicizeScope current)
    {
        int comparison = candidate.Specificity.CompareTo(current.Specificity);
        if (comparison != 0)
        {
            return comparison > 0;
        }

        if (candidate.Deny != current.Deny)
        {
            return candidate.Deny;
        }

        // Equally tight and on the same side: the later item wins.
        return true;
    }
}
