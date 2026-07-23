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

        IEnumerable<TypeDef> types = module.GetTypes().Where(type => type.Name != "<Module>").OrderBy(type => type.FullName, StringComparer.Ordinal);

        foreach (TypeDef type in types)
        {
            builder
                .Append("TYPE ")
                .Append(type.FullName)
                .Append(" : ")
                .AppendLine(Access(type));

            foreach (FieldDef field in type.Fields.OrderBy(f => f.Name.String, StringComparer.Ordinal))
            {
                builder
                    .Append("  FIELD ")
                    .Append(field.Name)
                    .Append(" : ")
                    .AppendLine(Access(field));
            }

            foreach (MethodDef method in type.Methods.OrderBy(m => m.Name.String, StringComparer.Ordinal))
            {
                builder
                    .Append("  METHOD ")
                    .Append(method.Name)
                    .Append(" : ")
                    .AppendLine(Access(method));
            }
        }

        return builder.ToString();
    }

    private static string Access(TypeDef type) => type switch
    {
        { IsNested: false } => type.IsPublic ? "Public" : "NotPublic",
        { IsNestedPublic: true } => "NestedPublic",
        { IsNestedPrivate: true } => "NestedPrivate",
        { IsNestedFamily: true } => "NestedFamily",
        { IsNestedAssembly: true } => "NestedAssembly",
        { IsNestedFamilyOrAssembly: true } => "NestedFamilyOrAssembly",
        { IsNestedFamilyAndAssembly: true } => "NestedFamilyAndAssembly",
        _ => "NestedUnknown",
    };

    private static string Access(FieldDef field) => field switch
    {
        { IsPublic: true } => "Public",
        { IsPrivate: true } => "Private",
        { IsFamily: true } => "Family",
        { IsAssembly: true } => "Assembly",
        { IsFamilyOrAssembly: true } => "FamilyOrAssembly",
        { IsFamilyAndAssembly: true } => "FamilyAndAssembly",
        _ => "CompilerControlled",
    };

    private static string Access(MethodDef method) => method switch
    {
        { IsPublic: true } => "Public",
        { IsPrivate: true } => "Private",
        { IsFamily: true } => "Family",
        { IsAssembly: true } => "Assembly",
        { IsFamilyOrAssembly: true } => "FamilyOrAssembly",
        { IsFamilyAndAssembly: true } => "FamilyAndAssembly",
        _ => "CompilerControlled",
    };
}
