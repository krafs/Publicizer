using System.Globalization;
using System.Text;
using Microsoft.Build.Framework;

namespace Publicizer;

/// <summary>
/// Reads one <c>Publicize</c> / <c>DoNotPublicize</c> item into a
/// <see cref="PublicizerAssemblyContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// An item takes one of two forms. The <em>colon form</em> packs the whole target into the item spec
/// — <c>Assembly:Namespace.Type.Member</c> — and is the long-standing public contract. The
/// <em>structured form</em> leaves the item spec as the bare assembly name and moves each qualifier
/// into its own metadata, which is what lets a namespace be told apart from a nested type.
/// </para>
/// <para>
/// The two cannot be mixed on one item: there is no sensible reading of a colon spec that also
/// carries <c>Type</c> metadata, and honouring one while dropping the other would publicize
/// something the author did not ask for. Malformed items are reported as build errors rather than
/// thrown, so that one bad item does not hide the rest.
/// </para>
/// </remarks>
internal sealed class PublicizeItemParser
{
    /// <summary>
    /// Qualifiers the structured syntax reserves but does not implement yet. Rejected rather than
    /// ignored so that a target written against the eventual syntax fails loudly instead of silently
    /// publicizing a whole type.
    /// </summary>
    private static readonly string[] unsupportedMetadata = ["Field", "Method", "Property", "Event", "Accessor", "Parameters"];

    /// <summary>
    /// Qualifiers that turn off a scope's descent. Reserved for the same reason, and with more at
    /// stake: a scope is recursive unconditionally today, so ignoring an author's request to narrow
    /// it publicizes strictly more than they asked for.
    /// </summary>
    private static readonly string[] unsupportedDescentMetadata = ["IncludeSubNamespaces", "IncludeTypeContents"];

    private readonly ITaskItem item;
    private readonly ITaskLogger logger;
    private readonly bool deny;
    private readonly string spec;
    private readonly string itemName;
    private readonly int colon;

    private PublicizeItemParser(ITaskItem item, bool deny, ITaskLogger logger)
    {
        this.item = item;
        this.deny = deny;
        this.logger = logger;
        spec = item.ItemSpec;
        itemName = deny ? "DoNotPublicize" : "Publicize";
        colon = spec.IndexOf(':');
    }

    /// <summary>
    /// Applies one item to the context for the assembly it names, creating that context if this is
    /// the first item to mention the assembly.
    /// </summary>
    internal static bool TryApply(ITaskItem item, bool deny, Dictionary<string, PublicizerAssemblyContext> contexts, ITaskLogger logger)
    {
        var parser = new PublicizeItemParser(item, deny, logger);

        string assemblyName = parser.colon < 0 ? parser.spec : parser.spec.Substring(0, parser.colon);
        if (!contexts.TryGetValue(assemblyName, out PublicizerAssemblyContext? context))
        {
            context = new PublicizerAssemblyContext(assemblyName);
            contexts.Add(assemblyName, context);
        }

        return parser.TryApply(context);
    }

    private bool TryApply(PublicizerAssemblyContext context)
    {
        string? namespaceValue = Metadata("Namespace");
        string? typeValue = Metadata("Type");
        bool isStructured = namespaceValue is not null || typeValue is not null;

        if (colon >= 0 && isStructured)
        {
            return Fail($"the '{itemName} Include=\"Assembly:Member\"' form cannot be combined with 'Namespace' or 'Type' metadata. Use one form or the other.");
        }

        bool valid = true;
        foreach (string name in unsupportedMetadata)
        {
            if (Metadata(name) is not null)
            {
                valid = Fail($"'{name}' metadata is not supported yet. Target members with the '{itemName} Include=\"Assembly:Namespace.Type.Member\"' form.");
            }
        }

        foreach (string name in unsupportedDescentMetadata)
        {
            if (Metadata(name) is not null)
            {
                valid = Fail($"'{name}' metadata is not supported yet. A '{itemName}' scope reaches everything beneath the node it names, and that cannot be narrowed yet.");
            }
        }

        if (!valid)
        {
            return false;
        }

        if (colon >= 0)
        {
            // Everything after the first colon is the member name, further colons included.
            string memberPattern = spec.Substring(colon + 1);
            HashSet<string> patterns = deny ? context.DoNotPublicizeMemberPatterns : context.PublicizeMemberPatterns;
            _ = patterns.Add(memberPattern);
            logger.Info($"{itemName}: {item}");
            return true;
        }

        return isStructured
            ? TryApplyScope(context, namespaceValue, typeValue)
            : ApplyAssemblyForm(context);
    }

    private bool ApplyAssemblyForm(PublicizerAssemblyContext context)
    {
        if (deny)
        {
            context.ExplicitlyDoNotPublicizeAssembly = true;
            logger.Info($"{itemName}: {item}");
            return true;
        }

        // Assigned unconditionally, so a later item resets what an earlier one set. Long-standing
        // last-wins behavior; see docs/publicization-semantics.md.
        context.IncludeCompilerGeneratedMembers = item.IncludeCompilerGeneratedMembers();
        context.IncludeVirtualMembers = item.IncludeVirtualMembers();
        context.ExplicitlyPublicizeAssembly = true;
        context.PublicizeMemberRegexPattern = item.MemberPattern();
        logger.Info($"Publicize: {item}, virtual members: {context.IncludeVirtualMembers}, compiler-generated members: {context.IncludeCompilerGeneratedMembers}, member pattern: {context.PublicizeMemberRegexPattern}");
        return true;
    }

    private bool TryApplyScope(PublicizerAssemblyContext context, string? namespaceValue, string? typeValue)
    {
        string namespaceName = namespaceValue ?? "";

        if (namespaceName.IndexOfAny(['`', '{', '}', '+']) >= 0)
        {
            return Fail($"'Namespace' must be a plain dotted namespace name, but was '{namespaceName}'.");
        }

        // Same segment rule 'Type' is held to: a dot separates two names, so neither side may be empty.
        if (namespaceName.Length > 0 && Array.Exists(namespaceName.Split('.'), segment => segment.Length == 0))
        {
            return Fail($"'Namespace' has an empty name segment: '{namespaceName}'.");
        }

        if (deny && !TryRejectFiltersOnDenyScope())
        {
            return false;
        }

        string? typeReflectionName = null;
        if (typeValue is not null)
        {
            if (!TryLowerTypeName(typeValue, out string loweredTypeName))
            {
                return false;
            }

            typeReflectionName = namespaceName.Length == 0 ? loweredTypeName : namespaceName + "." + loweredTypeName;
        }

        context.Scopes.Add(new PublicizeScope
        {
            Namespace = namespaceName,
            TypeReflectionName = typeReflectionName,
            Deny = deny,
            IncludeVirtualMembers = NullableBool("IncludeVirtualMembers"),
            IncludeCompilerGeneratedMembers = NullableBool("IncludeCompilerGeneratedMembers"),
            MemberPattern = item.MemberPattern(),
        });

        logger.Info($"{itemName}: {spec}, namespace: '{namespaceName}', type: {typeReflectionName ?? "(whole namespace)"}");
        return true;
    }

    /// <summary>
    /// Rejects the sweep filters on a <c>DoNotPublicize</c> scope, which has no sweep for them to
    /// filter.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two booleans have no defensible reading here: <c>IncludeVirtualMembers="false"</c> on a
    /// deny scope would have to mean "do not deny the virtual members", and a user who misreads that
    /// double negative publicizes more than they meant to.
    /// </para>
    /// <para>
    /// <c>MemberPattern</c> does have a coherent reading — deny only the members it matches — but
    /// that turns a scope from all-or-nothing for a type into a per-member rule, which the
    /// single-winner resolution in <see cref="AssemblyPlan"/> cannot express. Rejected as
    /// not-yet-supported rather than as nonsense, since it is a capability worth adding.
    /// </para>
    /// </remarks>
    private bool TryRejectFiltersOnDenyScope()
    {
        bool valid = true;

        foreach (string name in new[] { "IncludeVirtualMembers", "IncludeCompilerGeneratedMembers" })
        {
            if (Metadata(name) is not null)
            {
                valid = Fail($"'{name}' has no meaning on a DoNotPublicize scope, which excludes members rather than sweeping them. Put it on the Publicize item whose sweep you want to filter.");
            }
        }

        if (Metadata("MemberPattern") is not null)
        {
            valid = Fail("'MemberPattern' on a DoNotPublicize scope is not supported yet. Exclude individual members with the 'DoNotPublicize Include=\"Assembly:Namespace.Type.Member\"' form.");
        }

        return valid;
    }

    /// <summary>
    /// Rewrites a structured type name into the reflection name dnlib reports: <c>.</c> separates
    /// nested types, and <c>{T1,T2}</c> becomes the arity suffix <c>`2</c>.
    /// </summary>
    /// <remarks>
    /// Braces are the only accepted spelling of generic arity. A backtick is rejected rather than
    /// passed through, so that <c>Parameters</c> never has to reconcile two spellings of the same
    /// type once overload targeting lands. Only the number of arguments is read here; the names
    /// inside the braces mean nothing until then.
    /// </remarks>
    private bool TryLowerTypeName(string typeValue, out string loweredTypeName)
    {
        loweredTypeName = "";

        if (typeValue.IndexOf('`') >= 0)
        {
            return Fail($"'Type' must not contain a backtick. Write generic arity as 'MyType{{T1,T2}}' rather than 'MyType`2'.");
        }

        if (typeValue.IndexOf('+') >= 0)
        {
            return Fail($"'Type' must not contain '+'. Separate a nested type from its enclosing type with '.', as in 'Outer.Inner'.");
        }

        var lowered = new StringBuilder(typeValue.Length);
        // Counted rather than a bool: a nested argument list has to survive this scan intact so that
        // the segment scanner can reject it by name, instead of it surfacing as unbalanced braces.
        int depth = 0;
        int segmentStart = 0;

        for (int i = 0; i <= typeValue.Length; i++)
        {
            if (i < typeValue.Length)
            {
                char c = typeValue[i];
                if (c == '{')
                {
                    depth++;
                    continue;
                }

                if (c == '}')
                {
                    depth--;
                    if (depth < 0)
                    {
                        return Fail($"'Type' has unbalanced braces: '{typeValue}'.");
                    }

                    continue;
                }

                // A dot inside braces belongs to a type argument, not to the nesting chain.
                if (c != '.' || depth > 0)
                {
                    continue;
                }
            }
            else if (depth != 0)
            {
                return Fail($"'Type' has unbalanced braces: '{typeValue}'.");
            }

            if (!TryLowerSegment(typeValue.Substring(segmentStart, i - segmentStart), typeValue, out string segment))
            {
                return false;
            }

            if (lowered.Length > 0)
            {
                _ = lowered.Append('+');
            }
            _ = lowered.Append(segment);
            segmentStart = i + 1;
        }

        loweredTypeName = lowered.ToString();
        return true;
    }

    private bool TryLowerSegment(string segment, string typeValue, out string loweredSegment)
    {
        loweredSegment = "";

        int brace = segment.IndexOf('{');
        string name = brace < 0 ? segment : segment.Substring(0, brace);

        if (name.Length == 0)
        {
            return Fail($"'Type' has an empty name segment: '{typeValue}'.");
        }

        if (brace < 0)
        {
            loweredSegment = name;
            return true;
        }

        if (segment[segment.Length - 1] != '}')
        {
            return Fail($"'Type' segment '{segment}' must end its type argument list with '}}'.");
        }

        string arguments = segment.Substring(brace + 1, segment.Length - brace - 2);
        if (arguments.Trim().Length == 0)
        {
            return Fail($"'Type' segment '{segment}' has an empty type argument list. Drop the braces for a non-generic type.");
        }

        // A nested argument list would make the commas ambiguous — 'Holder{Dictionary{K,V}}' has one
        // argument, not two — and nothing reads the names yet anyway, so refuse rather than guess.
        if (arguments.IndexOf('{') >= 0)
        {
            return Fail($"'Type' segment '{segment}' has a nested type argument list, which is not supported. Only the number of type arguments is read, so write 'MyType{{T1,T2}}'.");
        }

        // Only the count matters until Parameters lands; the argument names are not resolved yet.
        int arity = 1;
        foreach (char c in arguments)
        {
            if (c == ',')
            {
                arity++;
            }
        }

        loweredSegment = name + "`" + arity.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    /// <summary>Logs one rejection and returns <see langword="false"/>, so call sites can return it directly.</summary>
    private bool Fail(string message)
    {
        logger.Error($"{itemName} item '{spec}': {message}");
        return false;
    }

    private string? Metadata(string name)
    {
        string value = item.GetMetadata(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private bool? NullableBool(string name)
    {
        string? value = Metadata(name);
        if (value is null)
        {
            return null;
        }

        // Unparseable metadata means true, matching the colon form's long-standing leniency.
        return !bool.TryParse(value, out bool parsed) || parsed;
    }
}
