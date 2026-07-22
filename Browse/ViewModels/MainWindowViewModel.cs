// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using BitMiracle.LibTiff.Classic;
using Browse.Models;
using Browse.Services;
using DTC.Core.Extensions;
using DTC.Core.ViewModels;
using Material.Icons;

namespace Browse.ViewModels;

/// <summary>
/// Coordinates sidebar navigation, folder columns, previews, and file operations.
/// </summary>
/// <remarks>
/// Filesystem services remain independent so slow I/O never needs to run on the UI thread.
/// </remarks>
public sealed class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly DirectoryContentService m_directoryService;
    private readonly PreviewService m_previewService;
    private readonly FileOperationService m_fileOperationService;
    private readonly SettingsService m_settingsService;
    private readonly List<BrowserItem> m_selectedItems = [];
    private readonly List<BrowserItem> m_clipboardItems = [];
    private readonly Dictionary<FolderColumnViewModel, FileSystemWatcher> m_watchers = [];
    private readonly ConcurrentDictionary<FolderColumnViewModel, int> m_watcherGenerations = [];
    private CancellationTokenSource m_navigationCancellation = new();
    private CancellationTokenSource m_previewCancellation = new();
    private bool m_clipboardIsCut;
    private bool m_isGoToVisible;
    private bool m_isSettingsVisible;
    private string m_goToPath;
    private string m_currentPath;
    private string m_statusText = "Ready";
    private string m_previewSummary = "No selection";
    private string m_previewContent;
    private string m_previewImagePath;
    private Bitmap m_previewImage;
    private string m_previewPath;
    private string m_previewDetails;
    private MaterialIconKind m_previewIcon = MaterialIconKind.FileDocumentOutline;
    private IBrush m_previewIconBrush = Brush.Parse("#AEB7C4");
    private bool m_hasSelection;
    private PreviewKind m_previewKind;
    private string m_folderSize;
    private string m_renameText;
    private bool m_isRenameVisible;

    public MainWindowViewModel(
        DirectoryContentService directoryService,
        PreviewService previewService,
        FileOperationService fileOperationService,
        SettingsService settingsService)
    {
        m_directoryService = directoryService;
        m_previewService = previewService;
        m_fileOperationService = fileOperationService;
        m_settingsService = settingsService;
        Settings = settingsService.Load();
        PopulateSidebar();
    }

    public BrowserSettings Settings { get; }
    public ObservableCollection<SidebarEntryViewModel> Favorites { get; } = [];
    public ObservableCollection<SidebarEntryViewModel> Drives { get; } = [];
    public ObservableCollection<FolderColumnViewModel> Columns { get; } = [];

    public string CurrentPath
    {
        get => m_currentPath;
        private set => SetField(ref m_currentPath, value);
    }

    public string StatusText
    {
        get => m_statusText;
        private set => SetField(ref m_statusText, value);
    }

    public bool IsGoToVisible
    {
        get => m_isGoToVisible;
        set => SetField(ref m_isGoToVisible, value);
    }

    public string GoToPath
    {
        get => m_goToPath;
        set => SetField(ref m_goToPath, value);
    }

    public bool IsSettingsVisible
    {
        get => m_isSettingsVisible;
        set => SetField(ref m_isSettingsVisible, value);
    }

    public bool ShowHiddenItems
    {
        get => Settings.ShowHiddenItems;
        set
        {
            if (Settings.ShowHiddenItems == value)
                return;
            Settings.ShowHiddenItems = value;
            OnPropertyChanged();
            SaveSettingsAndReload();
        }
    }

    public bool ShowDotFolders
    {
        get => Settings.ShowDotFolders;
        set
        {
            if (Settings.ShowDotFolders == value)
                return;
            Settings.ShowDotFolders = value;
            OnPropertyChanged();
            SaveSettingsAndReload();
        }
    }

    public string PreviewSummary
    {
        get => m_previewSummary;
        private set => SetField(ref m_previewSummary, value);
    }

    public string PreviewContent
    {
        get => m_previewContent;
        private set => SetField(ref m_previewContent, value);
    }

    public string PreviewPath
    {
        get => m_previewPath;
        private set => SetField(ref m_previewPath, value);
    }

    public string PreviewDetails
    {
        get => m_previewDetails;
        private set => SetField(ref m_previewDetails, value);
    }

    public MaterialIconKind PreviewIcon
    {
        get => m_previewIcon;
        private set => SetField(ref m_previewIcon, value);
    }

    public IBrush PreviewIconBrush
    {
        get => m_previewIconBrush;
        private set => SetField(ref m_previewIconBrush, value);
    }

    public bool HasSelection
    {
        get => m_hasSelection;
        private set
        {
            if (SetField(ref m_hasSelection, value))
                OnPropertyChanged(nameof(IsNoPreview));
        }
    }

    public string PreviewImagePath
    {
        get => m_previewImagePath;
        private set => SetField(ref m_previewImagePath, value);
    }

    public Bitmap PreviewImage
    {
        get => m_previewImage;
        private set
        {
            var previous = m_previewImage;
            if (SetField(ref m_previewImage, value))
                previous?.Dispose();
        }
    }

    public PreviewKind PreviewKind
    {
        get => m_previewKind;
        private set
        {
            if (!SetField(ref m_previewKind, value))
                return;
            OnPropertyChanged(nameof(IsTextPreview));
            OnPropertyChanged(nameof(IsCodePreview));
            OnPropertyChanged(nameof(IsMarkdownPreview));
            OnPropertyChanged(nameof(IsArchivePreview));
            OnPropertyChanged(nameof(IsImagePreview));
            OnPropertyChanged(nameof(IsFolderPreview));
            OnPropertyChanged(nameof(IsNoPreview));
            OnPropertyChanged(nameof(CanExpandPreview));
        }
    }

    public bool IsTextPreview => PreviewKind == PreviewKind.Text;
    public bool IsCodePreview => PreviewKind == PreviewKind.Code;
    public bool IsMarkdownPreview => PreviewKind == PreviewKind.Markdown;
    public bool IsArchivePreview => PreviewKind == PreviewKind.Archive;
    public bool IsImagePreview => PreviewKind == PreviewKind.Image;
    public bool IsFolderPreview => PreviewKind == PreviewKind.Folder;
    public bool IsNoPreview => HasSelection && PreviewKind is PreviewKind.None or PreviewKind.Multiple;
    public bool CanExpandPreview => PreviewKind is PreviewKind.Text or PreviewKind.Code or PreviewKind.Markdown or PreviewKind.Archive or PreviewKind.Image;

    public string FolderSize
    {
        get => m_folderSize;
        private set
        {
            if (SetField(ref m_folderSize, value))
                OnPropertyChanged(nameof(CanCalculateFolderSize));
        }
    }

    public bool CanCalculateFolderSize => string.IsNullOrWhiteSpace(FolderSize);
    public bool CanPaste => m_clipboardItems.Count > 0;

    public bool ClipboardMatches(IEnumerable<string> paths)
    {
        var clipboardPaths = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return clipboardPaths.Count == m_clipboardItems.Count &&
               m_clipboardItems.All(item => clipboardPaths.Contains(item.FullPath));
    }

    public void ClearClipboard()
    {
        m_clipboardItems.Clear();
        m_clipboardIsCut = false;
        OnPropertyChanged(nameof(CanPaste));
    }

    public bool IsRenameVisible
    {
        get => m_isRenameVisible;
        set => SetField(ref m_isRenameVisible, value);
    }

    public string RenameText
    {
        get => m_renameText;
        set => SetField(ref m_renameText, value);
    }

    public async Task InitializeAsync(string requestedPath = null)
    {
        var path = requestedPath;
        if (string.IsNullOrWhiteSpace(path))
            path = Settings.DefaultPath;
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
            path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        await NavigateToAsync(path);
    }

    public async Task NavigateToAsync(string path)
    {
        try
        {
            path = ExpandPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            StatusText = "The path is not valid.";
            return;
        }
        if (File.Exists(path))
            path = Path.GetDirectoryName(path);
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            StatusText = "Path not found or unavailable.";
            return;
        }

        m_navigationCancellation.Cancel();
        m_navigationCancellation.Dispose();
        m_navigationCancellation = new CancellationTokenSource();
        DisposeWatchers();
        Columns.Clear();
        m_selectedItems.Clear();
        var directory = new DirectoryInfo(path);
        CurrentPath = directory.FullName;
        GoToPath = CurrentPath;
        Settings.DefaultPath = CurrentPath;
        m_settingsService.Save(Settings);
        await AddColumnAsync(directory, m_navigationCancellation.Token);
    }

    public async Task SelectAsync(FolderColumnViewModel column, IReadOnlyList<BrowserItem> selection)
    {
        column.SetSelection(selection);
        var columnIndex = Columns.IndexOf(column);
        while (Columns.Count > columnIndex + 1)
            RemoveLastColumn();

        m_selectedItems.Clear();
        m_selectedItems.AddRange(selection);
        FolderSize = null;
        await UpdatePreviewAsync();

        if (selection.Count == 1 && selection[0].IsDirectory)
        {
            CurrentPath = selection[0].FullPath;
            await AddColumnAsync(new DirectoryInfo(selection[0].FullPath), m_navigationCancellation.Token);
        }
        else
        {
            CurrentPath = column.Directory.FullName;
        }
        StatusText = selection.Count == 0 ? $"{column.Items.Count:N0} items" : $"{selection.Count:N0} selected";
    }

    public void ShowGoTo()
    {
        GoToPath = CurrentPath;
        IsGoToVisible = true;
    }

    public async Task SubmitGoToAsync()
    {
        IsGoToVisible = false;
        await NavigateToAsync(GoToPath);
    }

    public void CopySelection(bool cut)
    {
        m_clipboardItems.Clear();
        m_clipboardItems.AddRange(m_selectedItems);
        m_clipboardIsCut = cut;
        OnPropertyChanged(nameof(CanPaste));
        StatusText = $"{m_clipboardItems.Count:N0} item(s) ready to {(cut ? "move" : "copy")}.";
    }

    public async Task PasteAsync()
    {
        if (m_clipboardItems.Count == 0 || string.IsNullOrWhiteSpace(CurrentPath))
            return;
        var destination = new DirectoryInfo(CurrentPath);
        StatusText = m_clipboardIsCut ? "Moving…" : "Copying…";
        try
        {
            await m_fileOperationService.CopyAsync(m_clipboardItems, destination, m_clipboardIsCut);
            m_directoryService.Invalidate(destination);
            if (m_clipboardIsCut)
            {
                m_clipboardItems.Clear();
                OnPropertyChanged(nameof(CanPaste));
            }
            await ReloadCurrentAsync();
            StatusText = "File operation complete.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public async Task ImportDroppedPathsAsync(IEnumerable<string> paths, DirectoryInfo destination, bool move)
    {
        var items = paths
            .Where(path => File.Exists(path) || Directory.Exists(path))
            .Select(path => new BrowserItem(Directory.Exists(path) ? new DirectoryInfo(path) : new FileInfo(path)))
            .Where(item => !string.Equals(item.FullPath, destination.FullName, StringComparison.OrdinalIgnoreCase))
            .Where(item => !string.Equals(Path.GetDirectoryName(item.FullPath), destination.FullName, StringComparison.OrdinalIgnoreCase))
            .ToArray();
        if (items.Length == 0)
            return;
        try
        {
            StatusText = $"{(move ? "Moving" : "Copying")} {items.Length:N0} dropped item(s)…";
            await m_fileOperationService.CopyAsync(items, destination, move);
            m_directoryService.Invalidate(destination);
            await ReloadCurrentAsync();
            StatusText = "Drop complete.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public Task ImportClipboardPathsAsync(IEnumerable<string> paths) =>
        string.IsNullOrWhiteSpace(CurrentPath)
            ? Task.CompletedTask
            : ImportDroppedPathsAsync(paths, new DirectoryInfo(CurrentPath), false);

    public IReadOnlyList<BrowserItem> SelectedItems => m_selectedItems;

    public static string JoinPaths(IEnumerable<BrowserItem> items, bool namesOnly = false) =>
        string.Join(' ', items.Select(item => QuoteIfNeeded(namesOnly ? item.Name : item.FullPath)));

    public string GetSelectedPaths(bool namesOnly = false) => JoinPaths(m_selectedItems, namesOnly);

    public void OpenSelected()
    {
        if (m_selectedItems.Count != 1)
            return;
        if (m_selectedItems[0].IsDirectory)
            _ = NavigateToAsync(m_selectedItems[0].FullPath);
        else
            m_fileOperationService.Open(m_selectedItems[0]);
    }

    public void OpenTerminal()
    {
        var path = m_selectedItems.Count == 1
            ? m_selectedItems[0].IsDirectory ? m_selectedItems[0].FullPath : Path.GetDirectoryName(m_selectedItems[0].FullPath)
            : CurrentPath;
        if (!string.IsNullOrWhiteSpace(path))
            m_fileOperationService.OpenTerminal(new DirectoryInfo(path), Settings.TerminalCommand);
    }

    public void BeginRename()
    {
        if (m_selectedItems.Count != 1)
            return;
        RenameText = m_selectedItems[0].Name;
        IsRenameVisible = true;
    }

    public async Task CommitRenameAsync()
    {
        IsRenameVisible = false;
        if (m_selectedItems.Count != 1)
            return;
        try
        {
            var parent = new DirectoryInfo(Path.GetDirectoryName(m_selectedItems[0].FullPath) ?? CurrentPath);
            await m_fileOperationService.RenameAsync(m_selectedItems[0], RenameText);
            m_directoryService.Invalidate(parent);
            await ReloadCurrentAsync();
            StatusText = "Renamed.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public async Task CalculateFolderSizeAsync()
    {
        if (m_selectedItems.Count != 1 || !m_selectedItems[0].IsDirectory)
            return;
        FolderSize = "Calculating…";
        try
        {
            var bytes = await m_fileOperationService.CalculateFolderSizeAsync(new DirectoryInfo(m_selectedItems[0].FullPath));
            FolderSize = $"{bytes.ToSize()} ({bytes:N0} bytes)";
        }
        catch (Exception ex)
        {
            FolderSize = ex.Message;
        }
    }

    public async Task CreateZipAsync()
    {
        if (m_selectedItems.Count == 0)
            return;
        var parent = new DirectoryInfo(Path.GetDirectoryName(m_selectedItems[0].FullPath) ?? CurrentPath);
        var baseName = m_selectedItems.Count == 1 ? Path.GetFileNameWithoutExtension(m_selectedItems[0].Name) : "Archive";
        var output = new FileInfo(FileOperationService.GetAvailablePath(Path.Combine(parent.FullName, baseName + ".zip")));
        try
        {
            await m_fileOperationService.CreateZipAsync(m_selectedItems, output);
            StatusText = $"Created {output.Name}.";
            m_directoryService.Invalidate(parent);
            await ReloadCurrentAsync();
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public async Task DeleteSelectionAsync()
    {
        if (m_selectedItems.Count == 0)
            return;
        var items = m_selectedItems.ToArray();
        var parents = items
            .Select(item => Path.GetDirectoryName(item.FullPath))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => new DirectoryInfo(path))
            .ToArray();
        try
        {
            StatusText = $"Moving {items.Length:N0} item(s) to the recycle bin…";
            await m_fileOperationService.MoveToTrashAsync(items);
            foreach (var parent in parents)
                m_directoryService.Invalidate(parent);
            await ReloadCurrentAsync();
            StatusText = $"Moved {items.Length:N0} item(s) to the recycle bin.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public async Task ReloadAllAsync()
    {
        foreach (var column in Columns)
            m_directoryService.Invalidate(column.Directory);
        await ReloadCurrentAsync();
    }

    private async Task ReloadCurrentAsync()
    {
        var columns = Columns.ToArray();
        foreach (var column in columns)
            await LoadColumnAsync(column, m_navigationCancellation.Token);
    }

    private async Task AddColumnAsync(DirectoryInfo directory, CancellationToken cancellationToken)
    {
        var column = new FolderColumnViewModel(directory);
        Columns.Add(column);
        await LoadColumnAsync(column, cancellationToken);
        Watch(column);
    }

    private async Task LoadColumnAsync(FolderColumnViewModel column, CancellationToken cancellationToken)
    {
        column.IsLoading = true;
        column.Error = null;
        try
        {
            var items = await m_directoryService.GetItemsAsync(column.Directory, Settings, cancellationToken);
            column.ReplaceItems(items);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            column.Error = ex.Message;
        }
        finally
        {
            column.IsLoading = false;
        }
        StatusText = $"{column.Items.Count:N0} items";
    }

    private async Task UpdatePreviewAsync()
    {
        m_previewCancellation.Cancel();
        m_previewCancellation.Dispose();
        m_previewCancellation = new CancellationTokenSource();
        try
        {
            var result = await m_previewService.CreateAsync(m_selectedItems, m_previewCancellation.Token);
            Bitmap image = null;
            if (!string.IsNullOrWhiteSpace(result.ImagePath))
                image = await Task.Run(() => DecodePreviewImage(result.ImagePath), m_previewCancellation.Token);
            PreviewKind = result.Kind;
            PreviewSummary = result.Name;
            PreviewPath = result.Path;
            PreviewDetails = result.Details;
            PreviewContent = result.Content;
            PreviewImagePath = result.ImagePath;
            PreviewImage = image;
            HasSelection = m_selectedItems.Count > 0;
            if (m_selectedItems.Count == 1)
            {
                PreviewIcon = m_selectedItems[0].Icon;
                PreviewIconBrush = m_selectedItems[0].IconBrush;
            }
        }
        catch (OperationCanceledException)
        {
            // A newer selection owns the preview.
        }
        catch (Exception ex)
        {
            PreviewKind = PreviewKind.None;
            PreviewImage = null;
            PreviewContent = null;
            PreviewDetails = $"Preview unavailable · {ex.Message}";
            HasSelection = m_selectedItems.Count > 0;
        }
    }

    private static Bitmap DecodePreviewImage(string path)
    {
        var extension = Path.GetExtension(path);
        if (!extension.Equals(".tif", StringComparison.OrdinalIgnoreCase) &&
            !extension.Equals(".tiff", StringComparison.OrdinalIgnoreCase))
        {
            using var fileStream = File.OpenRead(path);
            return Bitmap.DecodeToHeight(fileStream, 700, BitmapInterpolationMode.HighQuality);
        }

        using var tiff = Tiff.Open(path, "r") ?? throw new IOException("The TIFF file could not be opened.");
        var width = tiff.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
        var height = tiff.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
        var raster = new int[checked(width * height)];
        if (!tiff.ReadRGBAImageOriented(width, height, raster, Orientation.TOPLEFT))
            throw new IOException("The TIFF image could not be decoded.");

        var scale = Math.Min(1.0, 700.0 / Math.Max(width, height));
        var previewWidth = Math.Max(1, (int)Math.Round(width * scale));
        var previewHeight = Math.Max(1, (int)Math.Round(height * scale));
        var pixels = new byte[previewWidth * previewHeight * 4];
        for (var y = 0; y < previewHeight; y++)
        {
            var sourceY = Math.Min(height - 1, (int)(y / scale));
            for (var x = 0; x < previewWidth; x++)
            {
                var sourceX = Math.Min(width - 1, (int)(x / scale));
                var rgba = raster[sourceY * width + sourceX];
                var offset = (y * previewWidth + x) * 4;
                pixels[offset] = (byte)Tiff.GetB(rgba);
                pixels[offset + 1] = (byte)Tiff.GetG(rgba);
                pixels[offset + 2] = (byte)Tiff.GetR(rgba);
                pixels[offset + 3] = (byte)Tiff.GetA(rgba);
            }
        }

        var bitmap = new WriteableBitmap(
            new PixelSize(previewWidth, previewHeight),
            new Vector(96, 96),
            PixelFormat.Bgra8888,
            AlphaFormat.Unpremul);
        using var framebuffer = bitmap.Lock();
        for (var y = 0; y < previewHeight; y++)
        {
            Marshal.Copy(
                pixels,
                y * previewWidth * 4,
                framebuffer.Address + y * framebuffer.RowBytes,
                previewWidth * 4);
        }
        return bitmap;
    }

    private void PopulateSidebar()
    {
        AddFavorite("Home", Environment.SpecialFolder.UserProfile);
        AddFavorite("Desktop", Environment.SpecialFolder.DesktopDirectory);
        AddFavorite("Documents", Environment.SpecialFolder.MyDocuments);
        var downloads = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        if (Directory.Exists(downloads))
            Favorites.Add(new SidebarEntryViewModel("Downloads", downloads));
        var applications = GetApplicationsPath();
        if (!string.IsNullOrWhiteSpace(applications) && Directory.Exists(applications))
            Favorites.Add(new SidebarEntryViewModel("Applications", applications));

        foreach (var drive in DriveInfo.GetDrives().Where(IsUsefulDrive))
        {
            var name = GetDriveName(drive);
            Drives.Add(new SidebarEntryViewModel(name, drive.RootDirectory.FullName, true));
        }
    }

    private static string GetApplicationsPath()
    {
        if (OperatingSystem.IsMacOS())
            return "/Applications";
        if (OperatingSystem.IsWindows())
            return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        return null;
    }

    private static string GetDriveName(DriveInfo drive)
    {
        if (OperatingSystem.IsMacOS() && drive.Name == "/")
            return "Macintosh HD";
        var root = drive.Name.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.IsNullOrWhiteSpace(root))
            root = drive.Name;
        return string.IsNullOrWhiteSpace(drive.VolumeLabel) || drive.VolumeLabel == drive.Name
            ? root
            : $"{drive.VolumeLabel} ({root})";
    }

    private static bool IsUsefulDrive(DriveInfo drive)
    {
        try
        {
            if (!drive.IsReady)
                return false;
            if (!OperatingSystem.IsMacOS())
                return true;
            return drive.Name == "/" || drive.Name.StartsWith("/Volumes/", StringComparison.Ordinal);
        }
        catch (IOException)
        {
            return false;
        }
    }

    private void AddFavorite(string name, Environment.SpecialFolder specialFolder)
    {
        var path = Environment.GetFolderPath(specialFolder);
        if (!string.IsNullOrWhiteSpace(path) && Directory.Exists(path))
            Favorites.Add(new SidebarEntryViewModel(name, path));
    }

    private void SaveSettingsAndReload()
    {
        m_settingsService.Save(Settings);
        _ = ReloadAllAsync();
    }

    private void Watch(FolderColumnViewModel column)
    {
        try
        {
            var watcher = new FileSystemWatcher(column.Directory.FullName)
            {
                NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                EnableRaisingEvents = true
            };
            FileSystemEventHandler changed = (_, _) => QueueWatcherRefresh(column);
            RenamedEventHandler renamed = (_, _) => QueueWatcherRefresh(column);
            watcher.Changed += changed;
            watcher.Created += changed;
            watcher.Deleted += changed;
            watcher.Renamed += renamed;
            m_watchers[column] = watcher;
        }
        catch (Exception)
        {
            // Some network and protected locations do not support file-system watchers.
        }
    }

    private void QueueWatcherRefresh(FolderColumnViewModel column)
    {
        var generation = m_watcherGenerations.AddOrUpdate(column, 1, (_, current) => current + 1);
        Dispatcher.UIThread.Post(async () =>
        {
            await Task.Delay(160);
            if (!m_watcherGenerations.TryGetValue(column, out var latest) || generation != latest || !Columns.Contains(column))
                return;
            m_directoryService.Invalidate(column.Directory);
            await LoadColumnAsync(column, m_navigationCancellation.Token);
        });
    }

    private void RemoveLastColumn()
    {
        var column = Columns[^1];
        if (m_watchers.Remove(column, out var watcher))
            watcher.Dispose();
        m_watcherGenerations.TryRemove(column, out _);
        Columns.RemoveAt(Columns.Count - 1);
    }

    private void DisposeWatchers()
    {
        foreach (var watcher in m_watchers.Values)
            watcher.Dispose();
        m_watchers.Clear();
        m_watcherGenerations.Clear();
    }

    public void Dispose()
    {
        DisposeWatchers();
        m_navigationCancellation.Cancel();
        m_navigationCancellation.Dispose();
        m_previewCancellation.Cancel();
        m_previewCancellation.Dispose();
        m_previewImage?.Dispose();
    }

    private static string ExpandPath(string path)
    {
        path = Environment.ExpandEnvironmentVariables(path?.Trim() ?? string.Empty);
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;
        if (path == "~")
            return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (path.StartsWith($"~{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[2..]);
        return Path.GetFullPath(path);
    }

    private static string QuoteIfNeeded(string value) => value.Any(char.IsWhiteSpace) ? $"\"{value}\"" : value;
}
