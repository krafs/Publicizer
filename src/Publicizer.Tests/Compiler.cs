using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace Publicizer.Tests;

/// <summary>
/// Compiles C# source to an in-memory assembly with Roslyn. The compiler version is pinned by
/// the Microsoft.CodeAnalysis.CSharp package (not the repo SDK), so the emitted metadata the
/// characterization tests observe is deterministic and independent of global.json bumps.
/// </summary>
internal static class Compiler
{
    private static readonly List<MetadataReference> References = BuildReferences();

    internal static byte[] Compile(string code, string assemblyName = "Fixture")
    {
        CSharpCompilation compilation = CSharpCompilation.Create(assemblyName, [CSharpSyntaxTree.ParseText(code)], References, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, deterministic: true));

        using var stream = new MemoryStream();
        EmitResult result = compilation.Emit(stream);
        if (!result.Success)
        {
            string errors = string.Join(Environment.NewLine, result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error));
            throw new InvalidOperationException($"Fixture compilation failed:{Environment.NewLine}{errors}");
        }

        return stream.ToArray();
    }

    private static List<MetadataReference> BuildReferences()
    {
        string trustedPlatformAssemblies = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;
        return trustedPlatformAssemblies.Split(Path.PathSeparator).Where(path => path.Length > 0).Select(path => (MetadataReference)MetadataReference.CreateFromFile(path)).ToList();
    }
}
