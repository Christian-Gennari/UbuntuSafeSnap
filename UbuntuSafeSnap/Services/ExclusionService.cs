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

    public static int EnsureExists(string configPath, bool nonInteractive, out string exclusionsFile)
    {
        exclusionsFile = Path.Combine(configPath, "exclusions.txt");

        if (File.Exists(exclusionsFile))
            return 0;

        if (nonInteractive)
        {
            Console.Error.WriteLine($"Required file not found: {exclusionsFile}");
            return 1;
        }

        File.WriteAllText(exclusionsFile, GetDefaultContent());
        Console.WriteLine($"[{exclusionsFile}] did not exist. A starter file has been created.");
        Console.WriteLine("Please review / edit the created file(s), then rerun the program.");
        Console.Write("Press Enter to exit...");
        Console.ReadLine();
        return 1;
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
