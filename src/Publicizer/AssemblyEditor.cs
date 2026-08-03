using dnlib.DotNet;

namespace Publicizer;

/// <summary>
/// Class for making edits to assemblies and related types.
/// </summary>
internal static class AssemblyEditor
{
    /// <summary>
    /// Publicizes this type alone. Making a nested type actually reachable also takes its
    /// enclosing types, which is the engine's call to make, not this one's: see
    /// <c>PublicizeAssemblies.PublicizeTypeAndEnclosers</c>.
    /// </summary>
    internal static bool PublicizeType(TypeDef type)
    {
        TypeAttributes oldAttributes = type.Attributes;
        type.Attributes &= ~TypeAttributes.VisibilityMask;
        type.Attributes |= type.IsNested ? TypeAttributes.NestedPublic : TypeAttributes.Public;

        return type.Attributes != oldAttributes;
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
