using UbuntuSafeSnap.Services.Shared;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Backup;

/// <summary>
/// Core file collection engine for backups. Iterates source directories, applies exclusion
/// rules, copies files to a staging directory, and writes a manifest for later restore.
/// </summary>
public class CollectorService(ExclusionService exclusionService)
{

    /// <summary>
    /// Entry point for file collection. Loads exclusions, iterates source directories
    /// (handling both individual files and directories), copies files to staging, and
    /// writes manifest.txt mapping relative paths to their original source directories.
    /// </summary>
    /// <param name="sourceDirectories">Paths to files or directories to back up.</param>
    /// <param name="stagingDirectory">Temporary directory to collect files into.</param>
    /// <param name="exclusionsFile">Path to the exclusions configuration file.</param>
    public async Task CollectFilesAsync(
        IEnumerable<string> sourceDirectories,
        string stagingDirectory,
        string exclusionsFile
    )
    {
        ArgumentNullException.ThrowIfNull(sourceDirectories);

        exclusionService.Load(exclusionsFile);

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
            Log.Info("CollectorService", $"The folder '{stagingDirectory}' has been created, since it did not exist.");
        }

        Log.Info("CollectorService", $"Starting file collection to: {stagingDirectory}");

        var manifestEntries = new List<string>();

        foreach (var sourceDir in sourceDirectories)
        {
            if (File.Exists(sourceDir))
            {
                if (exclusionService.ShouldExclude(sourceDir))
                {
                    Log.Info("CollectorService", $"Excluded file: {sourceDir}");
                    continue;
                }

                var attrs = File.GetAttributes(sourceDir);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Log.Info("CollectorService", $"Skipping symlink: {sourceDir}");
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
                Log.Info("CollectorService", $"Skipping non-existent/not found: {sourceDir}");
                continue;
            }

            Log.Info("CollectorService", $"Processing source: {sourceDir}");
            CollectFromDirectory(sourceDir, stagingDirectory, manifestEntries);
        }

        string manifestPath = Path.Combine(stagingDirectory, "manifest.txt");
        await File.WriteAllLinesAsync(manifestPath, manifestEntries);
        Log.Info("CollectorService", $"Manifest written: {manifestPath} ({manifestEntries.Count} entries)");
    }

    /// <summary>
    /// BFS traversal of a directory tree. For each directory, enumerates files and
    /// subdirectories, applies exclusion rules, skips symlinks, handles I/O exceptions,
    /// and tracks copied/excluded file counts.
    /// </summary>
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
                Log.Info("CollectorService", $"Unauthorized access to {currentDir}. Skipping...");
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                Log.Info("CollectorService", $"Directory not found: {currentDir}. Skipping...");
                continue;
            }

            int dirCopied = 0;

            foreach (var file in files)
            {
                if (exclusionService.ShouldExclude(file))
                {
                    totalExcluded++;
                    continue;
                }

                var attrs = File.GetAttributes(file);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Log.Info("CollectorService", $"Skipping symlink: {file}");
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
                    Log.Info("CollectorService", $"Unauthorized access to file: {file}");
                }
                catch (FileNotFoundException)
                {
                    Log.Info("CollectorService", $"File not found (broken symlink or deleted): {file}");
                }
                catch (IOException)
                {
                    Log.Info("CollectorService", $"Cannot copy file (socket, pipe, or in use): {file}");
                }
            }

            if (dirCopied > 0)
            {
                Log.Info("CollectorService", $"Copied {dirCopied} file(s) from {currentDir}");
            }

            foreach (var subDir in subDirs)
            {
                var attrs = File.GetAttributes(subDir);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    Log.Info("CollectorService", $"Skipping symlinked directory: {subDir}");
                    continue;
                }

                if (exclusionService.ShouldExcludeDirectory(subDir))
                {
                    Log.Info("CollectorService", $"Excluded directory: {subDir}");
                    totalExcluded++;
                    continue;
                }

                directories.Enqueue(subDir);
            }
        }

        Log.Info("CollectorService", $"Total: {totalCopied} file(s) copied, {totalExcluded} excluded.");
    }

    /// <summary>Copies a single file to the staging directory with error handling for access, I/O, and missing file issues.</summary>
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
            Log.Info("CollectorService", $"Copied file: {sourceFile}");
        }
        catch (UnauthorizedAccessException)
        {
            Log.Info("CollectorService", $"Unauthorized access to file: {sourceFile}");
        }
        catch (FileNotFoundException)
        {
            Log.Info("CollectorService", $"File not found (broken symlink or deleted): {sourceFile}");
        }
        catch (IOException)
        {
            Log.Info("CollectorService", $"Cannot copy file (socket, pipe, or in use): {sourceFile}");
        }
    }
}
