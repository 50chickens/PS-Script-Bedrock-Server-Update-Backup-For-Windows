using MineCraftManagementService.Models;
using Newtonsoft.Json.Linq;
using System.Text.RegularExpressions;

namespace MineCraftManagementService.Extensions;

public static class StringExtensions
{
    /// <summary>
    /// Extracts version number from console output using regex pattern.
    /// </summary>
    /// <param name="consoleOutputLine">The console output line to parse</param>
    /// <param name="version">The extracted version string</param>
    /// <returns>True if version was found, false otherwise</returns>
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

    /// <summary>
    /// Attempts to parse MineCraft server download information from JSON content.
    /// </summary>
    /// <param name="jsonContent">The JSON content to parse</param>
    /// <param name="mineCraftServer">The parsed server download information</param>
    /// <returns>True if parsing succeeded, false otherwise</returns>
    public static bool TryGetMineCraftServer(this string jsonContent, out MineCraftServerDownload mineCraftServer)
    {
        JObject jObject = JObject.Parse(jsonContent);
        var jTokens = jObject["result"]?["links"];
        if (jTokens != null)
        {
            var bedrockWindowsLink = jTokens.Children().FirstOrDefault(token => token["downloadType"]?.ToString() == "serverBedrockWindows");
            if (bedrockWindowsLink != null)
            {
                var downloadUrl = bedrockWindowsLink["downloadUrl"]?.ToString();
                if (!string.IsNullOrEmpty(downloadUrl))
                {
                    var match = Regex.Match(downloadUrl, @"bedrock-server-(\d+\.\d+\.\d+\.\d+)\.zip", RegexOptions.IgnoreCase);
                    if (match.Success)
                    {
                        var version = match.Groups[1].Value;
                        mineCraftServer = new MineCraftServerDownload
                        {
                            Version = version,
                            Url = downloadUrl
                        };
                        return true;
                    }
                }
            }
        }
        mineCraftServer = null!;
        return false;
    }
}
