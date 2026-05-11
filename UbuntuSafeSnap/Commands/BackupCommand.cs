using Microsoft.Extensions.DependencyInjection;
using UbuntuSafeSnap.Services.Backup;
using UbuntuSafeSnap.Services.Shared;

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
}