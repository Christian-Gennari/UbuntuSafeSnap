using System.IO.Compression;

namespace UbuntuSafeSnap.Services;

public static class ArchiveService
{
    public static void CreateArchive(string stagingDirectory, string outputPath)
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
            throw new DirectoryNotFoundException(
                $"[ArchiveService] Output directory not found: {outputDir}"
            );
        }

        Console.WriteLine($"[ArchiveService] Creating archive: {outputPath}");
        ZipFile.CreateFromDirectory(stagingDirectory, outputPath);
        Console.WriteLine($"[ArchiveService] Archive created successfully.");

        Console.WriteLine($"[ArchiveService] Cleaning up staging directory: {stagingDirectory}");
        Directory.Delete(stagingDirectory, recursive: true);
        Console.WriteLine("[ArchiveService] Staging directory removed.");
    }
}