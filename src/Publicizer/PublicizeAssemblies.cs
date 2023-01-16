using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Publicizer;
public class PublicizeAssemblies : Task
{
    internal static readonly string CompilerGeneratedFullName = typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute).FullName;

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

        Dictionary<string, PublicizerAssemblyContext> assemblyContexts = GetPublicizerAssemblyContexts(Publicizes, DoNotPublicizes);
        var referencePathsToDelete = new List<ITaskItem>();
        var referencePathsToAdd = new List<ITaskItem>();

        foreach (ITaskItem reference in ReferencePaths)
        {
            string assemblyName = reference.FileName();

            if (!assemblyContexts.TryGetValue(assemblyName, out PublicizerAssemblyContext? assemblyContext))
            {
                continue;
            }

            string assemblyPath = reference.FullPath();

            string hash = ComputeHash(assemblyPath, assemblyContext);

            string outputAssemblyFolder = Path.Combine(OutputDirectory, $"{assemblyName}.{hash}");
            Directory.CreateDirectory(outputAssemblyFolder);
            string outputAssemblyPath = Path.Combine(outputAssemblyFolder, assemblyName + ".dll");
            if (!File.Exists(outputAssemblyPath))
            {
                using ModuleDef module = ModuleDefMD.Load(assemblyPath);

                bool isAssemblyModified = PublicizeAssembly(module, assemblyContext);
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

            string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
            string originalDocumentationFullPath = Path.Combine(assemblyDirectory, assemblyName + ".xml");

            if (File.Exists(originalDocumentationFullPath))
            {
                string newDocumentationRelativePath = Path.Combine(outputAssemblyFolder, assemblyName + ".xml");
                string newDocumentationFullPath = Path.GetFullPath(newDocumentationRelativePath);
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

        foreach (ITaskItem item in publicizeItems)
        {
            int index = item.ItemSpec.IndexOf(':');
            bool isAssemblyPattern = index == -1;
            string assemblyName = isAssemblyPattern ? item.ItemSpec : item.ItemSpec.Substring(0, index);

            if (!contexts.TryGetValue(assemblyName, out PublicizerAssemblyContext? assemblyContext))
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
                string memberPattern = item.ItemSpec.Substring(index + 1);
                assemblyContext.PublicizeMemberPatterns.Add(memberPattern);
            }
        }

        foreach (ITaskItem item in doNotPublicizeItems)
        {
            int index = item.ItemSpec.IndexOf(':');
            bool isAssemblyPattern = index == -1;
            string assemblyName = isAssemblyPattern ? item.ItemSpec : item.ItemSpec.Substring(0, index);

            if (!contexts.TryGetValue(assemblyName, out PublicizerAssemblyContext? assemblyContext))
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
                string memberPattern = item.ItemSpec.Substring(index + 1);
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
        foreach (string publicizePattern in assemblyContext.PublicizeMemberPatterns)
        {
            sb.Append(publicizePattern);
        }
        foreach (string doNotPublicizePattern in assemblyContext.DoNotPublicizeMemberPatterns)
        {
            sb.Append(doNotPublicizePattern);
        }

        byte[] patternbytes = Encoding.UTF8.GetBytes(sb.ToString());
        byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
        byte[] allBytes = assemblyBytes.Concat(patternbytes).ToArray();

        return Hasher.ComputeHash(allBytes);
    }

    private static bool PublicizeAssembly(ModuleDef module, PublicizerAssemblyContext assemblyContext)
    {
        bool publicizedAnyMemberInAssembly = false;
        var doNotPublicizePropertyMethods = new HashSet<MethodDef>();

        // TYPES
        foreach (TypeDef? typeDef in module.GetTypes())
        {
            doNotPublicizePropertyMethods.Clear();

            bool publicizedAnyMemberInType = false;
            string typeName = typeDef.ReflectionFullName;

            bool explicitlyDoNotPublicizeType = assemblyContext.DoNotPublicizeMemberPatterns.Contains(typeName);

            // PROPERTIES
            foreach (PropertyDef? propertyDef in typeDef.Properties)
            {
                string propertyName = $"{typeName}.{propertyDef.Name}";

                bool explicitlyDoNotPublicizeProperty = assemblyContext.DoNotPublicizeMemberPatterns.Contains(propertyName);
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

                bool explicitlyPublicizeProperty = assemblyContext.PublicizeMemberPatterns.Contains(propertyName);
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
                    bool isCompilerGeneratedProperty = IsCompilerGenerated(propertyDef);
                    if (isCompilerGeneratedProperty && !assemblyContext.IncludeCompilerGeneratedMembers)
                    {
                        continue;
                    }

                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeProperty(propertyDef, assemblyContext.IncludeVirtualMembers);
                }
            }

            // METHODS
            foreach (MethodDef? methodDef in typeDef.Methods)
            {
                string methodName = $"{typeName}.{methodDef.Name}";

                bool isMethodOfNonPublicizedProperty = doNotPublicizePropertyMethods.Contains(methodDef);
                if (isMethodOfNonPublicizedProperty)
                {
                    continue;
                }

                bool explicitlyDoNotPublicizeMethod = assemblyContext.DoNotPublicizeMemberPatterns.Contains(methodName);
                if (explicitlyDoNotPublicizeMethod)
                {
                    continue;
                }

                bool explicitlyPublicizeMethod = assemblyContext.PublicizeMemberPatterns.Contains(methodName);
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
                    bool isCompilerGeneratedMethod = IsCompilerGenerated(methodDef);
                    if (isCompilerGeneratedMethod && !assemblyContext.IncludeCompilerGeneratedMembers)
                    {
                        continue;
                    }

                    publicizedAnyMemberInType |= AssemblyEditor.PublicizeMethod(methodDef, assemblyContext.IncludeVirtualMembers);
                }
            }

            // FIELDS
            foreach (FieldDef? fieldDef in typeDef.Fields)
            {
                string fieldName = $"{typeName}.{fieldDef.Name}";

                bool explicitlyDoNotPublicizeField = assemblyContext.DoNotPublicizeMemberPatterns.Contains(fieldName);
                if (explicitlyDoNotPublicizeField)
                {
                    continue;
                }

                bool explicitlyPublicizeField = assemblyContext.PublicizeMemberPatterns.Contains(fieldName);
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
                    bool isCompilerGeneratedField = IsCompilerGenerated(fieldDef);
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
                continue;
            }

            if (explicitlyDoNotPublicizeType)
            {
                continue;
            }

            bool explicitlyPublicizeType = assemblyContext.PublicizeMemberPatterns.Contains(typeName);
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
                bool isCompilerGeneratedType = IsCompilerGenerated(typeDef);
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
