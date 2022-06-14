using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using dnlib.DotNet;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Publicizer
{
    public class PublicizeAssemblies : Task
    {
        public ITaskItem[]? ReferencePaths { get; set; }
        public ITaskItem[]? Publicizes { get; set; }
        public ITaskItem[]? DoNotPublicizes { get; set; }
        public string? OutputDirectory { get; set; }
        public string? PublicizeAsReferenceAssemblies { get; set; }
        public string? PublicizeCompilerGenerated { get; set; }

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

            if (!bool.TryParse(PublicizeAsReferenceAssemblies, out bool publicizeAsReferenceAssemblies))
            {
                Log.LogError(nameof(PublicizeAsReferenceAssemblies) + " cannot be parsed as bool.");
                return false;
            }

            if (!bool.TryParse(PublicizeCompilerGenerated, out bool publicizeCompilerGenerated))
            {
                Log.LogError(nameof(PublicizeCompilerGenerated) + " cannot be parsed as bool.");
                return false;
            }

            if (DoNotPublicizes is null)
            {
                DoNotPublicizes = Array.Empty<ITaskItem>();
            }

            Directory.CreateDirectory(OutputDirectory);

            Dictionary<string, List<string>> publicizeDict = new Dictionary<string, List<string>>();
            foreach (ITaskItem item in Publicizes)
            {
                int index = item.ItemSpec.IndexOf(':');
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

                if (!publicizeDict.TryGetValue(assemblyName, out List<string> publicizes))
                {
                    publicizes = new List<string>();
                    publicizeDict.Add(assemblyName, publicizes);
                }

                publicizes.Add(pattern);
            }

            Dictionary<string, List<string>> doNotPublicizeDict = new Dictionary<string, List<string>>();
            foreach (ITaskItem item in DoNotPublicizes)
            {
                int index = item.ItemSpec.IndexOf(':');
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

                if (!doNotPublicizeDict.TryGetValue(assemblyName, out List<string> doNotPublicizes))
                {
                    doNotPublicizes = new List<string>();
                    doNotPublicizeDict.Add(assemblyName, doNotPublicizes);
                }

                doNotPublicizes.Add(pattern);
            }

            List<ITaskItem> referencePathsToDelete = new List<ITaskItem>();
            List<ITaskItem> referencePathsToAdd = new List<ITaskItem>();

            foreach (ITaskItem reference in ReferencePaths)
            {
                string assemblyName = reference.GetFileName();

                if (!publicizeDict.TryGetValue(assemblyName, out List<string> assemblyPublicizes))
                {
                    continue;
                }

                doNotPublicizeDict.TryGetValue(assemblyName, out List<string> assemblyDoNotPublicizes);
                assemblyDoNotPublicizes ??= new List<string>();

                string assemblyPath = reference.GetFullPath();

                string hash = ComputeHash(assemblyPath, assemblyPublicizes, assemblyDoNotPublicizes);

                string publicizedAssemblyName = $"{assemblyName}.{hash}.dll";
                string outputAssemblyPath = Path.Combine(OutputDirectory, publicizedAssemblyName);
                if (!File.Exists(outputAssemblyPath))
                {
                    using ModuleDef module = ModuleDefMD.Load(assemblyPath);

                    PublicizeAssembly(module, assemblyPublicizes, assemblyDoNotPublicizes, publicizeAsReferenceAssemblies, publicizeCompilerGenerated);

                    using FileStream fileStream = new FileStream(outputAssemblyPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
                    module.Write(fileStream);
                }

                referencePathsToDelete.Add(reference);
                ITaskItem newReference = new TaskItem(outputAssemblyPath);
                reference.CopyMetadataTo(newReference);
                referencePathsToAdd.Add(newReference);
            }

            ReferencePathsToDelete = referencePathsToDelete.ToArray();
            ReferencePathsToAdd = referencePathsToAdd.ToArray();

            return true;
        }

        private static string ComputeHash(string assemblyPath, List<string> publicizes, List<string> doNotPublicizes)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string publicizePattern in publicizes)
            {
                sb.Append(publicizePattern);
            }
            foreach (string doNotPublicizePattern in doNotPublicizes)
            {
                sb.Append(doNotPublicizePattern);
            }

            byte[] patternbytes = Encoding.UTF8.GetBytes(sb.ToString());
            byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
            byte[] allBytes = assemblyBytes.Concat(patternbytes).ToArray();

            return Hasher.ComputeHash(allBytes);
        }

        private const string s_compilerGeneratedAttribute = "CompilerGeneratedAttribute";

        private static void PublicizeAssembly(
            ModuleDef module,
            List<string> publicizePatterns,
            List<string> doNotPublicizePatterns,
            bool publicizeAsReferenceAssemblies,
            bool publicizeCompilerGenerated)
        {
            bool publicizeAll = publicizePatterns.Contains(module.Assembly.Name);
            var doNotPublicizePropertyMethods = new List<MethodDef>();

            // TYPES
            foreach (TypeDef typeDef in module.GetTypes())
            {
                doNotPublicizePropertyMethods.Clear();

                bool publicizedAnyMember = false;
                string typeName = typeDef.ReflectionFullName;

                if (doNotPublicizePatterns.Contains(typeName))
                {
                    continue;
                }

                // PROPERTIES
                foreach (PropertyDef propertyDef in typeDef.Properties)
                {
                    string propertyName = $"{typeName}.{propertyDef.Name}";

                    bool forcePublicizeMethod = publicizePatterns.Contains(propertyName);

                    if (publicizeAll)
                    {
                        if (!forcePublicizeMethod)
                        {
                            if (doNotPublicizePatterns.Contains(propertyName))
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

                            if (!publicizeCompilerGenerated && propertyDef.HasCustomAttributes &&
                                propertyDef.CustomAttributes.Any(x => x.AttributeType.TypeName == s_compilerGeneratedAttribute))
                            {
                                continue;
                            }
                        }
                    }
                    else if (!forcePublicizeMethod)
                    {
                        continue;
                    }

                    AssemblyEditor.PublicizeProperty(propertyDef, publicizeAsReferenceAssemblies);
                    publicizedAnyMember = true;
                }

                // METHODS
                foreach (MethodDef methodDef in typeDef.Methods)
                {
                    string methodName = $"{typeName}.{methodDef.Name}";

                    bool forcePublicizeMethod = publicizePatterns.Contains(methodName);

                    if (publicizeAll)
                    {
                        if (!forcePublicizeMethod)
                        {
                            if (doNotPublicizePatterns.Contains(methodName))
                            {
                                continue;
                            }

                            if (!publicizeCompilerGenerated && methodDef.HasCustomAttributes &&
                                methodDef.CustomAttributes.Any(x => x.AttributeType.TypeName == s_compilerGeneratedAttribute))
                            {
                                continue;
                            }

                            if (doNotPublicizePropertyMethods.Contains(methodDef))
                            {
                                continue;
                            }
                        }
                    }
                    else if (!forcePublicizeMethod)
                    {
                        continue;
                    }

                    AssemblyEditor.PublicizeMethod(methodDef, publicizeAsReferenceAssemblies);
                    publicizedAnyMember = true;
                }

                // FIELDS
                foreach (FieldDef fieldDef in typeDef.Fields)
                {
                    string fieldName = $"{typeName}.{fieldDef.Name}";

                    bool forcePublicizeField = publicizePatterns.Contains(fieldName);

                    if (publicizeAll)
                    {
                        if (!forcePublicizeField)
                        {
                            if (doNotPublicizePatterns.Contains(fieldName))
                            {
                                continue;
                            }

                            if (!publicizeCompilerGenerated && fieldDef.HasCustomAttributes &&
                                fieldDef.CustomAttributes.Any(x => x.AttributeType.TypeName == s_compilerGeneratedAttribute))
                            {
                                continue;
                            }
                        }
                    }
                    else if (!forcePublicizeField)
                    {
                        continue;
                    }

                    AssemblyEditor.PublicizeField(fieldDef);
                    publicizedAnyMember = true;
                }

                bool forcePublicizeType = publicizePatterns.Contains(typeName);

                if (publicizeAll)
                {
                    if (!forcePublicizeType && !publicizeCompilerGenerated && typeDef.HasCustomAttributes &&
                        typeDef.CustomAttributes.Any(x => x.AttributeType.TypeName == s_compilerGeneratedAttribute))
                    {
                        continue;
                    }
                }
                else if (!publicizedAnyMember && !forcePublicizeType)
                {
                    continue;
                }

                AssemblyEditor.PublicizeType(typeDef);
            }
        }
    }
}
