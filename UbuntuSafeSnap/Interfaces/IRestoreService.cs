namespace UbuntuSafeSnap.Interfaces;

public interface IRestoreService
{
    Task<int> RestoreAsync(string archivePath);
}