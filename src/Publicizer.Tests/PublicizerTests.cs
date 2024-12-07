using System.IO;
using NUnit.Framework;

namespace Publicizer.Tests;

public class PublicizerTests
{
    private const string TestTargetFramework = "net9.0";

    [Test]
    public void PublicizePrivateField_CompilesAndRunsWithExitCode0AndPrintsFieldValue()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "PrivateClass.cs");
        string libraryCode = """
            namespace PrivateNamespace;
            class PrivateClass
            {
                private static string PrivateField = "foobar";
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = "System.Console.Write(PrivateNamespace.PrivateClass.PrivateField);";
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
                <Publicize Include="PrivateAssembly:PrivateNamespace.PrivateClass.PrivateField" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobar"), runAppProcess.Output);
    }

    [Test]
    public void PublicizePrivateProperty_CompilesAndRunsWithExitCode0AndPrintsPropertyValue()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "PrivateClass.cs");
        string libraryCode = """
            namespace PrivateNamespace;
            class PrivateClass
            {
                private static string PrivateProperty => "foobar";
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = "System.Console.Write(PrivateNamespace.PrivateClass.PrivateProperty);";
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
                <Publicize Include="PrivateAssembly:PrivateNamespace.PrivateClass.PrivateProperty" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobar"), runAppProcess.Output);
    }

    [Test]
    public void PublicizePrivateMethod_CompilesAndRunsWithExitCode0AndPrintsReturnValue()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "PrivateClass.cs");
        string libraryCode = """
            namespace PrivateNamespace;
            class PrivateClass
            {
                private static string PrivateMethod() => "foobar";
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = "System.Console.Write(PrivateNamespace.PrivateClass.PrivateMethod());";
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
                <Publicize Include="PrivateAssembly:PrivateNamespace.PrivateClass.PrivateMethod" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobar"), runAppProcess.Output);
    }

    [Test]
    public void PublicizePrivateConstructor_CompilesAndRunsWithExitCode0()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "PrivateClass.cs");
        string libraryCode = """
            namespace PrivateNamespace;
            class PrivateClass
            {
                private PrivateClass()
                { }
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = """
            _ = new PrivateNamespace.PrivateClass();
            System.Console.Write("foobar"); // Printing this means success, because failing the PrivateClass constructor above would throw.
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
                <Publicize Include="PrivateAssembly:PrivateNamespace.PrivateClass..ctor" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobar"), runAppProcess.Output);
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
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

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
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
        ProcessResult buildLibrary1Result = Runner.Run("dotnet", "build", library1CsprojPath);
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
        ProcessResult buildLibrary2Result = Runner.Run("dotnet", "build", library2CsprojPath);
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

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobarfoobar"), runAppProcess.Output);
    }

    [Test]
    public void PublicizeAssembly_ExceptProtectedMethod_OverridingThatMethod_CompilesAndRunsWithExitCode0()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "ProtectedClass.cs");
        string libraryCode = """
            namespace ProtectedNamespace;
            public abstract class ProtectedClass
            {
                protected abstract void ProtectedMethod();
            }
            """;
        File.WriteAllText(libraryCodePath, libraryCode);

        string libraryCsprojPath = Path.Combine(libraryFolder.Path, "ProtectedAssembly.csproj");
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = """
            _ = new SubClass();
            System.Console.Write("foobar");
            class SubClass : ProtectedNamespace.ProtectedClass
            {
                protected override void ProtectedMethod() { }
            }
            """;
        File.WriteAllText(appCodePath, appCode);
        string libraryPath = Path.Combine(libraryFolder.Path, "ProtectedAssembly.dll");

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
                <Reference Include="ProtectedAssembly" HintPath="{libraryPath}" />
                <PackageReference Include="Krafs.Publicizer" Version="*" />
                <Publicize Include="ProtectedAssembly" />
                <DoNotPublicize Include="ProtectedAssembly:ProtectedNamespace.ProtectedClass.ProtectedMethod" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        ProcessResult runAppProcess = Runner.Run("dotnet", appPath);

        Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.Output);
        Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.Output);
        Assert.That(runAppProcess.Output, Is.EqualTo("foobar"), runAppProcess.Output);
    }

    [Test]
    public void PublicizeAssembly_ExceptCompilerGenerated_FailsCompileAndPrintsErrorCodeCS0117ForCompilerGeneratedField()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "LibraryClass.cs");
        string libraryCode = """
            namespace LibraryNamespace;
            public class LibraryClass
            {
                [System.Runtime.CompilerServices.CompilerGenerated]
                private static string CompilerGeneratedPrivateField;
                private static string PrivateField;
            }
            """;
        File.WriteAllText(libraryCodePath, libraryCode);

        string libraryCsprojPath = Path.Combine(libraryFolder.Path, "LibraryAssembly.csproj");
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = """
            _ = LibraryNamespace.LibraryClass.CompilerGeneratedPrivateField;
            _ = LibraryNamespace.LibraryClass.PrivateField;
            """;
        File.WriteAllText(appCodePath, appCode);
        string libraryPath = Path.Combine(libraryFolder.Path, "LibraryAssembly.dll");

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
                <Reference Include="LibraryAssembly" HintPath="{libraryPath}" />
                <PackageReference Include="Krafs.Publicizer" Version="*" />
                <Publicize Include="LibraryAssembly" IncludeCompilerGeneratedMembers="false" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);

        Assert.That(buildAppProcess.ExitCode, Is.Not.Zero, buildAppProcess.Output);
        Assert.That(buildAppProcess.Output, Does.Match("CS0117: 'LibraryClass' does not contain a definition for 'CompilerGeneratedPrivateField'"));
        Assert.That(buildAppProcess.Output, Does.Not.Match("CS0117: 'LibraryClass' does not contain a definition for 'PrivateField'"));
    }

    [Test]
    public void PublicizeAssembly_ExceptVirtual_FailsCompileAndPrintsErrorCodeCS0122ForVirtualProperty()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "LibraryClass.cs");
        string libraryCode = """
            namespace LibraryNamespace;
            public class LibraryClass
            {
                protected virtual string VirtualProtectedProperty => "foo";
                protected string ProtectedProperty => "bar";
            }
            """;
        File.WriteAllText(libraryCodePath, libraryCode);

        string libraryCsprojPath = Path.Combine(libraryFolder.Path, "LibraryAssembly.csproj");
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = """
            var instance = new LibraryNamespace.LibraryClass();
            _ = instance.VirtualProtectedProperty;
            _ = instance.ProtectedProperty;
            """;
        File.WriteAllText(appCodePath, appCode);
        string libraryPath = Path.Combine(libraryFolder.Path, "LibraryAssembly.dll");

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
                <Reference Include="LibraryAssembly" HintPath="{libraryPath}" />
                <PackageReference Include="Krafs.Publicizer" Version="*" />
                <Publicize Include="LibraryAssembly" IncludeVirtualMembers="false" />
              </ItemGroup>

            </Project>
            """;

        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);

        Assert.That(buildAppProcess.ExitCode, Is.Not.Zero, buildAppProcess.Output);
        Assert.That(buildAppProcess.Output, Does.Match("CS0122: 'LibraryClass.VirtualProtectedProperty' is inaccessible due to its protection level"));
        Assert.That(buildAppProcess.Output, Does.Not.Match("CS0122: 'LibraryClass.ProtectedProperty' is inaccessible due to its protection level"));
    }
    
    [Test]
    public void PublicizePrivateMembersWithMemberPattern_CompilesAndRunsWithExitCode0AndPrintsExpectedValues()
    {
        using var libraryFolder = new TemporaryFolder();
        string libraryCodePath = Path.Combine(libraryFolder.Path, "PrivateClass.cs");
        string libraryCode = """"
            namespace PrivateNamespace;
            class PrivateClass
            {
                protected string PrivateFooField = "foo";
                protected string PrivateBarField = "bar";
                protected string PrivateFooProperty { get; } = "foo";
            }
            """";
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
        ProcessResult buildLibraryResult = Runner.Run("dotnet", "build", libraryCsprojPath);
        Assert.That(buildLibraryResult.ExitCode, Is.Zero, buildLibraryResult.Output);

        using var appFolder = new TemporaryFolder();
        string appCodePath = Path.Combine(appFolder.Path, "Program.cs");
        string appCode = """
                         var instance = new PrivateNamespace.PrivateClass();
                         _ = instance.PrivateFooField;
                         _ = instance.PrivateBarField;
                         _ = instance.PrivateFooProperty;
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
                <Publicize Include="PrivateAssembly" MemberPattern=".*Foo.*" />
              </ItemGroup>

            </Project>
            """;
    
        string appCsprojPath = Path.Combine(appFolder.Path, "App.csproj");
        File.WriteAllText(appCsprojPath, appCsproj);
        string appPath = Path.Combine(appFolder.Path, "App.dll");
        NugetConfigMaker.CreateConfigThatRestoresPublicizerLocally(appFolder.Path);

        ProcessResult buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
        Assert.That(buildAppProcess.ExitCode, Is.Not.Zero, buildAppProcess.Output);
        Assert.That(buildAppProcess.Output, Does.Match("CS0122: 'PrivateNamespace.PrivateClass.PrivateBarField' is inaccessible due to its protection level"));
        Assert.That(buildAppProcess.Output, Does.Not.Match("CS0122: 'PrivateNamespace.PrivateClass.PrivateFooField' is inaccessible due to its protection level"));
        Assert.That(buildAppProcess.Output, Does.Not.Match("CS0122: 'PrivateNamespace.PrivateClass.PrivateFooProperty' is inaccessible due to its protection level"));
    }
}
