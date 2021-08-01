using System;
using System.IO;
using System.Reflection;

namespace Publicizer.Tests
{
    public static class CsFilePaths
    {
        public static readonly string RootDirectory;

        static CsFilePaths()
        {
            string? assemblyDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            if (assemblyDirectory is null)
            {
                throw new InvalidOperationException("Task assembly location invalid.");
            }

            RootDirectory = Path.Combine(assemblyDirectory, "resources");
        }

        public static string AccessPrivateField => Path.Combine(RootDirectory, "AccessPrivateField.cs");
        public static string AccessPrivateMethod => Path.Combine(RootDirectory, "AccessPrivateMethod.cs");
        public static string AccessPrivateMethodOverload => Path.Combine(RootDirectory, "AccessPrivateMethodOverload.cs");
        public static string AccessPrivateSetProperty => Path.Combine(RootDirectory, "AccessPrivateSetProperty.cs");
        public static string AccessPrivateGetProperty => Path.Combine(RootDirectory, "AccessPrivateGetProperty.cs");
        public static string AccessFieldInPrivateNestedClass => Path.Combine(RootDirectory, "AccessFieldInPrivateNestedClass.cs");
        public static string AccessPrivateConstructor => Path.Combine(RootDirectory, "AccessPrivateConstructor.cs");
    }
}
