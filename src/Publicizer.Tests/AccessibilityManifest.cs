using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using dnlib.DotNet;

namespace Publicizer.Tests;

/// <summary>
/// Renders a module's types, fields, and methods with their resulting access
/// levels as a deterministic, sorted text block. This is the assertion
/// vocabulary for the characterization tests: publicization changes access
/// bits, and a manifest diff makes any change visible. Property publicization
/// shows up through the accessor methods (get_/set_), so properties are not
/// listed separately.
/// </summary>
internal static class AccessibilityManifest
{
    internal static string Of(ModuleDef module)
    {
        var builder = new StringBuilder();

        IEnumerable<TypeDef> types = module.GetTypes()
            .Where(type => type.Name != "<Module>")
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (TypeDef type in types)
        {
            builder.Append("TYPE ").Append(type.FullName).Append(" : ").AppendLine(Access(type));

            foreach (FieldDef field in type.Fields.OrderBy(f => f.Name.String, StringComparer.Ordinal))
            {
                builder.Append("  FIELD ").Append(field.Name).Append(" : ").AppendLine(Access(field));
            }

            foreach (MethodDef method in type.Methods.OrderBy(m => m.Name.String, StringComparer.Ordinal))
            {
                builder.Append("  METHOD ").Append(method.Name).Append(" : ").AppendLine(Access(method));
            }
        }

        return builder.ToString();
    }

    private static string Access(TypeDef type)
    {
        if (!type.IsNested)
        {
            return type.IsPublic ? "Public" : "NotPublic";
        }

        if (type.IsNestedPublic) return "NestedPublic";
        if (type.IsNestedPrivate) return "NestedPrivate";
        if (type.IsNestedFamily) return "NestedFamily";
        if (type.IsNestedAssembly) return "NestedAssembly";
        if (type.IsNestedFamilyOrAssembly) return "NestedFamilyOrAssembly";
        if (type.IsNestedFamilyAndAssembly) return "NestedFamilyAndAssembly";
        return "NestedUnknown";
    }

    private static string Access(FieldDef field)
    {
        if (field.IsPublic) return "Public";
        if (field.IsPrivate) return "Private";
        if (field.IsFamily) return "Family";
        if (field.IsAssembly) return "Assembly";
        if (field.IsFamilyOrAssembly) return "FamilyOrAssembly";
        if (field.IsFamilyAndAssembly) return "FamilyAndAssembly";
        return "CompilerControlled";
    }

    private static string Access(MethodDef method)
    {
        if (method.IsPublic) return "Public";
        if (method.IsPrivate) return "Private";
        if (method.IsFamily) return "Family";
        if (method.IsAssembly) return "Assembly";
        if (method.IsFamilyOrAssembly) return "FamilyOrAssembly";
        if (method.IsFamilyAndAssembly) return "FamilyAndAssembly";
        return "CompilerControlled";
    }
}
