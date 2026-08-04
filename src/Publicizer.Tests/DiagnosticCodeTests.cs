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
            Assert.That(documentation, Does.Contain($"`{code}`"), $"{name} = '{code}' is missing from docs/diagnostics.md");
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
