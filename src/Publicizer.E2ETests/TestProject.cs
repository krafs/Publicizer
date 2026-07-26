namespace Publicizer.E2ETests;

// A throwaway consumer project in a temporary folder: one source file, a csproj assembled
// from the properties and items a test adds, and the build/run of it. Each project builds
// into its own folder, so tests reference each other's output by path.
internal sealed class TestProject : IDisposable
{
    internal const string DefaultTargetFramework = "net10.0";

    // Fixed: each consumer restores into its own globally-isolated packages folder, so
    // nothing collides across tests and the reference can be pinned exactly.
    private const string PackageVersion = "1.0.0";

    private readonly TemporaryFolder folder = new();
    private readonly string name;
    private readonly string outputType;
    private readonly string sourcePath;
    private readonly List<string> properties = [];
    private readonly List<string> items = [];
    private readonly List<string> rawXml = [];
    private readonly List<LocalPackageSource> packageSources = [];
    private List<string> targetFrameworks = [DefaultTargetFramework];

    private TestProject(string name, string outputType, string sourceCode)
    {
        this.name = name;
        this.outputType = outputType;
        sourcePath = Path.Combine(folder.Path, "Source.cs");
        File.WriteAllText(sourcePath, sourceCode);
    }

    internal static TestProject Library(string name, string sourceCode) => new(name, "library", sourceCode);

    internal static TestProject App(string sourceCode) => new("App", "exe", sourceCode);

    internal string Folder => folder.Path;

    // Output goes to a subfolder, not the project directory: with the two the same, a
    // consumer's copy-local of a reference lands next to its own project and gets resolved
    // in preference to the HintPath on the next build — stale, and nothing to do with the
    // code under test.
    private string OutputFolder => Path.Combine(folder.Path, "bin");

    private bool IsMultiTargeted => targetFrameworks.Count > 1;

    private bool TargetsNetFramework => targetFrameworks[0].StartsWith("net4", StringComparison.Ordinal);

    private string PackageFolder => Path.Combine(folder.Path, "package");

    internal string AssemblyPath => AssemblyPathFor(targetFrameworks[0]);

    internal string ProjectPath => Path.Combine(folder.Path, $"{name}.csproj");

    // Multi-targeted builds give each inner build its own OutDir, so an assembly is only
    // addressable per target framework.
    internal string AssemblyPathFor(string targetFramework) => Path.Combine(IsMultiTargeted ? Path.Combine(OutputFolder, targetFramework) : OutputFolder, $"{name}.dll");

    // .NET Framework consumers resolve references through a different set of search paths
    // and reference assemblies than .NET ones.
    internal TestProject TargetingFramework(string targetFramework) => TargetingFrameworks(targetFramework);

    // More than one framework makes MSBuild run an inner build per framework, each with its
    // own IntermediateOutputPath and its own pass over the references.
    internal TestProject TargetingFrameworks(params string[] targetFrameworks)
    {
        this.targetFrameworks = [.. targetFrameworks];
        return this;
    }

    internal TestProject Property(string name, string value)
    {
        properties.Add($"<{name}>{value}</{name}>");
        return this;
    }

    internal TestProject Item(string type, string include, string? attributes = null)
    {
        items.Add($"""<{type} Include="{include}"{(attributes is null ? "" : " " + attributes)} />""");
        return this;
    }

    internal TestProject Referencing(TestProject other) => Item("Reference", other.name, $"""HintPath="{other.AssemblyPath}" """);

    // The PackageReference plus the local source that resolves it from the locally packed nupkg.
    internal TestProject ConsumingPublicizer()
    {
        packageSources.Add(new LocalPackageSource("publicizer", NugetConfigMaker.PublicizerPackagesFolder, "Krafs.Publicizer"));
        return Item("PackageReference", "Krafs.Publicizer", """Version="*" """);
    }

    // Consume another test project the way most real consumers do: as a package, so its
    // assembly arrives on ReferencePath through NuGet rather than a HintPath. Requires the
    // other project to have been packed.
    internal TestProject ReferencingPackage(TestProject other)
    {
        packageSources.Add(new LocalPackageSource(other.name, other.PackageFolder, other.name));
        return Item("PackageReference", other.name, $"""Version="{PackageVersion}" """);
    }

    // Raw XML appended inside the project, for the odd test that needs a target of its own.
    internal TestProject RawXml(string xml)
    {
        rawXml.Add(xml);
        return this;
    }

    internal ProcessResult Build(params string[] extraArguments)
    {
        Materialize();
        return Runner.Build(ProjectPath, extraArguments);
    }

    // Packs the project so another one can consume it as a PackageReference.
    internal TestProject PackOrFail()
    {
        Property("PackageVersion", PackageVersion);
        Property("PackageOutputPath", PackageFolder);
        Materialize();

        ProcessResult result = Runner.Pack(ProjectPath);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Packing {name} failed with exit code {result.ExitCode}:{Environment.NewLine}{result.Output}{result.Error}");
        }
        return this;
    }

    // Builds through a node that stays alive afterwards, as a Visual Studio session does.
    internal ProcessResult BuildReusingNodes()
    {
        Materialize();
        return Runner.Build(ProjectPath, reuseNodes: true, []);
    }

    // Written late rather than as each call comes in: the nuget.config has to list every
    // local source at once, and sources are added one builder call at a time.
    private void Materialize()
    {
        File.WriteAllText(ProjectPath, ToCsproj());
        if (packageSources.Count > 0)
        {
            NugetConfigMaker.CreateConfig(folder.Path, packageSources);
        }
    }

    internal TestProject Rewrite(string sourceCode)
    {
        File.WriteAllText(sourcePath, sourceCode);
        return this;
    }

    // For the setup steps a test isn't itself about: a failure here is a broken fixture, not a finding.
    internal TestProject BuildOrFail()
    {
        ProcessResult result = Build();
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException($"Building {name} failed with exit code {result.ExitCode}:{Environment.NewLine}{result.Output}{result.Error}");
        }
        return this;
    }

    // .NET Framework apps produce a runnable .exe; .NET apps are launched through the host.
    internal ProcessResult Run() => TargetsNetFramework
        ? Runner.Run(Path.Combine(OutputFolder, $"{name}.exe"))
        : Runner.Run("dotnet", AssemblyPath);

    private string ToCsproj()
    {
        string propertiesXml = string.Join(Environment.NewLine + "    ", properties);
        string itemsXml = string.Join(Environment.NewLine + "    ", items);
        string rawXmlText = string.Join(Environment.NewLine, rawXml);

        string targetFrameworksXml = IsMultiTargeted
            ? $"<TargetFrameworks>{string.Join(";", targetFrameworks)}</TargetFrameworks>"
            : $"<TargetFramework>{targetFrameworks[0]}</TargetFramework>";

        // Inner builds would otherwise all write to the same OutDir and overwrite each other.
        string outDir = IsMultiTargeted
            ? Path.Combine(OutputFolder, "$(TargetFramework)")
            : OutputFolder;

        return $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                {targetFrameworksXml}
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutputType>{outputType}</OutputType>
                <OutDir>{outDir}</OutDir>
                {propertiesXml}
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="{sourcePath}" />
                {itemsXml}
              </ItemGroup>

              {rawXmlText}

            </Project>
            """;
    }

    public void Dispose() => ((IDisposable)folder).Dispose();
}
