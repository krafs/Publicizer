using dnlib.DotNet;
using dnlib.DotNet.Emit;

namespace Publicizer;

/// <summary>
/// Class for making edits to assemblies and related types.
/// </summary>
internal static class AssemblyEditor
{
    internal static void PublicizeType(TypeDef type)
    {
        type.Attributes &= ~TypeAttributes.VisibilityMask;

        if (type.IsNested)
        {
            type.Attributes |= TypeAttributes.NestedPublic;
        }
        else
        {
            type.Attributes |= TypeAttributes.Public;
        }
    }

    internal static void PublicizeProperty(PropertyDef property, bool includeVirtual = true)
    {
        if (property.GetMethod is MethodDef getMethod)
        {
            PublicizeMethod(getMethod, includeVirtual);
        }

        if (property.SetMethod is MethodDef setMethod)
        {
            PublicizeMethod(setMethod, includeVirtual);
        }
    }

    internal static void PublicizeMethod(MethodDef method, bool includeVirtual = true)
    {
        if (includeVirtual || !method.IsVirtual)
        {
            method.Attributes &= ~MethodAttributes.MemberAccessMask;
            method.Attributes |= MethodAttributes.Public;
        }
    }

    internal static void PublicizeField(FieldDef field)
    {
        field.Attributes &= ~FieldAttributes.FieldAccessMask;
        field.Attributes |= FieldAttributes.Public;
    }
}
