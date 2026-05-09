using System.IO.Compression;
using UbuntuSafeSnap.Interfaces;

namespace UbuntuSafeSnap.Services;

public class ArchiveService : IArchiveService
{
    public void CreateArchive(string stagingDirectory, string outputPath)
    {
        ArgumentNullException.ThrowIfNull(stagingDirectory);
        ArgumentNullException.ThrowIfNull(outputPath);

        if (!Directory.Exists(stagingDirectory))
        {
            throw new DirectoryNotFoundException(
                $"[ArchiveService] Staging directory not found: {stagingDirectory}"
            );
        }

        string? outputDir = Path.GetDirectoryName(outputPath);
        if (outputDir is not null && !Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
            Console.WriteLine($"[ArchiveService] Created output directory: {outputDir}");
        }

        Console.WriteLine($"[ArchiveService] Creating archive: {outputPath}");
        ZipFile.CreateFromDirectory(stagingDirectory, outputPath);
        Console.WriteLine($"[ArchiveService] Archive created successfully.");

        Console.WriteLine($"[ArchiveService] Cleaning up staging directory: {stagingDirectory}");
        Directory.Delete(stagingDirectory, recursive: true);
        Console.WriteLine("[ArchiveService] Staging directory removed.");
    }

    public void PruneOldArchives(string backupsDirectory, int keepCount)
    {
        ArgumentNullException.ThrowIfNull(backupsDirectory);

        if (keepCount < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(keepCount),
                "[ArchiveService] keepCount must be non-negative."
            );
        }

        if (!Directory.Exists(backupsDirectory))
        {
            Console.WriteLine("[ArchiveService] Backups directory not found. Nothing to prune.");
            return;
        }

        var archives = Directory.GetFiles(backupsDirectory, "*.zip")
            .OrderDescending()
            .ToArray();

        if (archives.Length <= keepCount)
        {
            Console.WriteLine($"[ArchiveService] {archives.Length} backup(s) found, keeping all (limit: {keepCount}).");
            return;
        }

        int pruned = 0;
        foreach (var archive in archives[keepCount..])
        {
            File.Delete(archive);
            Console.WriteLine($"[ArchiveService] Pruned old backup: {Path.GetFileName(archive)}");
            pruned++;
        }

        Console.WriteLine($"[ArchiveService] Pruned {pruned} backup(s), kept {keepCount} most recent.");
    }
}
