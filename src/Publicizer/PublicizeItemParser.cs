using System.Text;
using Microsoft.Build.Framework;

namespace Publicizer;

/// <summary>
/// Reads one <c>Publicize</c> / <c>DoNotPublicize</c> item into a
/// <see cref="PublicizerAssemblyContext"/>.
/// </summary>
/// <remarks>
/// An item takes either the <em>colon form</em>, which packs the whole target into the item spec, or
/// the <em>structured form</em>, which leaves the item spec as the bare assembly name and moves each
/// qualifier into its own metadata. The two cannot be mixed on one item, and malformed items are
/// reported as build errors rather than thrown, so one bad item does not hide the rest. See
/// docs/publicization-semantics.md.
/// </remarks>
internal sealed class PublicizeItemParser
{
    /// <summary>Qualifiers the structured syntax reserves but does not implement yet.</summary>
    private static readonly string[] unsupportedMetadata = ["Field", "Method", "Property", "Event", "Accessor", "Parameters"];

    /// <summary>Qualifiers that would turn off a scope's descent, which is unconditional today.</summary>
    private static readonly string[] unsupportedDescentMetadata = ["IncludeSubNamespaces", "IncludeTypeContents"];

    /// <summary>Sweep filters, which a <c>DoNotPublicize</c> scope has no sweep to apply them to.</summary>
    private static readonly string[] sweepFilterMetadata = ["IncludeVirtualMembers", "IncludeCompilerGeneratedMembers"];

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

        // Reserved qualifiers are rejected rather than ignored, so a target written against the
        // eventual syntax fails loudly instead of silently sweeping a whole type.
        bool valid = true;
        foreach (string name in unsupportedMetadata)
        {
            if (Metadata(name) is not null)
            {
                Error($"'{name}' metadata is not supported yet. Target members with the '{itemName} Include=\"Assembly:Namespace.Type.Member\"' form.");
                valid = false;
            }
        }

        foreach (string name in unsupportedDescentMetadata)
        {
            if (Metadata(name) is not null)
            {
                Error($"'{name}' metadata is not supported yet. A '{itemName}' scope reaches everything beneath the node it names, and that cannot be narrowed yet.");
                valid = false;
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

        if (deny && HasFiltersOnDenyScope())
        {
            return false;
        }

        string? typeReflectionName = null;
        if (typeValue is not null)
        {
            if (!TryGetTypeReflectionName(typeValue, out string typeName))
            {
                return false;
            }

            typeReflectionName = namespaceName.Length == 0 ? typeName : namespaceName + "." + typeName;
        }

        context.Scopes.Add(new PublicizeScope
        {
            // Only one of the two is kept: a type's reflection name already carries the namespace.
            Namespace = typeReflectionName is null ? namespaceName : "",
            TypeReflectionName = typeReflectionName,
            Deny = deny,
            Display = Describe(namespaceName, typeValue),
            IncludeVirtualMembers = NullableBool("IncludeVirtualMembers"),
            IncludeCompilerGeneratedMembers = NullableBool("IncludeCompilerGeneratedMembers"),
            MemberPattern = item.MemberPattern(),
        });

        logger.Info($"{itemName}: {spec}, namespace: '{namespaceName}', type: {typeReflectionName ?? "(whole namespace)"}");
        return true;
    }

    /// <summary>
    /// Reports every sweep filter found on a <c>DoNotPublicize</c> scope, and whether there was one.
    /// A deny scope has no sweep for a filter to apply to; see docs/publicization-semantics.md for
    /// why neither reading of one is safe to accept.
    /// </summary>
    private bool HasFiltersOnDenyScope()
    {
        bool found = false;

        foreach (string name in sweepFilterMetadata)
        {
            if (Metadata(name) is not null)
            {
                Error($"'{name}' has no meaning on a DoNotPublicize scope, which excludes members rather than sweeping them. Put it on the Publicize item whose sweep you want to filter.");
                found = true;
            }
        }

        if (Metadata("MemberPattern") is not null)
        {
            Error("'MemberPattern' on a DoNotPublicize scope is not supported yet. Exclude individual members with the 'DoNotPublicize Include=\"Assembly:Namespace.Type.Member\"' form.");
            found = true;
        }

        return found;
    }

    /// <summary>
    /// Rewrites a structured type name into the reflection name dnlib reports: <c>.</c> separates
    /// nested types, and <c>{T1,T2}</c> becomes the arity suffix <c>`2</c>. Braces are the only
    /// accepted spelling of arity, and only the number of arguments is read.
    /// </summary>
    private bool TryGetTypeReflectionName(string typeValue, out string reflectionName)
    {
        reflectionName = "";

        if (typeValue.IndexOf('`') >= 0)
        {
            return Fail($"'Type' must not contain a backtick. Write generic arity as 'MyType{{T1,T2}}' rather than 'MyType`2'.");
        }

        if (typeValue.IndexOf('+') >= 0)
        {
            return Fail($"'Type' must not contain '+'. Separate a nested type from its enclosing type with '.', as in 'Outer.Inner'.");
        }

        if (!TrySplitNestingChain(typeValue, out List<string> segments))
        {
            return false;
        }

        var builder = new StringBuilder(typeValue.Length);
        foreach (string segment in segments)
        {
            if (!TryGetSegmentReflectionName(segment, typeValue, out string segmentName))
            {
                return false;
            }

            if (builder.Length > 0)
            {
                _ = builder.Append('+');
            }

            _ = builder.Append(segmentName);
        }

        reflectionName = builder.ToString();
        return true;
    }

    /// <summary>
    /// Splits a type name on the dots that separate nested types, leaving the dots inside a type
    /// argument list alone.
    /// </summary>
    private bool TrySplitNestingChain(string typeValue, out List<string> segments)
    {
        segments = [];

        // Counted rather than a bool: a nested argument list has to survive this scan intact so that
        // the segment scanner can reject it by name, instead of it surfacing as unbalanced braces.
        int depth = 0;
        int segmentStart = 0;

        for (int i = 0; i < typeValue.Length; i++)
        {
            char c = typeValue[i];
            if (c == '{')
            {
                depth++;
            }
            else if (c == '}')
            {
                depth--;
                if (depth < 0)
                {
                    return Fail($"'Type' has unbalanced braces: '{typeValue}'.");
                }
            }
            else if (c == '.' && depth == 0)
            {
                segments.Add(typeValue.Substring(segmentStart, i - segmentStart));
                segmentStart = i + 1;
            }
        }

        if (depth != 0)
        {
            return Fail($"'Type' has unbalanced braces: '{typeValue}'.");
        }

        segments.Add(typeValue.Substring(segmentStart));
        return true;
    }

    private bool TryGetSegmentReflectionName(string segment, string typeValue, out string segmentName)
    {
        segmentName = "";

        int brace = segment.IndexOf('{');
        string name = brace < 0 ? segment : segment.Substring(0, brace);

        if (name.Length == 0)
        {
            return Fail($"'Type' has an empty name segment: '{typeValue}'.");
        }

        if (brace < 0)
        {
            segmentName = name;
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

        // Only the count is read today, but the names are reserved for 'Parameters'. Accepting a
        // nameless argument now would freeze a spelling that has to mean something once they are.
        string[] typeArguments = arguments.Split(',');
        if (Array.Exists(typeArguments, argument => argument.Trim().Length == 0))
        {
            return Fail($"'Type' segment '{segment}' has an empty type argument name. Name every argument, as in 'MyType{{T1,T2}}'.");
        }

        segmentName = name + "`" + typeArguments.Length;
        return true;
    }

    /// <summary>The scope as the author wrote it, so a diagnostic quotes their spelling rather than the lowered name.</summary>
    private static string Describe(string namespaceName, string? typeValue)
    {
        if (typeValue is null)
        {
            return $"Namespace=\"{namespaceName}\"";
        }

        return namespaceName.Length == 0 ? $"Type=\"{typeValue}\"" : $"Namespace=\"{namespaceName}\" Type=\"{typeValue}\"";
    }

    private void Error(string message) => logger.Error($"{itemName} item '{spec}': {message}");

    /// <summary>Reports one rejection and returns <see langword="false"/>, so call sites can return it directly.</summary>
    private bool Fail(string message)
    {
        Error(message);
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
