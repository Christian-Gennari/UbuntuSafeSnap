namespace UbuntuSafeSnap.Services;

public static class ExclusionService
{
    private static readonly HashSet<string> ForbiddenExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".env", ".key", ".pem"
    };

    private static readonly HashSet<string> ForbiddenFilenames = new(StringComparer.OrdinalIgnoreCase)
    {
        "secrets.json", "secrets.lua"
    };

    public static bool ShouldExclude(string filePath)
    {
        return ForbiddenExtensions.Contains(Path.GetExtension(filePath))
            || ForbiddenFilenames.Contains(Path.GetFileName(filePath));
    }
}
