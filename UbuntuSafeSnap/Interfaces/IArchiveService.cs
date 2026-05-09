namespace UbuntuSafeSnap.Interfaces;

public interface IArchiveService
{
    void CreateArchive(string stagingDirectory, string outputPath);
    void PruneOldArchives(string backupsDirectory, int keepCount);
}
