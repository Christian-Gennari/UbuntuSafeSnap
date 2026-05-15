using System.CommandLine;
using UbuntuSafeSnap.Commands;
using UbuntuSafeSnap.UI;

/// <summary>Default filename for the target directories configuration file.</summary>
const string TargetsFile = "targets.txt";
/// <summary>Default filename for the file/directory exclusion rules configuration file.</summary>
const string ExclusionsFile = "exclusions.txt";
/// <summary>Default filename for the backup settings configuration file.</summary>
const string SettingsFile = "settings.txt";

var restoreFileArgument = new Argument<string?>("file")
{
    Description = "Path to the .zip backup to restore. If omitted, selects from ./backups/",
    Arity = new ArgumentArity(0, 1),
};

var keepOption = new Option<int?>("--keep")
{
    Description = "Number of backups to keep (default: 5, configurable in settings.txt)",
};
var backupCommand = new Command("backup", "Create a backup of packages and files");
backupCommand.Options.Add(keepOption);

var initCommand = new Command("init", "Create default targets.txt, exclusions.txt and settings.txt in the current directory");

var dryRunOption = new Option<bool>("--dry-run")
{
    Description = "Preview what restore would do without making any changes",
};
var restoreCommand = new Command("restore", "Restore system from a backup archive");
restoreCommand.Arguments.Add(restoreFileArgument);
restoreCommand.Options.Add(dryRunOption);

var rootCommand = new RootCommand("UbuntuSafeSnap — Ubuntu backup & restore utility");
rootCommand.Subcommands.Add(backupCommand);
rootCommand.Subcommands.Add(initCommand);
rootCommand.Subcommands.Add(restoreCommand);

backupCommand.SetAction(async (ParseResult parseResult) =>
{
    // When running as root without sudo (e.g. sudo su), $HOME resolves to /root
    // instead of the original user's home, which may be unexpected.
    if (Environment.UserName == "root" && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SUDO_USER")))
    {
        Log.Info("BackupCommand", "Warning: Running backup as root without sudo. Home directory targets will resolve to /root.");
        Log.Info("BackupCommand", "Consider running without sudo: UbuntuSafeSnap backup");
    }

    string targetsPath = Path.Combine(Directory.GetCurrentDirectory(), TargetsFile);
    string exclusionsPath = Path.Combine(Directory.GetCurrentDirectory(), ExclusionsFile);

    if (!File.Exists(targetsPath))
    {
        Log.Error("BackupCommand", $"{TargetsFile} not found in current directory.");
        Log.Error("BackupCommand", "Run UbuntuSafeSnap from its home directory containing targets.txt and exclusions.txt.");
        return 1;
    }

    if (!File.Exists(exclusionsPath))
    {
        Log.Error("BackupCommand", $"{ExclusionsFile} not found in current directory.");
        Log.Error("BackupCommand", "Run UbuntuSafeSnap from its home directory containing targets.txt and exclusions.txt.");
        return 1;
    }

    string settingsPath = Path.Combine(Directory.GetCurrentDirectory(), SettingsFile);
    int? keepOptionValue = parseResult.GetValue(keepOption);
    int keep = keepOptionValue ?? ParseKeepFromSettings(settingsPath);
    Log.Info("BackupCommand", $"Using keep = {keep}");
    return await BackupCommand.ExecuteAsync(targetsPath, exclusionsPath, keep);
});

initCommand.SetAction((ParseResult parseResult) =>
{
    string targetsPath = Path.Combine(Directory.GetCurrentDirectory(), TargetsFile);
    string exclusionsPath = Path.Combine(Directory.GetCurrentDirectory(), ExclusionsFile);
    string settingsPath = Path.Combine(Directory.GetCurrentDirectory(), SettingsFile);

    return InitCommand.Execute(targetsPath, exclusionsPath, settingsPath);
});

restoreCommand.SetAction(async (ParseResult parseResult) =>
{
    string? restoreFilePath = parseResult.GetValue(restoreFileArgument);
    bool dryRun = parseResult.GetValue(dryRunOption);
    return await RestoreCommand.ExecuteAsync(restoreFilePath, dryRun);
});

return await rootCommand.Parse(args).InvokeAsync(new InvocationConfiguration());

/// <summary>Parses the keep value from settings.txt, handling # comments and blank lines. Returns default 5 if file is missing or no valid keep entry found.</summary>
static int ParseKeepFromSettings(string settingsPath)
{
    if (!File.Exists(settingsPath))
        return 5;

    foreach (var rawLine in File.ReadAllLines(settingsPath))
    {
        var line = rawLine.Trim();

        if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
            continue;

        int eqIndex = line.IndexOf('=');
        if (eqIndex < 0) continue;

        string key = line[..eqIndex].Trim();

        if (!key.Equals("keep", StringComparison.OrdinalIgnoreCase))
            continue;

        string valuePart = line[(eqIndex + 1)..].Trim();

        if (int.TryParse(valuePart, out int keep) && keep > 0)
            return keep;
    }

    return 5;
}