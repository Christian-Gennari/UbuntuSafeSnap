namespace UbuntuSafeSnap.Services.Shared;

public class ExclusionService
{
    private readonly HashSet<string> _excludedExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedFilenames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedDirectories = new(StringComparer.OrdinalIgnoreCase);

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

        _excludedExtensions.Clear();
        _excludedFilenames.Clear();
        _excludedDirectories.Clear();

        foreach (var rawLine in File.ReadAllLines(filePath))
        {
            var line = rawLine.Trim();

            if (string.IsNullOrEmpty(line) || line.StartsWith('#'))
                continue;

            if (line.EndsWith('/'))
            {
                _excludedDirectories.Add(line.TrimEnd('/'));
            }
            else if (line.StartsWith('.'))
            {
                _excludedExtensions.Add(line);
            }
            else
            {
                _excludedFilenames.Add(line);
            }
        }
    }

    public bool ShouldExclude(string filePath)
    {
        return _excludedExtensions.Contains(Path.GetExtension(filePath))
            || _excludedFilenames.Contains(Path.GetFileName(filePath));
    }

    public bool ShouldExcludeDirectory(string dirPath)
    {
        return _excludedDirectories.Contains(Path.GetFileName(dirPath));
    }
}