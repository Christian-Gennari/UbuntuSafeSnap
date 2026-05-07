namespace UbuntuSafeSnap.Interfaces;

public interface IExclusionService
{
    void Load(string filePath);
    bool ShouldExclude(string filePath);
}
