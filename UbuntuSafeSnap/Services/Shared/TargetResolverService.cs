using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Shared;

/// <summary>
/// Resolves target entries from targets.txt into absolute filesystem paths.
/// Handles ~ expansion, sudo-aware home directory detection, and skips the home directory itself.
/// </summary>
public class TargetResolverService
{
    /// <summary>
    /// Reads targets.txt, expands ~ to the real user's home directory (sudo-aware),
    /// resolves full paths, and skips entries that resolve to the home directory itself.
    /// </summary>
    /// <param name="filePath">Path to the targets.txt file.</param>
    /// <returns>Resolved absolute directory paths to back up.</returns>
    public IEnumerable<string> Resolve(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Target file not found: {filePath}",
                filePath);
        }

        string homeDirectory = UserHomeHelper.GetRealUserHome();

        foreach (var raw in File.ReadAllLines(filePath))
        {
            var line = raw.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            string expanded = line.StartsWith('~')
                ? homeDirectory + line[1..]
                : line;

            string fullPath = Path.GetFullPath(expanded);

            if (fullPath == homeDirectory)
            {
                Log.Info("TargetResolverService", $"Skipping home directory: {fullPath}");
                continue;
            }

            yield return fullPath;
        }
    }

}