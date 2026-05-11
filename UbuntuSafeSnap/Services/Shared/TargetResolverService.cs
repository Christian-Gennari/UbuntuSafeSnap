using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Shared;

/// <summary>
/// Resolves target entries from targets.txt into absolute filesystem paths.
/// Handles ~ expansion, sudo-aware home directory detection, and skips the home directory itself.
/// </summary>
public class TargetResolverService
{
    /// <summary>
    /// Reads targets.txt, expands ~ to the real user's home directory (sudo-aware),
    /// resolves full paths, and skips entries that resolve to the home directory itself.
    /// </summary>
    /// <param name="filePath">Path to the targets.txt file.</param>
    /// <returns>Resolved absolute directory paths to back up.</returns>
    public IEnumerable<string> Resolve(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Target file not found: {filePath}",
                filePath);
        }

        string homeDirectory = GetRealUserHome();

        foreach (var raw in File.ReadAllLines(filePath))
        {
            var line = raw.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            string expanded = line.StartsWith('~')
                ? homeDirectory + line[1..]
                : line;

            string fullPath = Path.GetFullPath(expanded);

            if (fullPath == homeDirectory)
            {
                Log.Info("TargetResolverService", $"Skipping home directory: {fullPath}");
                continue;
            }

            yield return fullPath;
        }
    }

    /// <summary>
    /// Detects the original user's home directory when running under sudo
    /// (via SUDO_USER + getent), falling back to Environment.SpecialFolder.UserProfile.
    /// </summary>
    private static string GetRealUserHome()
    {
        string? sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");

        if (!string.IsNullOrEmpty(sudoUser))
        {
            string? passwdEntry = GetPasswdHome(sudoUser);
            if (passwdEntry is not null)
            {
                Log.Info("TargetResolverService", $"Running under sudo for user '{sudoUser}', using home: {passwdEntry}");
                return passwdEntry;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Looks up a user's home directory from /etc/passwd via getent.</summary>
    private static string? GetPasswdHome(string userName)
    {
        try
        {
            using var process = new System.Diagnostics.Process();
            process.StartInfo.FileName = "getent";
            process.StartInfo.ArgumentList.Add("passwd");
            process.StartInfo.ArgumentList.Add(userName);
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.Start();

            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                return null;

            string[] fields = output.Trim().Split(':');
            if (fields.Length >= 6)
                return fields[5];
        }
        catch
        {
        }

        return null;
    }
}