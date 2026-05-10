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

    public static int Execute(string targetsPath, string exclusionsPath)
    {
        bool targetsCreated = false;
        bool exclusionsCreated = false;

        if (!File.Exists(targetsPath))
        {
            File.WriteAllText(targetsPath, DefaultTargets);
            Log.Info("Init", $"Created {targetsPath} with default configuration.");
            targetsCreated = true;
        }
        else
        {
            Log.Info("Init", "targets.txt already exists — skipped.");
        }

        if (!File.Exists(exclusionsPath))
        {
            File.WriteAllText(exclusionsPath, DefaultExclusions);
            Log.Info("Init", $"Created {exclusionsPath} with default configuration.");
            exclusionsCreated = true;
        }
        else
        {
            Log.Info("Init", "exclusions.txt already exists — skipped.");
        }

        if (targetsCreated || exclusionsCreated)
        {
            Console.WriteLine();
            Log.Info("Init", "Review the created file(s) and adjust to your needs, then run:");
            Console.WriteLine("  ubuntusafesnap backup");
        }
        else
        {
            Console.WriteLine();
            Log.Info("Init", "All configuration files already exist. No changes made.");
        }

        return 0;
    }
}