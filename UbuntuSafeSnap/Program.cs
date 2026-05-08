using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using UbuntuSafeSnap.Interfaces;
using UbuntuSafeSnap.Services;

var configPathOption = new Option<string>("--config-path")
{
    Description = "Path to directory containing targets.txt and exclusions.txt",
};
configPathOption.DefaultValueFactory = _ => "./";

var nonInteractiveOption = new Option<bool>("--non-interactive")
{
    Description = "Skip all interactive prompts; exit with error code if input is needed",
};

var restoreFileArgument = new Argument<string>("file")
{
    Description = "Path to the .zip backup to restore",
};

var backupCommand = new Command("backup", "Create a backup of packages and config files");
backupCommand.Options.Add(configPathOption);
backupCommand.Options.Add(nonInteractiveOption);

var restoreCommand = new Command("restore", "Restore system from a backup archive");
restoreCommand.Arguments.Add(restoreFileArgument);

var rootCommand = new RootCommand("UbuntuSafeSnap — Ubuntu backup & restore utility");
rootCommand.Subcommands.Add(backupCommand);
rootCommand.Subcommands.Add(restoreCommand);

backupCommand.SetAction(async (ParseResult parseResult) =>
{
    var configPath = parseResult.GetValue(configPathOption)!;
    var nonInteractive = parseResult.GetValue(nonInteractiveOption);

    int result = TargetResolverService.EnsureExists(configPath, nonInteractive, out string targetsFile);
    if (result != 0) return result;
    result = ExclusionService.EnsureExists(configPath, nonInteractive, out string exclusionsFile);
    if (result != 0) return result;

    return await RunBackupAsync(targetsFile, exclusionsFile);
});

restoreCommand.SetAction(async (ParseResult parseResult) =>
{
    var restoreFilePath = parseResult.GetValue(restoreFileArgument);

    var restoreServices = new ServiceCollection()
        .AddSingleton<IConflictResolverService, ConflictResolverService>()
        .AddSingleton<IRestoreService, RestoreService>()
        .BuildServiceProvider();

    var restoreService = restoreServices.GetRequiredService<IRestoreService>();
    return await restoreService.RestoreAsync(restoreFilePath!);
});

return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration());

static async Task<int> RunBackupAsync(string targetsFile, string exclusionsFile)
{
    string stagingDirectory = Path.Combine(Directory.GetCurrentDirectory(), "staging");

    if (Directory.Exists(stagingDirectory))
        Directory.Delete(stagingDirectory, recursive: true);

    var services = new ServiceCollection()
        .AddSingleton<ITargetResolverService, TargetResolverService>()
        .AddSingleton<IExclusionService, ExclusionService>(_ => new ExclusionService(exclusionsFile))
        .AddSingleton<IPackageService, PackageService>()
        .AddSingleton<IConfigService, ConfigService>()
        .AddSingleton<IArchiveService, ArchiveService>()
        .BuildServiceProvider();

    var targetResolver = services.GetRequiredService<ITargetResolverService>();
    var targetDirectories = targetResolver.Resolve(targetsFile).ToArray();

    var packageService = services.GetRequiredService<IPackageService>();
    await packageService.ExtractPackageListAsync(stagingDirectory);

    var configService = services.GetRequiredService<IConfigService>();
    await configService.CollectConfigFilesAsync(targetDirectories, stagingDirectory);

    string archivePath = Path.Combine(
        Directory.GetCurrentDirectory(),
        $"ubuntusafesnap-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
    );

    var archiveService = services.GetRequiredService<IArchiveService>();
    archiveService.CreateArchive(stagingDirectory, archivePath);

    return 0;
}

