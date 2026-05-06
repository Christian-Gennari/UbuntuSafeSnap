namespace UbuntuSafeSnap.Services;

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
        var directories = new Queue<string>();
        directories.Enqueue(sourceDir);

        while (directories.Count > 0)
        {
            var currentDir = directories.Dequeue();
            string[] files;
            string[] subDirs;

            try
            {
                files = Directory.GetFiles(currentDir);
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException)
            {
                Console.WriteLine(
                    $"[ConfigService] Unauthorized access to {currentDir}. Skipping..."
                );
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                Console.WriteLine(
                    $"[ConfigService] Directory not found: {currentDir}. Skipping..."
                );
                continue;
            }

            foreach (var file in files)
            {
                if (ShouldExclude(file))
                {
                    Console.WriteLine(
                        $"[ConfigService] Skipped excluded file: {Path.GetFileName(file)}"
                    );
                    continue;
                }

                try
                {
                    string relativePath = Path.GetRelativePath(sourceDir, file);
                    string destPath = Path.Combine(stagingDirectory, relativePath);
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (destDir is not null)
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file, destPath, overwrite: true);
                    Console.WriteLine($"[ConfigService] Copied: {relativePath}");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine($"[ConfigService] Unauthorized access to file: {file}");
                }
            }

            foreach (var subDir in subDirs)
            {
                directories.Enqueue(subDir);
            }
        }

        return Task.CompletedTask;
    }

    private bool ShouldExclude(string filePath)
    {
        return ExclusionService.ShouldExclude(filePath);
    }
}
