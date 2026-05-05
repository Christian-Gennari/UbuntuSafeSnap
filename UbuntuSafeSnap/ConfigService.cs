namespace UbuntuSafeSnap;

public class ConfigService
{
    public async Task CollectConfigFilesAsync(
        IEnumerable<string> sourceDirectories,
        string stagingDirectory
    )
    {
        ArgumentNullException.ThrowIfNull(sourceDirectories);

        if (string.IsNullOrWhiteSpace(stagingDirectory))
        {
            throw new ArgumentException(
                "[ConfigService] Staging directory cannot be null or empty",
                nameof(stagingDirectory)
            );
        }

        if (!Directory.Exists(stagingDirectory))
        {
            Directory.CreateDirectory(stagingDirectory);
            Console.WriteLine(
                $"[ConfigService] The folder '{stagingDirectory}' has been created, since it did not exist."
            );
        }

        Console.WriteLine($"[ConfigService] Starting config collection to: {stagingDirectory}");

        foreach (var sourceDir in sourceDirectories)
        {
            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine(
                    $"[ConfigService] Skipping non-existent/not found directory: {sourceDir}"
                );
                continue;
            }

            Console.WriteLine($"[ConfigService] Processing source: {sourceDir}");
            await CollectFromDirectoryAsync(sourceDir, stagingDirectory);
        }
    }

    private Task CollectFromDirectoryAsync(string sourceDir, string stagingDirectory)
    {
        return Task.CompletedTask;
    }

    private bool ShouldExclude(string filePath)
    {
        // Placeholder for Issue #4: Secret and Env Exclusion
        return false;
    }
}
