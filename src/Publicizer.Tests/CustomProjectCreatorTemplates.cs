using System;
using System.IO;
using Microsoft.Build.Utilities.ProjectCreation;

namespace Publicizer.Tests
{
    public static class CustomProjectCreatorTemplates
    {
        public static ProjectCreator PublicizerCsproj(this ProjectCreatorTemplates projectCreator)
        {
            return projectCreator.AccessOverrideCsproj(TestHelper.GetRandomProjectFilePath());
        }

        public static ProjectCreator AccessOverrideCsproj(this ProjectCreatorTemplates projectCreator, string path)
        {
            string assemblyPath = typeof(PublicizeAssemblies).Assembly.Location;
            string? assemblyDirectory = Path.GetDirectoryName(assemblyPath);

            if (assemblyDirectory is null)
            {
                throw new InvalidOperationException("Task assembly location invalid.");
            }

            return projectCreator.SdkCsproj(path: path)
                .Property(PropertyConstants.PublicizeAssembliesTaskAssembly, assemblyPath)
                .Import(Path.Combine(assemblyDirectory, "Krafs.Publicizer.props"))
                .Import(Path.Combine(assemblyDirectory, "Krafs.Publicizer.targets"));
        }
    }
}
