using System.CommandLine;
using UbuntuSafeSnap.Commands;
using UbuntuSafeSnap.UI;

const string TargetsFile = "targets.txt";
const string ExclusionsFile = "exclusions.txt";

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
var backupCommand = new Command("backup", "Create a backup of packages and files");
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
    if (Environment.UserName == "root" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SUDO_USER")))
    {
        Log.Info("Backup", "Warning: Running backup as root without sudo. Home directory targets will resolve to /root.");
        Log.Info("Backup", "Consider running without sudo: UbuntuSafeSnap backup");
    }

    string targetsPath = Path.Combine(Directory.GetCurrentDirectory(), TargetsFile);
    string exclusionsPath = Path.Combine(Directory.GetCurrentDirectory(), ExclusionsFile);

    if (!File.Exists(targetsPath))
    {
        Log.Error("Backup", $"{TargetsFile} not found in current directory.");
        Log.Error("Backup", "Run UbuntuSafeSnap from its home directory containing targets.txt and exclusions.txt.");
        return 1;
    }

    if (!File.Exists(exclusionsPath))
    {
        Log.Error("Backup", $"{ExclusionsFile} not found in current directory.");
        Log.Error("Backup", "Run UbuntuSafeSnap from its home directory containing targets.txt and exclusions.txt.");
        return 1;
    }

    int keep = parseResult.GetValue(keepOption);
    return await BackupCommand.ExecuteAsync(targetsPath, exclusionsPath, keep);
});

initCommand.SetAction((ParseResult parseResult) =>
{
    string targetsPath = Path.Combine(Directory.GetCurrentDirectory(), TargetsFile);
    string exclusionsPath = Path.Combine(Directory.GetCurrentDirectory(), ExclusionsFile);

    return InitCommand.Execute(targetsPath, exclusionsPath);
});

restoreCommand.SetAction(async (ParseResult parseResult) =>
{
    string? restoreFilePath = parseResult.GetValue(restoreFileArgument);
    return await RestoreCommand.ExecuteAsync(restoreFilePath);
});

return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration());