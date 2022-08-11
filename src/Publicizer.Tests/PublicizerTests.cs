using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace Publicizer.Tests
{
    public class PublicizerTests
    {
        [Test]
        public void PrivateField()
        {
            string targetFramework = "net6.0";
            string libraryRoot = Temporary.NewFolder();
            string libraryCodePath = Path.Combine(libraryRoot, "PrivateClass.cs");
            string libraryCode = """
            namespace PrivateNamespace;
            class PrivateClass
            {
                private static string PrivateField = "foobar";
            }
            """;
            File.WriteAllText(libraryCodePath, libraryCode);

            string libraryCsprojPath = Path.Combine(libraryRoot, "PrivateAssembly.csproj");
            string libraryCsproj = $"""
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
            Process buildLibraryProcess = Runner.Run("dotnet", "build", libraryCsprojPath);
            Assert.That(buildLibraryProcess.ExitCode, Is.Zero, buildLibraryProcess.StandardOutput.ReadToEnd);

            string appRoot = Temporary.NewFolder();
            string appCodePath = Path.Combine(appRoot, "Program.cs");
            string appCode = "System.Console.Write(PrivateNamespace.PrivateClass.PrivateField);";
            File.WriteAllText(appCodePath, appCode);
            string libraryPath = Path.Combine(libraryRoot, "PrivateAssembly.dll");

            string appCsproj = $"""
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

            string appCsprojPath = Path.Combine(appRoot, "App.csproj");
            File.WriteAllText(appCsprojPath, appCsproj);
            string appPath = Path.Combine(appRoot, "App.dll");
            Nuget.CreateConfigThatRestoresPublicizerLocally(appRoot);

            Process buildAppProcess = Runner.Run("dotnet", "build", appCsprojPath);
            Process runAppProcess = Runner.Run("mono", appPath);

            Assert.That(buildAppProcess.ExitCode, Is.Zero, buildAppProcess.StandardOutput.ReadToEnd);
            Assert.That(runAppProcess.ExitCode, Is.Zero, runAppProcess.StandardOutput.ReadToEnd);
            Assert.That(runAppProcess.StandardOutput.ReadToEnd, Is.EqualTo("foobar"), runAppProcess.StandardOutput.ReadToEnd);
        }
    }
}
