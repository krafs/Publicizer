using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Publicizer;
public sealed class PublicizeAssemblies : Task
{
    [Required]
    public string OutputDirectory { get; set; } = null!;

    [Required]
    public ITaskItem[] ReferencePaths { get; set; } = null!;

    public ITaskItem[]? Publicizes { get; set; }
    public ITaskItem[]? DoNotPublicizes { get; set; }
    public string? LogFilePath { get; set; }

    [Output]
    public ITaskItem[]? ReferencePathsToDelete { get; set; }

    [Output]
    public ITaskItem[]? ReferencePathsToAdd { get; set; }

    private Logger GetLogger()
    {
        Stream logStream = Stream.Null;
        if (!string.IsNullOrWhiteSpace(LogFilePath))
        {
            try
            {
                string directory = Path.GetDirectoryName(LogFilePath);
                Directory.CreateDirectory(directory);
                logStream = File.Open(LogFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);

                // Ensure log file is empty.
                logStream.SetLength(0);
            }
            catch (Exception e)
            {
                Log.LogError($"Error creating Publicizer log file: {e.Message}");
            }
        }

        return new Logger(Log, logStream);
    }

    public override bool Execute()
    {
        using Logger logger = GetLogger();
        logger.Info($"Initializing assembly publicization");

        Publicizes ??= Array.Empty<ITaskItem>();
        DoNotPublicizes ??= Array.Empty<ITaskItem>();

        logger.Info($"Referenced assemblies: {ReferencePaths.Length}");

        if (Publicizes.Length == 0)
        {
            logger.Info("No Publicizes provided. Terminating task.");
            return true;
        }

        try
        {
            Directory.CreateDirectory(OutputDirectory);
        }
        catch (Exception e)
        {
            logger.Error($"{nameof(OutputDirectory)} '{OutputDirectory}' is not a valid directory path: {e.Message}");
            return false;
        }

        Dictionary<string, PublicizerAssemblyContext> assemblyContexts = GetPublicizerAssemblyContexts(Publicizes, DoNotPublicizes, logger);

        var referencePathsToDelete = new List<ITaskItem>();
        var referencePathsToAdd = new List<ITaskItem>();

        foreach (ITaskItem reference in ReferencePaths)
        {
            string assemblyName = reference.FileName();
            if (!assemblyContexts.TryGetValue(assemblyName, out PublicizerAssemblyContext? assemblyContext))
            {
                continue;
            }

            ITaskLogger scopedLogger = logger.CreateScope(assemblyName);
            scopedLogger.Info($"Assembly processing starting...");
            string assemblyPath = reference.FullPath();
            scopedLogger.Info($"Path: {assemblyPath}");

            string hash = Hasher.ComputeHash(assemblyPath, assemblyContext);
            scopedLogger.Info($"Publicization hash: {hash}");

            string outputAssemblyFolder = Path.Combine(OutputDirectory, $"{assemblyName}.{hash}");
            Directory.CreateDirectory(outputAssemblyFolder);
            string outputAssemblyPath = Path.Combine(outputAssemblyFolder, assemblyName + ".dll");

            if (File.Exists(outputAssemblyPath))
            {
                scopedLogger.Info($"Assembly already publicized at {outputAssemblyPath}");
            }
            else
            {
                using ModuleDef module = ModuleDefMD.Load(assemblyPath);
                scopedLogger.Info("Publicizing members...");
                bool isAssemblyModified = PublicizeAssembly(module, assemblyContext, scopedLogger);
                if (!isAssemblyModified)
                {
                    scopedLogger.Warning("Assembly is marked for publicization, but no members were publicized");
                    continue;
                }

                using var fileStream = new FileStream(outputAssemblyPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

                var writerOptions = new ModuleWriterOptions(module)
                {
                    // Writing the module sometime fails without this flag due to how it was originally compiled.
                    // https://github.com/krafs/Publicizer/issues/42
                    MetadataOptions = new MetadataOptions(MetadataFlags.KeepOldMaxStack),
                    Logger = DummyLogger.NoThrowInstance
                };
                scopedLogger.Info($"Saving publicized assembly to {outputAssemblyPath}");
                module.Write(fileStream, writerOptions);

                string assemblyDirectory = Path.GetDirectoryName(assemblyPath);
                string originalDocumentationFullPath = Path.Combine(assemblyDirectory, assemblyName + ".xml");

                if (File.Exists(originalDocumentationFullPath))
                {
                    scopedLogger.Info($"Found XML documentation at {originalDocumentationFullPath}");
                    string newDocumentationRelativePath = Path.Combine(outputAssemblyFolder, assemblyName + ".xml");
                    string newDocumentationFullPath = Path.GetFullPath(newDocumentationRelativePath);
                    scopedLogger.Info($"Copying XML documentation to {newDocumentationFullPath}");
                    File.Copy(originalDocumentationFullPath, newDocumentationFullPath, overwrite: true);
                }
            }

            referencePathsToDelete.Add(reference);
            ITaskItem newReference = new TaskItem(outputAssemblyPath);
            reference.CopyMetadataTo(newReference);
            reference.SetMetadata("Publicized", bool.TrueString);
            referencePathsToAdd.Add(newReference);
            scopedLogger.Info("Assembly processing finished");
        }

        ReferencePathsToDelete = referencePathsToDelete.ToArray();
        ReferencePathsToAdd = referencePathsToAdd.ToArray();

        logger.Info($"Finished processing {assemblyContexts.Count} assemblies. Terminating task.");

        return true;
    }

    private static Dictionary<string, PublicizerAssemblyContext> GetPublicizerAssemblyContexts(
        ITaskItem[] publicizeItems,
        ITaskItem[] doNotPublicizeItems,
        ITaskLogger logger)
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
                assemblyContext.PublicizeMemberRegexPattern = item.MemberPattern();
                logger.Info($"Publicize: {item}, virtual members: {assemblyContext.IncludeVirtualMembers}, compiler-generated members: {assemblyContext.IncludeCompilerGeneratedMembers}, member pattern: {assemblyContext.PublicizeMemberRegexPattern}");
            }
            else
            {
                string memberPattern = item.ItemSpec.Substring(index + 1);
                assemblyContext.PublicizeMemberPatterns.Add(memberPattern);
                logger.Info($"Publicize: {item}");
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

            logger.Info($"DoNotPublicize: {item}");
        }

        return contexts;
    }

    private static bool PublicizeAssembly(ModuleDef module, PublicizerAssemblyContext assemblyContext, ITaskLogger logger)
    {
        var doNotPublicizePropertyMethods = new HashSet<MethodDef>();

        var assemblyEditor = new AssemblyEditor(module, assemblyContext.AddOriginalAccessModifierAttribute);

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
                    logger.Verbose($"Explicitly ignoring property: {propertyName}");
                    continue;
                }

                bool explicitlyPublicizeProperty = assemblyContext.PublicizeMemberPatterns.Contains(propertyName);
                if (explicitlyPublicizeProperty)
                {
                    if (assemblyEditor.PublicizeProperty(propertyDef))
                    {
                        publicizedAnyMemberInType = true;
                        logger.Verbose($"Explicitly publicizing property: {propertyName}");
                    }
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
                    
                    bool isRegexPatternMatch = assemblyContext.PublicizeMemberRegexPattern?.IsMatch(propertyName) ?? true;
                    if (!isRegexPatternMatch)
                    {
                        continue;
                    }

                    if (assemblyEditor.PublicizeProperty(propertyDef, assemblyContext.IncludeVirtualMembers))
                    {
                        publicizedAnyMemberInType = true;
                    }
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
                    logger.Verbose($"Explicitly ignoring method: {methodName}");
                    continue;
                }

                bool explicitlyPublicizeMethod = assemblyContext.PublicizeMemberPatterns.Contains(methodName);
                if (explicitlyPublicizeMethod)
                {
                    if (assemblyEditor.PublicizeMethod(methodDef))
                    {
                        publicizedAnyMemberInType = true;
                        logger.Verbose($"Explicitly publicizing method: {methodName}");
                    }
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
                    
                    bool isRegexPatternMatch = assemblyContext.PublicizeMemberRegexPattern?.IsMatch(methodName) ?? true;
                    if (!isRegexPatternMatch)
                    {
                        continue;
                    }

                    if (assemblyEditor.PublicizeMethod(methodDef, assemblyContext.IncludeVirtualMembers))
                    {
                        publicizedAnyMemberInType = true;
                    }
                }
            }

            // FIELDS
            foreach (FieldDef? fieldDef in typeDef.Fields)
            {
                string fieldName = $"{typeName}.{fieldDef.Name}";

                bool explicitlyDoNotPublicizeField = assemblyContext.DoNotPublicizeMemberPatterns.Contains(fieldName);
                if (explicitlyDoNotPublicizeField)
                {
                    logger.Verbose($"Explicitly ignoring field: {fieldName}");
                    continue;
                }

                bool explicitlyPublicizeField = assemblyContext.PublicizeMemberPatterns.Contains(fieldName);
                if (explicitlyPublicizeField)
                {
                    if (assemblyEditor.PublicizeField(fieldDef))
                    {
                        publicizedAnyMemberInType = true;
                        logger.Verbose($"Explicitly publicizing field: {fieldName}");
                    }
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
                    
                    bool isRegexPatternMatch = assemblyContext.PublicizeMemberRegexPattern?.IsMatch(fieldName) ?? true;
                    if (!isRegexPatternMatch)
                    {
                        continue;
                    }

                    if (assemblyEditor.PublicizeField(fieldDef))
                    {
                        publicizedAnyMemberInType = true;
                    }
                }
            }

            if (publicizedAnyMemberInType)
            {
                assemblyEditor.PublicizeType(typeDef);
                continue;
            }

            if (explicitlyDoNotPublicizeType)
            {
                logger.Verbose($"Explicitly ignoring type: {typeName}");
                continue;
            }

            bool explicitlyPublicizeType = assemblyContext.PublicizeMemberPatterns.Contains(typeName);
            if (explicitlyPublicizeType)
            {
                if (assemblyEditor.PublicizeType(typeDef))
                {
                    logger.Verbose($"Explicitly publicizing type: {typeName}");
                }
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

                bool isRegexPatternMatch = assemblyContext.PublicizeMemberRegexPattern?.IsMatch(typeName) ?? true;
                if (!isRegexPatternMatch)
                {
                    continue;
                }

                assemblyEditor.PublicizeType(typeDef);
            }
        }

        logger.Info("Publicized types: " + assemblyEditor.publicizedTypesCount);
        logger.Info("Publicized properties: " + assemblyEditor.publicizedPropertiesCount);
        logger.Info("Publicized methods: " + assemblyEditor.publicizedMethodsCount);
        logger.Info("Publicized fields: " + assemblyEditor.publicizedFieldsCount);

        return assemblyEditor.publicizedAnyMemberInAssembly;
    }

    private static bool IsCompilerGenerated(IHasCustomAttribute memberDef)
    {
        return memberDef.CustomAttributes.Any(x => x.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
    }
}
