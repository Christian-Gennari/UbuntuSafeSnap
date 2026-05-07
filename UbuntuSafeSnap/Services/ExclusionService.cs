using UbuntuSafeSnap.Interfaces;

namespace UbuntuSafeSnap.Services;

public class ExclusionService : IExclusionService
{
    private readonly HashSet<string> _forbiddenExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _forbiddenFilenames = new(StringComparer.OrdinalIgnoreCase);

    public ExclusionService(string exclusionsFilePath)
    {
        Load(exclusionsFilePath);
    }

    public static string GetDefaultContent()
    {
        return @"# Exclusion rules for UbuntuSafeSnap
# Files matching these patterns will be excluded from backups.
#
# To exclude by EXTENSION: list the extension with a leading dot
#   .env
#   .key
#   .pem
#
# To exclude by FILENAME: list the filename without a path
#   secrets.json
#   secrets.lua
#
# Lines starting with # are comments and will be ignored.

.env
.key
.pem
secrets.json
secrets.lua";
    }

    public void Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"[ExclusionService] Exclusion file not found: {filePath}",
                filePath);
        }

        _forbiddenExtensions.Clear();
        _forbiddenFilenames.Clear();

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            if (line.StartsWith('.'))
            {
                _forbiddenExtensions.Add(line);
            }
            else
            {
                _forbiddenFilenames.Add(line);
            }
        }
    }

    public bool ShouldExclude(string filePath)
    {
        return _forbiddenExtensions.Contains(Path.GetExtension(filePath))
            || _forbiddenFilenames.Contains(Path.GetFileName(filePath));
    }
}
