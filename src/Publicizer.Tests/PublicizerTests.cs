using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Publicizer.Tests;

public class PublicizerTests
{
    [Test]
    public void PrivateField()
    {
        var targetFramework = "net6.0";
        var libraryRoot = Temporary.NewFolder();
        var libraryCodePath = Path.Combine(libraryRoot, "PrivateClass.cs");
        var libraryCode = """
            namespace PrivateNamespace;
            class PrivateClass
            {
                private static string PrivateField = "foobar";
            }
            """;
        File.WriteAllText(libraryCodePath, libraryCode);

        var libraryCsprojPath = Path.Combine(libraryRoot, "PrivateAssembly.csproj");
        var libraryCsproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutDir>{libraryRoot}</OutDir>
              </PropertyGroup>
          
              <ItemGroup>
                <Compile Include="{libraryCodePath}" />
              </ItemGroup>

            </Project>
            """;

        File.WriteAllText(libraryCsprojPath, libraryCsproj);
        var buildLibraryProcess = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryProcess.ExitCode, Is.Zero, buildLibraryProcess.StandardOutput.ReadToEnd);

        var appRoot = Temporary.NewFolder();
        var appCodePath = Path.Combine(appRoot, "Program.cs");
        var appCode = "System.Console.Write(PrivateNamespace.PrivateClass.PrivateField);";
        File.WriteAllText(appCodePath, appCode);
        var libraryPath = Path.Combine(libraryRoot, "PrivateAssembly.dll");

        var appCsproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{targetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutputType>exe</OutputType>
                <OutDir>{appRoot}</OutDir>
              </PropertyGroup>
          
              <ItemGroup>
                <Compile Include="{appCodePath}" />
                <Reference Include="PrivateAssembly" HintPath="{libraryPath}" />
                <PackageReference Include="Krafs.Publicizer" Version="*" />
                <Publicize Include="PrivateAssembly:PrivateNamespace.PrivateClass.PrivateField" />
              </ItemGroup>

            </Project>
            """;

        var appCsprojPath = Path.Combine(appRoot, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        var appPath = Path.Combine(appRoot, "App.dll");
        Nuget.CreateConfigThatRestoresPublicizerLocally(appRoot);

        var buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        var runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.StandardOutput.ReadToEnd);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.StandardOutput.ReadToEnd);
        Assert.That(runAppProcess.StandardOutput.ReadToEnd, Is.EqualTo("foobar"), runAppProcess.StandardOutput.ReadToEnd);
    }
}
