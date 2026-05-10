namespace UbuntuSafeSnap.Services;

public class ConfigService(ExclusionService exclusionService)
{
    private readonly ExclusionService _exclusionService = exclusionService;

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

        var manifestEntries = new List<string>();

        foreach (var sourceDir in sourceDirectories)
        {
            if (File.Exists(sourceDir))
            {
                if (_exclusionService.ShouldExclude(sourceDir))
                {
                    Console.WriteLine($"[ConfigService] Excluded file: {sourceDir}");
                    continue;
                }

                var attrs = File.GetAttributes(sourceDir);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Console.WriteLine($"[ConfigService] Skipping symlink: {sourceDir}");
                    continue;
                }

                string relativePath = Path.GetFileName(sourceDir);
                string destPath = Path.Combine(stagingDirectory, relativePath);
                string absoluteParentDir = Path.GetFullPath(Path.GetDirectoryName(sourceDir)!);

                try
                {
                    File.Copy(sourceDir, destPath, overwrite: true);
                    Console.WriteLine($"[ConfigService] Copied file: {sourceDir}");
                    manifestEntries.Add($"{absoluteParentDir}|{relativePath}");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine($"[ConfigService] Unauthorized access to file: {sourceDir}");
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"[ConfigService] File not found (broken symlink or deleted): {sourceDir}");
                }
                catch (IOException)
                {
                    Console.WriteLine($"[ConfigService] Cannot copy file (socket, pipe, or in use): {sourceDir}");
                }

                continue;
            }

            if (!Directory.Exists(sourceDir))
            {
                Console.WriteLine(
                    $"[ConfigService] Skipping non-existent/not found: {sourceDir}"
                );
                continue;
            }

            Console.WriteLine($"[ConfigService] Processing source: {sourceDir}");
            CollectFromDirectory(sourceDir, stagingDirectory, manifestEntries);
        }

        string manifestPath = Path.Combine(stagingDirectory, "manifest.txt");
        await File.WriteAllLinesAsync(manifestPath, manifestEntries);
        Console.WriteLine($"[ConfigService] Manifest written: {manifestPath} ({manifestEntries.Count} entries)");
    }

    private void CollectFromDirectory(string sourceDir, string stagingDirectory, List<string> manifestEntries)
    {
        var directories = new Queue<string>();
        directories.Enqueue(sourceDir);

        int totalCopied = 0;
        int totalExcluded = 0;

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

            int dirCopied = 0;

            foreach (var file in files)
            {
                if (_exclusionService.ShouldExclude(file))
                {
                    totalExcluded++;
                    continue;
                }

                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Console.WriteLine($"[ConfigService] Skipping symlink: {file}");
                    totalExcluded++;
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
                    dirCopied++;
                    totalCopied++;

                    string absoluteSourceDir = Path.GetFullPath(sourceDir);
                    manifestEntries.Add($"{absoluteSourceDir}|{relativePath}");
                }
                catch (UnauthorizedAccessException)
                {
                    Console.WriteLine($"[ConfigService] Unauthorized access to file: {file}");
                }
                catch (FileNotFoundException)
                {
                    Console.WriteLine($"[ConfigService] File not found (broken symlink or deleted): {file}");
                }
                catch (IOException)
                {
                    Console.WriteLine($"[ConfigService] Cannot copy file (socket, pipe, or in use): {file}");
                }
            }

            if (dirCopied > 0)
            {
                Console.WriteLine($"[ConfigService] Copied {dirCopied} file(s) from {currentDir}");
            }

            foreach (var subDir in subDirs)
            {
                var attrs = File.GetAttributes(subDir);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Console.WriteLine($"[ConfigService] Skipping symlinked directory: {subDir}");
                    continue;
                }

                directories.Enqueue(subDir);
            }
        }

        Console.WriteLine($"[ConfigService] Total: {totalCopied} file(s) copied, {totalExcluded} excluded.");
    }
}
