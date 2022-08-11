using System.IO;
using System.Reflection;

namespace Publicizer.Tests;

internal static class Nuget
{
    internal static void CreateConfigThatRestoresPublicizerLocally(string root)
    {
        // Given the built Krafs.Publicizer nuget package is located next to the Publicizer assembly.
        var publicizerPackagesFolder = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        var globalPackagesFolder = Directory.CreateDirectory(Path.Combine(root, ".nuget", "packages"));

        var nugetConfig = $"""
        <?xml version="1.0" encoding="utf-8"?>
        <configuration>
          <config>
            <clear />
            <add key="globalPackagesFolder" value="{globalPackagesFolder}" />
          </config>
          <packageSources>
            <clear />
            <add key="publicizer" value="{publicizerPackagesFolder}" />
            <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
          </packageSources>
          <packageSourceMapping>
            <clear />
            <packageSource key="publicizer">
              <package pattern="Krafs.Publicizer" />
            </packageSource>
            <packageSource key="nuget.org">
              <package pattern="*" />
            </packageSource>
          </packageSourceMapping>
        </configuration>
        """;

        var nugetConfigPath = Path.Combine(root, "nuget.config");
        File.WriteAllText(nugetConfigPath, nugetConfig);
    }
}
