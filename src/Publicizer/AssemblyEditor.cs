using System;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using FieldAttributes = dnlib.DotNet.FieldAttributes;
using MethodAttributes = dnlib.DotNet.MethodAttributes;
using TypeAttributes = dnlib.DotNet.TypeAttributes;

namespace Publicizer;

/// <summary>
/// Class for making edits to assemblies and related types.
/// </summary>
internal class AssemblyEditor
{
    private readonly ModuleDef _module;
    private readonly TypeDef? _accessAttributeType;
    private readonly MethodDef? _accessAttributeConstructor;
    internal AssemblyEditor(ModuleDef module, bool addOriginalAccessModifierAttribute)
    {
        _module = module;

        // workaround for custom attributes not being supported in, at least some, core libs
        bool isCoreLib = module.IsCoreLibraryModule ?? false;
        if (!isCoreLib && addOriginalAccessModifierAttribute)
        {
            _accessAttributeType = GetOrCreateAccessAttributeType(module);
            _accessAttributeConstructor = _accessAttributeType.FindConstructors().First();
        }
    }

    internal bool IsAccessAttribute(ITypeDefOrRef typeRef)
    {
        return typeRef == _accessAttributeType;
    }

    internal bool publicizedAnyMemberInAssembly;
    internal int publicizedTypesCount;
    internal int publicizedPropertiesCount;
    internal int publicizedMethodsCount;
    internal int publicizedFieldsCount;

    internal bool PublicizeType(TypeDef type)
    {
        TypeAttributes oldAttributes = type.Attributes;
        type.Attributes &= ~TypeAttributes.VisibilityMask;

        if (type.IsNested)
        {
            type.Attributes |= TypeAttributes.NestedPublic;
        }
        else
        {
            type.Attributes |= TypeAttributes.Public;
        }

        if (type.Attributes != oldAttributes)
        {
            AddOriginalAccessModifierAttribute(type, ConvertAttributes(oldAttributes));
            publicizedTypesCount++;
            return true;
        }
        return false;
    }

    internal bool PublicizeProperty(PropertyDef property, bool includeVirtual = true)
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

        if (publicized)
        {
            publicizedPropertiesCount++;
        }
        return publicized;
    }

    internal bool PublicizeMethod(MethodDef method, bool includeVirtual = true)
    {
        if (includeVirtual || !method.IsVirtual)
        {
            MethodAttributes oldAttributes = method.Attributes;
            method.Attributes &= ~MethodAttributes.MemberAccessMask;
            method.Attributes |= MethodAttributes.Public;
            if (method.Attributes != oldAttributes)
            {
                AddOriginalAccessModifierAttribute(method, ConvertAttributes(oldAttributes));
                publicizedAnyMemberInAssembly = true;
                publicizedMethodsCount++;
                return true;
            }
        }
        return false;
    }

    internal bool PublicizeField(FieldDef field)
    {
        FieldAttributes oldAttributes = field.Attributes;
        field.Attributes &= ~FieldAttributes.FieldAccessMask;
        field.Attributes |= FieldAttributes.Public;
        if (field.Attributes != oldAttributes)
        {
            AddOriginalAccessModifierAttribute(field, ConvertAttributes(oldAttributes));
            publicizedAnyMemberInAssembly = true;
            publicizedFieldsCount++;
            return true;
        }
        return false;
    }

    private void AddOriginalAccessModifierAttribute(IHasCustomAttribute item, AccessModifier original)
    {
        if (_accessAttributeConstructor == null)
        {
            return;
        }
        var attribute = new CustomAttribute(_accessAttributeConstructor);
        var caArgument = new CAArgument(_module.CorLibTypes.String, AccessModifierToString(original));
        attribute.ConstructorArguments.Add(caArgument);
        item.CustomAttributes.Add(attribute);
    }

    private static readonly string s_accessAttributeNamespace = new UTF8String(nameof(Publicizer));
    private static readonly string s_accessAttributeName = new UTF8String("OriginalAccessModifierAttribute");
    private static TypeDef GetOrCreateAccessAttributeType(ModuleDef module)
    {
        TypeDef? attributeTypeDef = module.Types.FirstOrDefault(t => t.Namespace == s_accessAttributeNamespace && t.Name == s_accessAttributeName);
        if (attributeTypeDef == null)
        {
            TypeRef attributeBaseTypeRef = module.CorLibTypes.GetTypeRef("System", "Attribute");
            MemberRef attributeBaseConstructorMethodRef = new MemberRefUser(
                module,
                ".ctor",
                MethodSig.CreateInstance(module.CorLibTypes.Void),
                attributeBaseTypeRef
            );
            attributeTypeDef = new TypeDefUser(s_accessAttributeNamespace, s_accessAttributeName, attributeBaseTypeRef);
            attributeTypeDef.Attributes = TypeAttributes.NestedAssembly | TypeAttributes.Sealed;

            var methodSig = MethodSig.CreateInstance(
                module.CorLibTypes.Void,
                module.CorLibTypes.String
            );
            var methodDef = new MethodDefUser(
                ".ctor",
                methodSig,
                MethodAttributes.Assembly | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName | MethodAttributes.HideBySig
            );

            CilBody body = methodDef.Body = new CilBody();
            body.Instructions.Add(new Instruction(OpCodes.Ldarg_0));
            body.Instructions.Add(new Instruction(OpCodes.Call, attributeBaseConstructorMethodRef));
            body.Instructions.Add(new Instruction(OpCodes.Ret));

            attributeTypeDef.Methods.Add(methodDef);

            module.Types.Add(attributeTypeDef);
        }
        return attributeTypeDef;
    }

    private static AccessModifier ConvertAttributes(TypeAttributes attributes)
    {
        attributes &= ~TypeAttributes.VisibilityMask;
        return attributes switch
        {
            TypeAttributes.NotPublic => AccessModifier.Private,
            TypeAttributes.Public => AccessModifier.Public,
            TypeAttributes.NestedPublic => AccessModifier.Public,
            TypeAttributes.NestedPrivate => AccessModifier.Private,
            TypeAttributes.NestedFamily => AccessModifier.Protected,
            TypeAttributes.NestedAssembly => AccessModifier.Internal,
            TypeAttributes.NestedFamANDAssem => AccessModifier.PrivateProtected,
            TypeAttributes.NestedFamORAssem => AccessModifier.ProtectedInternal,
            _ => AccessModifier.File
        };
    }
    private static AccessModifier ConvertAttributes(MethodAttributes attributes)
    {
        // methods and fields share the same visibility mask order and values
        return ConvertAttributes((FieldAttributes)attributes);
    }
    private static AccessModifier ConvertAttributes(FieldAttributes attributes)
    {
        attributes &= ~FieldAttributes.FieldAccessMask;
        return attributes switch
        {
            FieldAttributes.PrivateScope => AccessModifier.CompilerControlled,
            FieldAttributes.Private => AccessModifier.Private,
            FieldAttributes.FamANDAssem => AccessModifier.PrivateProtected,
            FieldAttributes.Assembly => AccessModifier.Internal,
            FieldAttributes.Family => AccessModifier.Protected,
            FieldAttributes.FamORAssem => AccessModifier.ProtectedInternal,
            FieldAttributes.Public => AccessModifier.Public,
            _ => AccessModifier.File
        };
    }
    // makes it more readable
    // corresponds to official documentation
    // https://learn.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/access-modifiers
    private enum AccessModifier
    {
        Public, Private, Protected, Internal, ProtectedInternal, PrivateProtected, File, CompilerControlled
    }
    private static string AccessModifierToString(AccessModifier modifier)
    {
        return modifier switch
        {
            AccessModifier.Public => "public",
            AccessModifier.Private => "private",
            AccessModifier.Protected => "protected",
            AccessModifier.Internal => "internal",
            AccessModifier.ProtectedInternal => "protected internal",
            AccessModifier.PrivateProtected => "private protected",
            AccessModifier.File => "file",
            AccessModifier.CompilerControlled => "[CompilerControlled|PrivateScope]",
            _ => throw new ArgumentOutOfRangeException(nameof(modifier), modifier, null)
        };
    }
}
