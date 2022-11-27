using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Publicizer;
public class PublicizeAssemblies : Task
{
    private static readonly string CompilerGeneratedFullName = typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).FullName;

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

        var assemblyContexts = GetPublicizerAssemblyContexts(Publicizes, DoNotPublicizes);
        var referencePathsToDelete = new List<ITaskItem>();
        var referencePathsToAdd = new List<ITaskItem>();

        foreach (var reference in ReferencePaths)
        {
            var assemblyName = reference.FileName();

            if (!assemblyContexts.TryGetValue(assemblyName, out var assemblyContext))
            {
                continue;
            }

            var assemblyPath = reference.FullPath();

            var hash = ComputeHash(assemblyPath, assemblyContext);

            var outputAssemblyFolder = Path.Combine(OutputDirectory, $"{assemblyName}.{hash}");
            Directory.CreateDirectory(outputAssemblyFolder);
            var outputAssemblyPath = Path.Combine(outputAssemblyFolder, assemblyName + ".dll");
            if (!File.Exists(outputAssemblyPath))
            {
                using ModuleDef module = ModuleDefMD.Load(assemblyPath);

                var isAssemblyModified = PublicizeAssembly(module, assemblyContext);
                if (!isAssemblyModified)
                {
                    continue;
                }

                using var fileStream = new FileStream(outputAssemblyPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

                var writerOptions = new ModuleWriterOptions(module);

                // Writing the module sometime fails without this flag due to how it was originally compiled.
                // https://github.com/krafs/Publicizer/issues/42
                writerOptions.MetadataOptions.Flags |= MetadataFlags.KeepOldMaxStack;

                module.Write(fileStream, writerOptions);
            }
            referencePathsToDelete.Add(reference);
            ITaskItem newReference = new TaskItem(outputAssemblyPath);
            reference.CopyMetadataTo(newReference);
            reference.SetMetadata("Publicized", bool.TrueString);

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

    private static Dictionary<string, PublicizerAssemblyContext> GetPublicizerAssemblyContexts(
        ITaskItem[] publicizeItems,
        ITaskItem[] doNotPublicizeItems)
    {
        var contexts = new Dictionary<string, PublicizerAssemblyContext>();

        foreach (var item in publicizeItems)
        {
            var index = item.ItemSpec.IndexOf(':');
            var isAssemblyPattern = index == -1;
            var assemblyName = isAssemblyPattern ? item.ItemSpec : item.ItemSpec.Substring(0, index);

            if (!contexts.TryGetValue(assemblyName, out var assemblyContext))
            {
                assemblyContext = new PublicizerAssemblyContext(assemblyName);
                contexts.Add(assemblyName, assemblyContext);
            }

            if (isAssemblyPattern)
            {
                assemblyContext.IncludeCompilerGeneratedMembers = item.IncludeCompilerGeneratedMembers();
                assemblyContext.IncludeVirtualMembers = item.IncludeVirtualMembers();
                assemblyContext.ExplicitlyPublicizeAssembly = true;
            }
            else
            {
                var memberPattern = item.ItemSpec.Substring(index + 1);
                assemblyContext.PublicizeMemberPatterns.Add(memberPattern);
            }
        }

        foreach (var item in doNotPublicizeItems)
        {
            var index = item.ItemSpec.IndexOf(':');
            var isAssemblyPattern = index == -1;
            var assemblyName = isAssemblyPattern ? item.ItemSpec : item.ItemSpec.Substring(0, index);

            if (!contexts.TryGetValue(assemblyName, out var assemblyContext))
            {
                assemblyContext = new PublicizerAssemblyContext(assemblyName);
                contexts.Add(assemblyName, assemblyContext);
            }

            if (isAssemblyPattern)
            {
                assemblyContext.ExplicitlyDoNotPublicizeAssembly = true;
            }
            else
            {
                var memberPattern = item.ItemSpec.Substring(index + 1);
                assemblyContext.DoNotPublicizeMemberPatterns.Add(memberPattern);
            }
        }

        return contexts;
    }
    private static string ComputeHash(string assemblyPath, PublicizerAssemblyContext assemblyContext)
    {
        var sb = new StringBuilder();
        sb.Append(assemblyContext.AssemblyName);
        sb.Append(assemblyContext.IncludeCompilerGeneratedMembers);
        sb.Append(assemblyContext.IncludeVirtualMembers);
        sb.Append(assemblyContext.ExplicitlyPublicizeAssembly);
        sb.Append(assemblyContext.ExplicitlyDoNotPublicizeAssembly);
        foreach (var publicizePattern in assemblyContext.PublicizeMemberPatterns)
        {
            sb.Append(publicizePattern);
        }
        foreach (var doNotPublicizePattern in assemblyContext.DoNotPublicizeMemberPatterns)
        {
            sb.Append(doNotPublicizePattern);
        }

        var patternbytes = Encoding.UTF8.GetBytes(sb.ToString());
        var assemblyBytes = File.ReadAllBytes(assemblyPath);
        var allBytes = assemblyBytes.Concat(patternbytes).ToArray();

        return Hasher.ComputeHash(allBytes);
    }

    private static bool PublicizeAssembly(ModuleDef module, PublicizerAssemblyContext assemblyContext)
    {
        var publicizedAnyMemberInAssembly = false;
        var doNotPublicizePropertyMethods = new HashSet<MethodDef>();

        // TYPES
        foreach (var typeDef in module.GetTypes())
        {
            doNotPublicizePropertyMethods.Clear();

            var publicizedAnyMemberInType = false;
            var typeName = typeDef.ReflectionFullName;

            var explicitlyDoNotPublicizeType = assemblyContext.DoNotPublicizeMemberPatterns.Contains(typeName);

            // PROPERTIES
            foreach (var propertyDef in typeDef.Properties)
            {
                var propertyName = $"{typeName}.{propertyDef.Name}";

                var explicitlyDoNotPublicizeProperty = assemblyContext.DoNotPublicizeMemberPatterns.Contains(propertyName);
                if (explicitlyDoNotPublicizeProperty)
                {
                    if (propertyDef.GetMethod is MethodDef getter)
                    {
                        doNotPublicizePropertyMethods.Add(getter);
                    }
                    if (propertyDef.SetMethod is MethodDef setter)
                    {
                        doNotPublicizePropertyMethods.Add(setter);
                    }
                    continue;
                }

                var explicitlyPublicizeProperty = assemblyContext.PublicizeMemberPatterns.Contains(propertyName);
                if (explicitlyPublicizeProperty)
                {
                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeProperty(propertyDef);
                    continue;
                }

                if (explicitlyDoNotPublicizeType)
                {
                    continue;
                }

                if (assemblyContext.ExplicitlyDoNotPublicizeAssembly)
                {
                    continue;
                }

                if (assemblyContext.ExplicitlyPublicizeAssembly)
                {
                    var isCompilerGeneratedProperty = IsCompilerGenerated(propertyDef);
                    if (isCompilerGeneratedProperty && !assemblyContext.IncludeCompilerGeneratedMembers)
                    {
                        continue;
                    }

                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeProperty(propertyDef, assemblyContext.IncludeVirtualMembers);
                }
            }

            // METHODS
            foreach (var methodDef in typeDef.Methods)
            {
                var methodName = $"{typeName}.{methodDef.Name}";

                var isMethodOfNonPublicizedProperty = doNotPublicizePropertyMethods.Contains(methodDef);
                if (isMethodOfNonPublicizedProperty)
                {
                    continue;
                }

                var explicitlyDoNotPublicizeMethod = assemblyContext.DoNotPublicizeMemberPatterns.Contains(methodName);
                if (explicitlyDoNotPublicizeMethod)
                {
                    continue;
                }

                var explicitlyPublicizeMethod = assemblyContext.PublicizeMemberPatterns.Contains(methodName);
                if (explicitlyPublicizeMethod)
                {
                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeMethod(methodDef);
                    continue;
                }

                if (explicitlyDoNotPublicizeType)
                {
                    continue;
                }

                if (assemblyContext.ExplicitlyDoNotPublicizeAssembly)
                {
                    continue;
                }

                if (assemblyContext.ExplicitlyPublicizeAssembly)
                {
                    var isCompilerGeneratedMethod = IsCompilerGenerated(methodDef);
                    if (isCompilerGeneratedMethod && !assemblyContext.IncludeCompilerGeneratedMembers)
                    {
                        continue;
                    }

                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeMethod(methodDef, assemblyContext.IncludeVirtualMembers);
                }
            }

            // FIELDS
            foreach (var fieldDef in typeDef.Fields)
            {
                var fieldName = $"{typeName}.{fieldDef.Name}";

                var explicitlyDoNotPublicizeField = assemblyContext.DoNotPublicizeMemberPatterns.Contains(fieldName);
                if (explicitlyDoNotPublicizeField)
                {
                    continue;
                }

                var explicitlyPublicizeField = assemblyContext.PublicizeMemberPatterns.Contains(fieldName);
                if (explicitlyPublicizeField)
                {
                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeField(fieldDef);
                    continue;
                }

                if (explicitlyDoNotPublicizeType)
                {
                    continue;
                }

                if (assemblyContext.ExplicitlyDoNotPublicizeAssembly)
                {
                    continue;
                }

                if (assemblyContext.ExplicitlyPublicizeAssembly)
                {
                    var isCompilerGeneratedField = IsCompilerGenerated(fieldDef);
                    if (isCompilerGeneratedField && !assemblyContext.IncludeCompilerGeneratedMembers)
                    {
                        continue;
                    }

                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeField(fieldDef);
                }
            }

            if (publicizedAnyMemberInType)
            {
                AssemblyEditor.PublicizeType(typeDef);
                publicizedAnyMemberInAssembly = true;
            }

            if (explicitlyDoNotPublicizeType)
            {
                continue;
            }

            var explicitlyPublicizeType = assemblyContext.PublicizeMemberPatterns.Contains(typeName);
            if (explicitlyPublicizeType)
            {
                publicizedAnyMemberInAssembly |= AssemblyEditor.PublicizeType(typeDef);
                continue;
            }

            if (assemblyContext.ExplicitlyDoNotPublicizeAssembly)
            {
                continue;
            }

            if (assemblyContext.ExplicitlyPublicizeAssembly)
            {
                var isCompilerGeneratedType = IsCompilerGenerated(typeDef);
                if (isCompilerGeneratedType && !assemblyContext.IncludeCompilerGeneratedMembers)
                {
                    continue;
                }

                publicizedAnyMemberInAssembly |= AssemblyEditor.PublicizeType(typeDef);
            }
        }

        return publicizedAnyMemberInAssembly;
    }

    private static bool IsCompilerGenerated(IHasCustomAttribute memberDef)
    {
        return memberDef.CustomAttributes.Any(x => x.TypeFullName == CompilerGeneratedFullName);
    }
}
