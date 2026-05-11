namespace UbuntuSafeSnap.Services.Shared;

/// <summary>
/// Loads exclusion rules from a configuration file and evaluates whether files or
/// directories should be excluded from backup based on extension, filename, or
/// directory name matching (case-insensitive).
/// </summary>
public class ExclusionService
{
    private readonly HashSet<string> _excludedExtensions = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedFilenames = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedDirectories = new(StringComparer.OrdinalIgnoreCase);

    public ExclusionService() { }

    /// <summary>
    /// Parses the exclusion file. Lines ending with / are directory rules,
    /// lines starting with . are extension rules, everything else is a filename rule.
    /// Lines starting with # and blank lines are ignored.
    /// </summary>
    /// <param name="filePath">Path to the exclusions configuration file.</param>
    /// <exception cref="FileNotFoundException">Thrown when the file does not exist.</exception>
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

    /// <summary>Returns true if the file's extension or filename matches any exclusion rule.</summary>
    public bool ShouldExclude(string filePath)
    {
        return _excludedExtensions.Contains(Path.GetExtension(filePath))
            || _excludedFilenames.Contains(Path.GetFileName(filePath));
    }

    /// <summary>Returns true if the directory name matches any directory exclusion rule.</summary>
    public bool ShouldExcludeDirectory(string dirPath)
    {
        return _excludedDirectories.Contains(Path.GetFileName(dirPath));
    }
}