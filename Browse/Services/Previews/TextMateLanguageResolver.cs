// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any
// purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using TextMateSharp.Grammars;

namespace Browse.Services.Previews;

/// <summary>
/// Resolves file extensions supported by TextMateSharp's bundled grammars.
/// </summary>
/// <remarks>
/// Keeping detection and rendering on the same grammar registry prevents the preview list drifting out of date.
/// </remarks>
public static class TextMateLanguageResolver
{
    private static readonly RegistryOptions RegistryOptions = new(default);

    public static string Resolve(string path)
    {
        var extension = Path.GetExtension(path);
        if (string.IsNullOrWhiteSpace(extension) || RegistryOptions.GetScopeByExtension(extension) == null)
            return null;
        return extension.TrimStart('.');
    }
}
