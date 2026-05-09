using System.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Spectre.Console;
using UbuntuSafeSnap.Interfaces;
using UbuntuSafeSnap.Services;

const string TargetsFile = "targets.txt";
const string ExclusionsFile = "exclusions.txt";
const string BackupsDirectory = "backups";

var restoreFileArgument = new Argument<string?>("file")
{
    Description = "Path to the .zip backup to restore. If omitted, selects from ./backups/",
    Arity = new ArgumentArity(0, 1),
};

var keepOption = new Option<int>("--keep")
{
    Description = "Number of backups to keep (oldest are pruned)",
    DefaultValueFactory = _ => 5,
};
var backupCommand = new Command("backup", "Create a backup of packages and config files");
backupCommand.Options.Add(keepOption);

var initCommand = new Command("init", "Create default targets.txt and exclusions.txt in the current directory");

var restoreCommand = new Command("restore", "Restore system from a backup archive");
restoreCommand.Arguments.Add(restoreFileArgument);

var rootCommand = new RootCommand("UbuntuSafeSnap — Ubuntu backup & restore utility");
rootCommand.Subcommands.Add(backupCommand);
rootCommand.Subcommands.Add(initCommand);
rootCommand.Subcommands.Add(restoreCommand);

backupCommand.SetAction(async (ParseResult parseResult) =>
{
    string targetsPath = Path.Combine(Directory.GetCurrentDirectory(), TargetsFile);
    string exclusionsPath = Path.Combine(Directory.GetCurrentDirectory(), ExclusionsFile);

    if (!File.Exists(targetsPath))
    {
        Console.Error.WriteLine($"Error: {TargetsFile} not found in current directory.");
        Console.Error.WriteLine("Run UbuntuSafeSnap from its home directory containing targets.txt and exclusions.txt.");
        return 1;
    }

    if (!File.Exists(exclusionsPath))
    {
        Console.Error.WriteLine($"Error: {ExclusionsFile} not found in current directory.");
        Console.Error.WriteLine("Run UbuntuSafeSnap from its home directory containing targets.txt and exclusions.txt.");
        return 1;
    }

    int keep = parseResult.GetValue(keepOption);
    return await RunBackupAsync(targetsPath, exclusionsPath, keep);
});

initCommand.SetAction((ParseResult parseResult) =>
{
    string targetsPath = Path.Combine(Directory.GetCurrentDirectory(), TargetsFile);
    string exclusionsPath = Path.Combine(Directory.GetCurrentDirectory(), ExclusionsFile);

    return InitService.Initialize(targetsPath, exclusionsPath);
});

restoreCommand.SetAction(async (ParseResult parseResult) =>
{
    string? restoreFilePath = parseResult.GetValue(restoreFileArgument);

    if (string.IsNullOrWhiteSpace(restoreFilePath))
    {
        string backupsPath = Path.Combine(Directory.GetCurrentDirectory(), BackupsDirectory);

        if (!Directory.Exists(backupsPath))
        {
            Console.Error.WriteLine($"Error: No backups directory found at {backupsPath}.");
            Console.Error.WriteLine("Run 'ubuntusafesnap backup' first to create a backup.");
            return 1;
        }

        string[] zipFiles = Directory.GetFiles(backupsPath, "*.zip")
            .OrderDescending()
            .ToArray();

        if (zipFiles.Length == 0)
        {
            Console.Error.WriteLine($"Error: No backup files found in {backupsPath}.");
            Console.Error.WriteLine("Run 'ubuntusafesnap backup' first to create a backup.");
            return 1;
        }

        if (zipFiles.Length == 1)
        {
            restoreFilePath = zipFiles[0];
            Console.WriteLine($"Using only available backup: {Path.GetFileName(restoreFilePath)}");
        }
        else
        {
            restoreFilePath = AnsiConsole.Prompt(
                new SelectionPrompt<string>()
                    .Title("Select a backup to restore:")
                    .AddChoices(zipFiles)
            );
        }
    }

    var restoreServices = new ServiceCollection()
        .AddSingleton<IConflictResolverService, ConflictResolverService>()
        .AddSingleton<IRestoreService, RestoreService>()
        .BuildServiceProvider();

    var restoreService = restoreServices.GetRequiredService<IRestoreService>();
    return await restoreService.RestoreAsync(restoreFilePath);
});

return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration());

static async Task<int> RunBackupAsync(string targetsFile, string exclusionsFile, int keep)
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

    string backupsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "backups");
    string archivePath = Path.Combine(
        backupsDirectory,
        $"ubuntusafesnap-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
    );

    var archiveService = services.GetRequiredService<IArchiveService>();
    archiveService.CreateArchive(stagingDirectory, archivePath);

    archiveService.PruneOldArchives(backupsDirectory, keep);

    return 0;
}

