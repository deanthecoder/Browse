// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

namespace Browse.Models;

/// <summary>
/// Resolves user-defined file extensions to the built-in type they should behave like.
/// </summary>
/// <remarks>
/// Aliases affect presentation and supported actions without changing the underlying filename.
/// </remarks>
public sealed class FileTypeAliasMap
{
    private readonly Dictionary<string, string> m_aliases = new(StringComparer.OrdinalIgnoreCase);

    public FileTypeAliasMap(IEnumerable<string> mappings)
    {
        foreach (var mapping in mappings ?? [])
        {
            if (TryParseMapping(mapping, out var source, out var target))
                m_aliases[source] = target;
        }
    }

    /// <summary>
    /// Gets the final effective extension for a filename, following chained aliases.
    /// </summary>
    public string ResolveExtension(string fileName)
    {
        var original = Path.GetExtension(fileName).ToLowerInvariant();
        var extension = original;
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (m_aliases.TryGetValue(extension, out var alias))
        {
            if (!visited.Add(extension))
                return original;
            extension = alias;
        }
        return extension;
    }

    /// <summary>
    /// Validates and normalizes newline-separated mappings such as <c>.bundle=.zip</c>.
    /// </summary>
    public static bool TryNormalize(string text, out string[] mappings, out string error)
    {
        var normalized = new List<string>();
        var sources = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var lines = (text ?? string.Empty).Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0)
                continue;
            if (!TryParseMapping(line, out var source, out var target))
            {
                mappings = [];
                error = $"Line {index + 1} should look like .bundle=.zip";
                return false;
            }
            if (!sources.Add(source))
            {
                mappings = [];
                error = $"{source} is mapped more than once.";
                return false;
            }
            normalized.Add($"{source}={target}");
        }
        mappings = normalized.ToArray();
        error = null;
        return true;
    }

    private static bool TryParseMapping(string mapping, out string source, out string target)
    {
        source = null;
        target = null;
        var parts = mapping?.Split('=', StringSplitOptions.TrimEntries);
        if (parts is not { Length: 2 })
            return false;
        source = NormalizeExtension(parts[0]);
        target = NormalizeExtension(parts[1]);
        return source != null && target != null;
    }

    private static string NormalizeExtension(string extension)
    {
        extension = extension?.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(extension))
            return null;
        if (!extension.StartsWith('.'))
            extension = $".{extension}";
        return extension.Length > 1 && !extension.Any(character =>
            char.IsWhiteSpace(character) || character is '/' or '\\' or '=')
            ? extension
            : null;
    }
}
