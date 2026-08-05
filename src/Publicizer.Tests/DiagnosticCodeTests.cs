using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Build.Utilities;
using NUnit.Framework;

namespace Publicizer.Tests;

/// <summary>
/// Guards the diagnostic codes themselves: their shape, their uniqueness, and that each one is
/// documented. The individual diagnostics are covered where their behavior is — mostly
/// <see cref="StructuredTargetTests"/>.
/// </summary>
internal static class DiagnosticCodeTests
{
    private static IEnumerable<(string Name, string Code)> Codes() =>
        typeof(DiagnosticCode)
            .GetFields(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static)
            .Where(field => field.IsLiteral)
            .Select(field => (field.Name, (string)field.GetRawConstantValue()!));

    /// <summary>Resolves docs/diagnostics.md from this source file, so it does not depend on the test's working directory.</summary>
    private static string DocumentationPath([CallerFilePath] string testFilePath = "") =>
        Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testFilePath)!, "..", "..", "docs", "diagnostics.md"));

    [Test]
    public static void Codes_AreWellFormedAndUnique()
    {
        (string Name, string Code)[] codes = [.. Codes()];

        Assert.That(codes, Is.Not.Empty);
        Assert.That(codes.Select(entry => entry.Code), Is.Unique);

        foreach ((string name, string code) in codes)
        {
            Assert.That(code, Has.Length.EqualTo(7), $"{name} = '{code}' is not a PUBxxxx code");
            Assert.That(code, Does.StartWith("PUB"), $"{name} = '{code}' is not a PUBxxxx code");
            Assert.That(code[3..], Is.All.InRange('0', '9'), $"{name} = '{code}' is not a PUBxxxx code");
        }
    }

    [Test]
    public static void Codes_AreDocumented()
    {
        // The table is the user-facing contract; a code that never reaches it cannot be looked up.
        string documentation = File.ReadAllText(DocumentationPath());

        foreach ((string name, string code) in Codes())
        {
            Assert.That(documentation, Does.Contain($"| `{code}` |"), $"{name} = '{code}' has no row in docs/diagnostics.md");
        }
    }

    [Test]
    public static void DocumentedCodes_StillExist()
    {
        // The other direction: a removed code leaves a row behind that nothing raises, which reads
        // as a diagnostic a user can expect and never gets.
        string[] declared = [.. Codes().Select(entry => entry.Code)];
        // Only rows naming a concrete code: the band table above them spells its rows 'PUB1xxx'.
        string[] documented = [.. File.ReadAllLines(DocumentationPath())
            .Where(line => line.StartsWith("| `PUB", StringComparison.Ordinal))
            .Select(line => line.Split('`')[1])
            .Where(code => code[3..].All(char.IsAsciiDigit))];

        Assert.That(documented, Is.Not.Empty);

        foreach (string code in documented)
        {
            Assert.That(declared, Does.Contain(code), $"docs/diagnostics.md documents '{code}', which no longer exists in DiagnosticCode");
        }
    }

    [Test]
    public static void TargetsRaisedCode_MatchesItsConstant()
    {
        // PUB4001 is raised by the targets, so the constant is the only thing tying it to this
        // table — nothing would otherwise notice the two drifting apart.
        string targets = File.ReadAllText(Path.Combine(Path.GetDirectoryName(DocumentationPath())!, "..", "src", "Publicizer", "Krafs.Publicizer.targets"));

        Assert.That(targets, Does.Contain($"""Code="{DiagnosticCode.NoRuntimeStrategy}" """.TrimEnd()));
    }

    [Test]
    public static void Codes_AreBanded()
    {
        // An unbanded code would sit in whichever band it happens to collide with, and the band is
        // the thing that keeps room to insert later.
        string[] bands = ["PUB1", "PUB2", "PUB3", "PUB4", "PUB9"];

        foreach ((string name, string code) in Codes())
        {
            Assert.That(bands, Has.Some.EqualTo(code[..4]), $"{name} = '{code}' is outside every documented band");
        }
    }

    [Test]
    public static void LoggedDiagnostics_ReachMSBuildWithTheirCode()
    {
        // The task logger has overloads that silently drop the code, so assert on what the build
        // engine actually receives rather than on the logger call.
        var engine = new FakeBuildEngine();
        using var logger = new Logger(new TaskLoggingHelper(engine, "PublicizeAssemblies"), Stream.Null);

        logger.Error(DiagnosticCode.InvalidType, "an error");
        logger.Warning(DiagnosticCode.NothingPublicized, "a warning");

        string[] expectedErrorCodes = [DiagnosticCode.InvalidType];
        string[] expectedWarningCodes = [DiagnosticCode.NothingPublicized];
        Assert.That(engine.ErrorCodes, Is.EqualTo(expectedErrorCodes));
        Assert.That(engine.WarningCodes, Is.EqualTo(expectedWarningCodes));
    }

    [Test]
    public static void MessagesWithBraces_AreNotTreatedAsFormatStrings()
    {
        // Several 'Type' diagnostics quote a spelling like 'MyType{T1,T2}', which MSBuild would try
        // to format if any format arguments were passed.
        var engine = new FakeBuildEngine();
        using var logger = new Logger(new TaskLoggingHelper(engine, "PublicizeAssemblies"), Stream.Null);

        logger.Error(DiagnosticCode.InvalidType, "write 'MyType{T1,T2}'");

        string[] expected = ["write 'MyType{T1,T2}'"];
        Assert.That(engine.Errors, Is.EqualTo(expected));
    }
}
