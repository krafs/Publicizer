using dnlib.DotNet;

namespace Publicizer;

/// <summary>
/// Class for making edits to assemblies and related types.
/// </summary>
internal static class AssemblyEditor
{
    internal static bool PublicizeType(TypeDef type)
    {
        bool modified = false;

        // A nested type is only reachable if every enclosing type is accessible too,
        // so walk up the declaring-type chain and publicize each one.
        for (TypeDef? current = type; current is not null; current = current.DeclaringType)
        {
            TypeAttributes oldAttributes = current.Attributes;
            current.Attributes &= ~TypeAttributes.VisibilityMask;

            if (current.IsNested)
            {
                current.Attributes |= TypeAttributes.NestedPublic;
            }
            else
            {
                current.Attributes |= TypeAttributes.Public;
            }

            modified |= current.Attributes != oldAttributes;
        }

        return modified;
    }

    internal static bool PublicizeProperty(PropertyDef property, bool includeVirtual = true)
    {
        bool publicized = false;

        if (property.GetMethod is MethodDef getMethod)
        {
            publicized |= PublicizeMethod(getMethod, includeVirtual);
        }

        if (property.SetMethod is MethodDef setMethod)
        {
            publicized |= PublicizeMethod(setMethod, includeVirtual);
        }

        return publicized;
    }

    internal static bool PublicizeMethod(MethodDef method, bool includeVirtual = true)
    {
        if (includeVirtual || !method.IsVirtual)
        {
            MethodAttributes oldAttributes = method.Attributes;
            method.Attributes &= ~MethodAttributes.MemberAccessMask;
            method.Attributes |= MethodAttributes.Public;
            return method.Attributes != oldAttributes;
        }
        return false;
    }

    internal static bool PublicizeField(FieldDef field)
    {
        FieldAttributes oldAttributes = field.Attributes;
        field.Attributes &= ~FieldAttributes.FieldAccessMask;
        field.Attributes |= FieldAttributes.Public;
        return field.Attributes != oldAttributes;
    }
}
