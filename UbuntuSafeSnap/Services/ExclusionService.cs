namespace UbuntuSafeSnap.Services;

public static class ExclusionService
{
    private static readonly HashSet<string> ForbiddenExtensions = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> ForbiddenFilenames = new(StringComparer.OrdinalIgnoreCase);

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

    public static void Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"[ExclusionService] Exclusion file not found: {filePath}",
                filePath);
        }

        ForbiddenExtensions.Clear();
        ForbiddenFilenames.Clear();

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            if (line.StartsWith('.'))
            {
                ForbiddenExtensions.Add(line);
            }
            else
            {
                ForbiddenFilenames.Add(line);
            }
        }
    }

    public static bool ShouldExclude(string filePath)
    {
        return ForbiddenExtensions.Contains(Path.GetExtension(filePath))
            || ForbiddenFilenames.Contains(Path.GetFileName(filePath));
    }
}