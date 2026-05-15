using System.Diagnostics;
using System.IO.Compression;
using UbuntuSafeSnap.Models;
using UbuntuSafeSnap.Services.Shared;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Restore;

/// <summary>
/// Orchestrates the full restore process: archive extraction, package reinstallation,
/// file restoration with conflict resolution, and temporary directory cleanup.
/// </summary>
public class RestoreService(ConflictResolverService conflictResolver)
{

    /// <summary>
    /// Validates root access, extracts the archive, reinstalls packages, restores files,
    /// and cleans up the temporary staging directory in a finally block.
    /// </summary>
    /// <param name="archivePath">Path to the backup zip archive.</param>
    /// <returns>Exit code (0 = success, 1 = failure).</returns>
    public async Task<int> RestoreAsync(string archivePath)
    {
        ArgumentNullException.ThrowIfNull(archivePath);

        if (Environment.UserName != "root")
        {
            Log.Error("RestoreService", "Run this command with sudo.");
            return 1;
        }

        if (!File.Exists(archivePath))
        {
            Log.Error("RestoreService", $"Archive not found: {archivePath}");
            return 1;
        }

        string stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ubuntusafesnap-restore-{Guid.NewGuid():N}"
        );

        try
        {
            Log.Info("RestoreService", $"Extracting archive to: {stagingDirectory}");

            try
            {
                ZipFile.ExtractToDirectory(archivePath, stagingDirectory);
            }
            catch (InvalidDataException ex)
            {
                Log.Error("RestoreService", $"Archive is corrupted or invalid: {ex.Message}");
                return 1;
            }
            catch (IOException ex)
            {
                Log.Error("RestoreService", $"IO error extracting archive: {ex.Message}");
                return 1;
            }

            Log.Info("RestoreService", "Archive extracted successfully.");

            int packageResult = await ReinstallPackagesAsync(stagingDirectory);
            if (packageResult != 0)
            {
                Log.Error("RestoreService", "Package re-installation failed. Aborting restore.");
                return packageResult;
            }

            var (restored, skipped, aborted) = await RestoreFilesAsync(stagingDirectory);

            if (aborted)
            {
                Log.Error("RestoreService", $"Restore aborted. {restored} file(s) restored, {skipped} file(s) skipped before abort.");
                return 1;
            }

            Log.Info("RestoreService", $"Restore complete. {restored} file(s) restored. {skipped} file(s) skipped.");

            return 0;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
                Log.Info("RestoreService", $"Cleaned up staging directory: {stagingDirectory}");
            }
        }
    }

    /// <summary>
    /// Reads packages.txt from the staging directory and reinstalls all listed packages via apt.
    /// </summary>
    /// <param name="stagingDirectory">The extracted archive directory.</param>
    /// <returns>Exit code from apt (0 = success), or 0 if no packages.txt found.</returns>
    private static async Task<int> ReinstallPackagesAsync(string stagingDirectory)
    {
        string packagesFile = Path.Combine(stagingDirectory, "packages.txt");

        if (!File.Exists(packagesFile))
        {
            Log.Info("RestoreService", "No packages.txt found in archive. Skipping package re-installation.");
            return 0;
        }

        string[] packages = await File.ReadAllLinesAsync(packagesFile);

        packages = packages
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        if (packages.Length == 0)
        {
            Log.Info("RestoreService", "packages.txt is empty. No packages to reinstall.");
            return 0;
        }

        Log.Info("RestoreService", $"Re-installing {packages.Length} package(s)...");

        using var process = new Process();
        process.StartInfo.FileName = "apt";
        process.StartInfo.ArgumentList.Add("install");
        process.StartInfo.ArgumentList.Add("-y");

        foreach (var pkg in packages)
        {
            process.StartInfo.ArgumentList.Add(pkg);
        }

        process.StartInfo.UseShellExecute = false;

        process.Start();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Log.Error("RestoreService", $"apt install failed with exit code {process.ExitCode}.");
            return process.ExitCode;
        }

        Log.Info("RestoreService", "Package re-installation complete.");
        return 0;
    }

    /// <summary>
    /// BFS traversal of the staging directory, restoring each file to its original location
    /// using the manifest. Delegates to ConflictResolverService when a destination file exists.
    /// </summary>
    /// <param name="stagingDirectory">The extracted archive directory.</param>
    /// <returns>Counts of restored/skipped files and whether the restore was aborted.</returns>
    private async Task<(int restored, int skipped, bool aborted)> RestoreFilesAsync(string stagingDirectory)
    {
        int restored = 0;
        int skipped = 0;

        var manifest = LoadManifest(stagingDirectory);

        string? oldHome = DetectOldHome(manifest);
        string newHome = UserHomeHelper.GetRealUserHome();

        if (oldHome != null && oldHome != newHome)
            Log.Info("RestoreService", $"Remapping home directory: {oldHome} → {newHome}");

        var directories = new Queue<string>();
        directories.Enqueue(stagingDirectory);

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
                Log.Info("RestoreService", $"Unauthorized access to {currentDir}. Skipping...");
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                Log.Info("RestoreService", $"Directory not found: {currentDir}. Skipping...");
                continue;
            }

            foreach (var file in files)
            {
                string relativePath = Path.GetRelativePath(stagingDirectory, file);

                if (relativePath == "packages.txt" || relativePath == "manifest.txt")
                    continue;

                string destPath;

                if (manifest.TryGetValue(relativePath, out string? sourceDir))
                {
                    if (oldHome != null && sourceDir.StartsWith(oldHome) &&
                        (sourceDir.Length == oldHome.Length || sourceDir[oldHome.Length] == '/'))
                        sourceDir = newHome + sourceDir[oldHome.Length..];

                    destPath = Path.Combine(sourceDir, relativePath);
                }
                else
                {
                    destPath = Path.Combine("/", relativePath);
                }

                if (File.Exists(destPath))
                {
                    var resolution = await conflictResolver.ResolveAsync(file, destPath);

                    switch (resolution)
                    {
                        case ConflictResolution.Identical:
                            Log.Info("RestoreService", $"Skipped (identical): {destPath}");
                            skipped++;
                            break;
                        case ConflictResolution.Skip:
                            Log.Info("RestoreService", $"Skipped (user choice): {destPath}");
                            skipped++;
                            break;
                        case ConflictResolution.Overwrite:
                            try
                            {
                                File.Copy(file, destPath, overwrite: true);
                                Log.Info("RestoreService", $"Overwritten: {destPath}");
                                restored++;
                            }
                            catch (UnauthorizedAccessException)
                            {
                                Log.Info("RestoreService", $"Unauthorized access to destination: {destPath}");
                                skipped++;
                            }
                            break;
                        case ConflictResolution.Abort:
                            return (restored, skipped, aborted: true);
                    }

                    continue;
                }

                try
                {
                    string? destDir = Path.GetDirectoryName(destPath);
                    if (destDir is not null && !Directory.Exists(destDir))
                    {
                        Directory.CreateDirectory(destDir);
                    }

                    File.Copy(file, destPath, overwrite: false);
                    Log.Info("RestoreService", $"Restored: {destPath}");
                    restored++;
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Info("RestoreService", $"Unauthorized access to destination: {destPath}");
                    skipped++;
                }
            }

            foreach (var subDir in subDirs)
            {
                directories.Enqueue(subDir);
            }
        }

        return (restored, skipped, aborted: false);
    }

    /// <summary>
    /// Parses manifest.txt (format: sourceDir|relativePath) into a lookup dictionary
    /// that maps relative paths back to their original source directories.
    /// </summary>
    /// <param name="stagingDirectory">The extracted archive directory.</param>
    /// <returns>Dictionary of relative path to source directory.</returns>
    private static Dictionary<string, string> LoadManifest(string stagingDirectory)
    {
        var manifest = new Dictionary<string, string>();

        string manifestPath = Path.Combine(stagingDirectory, "manifest.txt");

        if (!File.Exists(manifestPath))
        {
            Log.Info("RestoreService", "No manifest.txt found in archive. Files will be restored to /.");
            return manifest;
        }

        foreach (var rawLine in File.ReadAllLines(manifestPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            int separatorIndex = line.IndexOf('|');
            if (separatorIndex < 0)
                continue;

            string sourceDir = line[..separatorIndex];
            string relativePath = line[(separatorIndex + 1)..];

            manifest[relativePath] = sourceDir;
        }

        Log.Info("RestoreService", $"Loaded manifest with {manifest.Count} entries.");
        return manifest;
    }

    /// <summary>
    /// Scans the manifest for /home/* prefixed source directories and returns the
    /// most common /home/&lt;user&gt; prefix, or null if none are found.
    /// </summary>
    private static string? DetectOldHome(Dictionary<string, string> manifest)
    {
        var homeCounts = new Dictionary<string, int>();

        foreach (var sourceDir in manifest.Values)
        {
            if (!sourceDir.StartsWith("/home/"))
                continue;

            int endIndex = sourceDir.IndexOf('/', "/home/".Length);
            string homePrefix = endIndex >= 0 ? sourceDir[..endIndex] : sourceDir;

            homeCounts.TryGetValue(homePrefix, out int count);
            homeCounts[homePrefix] = count + 1;
        }

        if (homeCounts.Count == 0)
            return null;

        return homeCounts.MaxBy(kvp => kvp.Value).Key;
    }
}