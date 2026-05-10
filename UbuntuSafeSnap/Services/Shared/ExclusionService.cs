namespace UbuntuSafeSnap.Services.Shared;

public class ExclusionService
{
    private readonly HashSet<string> _forbiddenExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _forbiddenFilenames = new(StringComparer.OrdinalIgnoreCase);

    public ExclusionService(string exclusionsFilePath)
    {
        Load(exclusionsFilePath);
    }

    public void Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException(
                $"Exclusion file not found: {filePath}",
                filePath);
        }

        _forbiddenExtensions.Clear();
        _forbiddenFilenames.Clear();

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            if (line.StartsWith('.'))
            {
                _forbiddenExtensions.Add(line);
            }
            else
            {
                _forbiddenFilenames.Add(line);
            }
        }
    }

    public bool ShouldExclude(string filePath)
    {
        return _forbiddenExtensions.Contains(Path.GetExtension(filePath))
            || _forbiddenFilenames.Contains(Path.GetFileName(filePath));
    }
}