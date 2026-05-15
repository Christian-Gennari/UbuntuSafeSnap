using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Commands;

public static class InitCommand
{
    private const string DefaultTargets = """
# Directories to back up, one per line. ~ expands to your home directory.
# Lines starting with # are comments and will be ignored.

~/.config
~/.bashrc
~/.profile
~/.ssh
/etc/NetworkManager
""";

    private const string DefaultExclusions = """
# Files matching these patterns will be excluded from backups.
# Extension rules start with .  (e.g. .env, .key, .pem)
# Filename rules are just the name (e.g. secrets.json)
# Directory rules end with /  (e.g. node_modules/) to skip entire trees
# Lines starting with # are comments and will be ignored.

.env
.key
.pem
.log
.lock
.pid
.db
.sqlite
secrets.json
secrets.lua
id_rsa
id_ed25519
id_ecdsa
node_modules/
.cache/
__pycache__/
.git/
""";

    private const string DefaultSettings = """
# Backup settings
# Lines starting with # are comments and will be ignored.

# Number of backups to keep (oldest are pruned automatically)
keep = 5
""";

    public static int Execute(string targetsPath, string exclusionsPath, string settingsPath)
    {
        bool targetsCreated = false;
        bool exclusionsCreated = false;
        bool settingsCreated = false;

        if (!File.Exists(targetsPath))
        {
            File.WriteAllText(targetsPath, DefaultTargets);
            Log.Info("InitCommand", $"Created {targetsPath} with default configuration.");
            targetsCreated = true;
        }
        else
        {
            Log.Info("InitCommand", "targets.txt already exists — skipped.");
        }

        if (!File.Exists(exclusionsPath))
        {
            File.WriteAllText(exclusionsPath, DefaultExclusions);
            Log.Info("InitCommand", $"Created {exclusionsPath} with default configuration.");
            exclusionsCreated = true;
        }
        else
        {
            Log.Info("InitCommand", "exclusions.txt already exists — skipped.");
        }

        if (!File.Exists(settingsPath))
        {
            File.WriteAllText(settingsPath, DefaultSettings);
            Log.Info("InitCommand", $"Created {settingsPath} with default configuration.");
            settingsCreated = true;
        }
        else
        {
            Log.Info("InitCommand", "settings.txt already exists — skipped.");
        }

        if (targetsCreated || exclusionsCreated || settingsCreated)
        {
            Console.WriteLine();
            Log.Info("InitCommand", "Review the created file(s) and adjust to your needs, then run:");
            Console.WriteLine("  ubuntusafesnap backup");
        }
        else
        {
            Console.WriteLine();
            Log.Info("InitCommand", "All configuration files already exist. No changes made.");
        }

        return 0;
    }
}