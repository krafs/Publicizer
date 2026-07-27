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
internal static class PublicizeItemParser
{
    /// <summary>
    /// Qualifiers the structured syntax reserves but does not implement yet. Rejected rather than
    /// ignored so that a target written against the eventual syntax fails loudly instead of silently
    /// publicizing a whole type.
    /// </summary>
    private static readonly string[] unsupportedMetadata = ["Field", "Method", "Property", "Event", "Accessor", "Parameters"];

    internal static string AssemblyNameOf(ITaskItem item)
    {
        string spec = item.ItemSpec;
        int colon = spec.IndexOf(':');
        return colon < 0 ? spec : spec.Substring(0, colon);
    }

    internal static bool TryApply(ITaskItem item, bool deny, PublicizerAssemblyContext context, ITaskLogger logger)
    {
        string spec = item.ItemSpec;
        string itemName = deny ? "DoNotPublicize" : "Publicize";
        int colon = spec.IndexOf(':');

        string? namespaceValue = Metadata(item, "Namespace");
        string? typeValue = Metadata(item, "Type");
        bool isStructured = namespaceValue is not null || typeValue is not null;

        if (colon >= 0 && isStructured)
        {
            logger.Error($"{itemName} item '{spec}': the '{itemName} Include=\"Assembly:Member\"' form cannot be combined with 'Namespace' or 'Type' metadata. Use one form or the other.");
            return false;
        }

        bool valid = true;
        foreach (string name in unsupportedMetadata)
        {
            if (Metadata(item, name) is not null)
            {
                logger.Error($"{itemName} item '{spec}': '{name}' metadata is not supported yet. Target members with the '{itemName} Include=\"Assembly:Namespace.Type.Member\"' form.");
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

        if (!isStructured)
        {
            return ApplyAssemblyForm(item, deny, context, logger, itemName);
        }

        return TryApplyScope(item, deny, context, logger, itemName, namespaceValue, typeValue);
    }

    private static bool ApplyAssemblyForm(ITaskItem item, bool deny, PublicizerAssemblyContext context, ITaskLogger logger, string itemName)
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

    private static bool TryApplyScope(
        ITaskItem item,
        bool deny,
        PublicizerAssemblyContext context,
        ITaskLogger logger,
        string itemName,
        string? namespaceValue,
        string? typeValue)
    {
        string spec = item.ItemSpec;
        string namespaceName = namespaceValue ?? "";

        if (namespaceName.IndexOfAny(['`', '{', '}', '+']) >= 0)
        {
            logger.Error($"{itemName} item '{spec}': 'Namespace' must be a plain dotted namespace name, but was '{namespaceName}'.");
            return false;
        }

        // Same segment rule 'Type' is held to: a dot separates two names, so neither side may be empty.
        if (namespaceName.Length > 0 && Array.Exists(namespaceName.Split('.'), segment => segment.Length == 0))
        {
            logger.Error($"{itemName} item '{spec}': 'Namespace' has an empty name segment: '{namespaceName}'.");
            return false;
        }

        if (deny && !TryRejectFiltersOnDenyScope(item, spec, logger, itemName))
        {
            return false;
        }

        string? typeReflectionName = null;
        if (typeValue is not null)
        {
            if (!TryLowerTypeName(typeValue, itemName, spec, logger, out string loweredTypeName))
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
            IncludeVirtualMembers = NullableBool(item, "IncludeVirtualMembers"),
            IncludeCompilerGeneratedMembers = NullableBool(item, "IncludeCompilerGeneratedMembers"),
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
    private static bool TryRejectFiltersOnDenyScope(ITaskItem item, string spec, ITaskLogger logger, string itemName)
    {
        bool valid = true;

        foreach (string name in new[] { "IncludeVirtualMembers", "IncludeCompilerGeneratedMembers" })
        {
            if (Metadata(item, name) is not null)
            {
                logger.Error($"{itemName} item '{spec}': '{name}' has no meaning on a DoNotPublicize scope, which excludes members rather than sweeping them. Put it on the Publicize item whose sweep you want to filter.");
                valid = false;
            }
        }

        if (Metadata(item, "MemberPattern") is not null)
        {
            logger.Error($"{itemName} item '{spec}': 'MemberPattern' on a DoNotPublicize scope is not supported yet. Exclude individual members with the 'DoNotPublicize Include=\"Assembly:Namespace.Type.Member\"' form.");
            valid = false;
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
    private static bool TryLowerTypeName(string typeValue, string itemName, string spec, ITaskLogger logger, out string loweredTypeName)
    {
        loweredTypeName = "";

        if (typeValue.IndexOf('`') >= 0)
        {
            logger.Error($"{itemName} item '{spec}': 'Type' must not contain a backtick. Write generic arity as 'MyType{{T1,T2}}' rather than 'MyType`2'.");
            return false;
        }

        if (typeValue.IndexOf('+') >= 0)
        {
            logger.Error($"{itemName} item '{spec}': 'Type' must not contain '+'. Separate a nested type from its enclosing type with '.', as in 'Outer.Inner'.");
            return false;
        }

        var lowered = new StringBuilder(typeValue.Length);
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
                        logger.Error($"{itemName} item '{spec}': 'Type' has unbalanced braces: '{typeValue}'.");
                        return false;
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
                logger.Error($"{itemName} item '{spec}': 'Type' has unbalanced braces: '{typeValue}'.");
                return false;
            }

            if (!TryLowerSegment(typeValue.Substring(segmentStart, i - segmentStart), itemName, spec, typeValue, logger, out string segment))
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

    private static bool TryLowerSegment(string segment, string itemName, string spec, string typeValue, ITaskLogger logger, out string loweredSegment)
    {
        loweredSegment = "";

        int brace = segment.IndexOf('{');
        string name = brace < 0 ? segment : segment.Substring(0, brace);

        if (name.Length == 0)
        {
            logger.Error($"{itemName} item '{spec}': 'Type' has an empty name segment: '{typeValue}'.");
            return false;
        }

        if (brace < 0)
        {
            loweredSegment = name;
            return true;
        }

        if (segment[segment.Length - 1] != '}')
        {
            logger.Error($"{itemName} item '{spec}': 'Type' segment '{segment}' must end its type argument list with '}}'.");
            return false;
        }

        string arguments = segment.Substring(brace + 1, segment.Length - brace - 2);
        if (arguments.Trim().Length == 0)
        {
            logger.Error($"{itemName} item '{spec}': 'Type' segment '{segment}' has an empty type argument list. Drop the braces for a non-generic type.");
            return false;
        }

        // A nested argument list would make the commas ambiguous — 'Holder{Dictionary{K,V}}' has one
        // argument, not two — and nothing reads the names yet anyway, so refuse rather than guess.
        if (arguments.IndexOf('{') >= 0)
        {
            logger.Error($"{itemName} item '{spec}': 'Type' segment '{segment}' has a nested type argument list, which is not supported. Only the number of type arguments is read, so write 'MyType{{T1,T2}}'.");
            return false;
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

    private static string? Metadata(ITaskItem item, string name)
    {
        string value = item.GetMetadata(name);
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static bool? NullableBool(ITaskItem item, string name)
    {
        string? value = Metadata(item, name);
        if (value is null)
        {
            return null;
        }

        // Unparseable metadata means true, matching the colon form's long-standing leniency.
        return !bool.TryParse(value, out bool parsed) || parsed;
    }
}
