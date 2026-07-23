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

using Avalonia.Media;
using Material.Icons;

/// <summary>
/// Describes one file-system entry displayed in a browser column.
/// </summary>
/// <remarks>
/// The model keeps only inexpensive metadata so directory enumeration remains responsive.
/// </remarks>
public sealed class BrowserItem
{
    public BrowserItem(FileSystemInfo info)
    {
        Info = info;
        Name = info.Name.Length == 0 ? info.FullName : info.Name;
        (NamePrefix, NameSuffix) = SplitName(Name);
        FullPath = info.FullName;
        IsDirectory = info is DirectoryInfo;
        IsDotFolder = IsDirectory && Name.StartsWith(".", StringComparison.Ordinal) && Name.Length > 1;
        try
        {
            IsHidden = info.Attributes.HasFlag(FileAttributes.Hidden) ||
                       info.Attributes.HasFlag(FileAttributes.System) ||
                       (!IsDirectory && Name.StartsWith(".", StringComparison.Ordinal));
            LastWriteTime = info.LastWriteTime;
            Size = info is FileInfo file ? file.Length : null;
        }
        catch (Exception)
        {
            IsUnavailable = true;
        }
        (Icon, IconBrush) = GetIcon();
    }

    public FileSystemInfo Info { get; }
    public string Name { get; }
    public string NamePrefix { get; }
    public string NameSuffix { get; }
    public string FullPath { get; }
    public bool IsDirectory { get; }
    public bool IsDotFolder { get; }
    public bool IsHidden { get; }
    public bool IsUnavailable { get; }
    public DateTime LastWriteTime { get; }
    public long? Size { get; }
    public MaterialIconKind Icon { get; }
    public IBrush IconBrush { get; }

    public static (string Prefix, string Suffix) SplitName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length <= 30)
            return (name, null);
        const int suffixLength = 14;
        return (name[..(name.Length - suffixLength)], name[^suffixLength..]);
    }

    private (MaterialIconKind Icon, IBrush Brush) GetIcon()
    {
        if (IsDirectory)
            return (MaterialIconKind.Folder, Brush.Parse("#E7BD68"));
        var extension = Path.GetExtension(Name).ToLowerInvariant();
        if (extension is ".zip" or ".7z" or ".rar" or ".tar" or ".gz" or ".bz2")
            return (MaterialIconKind.ZipBox, Brush.Parse("#D7A6FF"));
        if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico" or ".icns" or ".tif" or ".tiff")
            return (MaterialIconKind.FileImage, Brush.Parse("#72D5C7"));
        if (extension == ".md")
            return (MaterialIconKind.LanguageMarkdown, Brush.Parse("#8DBCEB"));
        if (extension is ".txt" or ".log" or ".ini" or ".cfg")
            return (MaterialIconKind.FileDocument, Brush.Parse("#CAD2DC"));
        if (extension is ".cs" or ".cpp" or ".c" or ".h" or ".json" or ".xml" or ".xaml" or ".axaml" or ".ps1" or ".sh" or ".bat" or ".cmd" or ".js" or ".ts" or ".py")
            return (MaterialIconKind.FileCode, Brush.Parse("#7EB8F2"));
        if (extension == ".pdf")
            return (MaterialIconKind.FilePdfBox, Brush.Parse("#F28080"));
        if (extension is ".doc" or ".docx")
            return (MaterialIconKind.FileWord, Brush.Parse("#6EA8FE"));
        if (extension is ".xls" or ".xlsx" or ".csv")
            return (extension == ".csv" ? MaterialIconKind.FileDelimited : MaterialIconKind.FileExcel, Brush.Parse("#69C18E"));
        if (extension is ".ppt" or ".pptx")
            return (MaterialIconKind.FilePowerpoint, Brush.Parse("#EE8A63"));
        if (extension is ".mp3" or ".wav" or ".flac" or ".m4a" or ".ogg")
            return (MaterialIconKind.FileMusic, Brush.Parse("#E793D2"));
        if (extension is ".mp4" or ".mov" or ".avi" or ".mkv" or ".webm")
            return (MaterialIconKind.FileVideo, Brush.Parse("#B6A2F2"));
        if (extension is ".db" or ".sqlite" or ".sqlite3")
            return (MaterialIconKind.Database, Brush.Parse("#F0B56B"));
        if (extension is ".exe" or ".msi" or ".app")
            return (MaterialIconKind.Application, Brush.Parse("#AEB7C4"));
        return (MaterialIconKind.FileDocumentOutline, Brush.Parse("#AEB7C4"));
    }
}
