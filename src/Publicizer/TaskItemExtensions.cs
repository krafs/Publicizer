using Microsoft.Build.Framework;

namespace Publicizer
{
    public static class TaskItemExtensions
    {
        public static string GetFileName(this ITaskItem item)
        {
            return item.GetMetadata(ItemConstants.FileName);
        }

        public static string GetFullPath(this ITaskItem item)
        {
            return item.GetMetadata(ItemConstants.FullPath);
        }
    }
}
