using System;
using System.Collections.Generic;
using System.IO;

namespace Publicizer.E2ETests;

// A throwaway consumer project in a temporary folder: one source file, a csproj assembled
// from the properties and items a test adds, and the build/run of it. Each project builds
// into its own folder, so tests reference each other's output by path.
internal sealed class TestProject : IDisposable
{
    internal const string DefaultTargetFramework = "net10.0";

    private readonly TemporaryFolder _folder = new();
    private readonly string _name;
    private readonly string _outputType;
    private readonly string _sourcePath;
    private readonly List<string> _properties = [];
    private readonly List<string> _items = [];
    private readonly List<string> _rawXml = [];

    private TestProject(string name, string outputType, string sourceCode)
    {
        _name = name;
        _outputType = outputType;
        _sourcePath = Path.Combine(_folder.Path, "Source.cs");
        File.WriteAllText(_sourcePath, sourceCode);
    }

    internal static TestProject Library(string name, string sourceCode) => new(name, "library", sourceCode);

    internal static TestProject App(string sourceCode) => new("App", "exe", sourceCode);

    internal string Folder => _folder.Path;

    internal string AssemblyPath => Path.Combine(_folder.Path, $"{_name}.dll");

    internal string ProjectPath => Path.Combine(_folder.Path, $"{_name}.csproj");

    internal TestProject Property(string name, string value)
    {
        _properties.Add($"<{name}>{value}</{name}>");
        return this;
    }

    internal TestProject Item(string type, string include, string? attributes = null)
    {
        _items.Add($"""<{type} Include="{include}"{(attributes is null ? "" : " " + attributes)} />""");
        return this;
    }

    internal TestProject Referencing(TestProject other) => Item("Reference", other._name, $"""HintPath="{other.AssemblyPath}" """);

    // The PackageReference plus the nuget.config that resolves it from the locally packed nupkg.
    internal TestProject ConsumingPublicizer()
    {
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(_folder.Path);
        return Item("PackageReference", "Krafs.Publicizer", """Version="*" """);
    }

    // Raw XML appended inside the project, for the odd test that needs a target of its own.
    internal TestProject RawXml(string xml)
    {
        _rawXml.Add(xml);
        return this;
    }

    internal ProcessResult Build(params string[] extraArguments)
    {
        File.WriteAllText(ProjectPath, ToCsproj());
        return Runner.Build(ProjectPath, extraArguments);
    }

    // For the setup steps a test isn't itself about: a failure here is a broken fixture, not a finding.
    internal TestProject BuildOrFail()
    {
        ProcessResult result = Build();
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Building {_name} failed with exit code {result.ExitCode}:{Environment.NewLine}{result.Output}{result.Error}");
        }
        return this;
    }

    internal ProcessResult Run() => Runner.Run("dotnet", AssemblyPath);

    private string ToCsproj()
    {
        string properties = string.Join(Environment.NewLine + "    ", _properties);
        string items = string.Join(Environment.NewLine + "    ", _items);
        string rawXml = string.Join(Environment.NewLine, _rawXml);

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{DefaultTargetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutputType>{_outputType}</OutputType>
                <OutDir>{_folder.Path}</OutDir>
                {properties}
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="{_sourcePath}" />
                {items}
              </ItemGroup>

              {rawXml}

            </Project>
            """;
    }

    public void Dispose() => ((IDisposable)_folder).Dispose();
}
