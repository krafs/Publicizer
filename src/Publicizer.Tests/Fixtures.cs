using System.IO;
using System.Reflection;
using dnlib.DotNet;

namespace Publicizer.Tests;

/// <summary>
/// Loads the characterization fixture assembly. Each call reads a fresh
/// <see cref="ModuleDefMD"/> from disk; publicization mutates it in memory only,
/// so tests never contaminate one another and nothing is written back.
/// </summary>
internal static class Fixtures
{
    internal static string ShapesPath()
    {
        string directory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        return Path.Combine(directory, "Fixture.dll");
    }

    internal static ModuleDefMD LoadShapesModule() => ModuleDefMD.Load(ShapesPath());
}
