using System.Linq;
using dnlib.DotNet;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Characterizes <see cref="AssemblyEditor"/>: the low-level attribute flips and
/// their "was anything modified" return values.
/// </summary>
public class AssemblyEditorTests
{
    private static TypeDef ShapesType(ModuleDef module) => module.Find("Fixture.Shapes", isReflectionName: true);
    private static FieldDef Field(ModuleDef module, string name) => ShapesType(module).Fields.Single(f => f.Name == name);
    private static MethodDef Method(ModuleDef module, string name) => ShapesType(module).Methods.Single(m => m.Name == name);

    [Test]
    public void PublicizeField_PrivateField_BecomesPublicAndReturnsTrue()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        FieldDef field = Field(module, "PrivateField");

        bool modified = AssemblyEditor.PublicizeField(field);

        Assert.That(modified, Is.True);
        Assert.That(field.IsPublic, Is.True);
    }

    [Test]
    public void PublicizeField_AlreadyPublicField_ReturnsFalse()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        FieldDef field = Field(module, "PublicField");

        bool modified = AssemblyEditor.PublicizeField(field);

        Assert.That(modified, Is.False);
        Assert.That(field.IsPublic, Is.True);
    }

    [Test]
    public void PublicizeMethod_PrivateMethod_BecomesPublicAndReturnsTrue()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        MethodDef method = Method(module, "PrivateMethod");

        bool modified = AssemblyEditor.PublicizeMethod(method);

        Assert.That(modified, Is.True);
        Assert.That(method.IsPublic, Is.True);
    }

    [Test]
    public void PublicizeMethod_VirtualMethod_ExcludingVirtual_LeavesItUntouchedAndReturnsFalse()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        MethodDef method = Method(module, "ProtectedVirtualMethod");

        bool modified = AssemblyEditor.PublicizeMethod(method, includeVirtual: false);

        Assert.That(modified, Is.False);
        Assert.That(method.IsFamily, Is.True);
    }

    [Test]
    public void PublicizeMethod_VirtualMethod_IncludingVirtual_BecomesPublic()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        MethodDef method = Method(module, "ProtectedVirtualMethod");

        bool modified = AssemblyEditor.PublicizeMethod(method, includeVirtual: true);

        Assert.That(modified, Is.True);
        Assert.That(method.IsPublic, Is.True);
    }

    [Test]
    public void PublicizeType_NestedType_AlsoPublicizesEnclosingType()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        TypeDef inner = module.Find("Fixture.Shapes+Inner", isReflectionName: true);

        bool modified = AssemblyEditor.PublicizeType(inner);

        Assert.That(modified, Is.True);
        Assert.That(inner.IsNestedPublic, Is.True);
        Assert.That(ShapesType(module).IsPublic, Is.True);
    }

    [Test]
    public void PublicizeProperty_PublicizesAccessorMethods()
    {
        using ModuleDefMD module = Fixtures.LoadShapesModule();
        PropertyDef property = ShapesType(module).Properties.Single(p => p.Name == "PrivateAutoProp");

        bool modified = AssemblyEditor.PublicizeProperty(property);

        Assert.That(modified, Is.True);
        Assert.That(property.GetMethod!.IsPublic, Is.True);
        Assert.That(property.SetMethod!.IsPublic, Is.True);
    }
}
