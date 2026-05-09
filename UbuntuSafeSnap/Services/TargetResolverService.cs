using UbuntuSafeSnap.Interfaces;

namespace UbuntuSafeSnap.Services;

public class TargetResolverService : ITargetResolverService
{
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
                Console.WriteLine($"[TargetResolverService] Skipping home directory: {fullPath}");
                continue;
            }

            yield return fullPath;
        }
    }

    private static string GetRealUserHome()
    {
        string? sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");

        if (!string.IsNullOrEmpty(sudoUser))
        {
            string? passwdEntry = GetPasswdHome(sudoUser);
            if (passwdEntry is not null)
            {
                Console.WriteLine($"[TargetResolverService] Running under sudo for user '{sudoUser}', using home: {passwdEntry}");
                return passwdEntry;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    private static string? GetPasswdHome(string userName)
    {
        try
        {
            var process = new System.Diagnostics.Process();
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
