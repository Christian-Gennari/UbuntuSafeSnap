using UbuntuSafeSnap.Services.Shared;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Backup;

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
                "Staging directory cannot be null or empty.",
                nameof(stagingDirectory)
            );
        }

        if (!Directory.Exists(stagingDirectory))
        {
            Directory.CreateDirectory(stagingDirectory);
            Log.Info("ConfigService", $"The folder '{stagingDirectory}' has been created, since it did not exist.");
        }

        Log.Info("ConfigService", $"Starting config collection to: {stagingDirectory}");

        var manifestEntries = new List<string>();

        foreach (var sourceDir in sourceDirectories)
        {
            if (File.Exists(sourceDir))
            {
                if (_exclusionService.ShouldExclude(sourceDir))
                {
                    Log.Info("ConfigService", $"Excluded file: {sourceDir}");
                    continue;
                }

                var attrs = File.GetAttributes(sourceDir);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Log.Info("ConfigService", $"Skipping symlink: {sourceDir}");
                    continue;
                }

                string relativePath = Path.GetFileName(sourceDir);
                string destPath = Path.Combine(stagingDirectory, relativePath);
                string absoluteParentDir = Path.GetFullPath(Path.GetDirectoryName(sourceDir)!);

                CopyFileToStaging(sourceDir, destPath);
                manifestEntries.Add($"{absoluteParentDir}|{relativePath}");
                continue;
            }

            if (!Directory.Exists(sourceDir))
            {
                Log.Info("ConfigService", $"Skipping non-existent/not found: {sourceDir}");
                continue;
            }

            Log.Info("ConfigService", $"Processing source: {sourceDir}");
            CollectFromDirectory(sourceDir, stagingDirectory, manifestEntries);
        }

        string manifestPath = Path.Combine(stagingDirectory, "manifest.txt");
        await File.WriteAllLinesAsync(manifestPath, manifestEntries);
        Log.Info("ConfigService", $"Manifest written: {manifestPath} ({manifestEntries.Count} entries)");
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
                Log.Info("ConfigService", $"Unauthorized access to {currentDir}. Skipping...");
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                Log.Info("ConfigService", $"Directory not found: {currentDir}. Skipping...");
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
                    Log.Info("ConfigService", $"Skipping symlink: {file}");
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
                    Log.Info("ConfigService", $"Unauthorized access to file: {file}");
                }
                catch (FileNotFoundException)
                {
                    Log.Info("ConfigService", $"File not found (broken symlink or deleted): {file}");
                }
                catch (IOException)
                {
                    Log.Info("ConfigService", $"Cannot copy file (socket, pipe, or in use): {file}");
                }
            }

            if (dirCopied > 0)
            {
                Log.Info("ConfigService", $"Copied {dirCopied} file(s) from {currentDir}");
            }

            foreach (var subDir in subDirs)
            {
                var attrs = File.GetAttributes(subDir);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Log.Info("ConfigService", $"Skipping symlinked directory: {subDir}");
                    continue;
                }

                if (_exclusionService.ShouldExcludeDirectory(subDir))
                {
                    Log.Info("ConfigService", $"Excluded directory: {subDir}");
                    totalExcluded++;
                    continue;
                }

                directories.Enqueue(subDir);
            }
        }

        Log.Info("ConfigService", $"Total: {totalCopied} file(s) copied, {totalExcluded} excluded.");
    }

    private static void CopyFileToStaging(string sourceFile, string destPath)
    {
        string? destDir = Path.GetDirectoryName(destPath);
        if (destDir is not null)
        {
            Directory.CreateDirectory(destDir);
        }

        try
        {
            File.Copy(sourceFile, destPath, overwrite: true);
            Log.Info("ConfigService", $"Copied file: {sourceFile}");
        }
        catch (UnauthorizedAccessException)
        {
            Log.Info("ConfigService", $"Unauthorized access to file: {sourceFile}");
        }
        catch (FileNotFoundException)
        {
            Log.Info("ConfigService", $"File not found (broken symlink or deleted): {sourceFile}");
        }
        catch (IOException)
        {
            Log.Info("ConfigService", $"Cannot copy file (socket, pipe, or in use): {sourceFile}");
        }
    }
}