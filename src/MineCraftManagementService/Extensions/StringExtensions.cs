using System.Text.RegularExpressions;

namespace MineCraftManagementService.Extensions;

public static class stringExtensions
{
    public static bool TryGetVersionFromConsoleOutput(string consoleOutputLine, out string version)
    {
        version = string.Empty;
        var match = Regex.Match(consoleOutputLine, @"(\d+\.\d+\.\d+\.\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            version = match.Groups[1].Value;
            return true;
        }
        return false;
    }
}
