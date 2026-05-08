using UbuntuSafeSnap.Interfaces;

namespace UbuntuSafeSnap.Services;

public class TargetResolverService : ITargetResolverService
{
    public IEnumerable<string> Resolve(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Target file not found: {filePath}",
                filePath);
        }

        foreach (var raw in File.ReadAllLines(filePath))
        {
            var line = raw.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            string expanded = line.StartsWith('~')
                ? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + line[1..]
                : line;

            string fullPath = Path.GetFullPath(expanded);

            if (fullPath == Environment.GetFolderPath(Environment.SpecialFolder.UserProfile))
            {
                Console.WriteLine($"[TargetResolverService] Skipping home directory: {fullPath}");
                continue;
            }

            yield return fullPath;
        }
    }

    public static string GetDefaultContent() =>
        "# Add directories here, one per line" + Environment.NewLine + "tests/mock_system" + Environment.NewLine;

    public static int EnsureExists(string configPath, bool nonInteractive, out string targetsFile)
    {
        targetsFile = Path.Combine(configPath, "targets.txt");

        if (File.Exists(targetsFile))
            return 0;

        if (nonInteractive)
        {
            Console.Error.WriteLine($"Required file not found: {targetsFile}");
            return 1;
        }

        File.WriteAllText(targetsFile, GetDefaultContent());
        Console.WriteLine($"[{targetsFile}] did not exist. A starter file has been created.");
        Console.WriteLine("Please review / edit the created file(s), then rerun the program.");
        Console.Write("Press Enter to exit...");
        Console.ReadLine();
        return 1;
    }
}
