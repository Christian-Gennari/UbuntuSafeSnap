using System.IO.Compression;
using System.Runtime.InteropServices;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Backup;

/// <summary>
/// Manages backup archive creation, Linux permission handling on output directories,
/// and retention-based pruning of old backups.
/// </summary>
public class ArchiveService
{
    private const UnixFileMode DirectoryPermissions =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
        UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

    /// <summary>
    /// Creates a zip archive from the staging directory, sets Linux permissions on the
    /// output directory, then cleans up the staging directory.
    /// </summary>
    /// <param name="stagingDirectory">Directory containing collected files to archive.</param>
    /// <param name="outputPath">Destination path for the .zip file.</param>
    public void CreateArchive(string stagingDirectory, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(stagingDirectory);
        ArgumentNullException.ThrowIfNull(outputPath);

        if (!Directory.Exists(stagingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"Staging directory not found: {stagingDirectory}"
            );
        }

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (outputDir is not null)
        {
            if (!Directory.Exists(outputDir))
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Directory.CreateDirectory(outputDir, DirectoryPermissions);
                else
                    Directory.CreateDirectory(outputDir);
                Log.Info("ArchiveService", $"Created output directory: {outputDir}");
            }

            EnsureWritableDirectory(outputDir);
        }

        Log.Info("ArchiveService", $"Creating archive: {outputPath}");
        ZipFile.CreateFromDirectory(stagingDirectory, outputPath);
        Log.Info("ArchiveService", "Archive created successfully.");

        Log.Info("ArchiveService", $"Cleaning up staging directory: {stagingDirectory}");
        Directory.Delete(stagingDirectory, recursive: true);
        Log.Info("ArchiveService", "Staging directory removed.");
    }

    /// <summary>
    /// Removes oldest backup archives beyond the specified keepCount.
    /// Archives are sorted by name descending (reverse alphabetical = newest first).
    /// </summary>
    /// <param name="backupsDirectory">Directory containing backup .zip files.</param>
    /// <param name="keepCount">Number of most recent backups to retain.</param>
    public void PruneOldArchives(string backupsDirectory, int keepCount)
    {
        ArgumentNullException.ThrowIfNull(backupsDirectory);

        if (keepCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keepCount),
                "keepCount must be non-negative."
            );
        }

        if (!Directory.Exists(backupsDirectory))
        {
            Log.Info("ArchiveService", "Backups directory not found. Nothing to prune.");
            return;
        }

        var archives = Directory.GetFiles(backupsDirectory, "*.zip")
            .OrderDescending()
            .ToArray();

        if (archives.Length <= keepCount)
        {
            Log.Info("ArchiveService", $"{archives.Length} backup(s) found, keeping all (limit: {keepCount}).");
            return;
        }

        int pruned = 0;
        foreach (var archive in archives[keepCount..])
        {
            File.Delete(archive);
            Log.Info("ArchiveService", $"Pruned old backup: {Path.GetFileName(archive)}");
            pruned++;
        }

        Log.Info("ArchiveService", $"Pruned {pruned} backup(s), kept {keepCount} most recent.");
    }

    /// <summary>Sets Unix permissions to 755 (rwxr-xr-x) on the directory if running on Linux.</summary>
    /// <param name="directoryPath">Directory to make writable.</param>
    private static void EnsureWritableDirectory(string directoryPath)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        try
        {
            File.SetUnixFileMode(directoryPath, DirectoryPermissions);
        }
        catch (UnauthorizedAccessException)
        {
            Log.Error("ArchiveService", $"Cannot set permissions on {directoryPath}. Fix by running: sudo chown $(whoami) {directoryPath}");
        }
    }
}