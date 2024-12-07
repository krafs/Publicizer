using System.Text.RegularExpressions;
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
        if (bool.TryParse(item.GetMetadata("IncludeCompilerGeneratedMembers"), out bool includeCompilerGeneratedMembers))
        {
            return includeCompilerGeneratedMembers;
        }

        return true;
    }

    internal static bool IncludeVirtualMembers(this ITaskItem item)
    {
        if (bool.TryParse(item.GetMetadata("IncludeVirtualMembers"), out bool includeVirtualMembers))
        {
            return includeVirtualMembers;
        }

        return true;
    }
    
    internal static Regex? MemberPattern(this ITaskItem item)
    {
        string? memberPattern = item.GetMetadata("MemberPattern");
        if (string.IsNullOrWhiteSpace(memberPattern))
        {
            return null;
        }

        return new Regex(memberPattern, RegexOptions.Compiled);
    }
}
