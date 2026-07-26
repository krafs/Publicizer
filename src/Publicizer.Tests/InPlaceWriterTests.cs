using dnlib.DotNet;
using dnlib.DotNet.Writer;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// <see cref="InPlaceWriter"/> replaces a full dnlib metadata rebuild with a byte patch, so it has to be
/// indistinguishable from the dnlib writer in the only respect that matters — the accessibility of every
/// member — while leaving the rest of the file untouched.
/// </summary>
internal static class InPlaceWriterTests
{
    private static PublicizerAssemblyContext WholeAssembly() => new("Fixture") { ExplicitlyPublicizeAssembly = true };

    private static PublicizerAssemblyContext TargetedMember()
    {
        var context = new PublicizerAssemblyContext("Fixture");
        context.PublicizeMemberPatterns.Add("Fixture.Shapes.PrivateField");
        return context;
    }

    private static string WriteInPlace(TemporaryFolder folder, PublicizerAssemblyContext context)
    {
        string destination = Path.Combine(folder.Path, "inplace.dll");
        using var module = ModuleDefMD.Load(Fixtures.ShapesPath());
        PublicizeAssemblies.PublicizeAssembly(module, context, NullTaskLogger.Instance);

        bool written = InPlaceWriter.TryWrite(module, Fixtures.ShapesPath(), destination, NullTaskLogger.Instance);

        Assert.That(written, Is.True, "the fixture has an ordinary compressed-metadata layout, so the fast path must apply");
        return destination;
    }

    private static string WriteWithDnlib(TemporaryFolder folder, PublicizerAssemblyContext context)
    {
        string destination = Path.Combine(folder.Path, "dnlib.dll");
        using var module = ModuleDefMD.Load(Fixtures.ShapesPath());
        PublicizeAssemblies.PublicizeAssembly(module, context, NullTaskLogger.Instance);

        using var stream = new FileStream(destination, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        module.Write(stream, new ModuleWriterOptions(module)
        {
            MetadataOptions = new MetadataOptions(MetadataFlags.KeepOldMaxStack),
            Logger = DummyLogger.NoThrowInstance,
        });
        return destination;
    }

    private static string ManifestOf(string assemblyPath)
    {
        using var module = ModuleDefMD.Load(assemblyPath);
        return AccessibilityManifest.Of(module);
    }

    [Test]
    public static void WholeAssembly_ProducesSameAccessibilityAsDnlibWriter()
    {
        using var folder = new TemporaryFolder();

        string patched = WriteInPlace(folder, WholeAssembly());
        string reference = WriteWithDnlib(folder, WholeAssembly());

        Assert.That(ManifestOf(patched), Is.EqualTo(ManifestOf(reference)));
    }

    [Test]
    public static void TargetedMember_ProducesSameAccessibilityAsDnlibWriter()
    {
        using var folder = new TemporaryFolder();

        string patched = WriteInPlace(folder, TargetedMember());
        string reference = WriteWithDnlib(folder, TargetedMember());

        Assert.That(ManifestOf(patched), Is.EqualTo(ManifestOf(reference)));
    }

    [Test]
    public static void Output_DiffersFromInputOnlyInFlagBytes()
    {
        using var folder = new TemporaryFolder();
        byte[] original = File.ReadAllBytes(Fixtures.ShapesPath());

        byte[] patched = File.ReadAllBytes(WriteInPlace(folder, WholeAssembly()));

        Assert.That(patched, Has.Length.EqualTo(original.Length));

        int differing = original.Where((b, i) => b != patched[i]).Count();
        Assert.That(differing, Is.GreaterThan(0), "publicizing the fixture must change something");
        // Only the Flags columns of the TypeDef/Field/Method rows may move; anything larger means the
        // patch is straying outside the metadata tables it is supposed to touch.
        Assert.That(differing, Is.LessThan(original.Length / 100), "patch touched far more of the file than the flag columns");
    }

    [Test]
    public static void Output_IsStillLoadable()
    {
        using var folder = new TemporaryFolder();

        string patched = WriteInPlace(folder, WholeAssembly());

        using var module = ModuleDefMD.Load(patched);
        Assert.That(module.Find("Fixture.Shapes", isReflectionName: true).Fields.Single(f => f.Name == "PrivateField").IsPublic, Is.True);
    }
}
