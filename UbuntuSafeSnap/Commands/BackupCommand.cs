using Microsoft.Extensions.DependencyInjection;
using UbuntuSafeSnap.Services.Backup;
using UbuntuSafeSnap.Services.Shared;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Commands;

public static class BackupCommand
{
    public static async Task<int> ExecuteAsync(string targetsFile, string exclusionsFile, int keep)
    {
        string stagingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "staging");

        if (Directory.Exists(stagingDirectory))
            Directory.Delete(stagingDirectory, recursive: true);

        var services = new ServiceCollection()
            .AddSingleton<TargetResolverService>()
            .AddSingleton<ExclusionService>()
            .AddSingleton<PackageService>()
            .AddSingleton<CollectorService>()
            .AddSingleton<ArchiveService>()
            .BuildServiceProvider();

        var targetResolver = services.GetRequiredService<TargetResolverService>();
        var targetDirectories = targetResolver.Resolve(targetsFile).ToArray();

        var packageService = services.GetRequiredService<PackageService>();
        await packageService.ExtractPackageListAsync(stagingDirectory);
        var collectorService = services.GetRequiredService<CollectorService>();

        await collectorService.CollectFilesAsync(targetDirectories, stagingDirectory, exclusionsFile);

        CollectAptSources(stagingDirectory);

        string backupsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "backups");
        string archivePath = Path.Combine(
            backupsDirectory,
            $"ubuntusafesnap-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
        );

        var archiveService = services.GetRequiredService<ArchiveService>();
        archiveService.CreateArchive(stagingDirectory, archivePath);

        archiveService.PruneOldArchives(backupsDirectory, keep);

        return 0;
    }

    private static void CollectAptSources(string stagingDirectory)
    {
        string aptDestDir = Path.Combine(stagingDirectory, "apt-sources");

        string[] aptSourceDirs = [
            "/etc/apt/sources.list.d",
            "/etc/apt/keyrings"
        ];

        foreach (var sourceDir in aptSourceDirs)
        {
            if (!Directory.Exists(sourceDir))
                continue;

            string dirName = sourceDir.Split('/')[^1];
            string destDir = Path.Combine(aptDestDir, dirName);
            Directory.CreateDirectory(destDir);

            int copied = 0;
            foreach (var file in Directory.GetFiles(sourceDir))
            {
                try
                {
                    string destPath = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destPath, overwrite: true);
                    copied++;
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Info("BackupCommand", $"Skipping apt file (permission denied): {file}");
                }
                catch (FileNotFoundException)
                {
                    Log.Info("BackupCommand", $"Skipping apt file (not found): {file}");
                }
            }

            if (copied > 0)
                Log.Info("BackupCommand", $"Auto-included {copied} file(s) from {sourceDir}");
        }
    }
}