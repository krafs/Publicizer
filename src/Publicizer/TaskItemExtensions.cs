using Microsoft.Build.Framework;

namespace Publicizer;

internal static class TaskItemExtensions
{
    internal static string FileName(this ITaskItem item)
    {
        return item.GetMetadata("Filename");
    }

    internal static string FullPath(this ITaskItem item)
    {
        return item.GetMetadata("Fullpath");
    }

    internal static bool IncludeCompilerGeneratedMembers(this ITaskItem item)
    {
        if (bool.TryParse(item.GetMetadata("IncludeCompilerGeneratedMembers"), out var includeCompilerGeneratedMembers))
        {
            return includeCompilerGeneratedMembers;
        }

        return true;
    }

    internal static bool IncludeVirtualMembers(this ITaskItem item)
    {
        if (bool.TryParse(item.GetMetadata("IncludeVirtualMembers"), out var includeVirtualMembers))
        {
            return includeVirtualMembers;
        }

        return true;
    }
}
