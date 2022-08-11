using Microsoft.Build.Framework;

namespace Publicizer;

internal static class TaskItemExtensions
{
    internal static string GetFileName(this ITaskItem item)
    {
        return item.GetMetadata(ItemConstants.FileName);
    }

    internal static string GetFullPath(this ITaskItem item)
    {
        return item.GetMetadata(ItemConstants.FullPath);
    }
}
