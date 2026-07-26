using System.Reflection;

namespace Publicizer.E2ETests;

// A local folder to restore from, and the one package id it is allowed to serve.
internal readonly record struct LocalPackageSource(string Name, string Folder, string PackagePattern);

internal static class NugetConfigMaker
{
    // Given the built Krafs.Publicizer nuget package is located next to the Publicizer assembly.
    internal static string PublicizerPackagesFolder => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

    internal static void CreateConfig(string root, IEnumerable<LocalPackageSource> localSources)
    {
        DirectoryInfo globalPackagesFolder = Directory.CreateDirectory(Path.Combine(root, ".nuget", "packages"));

        string sources = string.Join("\n    ", localSources.Select(source => $"""<add key="{source.Name}" value="{source.Folder}" />"""));
        string mappings = string.Join("\n    ", localSources.Select(source => $"""<packageSource key="{source.Name}"><package pattern="{source.PackagePattern}" /></packageSource>"""));

        string nugetConfig = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <config>
            <clear />
            <add key="globalPackagesFolder" value="{globalPackagesFolder}" />
          </config>
          <packageSources>
            <clear />
            {sources}
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <clear />
            {mappings}
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;

        string nugetConfigPath = Path.Combine(root, "nuget.config");
        File.WriteAllText(nugetConfigPath, nugetConfig);
    }
}
