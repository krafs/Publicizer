using System;
using Microsoft.Build.Framework;

namespace Publicizer
{
    public static class TaskItemExtensions
    {
        public static string GetPattern(this ITaskItem item)
        {
            return item.GetMetadata(ItemConstants.Publicize.Pattern);
        }

        public static int GetPriority(this ITaskItem item)
        {
            if (!int.TryParse(item.GetMetadata(ItemConstants.Publicize.Priority), out int priority))
            {
                priority = 0;
            }

            return priority;
        }

        public static string GetFileName(this ITaskItem item)
        {
            return item.GetMetadata(ItemConstants.FileName);
        }

        public static string[] GetIgnorePatterns(this ITaskItem item)
        {
            string ignorePatternValue = item.GetMetadata("IgnorePatterns");
            if (string.IsNullOrWhiteSpace(ignorePatternValue))
            {
                return Array.Empty<string>();
            }

            return ignorePatternValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }

        public static string GetFullPath(this ITaskItem item)
        {
            return item.GetMetadata(ItemConstants.FullPath);
        }

        public static bool GetIgnore(this ITaskItem item)
        {
            string ignore = item.GetMetadata(ItemConstants.Publicize.Ignore);
            bool shouldIgnore = ignore?.Equals(bool.TrueString, StringComparison.OrdinalIgnoreCase) ?? false;

            return shouldIgnore;
        }

        public static void SetReferenceAssemblyPath(this ITaskItem item, string path)
        {
            item.SetMetadata(ItemConstants.ReferencePath.ReferenceAssembly, path);
        }

        public static string GetRawParameterTypes(this ITaskItem item)
        {
            return item.GetMetadata(ItemConstants.Publicize.ParameterTypes);
        }

        public static string[] GetParameterTypes(this ITaskItem item)
        {
            string parameterTypesValue = item.GetRawParameterTypes();
            if (string.IsNullOrWhiteSpace(parameterTypesValue))
            {
                return Array.Empty<string>();
            }

            return parameterTypesValue.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}
