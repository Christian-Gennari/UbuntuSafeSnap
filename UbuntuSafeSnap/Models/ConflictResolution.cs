namespace UbuntuSafeSnap.Models;

/// <summary>Represents the user's choice when a file conflict is detected during restore.</summary>
public enum ConflictResolution
{
    /// <summary>Replace the existing file with the backup version.</summary>
    Overwrite,
    /// <summary>Keep the existing file and skip restoring this file.</summary>
    Skip,
    /// <summary>Both files are identical — no action needed.</summary>
    Identical,
    /// <summary>Stop the entire restore process.</summary>
    Abort
}