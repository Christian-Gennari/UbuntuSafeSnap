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
restoreCommand.Options.Add(configPathOption);
restoreCommand.Options.Add(nonInteractiveOption);
restoreCommand.Arguments.Add(restoreFileArgument);

var rootCommand = new RootCommand("UbuntuSafeSnap — Ubuntu backup & restore utility");
rootCommand.Subcommands.Add(backupCommand);
rootCommand.Subcommands.Add(restoreCommand);

backupCommand.SetAction(async (ParseResult parseResult) =>
{
    var configPath = parseResult.GetValue(configPathOption)!;
    var nonInteractive = parseResult.GetValue(nonInteractiveOption);

    int bootstrapResult = EnsureConfigFilesExist(configPath, nonInteractive, out string targetsFile, out string exclusionsFile);
    if (bootstrapResult != 0)
        return bootstrapResult;

    return await RunBackupAsync(targetsFile, exclusionsFile);
});

restoreCommand.SetAction((ParseResult parseResult) =>
{
    var configPath = parseResult.GetValue(configPathOption)!;
    var nonInteractive = parseResult.GetValue(nonInteractiveOption);
    var restoreFilePath = parseResult.GetValue(restoreFileArgument);

    int bootstrapResult = EnsureConfigFilesExist(configPath, nonInteractive, out _, out _);
    if (bootstrapResult != 0)
        return bootstrapResult;

    return RunRestore(restoreFilePath!);
});

return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration());

static int EnsureConfigFilesExist(string configPath, bool nonInteractive, out string targetsFile, out string exclusionsFile)
{
    targetsFile = Path.Combine(configPath, "targets.txt");
    exclusionsFile = Path.Combine(configPath, "exclusions.txt");

    var missingFiles = new List<string>();
    if (!File.Exists(targetsFile))
        missingFiles.Add(targetsFile);
    if (!File.Exists(exclusionsFile))
        missingFiles.Add(exclusionsFile);

    if (missingFiles.Count > 0 && nonInteractive)
    {
        foreach (var f in missingFiles)
            Console.Error.WriteLine($"Required file not found: {f}");
        return 1;
    }

    bool createdAny = false;

    if (!File.Exists(targetsFile))
    {
        File.WriteAllText(targetsFile, "# Add directories here, one per line" + Environment.NewLine + "tests/mock_system" + Environment.NewLine);
        Console.WriteLine($"[{targetsFile}] did not exist. A starter file has been created.");
        createdAny = true;
    }

    if (!File.Exists(exclusionsFile))
    {
        File.WriteAllText(exclusionsFile, ExclusionService.GetDefaultContent());
        Console.WriteLine($"[{exclusionsFile}] did not exist. A starter file has been created.");
        createdAny = true;
    }

    if (createdAny)
    {
        Console.WriteLine("Please review / edit the created file(s), then rerun the program.");
        Console.Write("Press Enter to exit...");
        Console.ReadLine();
        return 1;
    }

    return 0;
}

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

static int RunRestore(string restoreFilePath)
{
    if (!File.Exists(restoreFilePath))
    {
        Console.Error.WriteLine($"Restore file not found: {restoreFilePath}");
        return 1;
    }

    Console.WriteLine($"Restore from {restoreFilePath} — not yet implemented (see #25)");
    return 0;
}