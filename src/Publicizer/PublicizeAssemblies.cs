using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Publicizer;

public class PublicizeAssemblies : Task
{
    public ITaskItem[]? ReferencePaths { get; set; }
    public ITaskItem[]? Publicizes { get; set; }
    public ITaskItem[]? DoNotPublicizes { get; set; }
    public string? OutputDirectory { get; set; }

    [Output]
    public ITaskItem[]? ReferencePathsToDelete { get; set; }

    [Output]
    public ITaskItem[]? ReferencePathsToAdd { get; set; }

    public override bool Execute()
    {
        if (ReferencePaths is null)
        {
            return true;
        }
        else if (Publicizes is null)
        {
            return true;
        }
        else if (OutputDirectory is null)
        {
            Log.LogError(nameof(OutputDirectory) + " was null!");
            return false;
        }

        DoNotPublicizes ??= Array.Empty<ITaskItem>();

        Directory.CreateDirectory(OutputDirectory);

        var publicizeDict = new Dictionary<string, List<string>>();
        foreach (var item in Publicizes)
        {
            var index = item.ItemSpec.IndexOf(':');
            string assemblyName;
            string pattern;
            if (index == -1)
            {
                assemblyName = item.ItemSpec;
                pattern = item.ItemSpec;
            }
            else
            {
                assemblyName = item.ItemSpec.Substring(0, index);
                pattern = item.ItemSpec.Substring(index + 1);
            }

            if (!publicizeDict.TryGetValue(assemblyName, out var publicizes))
            {
                publicizes = new List<string>();
                publicizeDict.Add(assemblyName, publicizes);
            }

            publicizes.Add(pattern);
        }

        var doNotPublicizeDict = new Dictionary<string, List<string>>();
        foreach (var item in DoNotPublicizes)
        {
            var index = item.ItemSpec.IndexOf(':');
            string assemblyName;
            string pattern;
            if (index == -1)
            {
                assemblyName = item.ItemSpec;
                pattern = item.ItemSpec;
            }
            else
            {
                assemblyName = item.ItemSpec.Substring(0, index);
                pattern = item.ItemSpec.Substring(index + 1);
            }

            if (!doNotPublicizeDict.TryGetValue(assemblyName, out var doNotPublicizes))
            {
                doNotPublicizes = new List<string>();
                doNotPublicizeDict.Add(assemblyName, doNotPublicizes);
            }

            doNotPublicizes.Add(pattern);
        }

        var referencePathsToDelete = new List<ITaskItem>();
        var referencePathsToAdd = new List<ITaskItem>();

        foreach (var reference in ReferencePaths)
        {
            var assemblyName = reference.GetFileName();

            if (!publicizeDict.TryGetValue(assemblyName, out var assemblyPublicizes))
            {
                continue;
            }

            doNotPublicizeDict.TryGetValue(assemblyName, out var assemblyDoNotPublicizes);
            assemblyDoNotPublicizes ??= new List<string>();

            var assemblyPath = reference.GetFullPath();

            var hash = ComputeHash(assemblyPath, assemblyPublicizes, assemblyDoNotPublicizes);

            var outputAssemblyFolder = Path.Combine(OutputDirectory, $"{assemblyName}.{hash}");
            Directory.CreateDirectory(outputAssemblyFolder);
            var outputAssemblyPath = Path.Combine(outputAssemblyFolder, assemblyName + ".dll");
            if (!File.Exists(outputAssemblyPath))
            {
                using ModuleDef module = ModuleDefMD.Load(assemblyPath);

                PublicizeAssembly(module, assemblyPublicizes, assemblyDoNotPublicizes);

                using var fileStream = new FileStream(outputAssemblyPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                module.Write(fileStream);
            }
            referencePathsToDelete.Add(reference);
            ITaskItem newReference = new TaskItem(outputAssemblyPath);
            reference.CopyMetadataTo(newReference);
            referencePathsToAdd.Add(newReference);

            var assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            var originalDocumentationFullPath = Path.Combine(assemblyDirectory, assemblyName + ".xml");

            if (File.Exists(originalDocumentationFullPath))
            {
                var newDocumentationRelativePath = Path.Combine(outputAssemblyFolder, assemblyName + ".xml");
                var newDocumentationFullPath = Path.GetFullPath(newDocumentationRelativePath);
                File.Copy(originalDocumentationFullPath, newDocumentationFullPath, overwrite: true);
            }
        }

        ReferencePathsToDelete = referencePathsToDelete.ToArray();
        ReferencePathsToAdd = referencePathsToAdd.ToArray();

        return true;
    }

    private static string ComputeHash(string assemblyPath, List<string> publicizes, List<string> doNotPublicizes)
    {
        var sb = new StringBuilder();
        foreach (var publicizePattern in publicizes)
        {
            sb.Append(publicizePattern);
        }
        foreach (var doNotPublicizePattern in doNotPublicizes)
        {
            sb.Append(doNotPublicizePattern);
        }

        var patternbytes = Encoding.UTF8.GetBytes(sb.ToString());
        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        var allBytes = assemblyBytes.Concat(patternbytes).ToArray();

        return Hasher.ComputeHash(allBytes);
    }

    private static void PublicizeAssembly(
        ModuleDef module,
        List<string> publicizePatterns,
        List<string> doNotPublicizePatterns)
    {
        var publicizeAll = publicizePatterns.Any(x => x == module.Assembly.Name);
        var doNotPublicizePropertyMethods = new List<MethodDef>();

        // TYPES
        foreach (var typeDef in module.GetTypes())
        {
            doNotPublicizePropertyMethods.Clear();

            var publicizedAnyMember = false;
            var typeName = typeDef.ReflectionFullName;

            if (doNotPublicizePatterns.Any(x => x == typeName))
            {
                continue;
            }

            // PROPERTIES
            foreach (var propertyDef in typeDef.Properties)
            {
                var propertyName = $"{typeName}.{propertyDef.Name}";

                var explicitlyDoNotPublicize = !doNotPublicizePatterns.Any(x => x == propertyName);
                if (explicitlyDoNotPublicize)
                {
                    if (propertyDef.GetMethod is MethodDef getter)
                    {
                        doNotPublicizePropertyMethods.Add(getter);
                    }
                    if (propertyDef.SetMethod is MethodDef setter)
                    {
                        doNotPublicizePropertyMethods.Add(setter);
                    }
                }

                var shouldPublicizeProperty = explicitlyDoNotPublicize
                    && (publicizeAll || publicizePatterns.Any(x => x == propertyName));

                if (shouldPublicizeProperty)
                {
                    AssemblyEditor.PublicizeProperty(propertyDef);
                    publicizedAnyMember = true;
                }
            }

            // METHODS
            foreach (var methodDef in typeDef.Methods)
            {
                var methodName = $"{typeName}.{methodDef.Name}";

                // DoNotPublicize does not override Publicize when both are present.
                var shouldPublicizeMethod = !doNotPublicizePropertyMethods.Contains(methodDef) && !doNotPublicizePatterns.Any(x => x == methodName)
                    && (publicizeAll || publicizePatterns.Any(x => x == methodName));

                if (shouldPublicizeMethod)
                {
                    AssemblyEditor.PublicizeMethod(methodDef);
                    publicizedAnyMember = true;
                }
            }

            // FIELDS
            foreach (var fieldDef in typeDef.Fields)
            {
                var fieldName = $"{typeName}.{fieldDef.Name}";

                var shouldPublicizeField = !doNotPublicizePatterns.Any(x => x == fieldName)
                    && (publicizeAll || publicizePatterns.Any(x => x == fieldName));

                if (shouldPublicizeField)
                {
                    AssemblyEditor.PublicizeField(fieldDef);
                    publicizedAnyMember = true;
                }
            }

            if (publicizedAnyMember || publicizeAll || publicizePatterns.Any(x => x == typeName))
            {
                AssemblyEditor.PublicizeType(typeDef);
            }
        }
    }
}
