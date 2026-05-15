namespace UbuntuSafeSnap.Models;

public record FileRestoreResult(int NewFiles, int IdenticalFiles, int ConflictingFiles, int Restored, int Skipped, bool Aborted);
