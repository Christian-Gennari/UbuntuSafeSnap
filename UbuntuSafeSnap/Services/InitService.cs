namespace UbuntuSafeSnap.Services;

public static class InitService
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
""";

    public static int Initialize(string targetsPath, string exclusionsPath)
    {
        int exitCode = 0;
        bool targetsCreated = false;
        bool exclusionsCreated = false;

        if (!File.Exists(targetsPath))
        {
            File.WriteAllText(targetsPath, DefaultTargets);
            Console.WriteLine($"Created {targetsPath} with default configuration.");
            targetsCreated = true;
        }
        else
        {
            Console.WriteLine($"targets.txt already exists — skipped.");
        }

        if (!File.Exists(exclusionsPath))
        {
            File.WriteAllText(exclusionsPath, DefaultExclusions);
            Console.WriteLine($"Created {exclusionsPath} with default configuration.");
            exclusionsCreated = true;
        }
        else
        {
            Console.WriteLine($"exclusions.txt already exists — skipped.");
        }

        if (targetsCreated || exclusionsCreated)
        {
            Console.WriteLine();
            Console.WriteLine("Review the created file(s) and adjust to your needs, then run:");
            Console.WriteLine("  ubuntusafesnap backup");
        }
        else
        {
            Console.WriteLine();
            Console.WriteLine("All configuration files already exist. No changes made.");
        }

        return exitCode;
    }
}