using dnlib.DotNet;
using dnlib.DotNet.Writer;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Task = Microsoft.Build.Utilities.Task;

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
                string? directory = Path.GetDirectoryName(LogFilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
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

        Publicizes ??= [];
        DoNotPublicizes ??= [];

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

        if (!TryGetPublicizerAssemblyContexts(Publicizes, DoNotPublicizes, logger, out Dictionary<string, PublicizerAssemblyContext> assemblyContexts))
        {
            return false;
        }

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
                using var module = ModuleDefMD.Load(assemblyPath);
                scopedLogger.Info("Publicizing members...");
                bool isAssemblyModified = PublicizeAssembly(module, assemblyContext, scopedLogger);
                if (!isAssemblyModified)
                {
                    scopedLogger.Warning("Assembly is marked for publicization, but no members were publicized");
                    continue;
                }

                scopedLogger.Info($"Saving publicized assembly to {outputAssemblyPath}");

                if (!InPlaceWriter.TryWrite(module, assemblyPath, outputAssemblyPath, scopedLogger))
                {
                    using var fileStream = new FileStream(outputAssemblyPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);

                    var writerOptions = new ModuleWriterOptions(module)
                    {
                        // Writing the module sometime fails without this flag due to how it was originally compiled.
                        // https://github.com/krafs/Publicizer/issues/42
                        MetadataOptions = new MetadataOptions(MetadataFlags.KeepOldMaxStack),
                        Logger = DummyLogger.NoThrowInstance
                    };
                    module.Write(fileStream, writerOptions);
                }

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
            referencePathsToAdd.Add(newReference);
            scopedLogger.Info("Assembly processing finished");
        }

        ReferencePathsToDelete = [.. referencePathsToDelete];
        ReferencePathsToAdd = [.. referencePathsToAdd];

        logger.Info($"Finished processing {assemblyContexts.Count} assemblies. Terminating task.");

        return true;
    }

    /// <summary>
    /// Parses every item into per-assembly contexts, returning <see langword="false"/> if any item
    /// was malformed. All items are parsed regardless, so one bad target does not hide the rest.
    /// </summary>
    internal static bool TryGetPublicizerAssemblyContexts(
        ITaskItem[] publicizeItems,
        ITaskItem[] doNotPublicizeItems,
        ITaskLogger logger,
        out Dictionary<string, PublicizerAssemblyContext> contexts)
    {
        contexts = [];
        bool valid = true;

        foreach (ITaskItem item in publicizeItems)
        {
            valid &= PublicizeItemParser.TryApply(item, deny: false, contexts, logger);
        }

        foreach (ITaskItem item in doNotPublicizeItems)
        {
            valid &= PublicizeItemParser.TryApply(item, deny: true, contexts, logger);
        }

        return valid;
    }

    /// <summary>
    /// Publicizes a type and every type enclosing it, because a nested type is unreachable unless
    /// all its enclosers are too. The walk stops at an encloser named in <c>DoNotPublicize</c>:
    /// reaching it is the engine's own inference, and inference yields to an explicit rule. The
    /// type itself is never subject to that check - it was already decided by the caller, where an
    /// explicitly publicized member outranks an excluded type.
    /// </summary>
    private static bool PublicizeTypeAndEnclosers(TypeDef type, AssemblyPlan plan)
    {
        bool modified = AssemblyEditor.PublicizeType(type);

        for (TypeDef? enclosing = type.DeclaringType; enclosing is not null; enclosing = enclosing.DeclaringType)
        {
            if (plan.IsDeniedType(enclosing.ReflectionFullName))
            {
                break;
            }

            modified |= AssemblyEditor.PublicizeType(enclosing);
        }

        return modified;
    }

    internal static bool PublicizeAssembly(ModuleDef module, PublicizerAssemblyContext assemblyContext, ITaskLogger logger)
    {
        var assemblyPlan = AssemblyPlan.Compile(assemblyContext);

        bool publicizedAnyMemberInAssembly = false;
        var doNotPublicizePropertyMethods = new HashSet<MethodDef>();

        int publicizedTypesCount = 0;
        int publicizedPropertiesCount = 0;
        int publicizedMethodsCount = 0;
        int publicizedFieldsCount = 0;

        foreach (TypeDef? typeDef in module.GetTypes())
        {
            string typeName = typeDef.ReflectionFullName;
            if (assemblyPlan.ForType(typeName, NamespaceOf(typeDef)) is not TypePlan typePlan)
            {
                // No rule can reach anything in this type; skip it without touching a member.
                continue;
            }

            doNotPublicizePropertyMethods.Clear();
            bool publicizedAnyMemberInType = false;

            bool needsCompilerGeneratedCheck = typePlan.NeedsCompilerGeneratedCheck;
            bool allMembersDecided = typePlan.TryDecideAllMembers(out PublicizeDecision uniformDecision);
            bool anyMemberCanBePublicized = !allMembersDecided || uniformDecision != PublicizeDecision.Skip;

            // Properties are walked before methods so that a denied property can suppress its own
            // accessors when the method loop reaches them.
            if (anyMemberCanBePublicized)
            {
                foreach (PropertyDef? propertyDef in typeDef.Properties)
                {
                    PublicizeDecision decision = allMembersDecided
                        ? uniformDecision
                        : typePlan.DecideMember(propertyDef.Name, needsCompilerGeneratedCheck && IsCompilerGenerated(propertyDef));

                    switch (decision)
                    {
                        case PublicizeDecision.DeniedExplicitly:
                            if (propertyDef.GetMethod is MethodDef getter)
                            {
                                doNotPublicizePropertyMethods.Add(getter);
                            }
                            if (propertyDef.SetMethod is MethodDef setter)
                            {
                                doNotPublicizePropertyMethods.Add(setter);
                            }
                            logger.Verbose($"Explicitly ignoring property: {typePlan.FullNameOf(propertyDef.Name)}");
                            break;

                        case PublicizeDecision.Explicit:
                            // Naming a member is an unambiguous opt-in that outranks IncludeVirtualMembers.
                            if (AssemblyEditor.PublicizeProperty(propertyDef, includeVirtual: true))
                            {
                                publicizedAnyMemberInType = true;
                                publicizedAnyMemberInAssembly = true;
                                publicizedPropertiesCount++;
                                logger.Verbose($"Explicitly publicizing property: {typePlan.FullNameOf(propertyDef.Name)}");
                            }
                            break;

                        case PublicizeDecision.ByAssemblyRule:
                            if (AssemblyEditor.PublicizeProperty(propertyDef, typePlan.IncludeVirtualMembers))
                            {
                                publicizedAnyMemberInType = true;
                                publicizedAnyMemberInAssembly = true;
                                publicizedPropertiesCount++;
                            }
                            break;

                        case PublicizeDecision.Skip:
                        default:
                            break;
                    }
                }

                foreach (MethodDef? methodDef in typeDef.Methods)
                {
                    if (doNotPublicizePropertyMethods.Contains(methodDef))
                    {
                        continue;
                    }

                    PublicizeDecision decision = allMembersDecided
                        ? uniformDecision
                        : typePlan.DecideMember(methodDef.Name, needsCompilerGeneratedCheck && IsCompilerGenerated(methodDef));

                    switch (decision)
                    {
                        case PublicizeDecision.DeniedExplicitly:
                            logger.Verbose($"Explicitly ignoring method: {typePlan.FullNameOf(methodDef.Name)}");
                            break;

                        case PublicizeDecision.Explicit:
                            if (AssemblyEditor.PublicizeMethod(methodDef, includeVirtual: true))
                            {
                                publicizedAnyMemberInType = true;
                                publicizedAnyMemberInAssembly = true;
                                publicizedMethodsCount++;
                                logger.Verbose($"Explicitly publicizing method: {typePlan.FullNameOf(methodDef.Name)}");
                            }
                            break;

                        case PublicizeDecision.ByAssemblyRule:
                            if (AssemblyEditor.PublicizeMethod(methodDef, typePlan.IncludeVirtualMembers))
                            {
                                publicizedAnyMemberInType = true;
                                publicizedAnyMemberInAssembly = true;
                                publicizedMethodsCount++;
                            }
                            break;

                        case PublicizeDecision.Skip:
                        default:
                            break;
                    }
                }

                foreach (FieldDef? fieldDef in typeDef.Fields)
                {
                    PublicizeDecision decision = allMembersDecided
                        ? uniformDecision
                        : typePlan.DecideMember(fieldDef.Name, needsCompilerGeneratedCheck && IsCompilerGenerated(fieldDef));

                    switch (decision)
                    {
                        case PublicizeDecision.DeniedExplicitly:
                            logger.Verbose($"Explicitly ignoring field: {typePlan.FullNameOf(fieldDef.Name)}");
                            break;

                        case PublicizeDecision.Explicit:
                        case PublicizeDecision.ByAssemblyRule:
                            // IncludeVirtualMembers has no meaning for fields.
                            if (AssemblyEditor.PublicizeField(fieldDef))
                            {
                                publicizedAnyMemberInType = true;
                                publicizedAnyMemberInAssembly = true;
                                publicizedFieldsCount++;
                                if (decision == PublicizeDecision.Explicit)
                                {
                                    logger.Verbose($"Explicitly publicizing field: {typePlan.FullNameOf(fieldDef.Name)}");
                                }
                            }
                            break;

                        case PublicizeDecision.Skip:
                        default:
                            break;
                    }
                }
            }

            // A type with any publicized member is publicized regardless of its own rules — a
            // publicized member is useless in an inaccessible type.
            if (publicizedAnyMemberInType)
            {
                if (PublicizeTypeAndEnclosers(typeDef, assemblyPlan))
                {
                    publicizedAnyMemberInAssembly = true;
                    publicizedTypesCount++;
                }
                continue;
            }

            PublicizeDecision typeDecision = typePlan.DecideType(needsCompilerGeneratedCheck && IsCompilerGenerated(typeDef));
            switch (typeDecision)
            {
                case PublicizeDecision.DeniedExplicitly:
                    logger.Verbose($"Explicitly ignoring type: {typeName}");
                    break;

                case PublicizeDecision.Explicit:
                case PublicizeDecision.ByAssemblyRule:
                    if (PublicizeTypeAndEnclosers(typeDef, assemblyPlan))
                    {
                        publicizedAnyMemberInAssembly = true;
                        publicizedTypesCount++;
                        if (typeDecision == PublicizeDecision.Explicit)
                        {
                            logger.Verbose($"Explicitly publicizing type: {typeName}");
                        }
                    }
                    break;

                case PublicizeDecision.Skip:
                default:
                    break;
            }
        }

        logger.Info("Publicized types: " + publicizedTypesCount);
        logger.Info("Publicized properties: " + publicizedPropertiesCount);
        logger.Info("Publicized methods: " + publicizedMethodsCount);
        logger.Info("Publicized fields: " + publicizedFieldsCount);

        return publicizedAnyMemberInAssembly;
    }

    /// <summary>
    /// The namespace a type belongs to. dnlib leaves <see cref="TypeDef.Namespace"/> empty on nested
    /// types, so the namespace has to come from the outermost enclosing type.
    /// </summary>
    private static string NamespaceOf(TypeDef typeDef)
    {
        TypeDef outermost = typeDef;
        while (outermost.DeclaringType is TypeDef declaringType)
        {
            outermost = declaringType;
        }

        return outermost.Namespace?.String ?? "";
    }

    private static bool IsCompilerGenerated(IHasCustomAttribute memberDef) => memberDef.CustomAttributes.Any(x => x.TypeFullName == "System.Runtime.CompilerServices.CompilerGeneratedAttribute");
}
