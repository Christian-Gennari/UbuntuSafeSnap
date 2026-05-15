using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Shared;

/// <summary>
/// Provides cross-platform detection of the real user's home directory,
/// with sudo-awareness via SUDO_USER and getent fallback.
/// </summary>
public static class UserHomeHelper
{
    /// <summary>
    /// Detects the original user's home directory when running under sudo
    /// (via SUDO_USER + getent), falling back to Environment.SpecialFolder.UserProfile.
    /// </summary>
    public static string GetRealUserHome()
    {
        string? sudoUser = Environment.GetEnvironmentVariable("SUDO_USER");

        if (!string.IsNullOrEmpty(sudoUser))
        {
            string? passwdEntry = GetPasswdHome(sudoUser);
            if (passwdEntry is not null)
            {
                Log.Info("UserHomeHelper", $"Running under sudo for user '{sudoUser}', using home: {passwdEntry}");
                return passwdEntry;
            }
        }

        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }

    /// <summary>Looks up a user's home directory from /etc/passwd via getent.</summary>
    public static string? GetPasswdHome(string userName)
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
