using System;
using System.IO;
using NUnit.Framework;

namespace Publicizer.E2ETests;

// End-to-end smoke of the MSBuild integration: prove the task loads, the targets run,
// and a real consumer builds and runs against a publicized reference. One case per
// MSBuild path (explicit Publicize items, and PublicizeAll). What publicization does to
// each member kind is covered by the characterization and engine unit tests, not here.
public class PublicizerTests
{
    private const string TestTargetFramework = "net10.0";

    [SetUp]
    public void RequireWindowsForDesktopMSBuild()
    {
        if (Runner.Builder.Equals("msbuild", StringComparison.OrdinalIgnoreCase) && !OperatingSystem.IsWindows())
        {
            Assume.That(false, "Desktop MSBuild.exe builder runs on Windows only.");
        }
    }

    [Test]
    public void PublicizeAssembly_CompilesAndRunsWithExitCode0AndPrintsReturnValuesFromAllPrivateMembersInPrivateClass()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "PrivateClass.cs");
        string libraryCode = """
            namespace PrivateNamespace;
            class PrivateClass
            {
                private PrivateClass()
                { }

                private string PrivateField = "foo";
                private string PrivateProperty => "ba";
                private string PrivateMethod() => "r";
            }
            """;
        File.WriteAllText(libraryCodePath, libraryCode);

        string libraryCsprojPath = Path.Combine(libraryFolder.Path, "PrivateAssembly.csproj");
        string libraryCsproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{TestTargetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutDir>{libraryFolder.Path}</OutDir>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="{libraryCodePath}" />
              </ItemGroup>

            </Project>
            """;

        File.WriteAllText(libraryCsprojPath, libraryCsproj);
        ProcessResult buildLibraryResult = Runner.Build(libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = """
            var privateClass = new PrivateNamespace.PrivateClass();
            var result = privateClass.PrivateField;
            result += privateClass.PrivateProperty;
            result += privateClass.PrivateMethod();
            System.Console.Write(result);
            """;
        File.WriteAllText(appCodePath, appCode);
        string libraryPath = Path.Combine(libraryFolder.Path, "PrivateAssembly.dll");

        string appCsproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{TestTargetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutputType>exe</OutputType>
                <OutDir>{appFolder.Path}</OutDir>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="{appCodePath}" />
                <Reference Include="PrivateAssembly" HintPath="{libraryPath}" />
                <PackageReference Include="Krafs.Publicizer" Version="*" />
                <Publicize Include="PrivateAssembly" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Build(appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobar"), runAppProcess.Output);
    }

    [Test]
    public void PublicizeAll_CompilesAndRunsWithExitCode0AndPrintsReturnValuesFromPrivateMembersFromTwoDifferentAssemblies()
    {
        using var library1Folder = new TemporaryFolder();
        string library1CodePath = Path.Combine(library1Folder.Path, "PrivateClass.cs");
        string library1Code = """
            namespace PrivateNamespace1;
            class PrivateClass
            {
                private PrivateClass()
                { }

                private string PrivateField = "foo";
                private string PrivateProperty => "ba";
                private string PrivateMethod() => "r";
            }
            """;
        File.WriteAllText(library1CodePath, library1Code);

        string library1CsprojPath = Path.Combine(library1Folder.Path, "PrivateAssembly1.csproj");
        string library1Csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{TestTargetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutDir>{library1Folder.Path}</OutDir>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="{library1CodePath}" />
              </ItemGroup>

            </Project>
            """;

        File.WriteAllText(library1CsprojPath, library1Csproj);
        ProcessResult buildLibrary1Result = Runner.Build(library1CsprojPath);
        Assert.That(buildLibrary1Result.ExitCode, Is.Zero, buildLibrary1Result.Output);

        using var library2Folder = new TemporaryFolder();
        string library2CodePath = Path.Combine(library2Folder.Path, "PrivateClass.cs");
        string library2Code = """
            namespace PrivateNamespace2;
            class PrivateClass
            {
                private PrivateClass()
                { }

                private string PrivateField = "foo";
                private string PrivateProperty => "ba";
                private string PrivateMethod() => "r";
            }
            """;
        File.WriteAllText(library2CodePath, library2Code);

        string library2CsprojPath = Path.Combine(library2Folder.Path, "PrivateAssembly2.csproj");
        string library2Csproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{TestTargetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutDir>{library2Folder.Path}</OutDir>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="{library2CodePath}" />
              </ItemGroup>

            </Project>
            """;

        File.WriteAllText(library2CsprojPath, library2Csproj);
        ProcessResult buildLibrary2Result = Runner.Build(library2CsprojPath);
        Assert.That(buildLibrary2Result.ExitCode, Is.Zero, buildLibrary2Result.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = """
            var privateClass1 = new PrivateNamespace1.PrivateClass();
            var result1 = privateClass1.PrivateField;
            result1 += privateClass1.PrivateProperty;
            result1 += privateClass1.PrivateMethod();

            var privateClass2 = new PrivateNamespace2.PrivateClass();
            var result2 = privateClass2.PrivateField;
            result2 += privateClass2.PrivateProperty;
            result2 += privateClass2.PrivateMethod();

            System.Console.Write(result1 + result2);
            """;
        File.WriteAllText(appCodePath, appCode);
        string library1Path = Path.Combine(library1Folder.Path, "PrivateAssembly1.dll");
        string library2Path = Path.Combine(library2Folder.Path, "PrivateAssembly2.dll");

        string appCsproj = $"""
            <Project Sdk="Microsoft.NET.Sdk">

              <PropertyGroup>
                <TargetFramework>{TestTargetFramework}</TargetFramework>
                <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
                <OutputType>exe</OutputType>
                <OutDir>{appFolder.Path}</OutDir>
                <PublicizeAll>true</PublicizeAll>
              </PropertyGroup>

              <ItemGroup>
                <Compile Include="{appCodePath}" />
                <Reference Include="PrivateAssembly1" HintPath="{library1Path}" />
                <Reference Include="PrivateAssembly2" HintPath="{library2Path}" />
                <PackageReference Include="Krafs.Publicizer" Version="*" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Build(appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobarfoobar"), runAppProcess.Output);
    }
}
