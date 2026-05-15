using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using UbuntuSafeSnap.Models;
using UbuntuSafeSnap.Services.Shared;
using UbuntuSafeSnap.UI;

namespace UbuntuSafeSnap.Services.Restore;

public class RestoreService(ConflictResolverService conflictResolver)
{

    public async Task<int> RestoreAsync(string archivePath, bool dryRun = false)
    {
        ArgumentNullException.ThrowIfNull(archivePath);

        if (Environment.UserName != "root")
        {
            if (dryRun)
            {
                Log.Info("RestoreService", "Running in dry-run mode. Root not required (read-only).");
            }
            else
            {
                Log.Error("RestoreService", "Run this command with sudo.");
                return 1;
            }
        }

        if (!File.Exists(archivePath))
        {
            Log.Error("RestoreService", $"Archive not found: {archivePath}");
            return 1;
        }

        string stagingDirectory = Path.Combine(
            Path.GetTempPath(),
            $"ubuntusafesnap-restore-{Guid.NewGuid():N}"
        );

        try
        {
            Log.Info("RestoreService", $"Extracting archive to: {stagingDirectory}");

            try
            {
                ZipFile.ExtractToDirectory(archivePath, stagingDirectory);
            }
            catch (InvalidDataException ex)
            {
                Log.Error("RestoreService", $"Archive is corrupted or invalid: {ex.Message}");
                return 1;
            }
            catch (IOException ex)
            {
                Log.Error("RestoreService", $"IO error extracting archive: {ex.Message}");
                return 1;
            }

            Log.Info("RestoreService", "Archive extracted successfully.");

            if (!dryRun)
                await RestoreAptSources(stagingDirectory);

            var pkgResult = await ReinstallPackagesAsync(stagingDirectory, dryRun);

            if (!dryRun)
            {
                string missingPkgPath = Path.Combine(stagingDirectory, "missing-packages.txt");
                if (File.Exists(missingPkgPath))
                {
                    string[] failedPkgs = await File.ReadAllLinesAsync(missingPkgPath);
                    Log.Info("RestoreService", $"Package re-installation had {failedPkgs.Length} failure(s). File restore will continue.");
                    Log.Info("RestoreService", "The following packages need third-party repos or manual installation:");
                    foreach (var pkg in failedPkgs)
                        Log.Info("RestoreService", $"  - {pkg}");
                    Log.Info("RestoreService", $"Details written to: {missingPkgPath}");
                }
            }

            var fileResult = await RestoreFilesAsync(stagingDirectory, dryRun);

            if (!dryRun && fileResult.Aborted)
            {
                Log.Error("RestoreService", $"Restore aborted. {fileResult.Restored} file(s) restored, {fileResult.Skipped} file(s) skipped before abort.");
                return 1;
            }

            if (dryRun)
            {
                Log.Info("RestoreService", "=== Dry-Run Summary ===");
                Log.Info("RestoreService", $"Packages: {pkgResult.AlreadyInstalled} already installed, {pkgResult.WouldInstall} would install");
                Log.Info("RestoreService", $"Files: {fileResult.NewFiles} new, {fileResult.IdenticalFiles} identical, {fileResult.ConflictingFiles} conflicting");
                Log.Info("RestoreService", "No changes were made to the system.");
            }
            else
            {
                Log.Info("RestoreService", $"Restore complete. {fileResult.Restored} file(s) restored. {fileResult.Skipped} file(s) skipped.");
            }

            return 0;
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
                Log.Info("RestoreService", $"Cleaned up staging directory: {stagingDirectory}");
            }
        }
    }

    private static async Task RestoreAptSources(string stagingDirectory)
    {
        string aptSourcesDir = Path.Combine(stagingDirectory, "apt-sources");

        if (!Directory.Exists(aptSourcesDir))
        {
            Log.Info("RestoreService", "No apt-sources found in archive. Skipping apt source restoration.");
            return;
        }

        bool restored = false;

        string sourcesListDir = Path.Combine(aptSourcesDir, "sources.list.d");
        if (Directory.Exists(sourcesListDir))
        {
            string destDir = "/etc/apt/sources.list.d";
            foreach (var file in Directory.GetFiles(sourcesListDir))
            {
                try
                {
                    string destPath = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destPath, overwrite: true);
                    Log.Info("RestoreService", $"Restored apt source: {destPath}");
                    restored = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Info("RestoreService", $"Cannot restore apt source {file} — permission denied (run with sudo).");
                }
            }
        }

        string keyringsDir = Path.Combine(aptSourcesDir, "keyrings");
        if (Directory.Exists(keyringsDir))
        {
            string destDir = "/etc/apt/keyrings";
            Directory.CreateDirectory(destDir);
            foreach (var file in Directory.GetFiles(keyringsDir))
            {
                try
                {
                    string destPath = Path.Combine(destDir, Path.GetFileName(file));
                    File.Copy(file, destPath, overwrite: true);
                    Log.Info("RestoreService", $"Restored apt keyring: {destPath}");
                    restored = true;
                }
                catch (UnauthorizedAccessException)
                {
                    Log.Info("RestoreService", $"Cannot restore keyring {file} — permission denied (run with sudo).");
                }
            }
        }

        if (restored)
        {
            Log.Info("RestoreService", "Ensuring ca-certificates is available for HTTPS repos...");
            using var certProcess = new Process();
            certProcess.StartInfo.FileName = "apt-get";
            certProcess.StartInfo.Arguments = "install -y ca-certificates";
            certProcess.StartInfo.UseShellExecute = false;
            certProcess.Start();
            await certProcess.WaitForExitAsync();

            if (certProcess.ExitCode != 0)
                Log.Info("RestoreService", "ca-certificates install returned non-zero (may already be up to date).");

            Log.Info("RestoreService", "Running apt update to refresh package cache...");
            using var updateProcess = new Process();
            updateProcess.StartInfo.FileName = "apt";
            updateProcess.StartInfo.Arguments = "update";
            updateProcess.StartInfo.UseShellExecute = false;
            updateProcess.Start();
            updateProcess.WaitForExit();

            if (updateProcess.ExitCode == 0)
                Log.Info("RestoreService", "apt update completed successfully.");
            else
                Log.Info("RestoreService", $"apt update returned exit code {updateProcess.ExitCode}. Some repos may not be available.");
        }
    }

    private static async Task<PackageRestoreResult> ReinstallPackagesAsync(string stagingDirectory, bool dryRun)
    {
        string packagesFile = Path.Combine(stagingDirectory, "packages.txt");

        if (!File.Exists(packagesFile))
        {
            Log.Info("RestoreService", "No packages.txt found in archive. Skipping package re-installation.");
            return new PackageRestoreResult(0, 0, 0);
        }

        string[] packages = await File.ReadAllLinesAsync(packagesFile);

        packages = packages
            .Select(p => p.Trim())
            .Where(p => !string.IsNullOrEmpty(p))
            .ToArray();

        if (packages.Length == 0)
        {
            Log.Info("RestoreService", "packages.txt is empty. No packages to reinstall.");
            return new PackageRestoreResult(0, 0, 0);
        }

        if (dryRun)
        {
            var installedPackages = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using (var dpkgProcess = new Process())
            {
                dpkgProcess.StartInfo.FileName = "dpkg";
                dpkgProcess.StartInfo.ArgumentList.Add("--get-selections");
                dpkgProcess.StartInfo.UseShellExecute = false;
                dpkgProcess.StartInfo.RedirectStandardOutput = true;

                dpkgProcess.Start();
                string output = await dpkgProcess.StandardOutput.ReadToEndAsync();
                await dpkgProcess.WaitForExitAsync();

                if (dpkgProcess.ExitCode != 0)
                {
                    Log.Info("RestoreService", $"dpkg --get-selections returned exit code {dpkgProcess.ExitCode}. Assuming all packages would install.");
                    return new PackageRestoreResult(0, packages.Length, 0);
                }

                foreach (var line in output.Split('\n'))
                {
                    var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 2 && parts[1] == "install")
                        installedPackages.Add(parts[0]);
                }
            }

            int alreadyInstalled = packages.Count(p => installedPackages.Contains(p));
            int wouldInstall = packages.Length - alreadyInstalled;

            Log.Info("RestoreService", $"Packages to check: {packages.Length}. {alreadyInstalled} already installed, {wouldInstall} would install.");
            return new PackageRestoreResult(alreadyInstalled, wouldInstall, 0);
        }

        Log.Info("RestoreService", $"Re-installing {packages.Length} package(s)...");

        using var process = new Process();
        process.StartInfo.FileName = "apt";
        process.StartInfo.ArgumentList.Add("install");
        process.StartInfo.ArgumentList.Add("-y");

        foreach (var pkg in packages)
        {
            process.StartInfo.ArgumentList.Add(pkg);
        }

        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardError = true;

        process.Start();
        string stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            Log.Info("RestoreService", $"apt install completed with exit code {process.ExitCode}. Continuing with file restore...");

            var failedPackages = new List<string>();
            foreach (Match match in Regex.Matches(stderr, @"E: Unable to locate package (.+)"))
                failedPackages.Add(match.Groups[1].Value);
            foreach (Match match in Regex.Matches(stderr, @"E: Package '(.+)' has no installation candidate"))
                failedPackages.Add(match.Groups[1].Value);

            var distinctFailed = failedPackages.Distinct().ToList();

            if (distinctFailed.Count > 0)
            {
                string missingPkgPath = Path.Combine(stagingDirectory, "missing-packages.txt");
                await File.WriteAllLinesAsync(missingPkgPath, distinctFailed);
                Log.Info("RestoreService", $"Wrote {distinctFailed.Count} missing package(s) to missing-packages.txt.");
            }

            return new PackageRestoreResult(0, 0, 0);
        }

        Log.Info("RestoreService", "Package re-installation complete.");
        return new PackageRestoreResult(0, 0, 0);
    }

    private async Task<FileRestoreResult> RestoreFilesAsync(string stagingDirectory, bool dryRun)
    {
        var manifest = LoadManifest(stagingDirectory);

        string? oldHome = DetectOldHome(manifest);
        string newHome = UserHomeHelper.GetRealUserHome();

        if (oldHome != null && oldHome != newHome)
            Log.Info("RestoreService", $"Remapping home directory: {oldHome} → {newHome}");

        if (dryRun)
        {
            int newFiles = 0, identicalFiles = 0, conflictingFiles = 0;

            foreach (var (file, destPath) in EnumerateRestoreFiles(stagingDirectory, manifest, oldHome, newHome))
            {
                if (File.Exists(destPath))
                {
                    try
                    {
                        string stagingHash = await ComputeSha256Async(file);
                        string destHash = await ComputeSha256Async(destPath);
                        if (stagingHash == destHash)
                        {
                            Log.Info("RestoreService", $"[Dry-run] Identical: {destPath}");
                            identicalFiles++;
                        }
                        else
                        {
                            Log.Info("RestoreService", $"[Dry-run] Conflicting: {destPath}");
                            conflictingFiles++;
                        }
                    }
                    catch (UnauthorizedAccessException)
                    {
                        Log.Info("RestoreService", $"[Dry-run] Unreadable: {destPath}");
                        conflictingFiles++;
                    }
                }
                else
                {
                    Log.Info("RestoreService", $"[Dry-run] New: {destPath}");
                    newFiles++;
                }
            }

            return new FileRestoreResult(newFiles, identicalFiles, conflictingFiles, 0, 0, false);
        }

        int restored = 0;
        int skipped = 0;

        foreach (var (file, destPath) in EnumerateRestoreFiles(stagingDirectory, manifest, oldHome, newHome))
        {
            if (File.Exists(destPath))
            {
                var resolution = await conflictResolver.ResolveAsync(file, destPath);

                switch (resolution)
                {
                    case ConflictResolution.Identical:
                        Log.Info("RestoreService", $"Skipped (identical): {destPath}");
                        skipped++;
                        break;
                    case ConflictResolution.Skip:
                        Log.Info("RestoreService", $"Skipped (user choice): {destPath}");
                        skipped++;
                        break;
                    case ConflictResolution.Overwrite:
                        try
                        {
                            File.Copy(file, destPath, overwrite: true);
                            Log.Info("RestoreService", $"Overwritten: {destPath}");
                            restored++;
                        }
                        catch (UnauthorizedAccessException)
                        {
                            Log.Info("RestoreService", $"Unauthorized access to destination: {destPath}");
                            skipped++;
                        }
                        break;
                    case ConflictResolution.Abort:
                        return new FileRestoreResult(0, 0, 0, restored, skipped, Aborted: true);
                }

                continue;
            }

            try
            {
                string? destDir = Path.GetDirectoryName(destPath);
                if (destDir is not null && !Directory.Exists(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                File.Copy(file, destPath, overwrite: false);
                Log.Info("RestoreService", $"Restored: {destPath}");
                restored++;
            }
            catch (UnauthorizedAccessException)
            {
                Log.Info("RestoreService", $"Unauthorized access to destination: {destPath}");
                skipped++;
            }
        }

        return new FileRestoreResult(0, 0, 0, restored, skipped, Aborted: false);
    }

    private static IEnumerable<(string file, string destPath)> EnumerateRestoreFiles(
        string stagingDirectory,
        Dictionary<string, string> manifest,
        string? oldHome,
        string newHome)
    {
        var directories = new Queue<string>();
        directories.Enqueue(stagingDirectory);

        while (directories.Count > 0)
        {
            var currentDir = directories.Dequeue();
            string[] files;
            string[] subDirs;

            try
            {
                files = Directory.GetFiles(currentDir);
                subDirs = Directory.GetDirectories(currentDir);
            }
            catch (UnauthorizedAccessException)
            {
                Log.Info("RestoreService", $"Unauthorized access to {currentDir}. Skipping...");
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                Log.Info("RestoreService", $"Directory not found: {currentDir}. Skipping...");
                continue;
            }

            foreach (var file in files)
            {
                string relativePath = Path.GetRelativePath(stagingDirectory, file);

                if (relativePath is "packages.txt" or "manifest.txt" or "missing-packages.txt" || relativePath.StartsWith("apt-sources/"))
                    continue;

                string destPath;

                if (manifest.TryGetValue(relativePath, out string? sourceDir))
                {
                    if (oldHome != null && sourceDir.StartsWith(oldHome) &&
                        (sourceDir.Length == oldHome.Length || sourceDir[oldHome.Length] == '/'))
                        sourceDir = newHome + sourceDir[oldHome.Length..];

                    destPath = Path.Combine(sourceDir, relativePath);
                }
                else
                {
                    destPath = Path.Combine("/", relativePath);
                }

                yield return (file, destPath);
            }

            foreach (var subDir in subDirs)
            {
                directories.Enqueue(subDir);
            }
        }
    }

    private static async Task<string> ComputeSha256Async(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        byte[] hash = await SHA256.HashDataAsync(stream);
        return Convert.ToHexString(hash);
    }

    private static Dictionary<string, string> LoadManifest(string stagingDirectory)
    {
        var manifest = new Dictionary<string, string>();

        string manifestPath = Path.Combine(stagingDirectory, "manifest.txt");

        if (!File.Exists(manifestPath))
        {
            Log.Info("RestoreService", "No manifest.txt found in archive. Files will be restored to /.");
            return manifest;
        }

        foreach (var rawLine in File.ReadAllLines(manifestPath))
        {
            var line = rawLine.Trim();
            if (string.IsNullOrEmpty(line))
                continue;

            int separatorIndex = line.IndexOf('|');
            if (separatorIndex < 0)
                continue;

            string sourceDir = line[..separatorIndex];
            string relativePath = line[(separatorIndex + 1)..];

            manifest[relativePath] = sourceDir;
        }

        Log.Info("RestoreService", $"Loaded manifest with {manifest.Count} entries.");
        return manifest;
    }

    private static string? DetectOldHome(Dictionary<string, string> manifest)
    {
        var homeCounts = new Dictionary<string, int>();

        foreach (var sourceDir in manifest.Values)
        {
            if (!sourceDir.StartsWith("/home/"))
                continue;

            int endIndex = sourceDir.IndexOf('/', "/home/".Length);
            string homePrefix = endIndex >= 0 ? sourceDir[..endIndex] : sourceDir;

            homeCounts.TryGetValue(homePrefix, out int count);
            homeCounts[homePrefix] = count + 1;
        }

        if (homeCounts.Count == 0)
            return null;

        return homeCounts.MaxBy(kvp => kvp.Value).Key;
    }
}
