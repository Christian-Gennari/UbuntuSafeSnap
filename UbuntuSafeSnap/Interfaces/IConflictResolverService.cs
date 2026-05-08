namespace UbuntuSafeSnap.Interfaces;

public enum ConflictResolution
{
    Overwrite,
    Skip,
    Abort
}

public interface IConflictResolverService
{
    Task<ConflictResolution> ResolveAsync(string stagingFile, string destFile);
}