namespace UbuntuSafeSnap.Models;

public record PackageRestoreResult(int AlreadyInstalled, int WouldInstall, int ExitCode);
