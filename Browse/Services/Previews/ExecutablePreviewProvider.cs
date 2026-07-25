// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Browse.Models;
using DTC.Core.Extensions;

namespace Browse.Services.Previews;

/// <summary>
/// Describes Windows executable and library version resources.
/// </summary>
/// <remarks>
/// Version resources are portable, while embedded signature inspection is enabled only on Windows.
/// </remarks>
public sealed class ExecutablePreviewProvider : IPreviewProvider
{
    private static readonly string[] SupportedExtensions = [".exe", ".dll"];

    public ValueTask<bool> CanPreviewAsync(BrowserItem item, CancellationToken cancellationToken) =>
        ValueTask.FromResult(
            !item.IsDirectory && SupportedExtensions.Contains(Path.GetExtension(item.Name), StringComparer.OrdinalIgnoreCase));

    public Task<PreviewContent> CreateAsync(BrowserItem item, CancellationToken cancellationToken) => Task.Run<PreviewContent>(() =>
    {
        cancellationToken.ThrowIfCancellationRequested();
        var file = (FileInfo)item.Info;
        var version = FileVersionInfo.GetVersionInfo(file.FullName);
        var lines = new List<string>();
        Add(lines, "Description", version.FileDescription);
        Add(lines, "Product", version.ProductName);
        Add(lines, "Company", version.CompanyName);
        Add(lines, "File version", version.FileVersion);
        Add(lines, "Product version", version.ProductVersion);
        Add(lines, "Copyright", version.LegalCopyright);
        Add(lines, "Trademarks", version.LegalTrademarks);
        Add(lines, "Original filename", version.OriginalFilename);
        Add(lines, "Internal name", version.InternalName);
        Add(lines, "Language", version.Language);
        Add(lines, "Build flags", GetBuildFlags(version));
        Add(lines, "Digital signature", GetSignatureDescription(file));

        return new TextPreviewContent(
            item.Name,
            item.FullPath,
            $"{item.Size?.ToSize() ?? "Unknown size"} · Modified {item.LastWriteTime:g}",
            string.Join('\n', lines),
            TextPreviewMode.Plain,
            canExpand: false);
    }, cancellationToken);

    private static void Add(ICollection<string> lines, string label, string value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            lines.Add($"{label}: {value}");
    }

    private static string GetBuildFlags(FileVersionInfo version)
    {
        var flags = new List<string>();
        if (version.IsDebug)
            flags.Add("Debug");
        if (version.IsPreRelease)
            flags.Add("Pre-release");
        if (version.IsPatched)
            flags.Add("Patched");
        if (version.IsPrivateBuild)
            flags.Add("Private build");
        if (version.IsSpecialBuild)
            flags.Add("Special build");
        return flags.Count == 0 ? null : string.Join(", ", flags);
    }

    private static string GetSignatureDescription(FileInfo file)
    {
        if (!OperatingSystem.IsWindows())
            return "Inspection available on Windows";
        try
        {
            using var certificate = new X509Certificate2(X509Certificate.CreateFromSignedFile(file.FullName));
            var signer = certificate.GetNameInfo(X509NameType.SimpleName, false);
            return string.IsNullOrWhiteSpace(signer) ? "Signed" : $"Signed by {signer}";
        }
        catch (CryptographicException)
        {
            return "Not signed";
        }
    }
}
