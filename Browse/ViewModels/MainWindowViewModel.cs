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
using System.Security.Cryptography;
using Avalonia.Media;
using Avalonia.Threading;
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
    private PreviewContent m_preview = new EmptyPreviewContent();
    private string m_previewNamePrefix = "No selection";
    private string m_previewNameSuffix;
    private MaterialIconKind m_previewIcon = MaterialIconKind.FileDocumentOutline;
    private IBrush m_previewIconBrush = Brush.Parse("#AEB7C4");
    private bool m_hasSelection;
    private string m_folderSize;
    private string m_renameText;
    private bool m_isRenameVisible;
    private bool m_isNewFolderVisible;
    private string m_newFolderName;
    private DirectoryInfo m_newFolderDestination;
    private string m_folderSizeExact;

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

    public PreviewContent Preview
    {
        get => m_preview;
        private set
        {
            var previous = m_preview;
            if (!SetField(ref m_preview, value))
                return;
            (PreviewNamePrefix, PreviewNameSuffix) = BrowserItem.SplitName(value.Name);
            OnPropertyChanged(nameof(PreviewSummary));
            OnPropertyChanged(nameof(PreviewContent));
            OnPropertyChanged(nameof(PreviewPath));
            OnPropertyChanged(nameof(PreviewDetails));
            OnPropertyChanged(nameof(PreviewImage));
            OnPropertyChanged(nameof(IsTextPreview));
            OnPropertyChanged(nameof(IsCodePreview));
            OnPropertyChanged(nameof(IsMarkdownPreview));
            OnPropertyChanged(nameof(IsArchivePreview));
            OnPropertyChanged(nameof(IsImagePreview));
            OnPropertyChanged(nameof(IsFolderPreview));
            OnPropertyChanged(nameof(IsNoPreview));
            OnPropertyChanged(nameof(CanExpandPreview));
            previous?.Dispose();
        }
    }

    public string PreviewSummary => Preview.Name;

    public string PreviewNamePrefix
    {
        get => m_previewNamePrefix;
        private set => SetField(ref m_previewNamePrefix, value);
    }

    public string PreviewNameSuffix
    {
        get => m_previewNameSuffix;
        private set => SetField(ref m_previewNameSuffix, value);
    }

    public string PreviewContent => Preview switch
    {
        TextPreviewContent text => text.Text,
        ArchivePreviewContent archive => archive.Text,
        _ => null
    };

    public string PreviewPath => Preview.Path;
    public string PreviewDetails => Preview.Details;

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

    public Avalonia.Media.Imaging.Bitmap PreviewImage => (Preview as ImagePreviewContent)?.Image;
    public bool IsTextPreview => Preview is TextPreviewContent { Mode: TextPreviewMode.Plain };
    public bool IsCodePreview => Preview is TextPreviewContent { Mode: TextPreviewMode.Code };
    public bool IsMarkdownPreview => Preview is TextPreviewContent { Mode: TextPreviewMode.Markdown };
    public bool IsArchivePreview => Preview is ArchivePreviewContent;
    public bool IsImagePreview => Preview is ImagePreviewContent;
    public bool IsFolderPreview => Preview is FolderPreviewContent;
    public bool IsNoPreview => HasSelection && Preview is EmptyPreviewContent or MultiplePreviewContent;
    public bool CanExpandPreview => Preview.CanExpand;

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
    public bool IsWindows => OperatingSystem.IsWindows();
    public string GlobalShortcutText => OperatingSystem.IsWindows() ? "Ctrl+Alt+B" : "Available on Windows";
    public IReadOnlyList<string> WindowsTerminals { get; } = ["Windows Terminal", "PowerShell", "Command Prompt"];

    public string TerminalCommand
    {
        get => string.IsNullOrWhiteSpace(Settings.TerminalCommand) ? "Windows Terminal" : Settings.TerminalCommand;
        set
        {
            if (Settings.TerminalCommand == value)
                return;
            Settings.TerminalCommand = value;
            OnPropertyChanged();
            m_settingsService.Save(Settings);
        }
    }

    public string FolderSizeExact
    {
        get => m_folderSizeExact;
        private set => SetField(ref m_folderSizeExact, value);
    }

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

    public bool IsNewFolderVisible
    {
        get => m_isNewFolderVisible;
        set => SetField(ref m_isNewFolderVisible, value);
    }

    public string NewFolderName
    {
        get => m_newFolderName;
        set => SetField(ref m_newFolderName, value);
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
        ClearPreview();
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
        FolderSizeExact = null;
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

    public void SetContextSelection(FolderColumnViewModel column, IReadOnlyList<BrowserItem> selection)
    {
        column.SetSelection(selection);
        m_selectedItems.Clear();
        m_selectedItems.AddRange(selection);
        FolderSize = null;
        FolderSizeExact = null;
        _ = UpdatePreviewAsync();
        StatusText = $"{selection.Count:N0} selected";
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

    public Task PasteAsync() => PasteAsync(new DirectoryInfo(CurrentPath));

    public async Task PasteAsync(DirectoryInfo destination)
    {
        if (m_clipboardItems.Count == 0 || destination == null)
            return;
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

    public Task ImportClipboardPathsAsync(IEnumerable<string> paths, DirectoryInfo destination = null) =>
        destination == null && string.IsNullOrWhiteSpace(CurrentPath)
            ? Task.CompletedTask
            : ImportDroppedPathsAsync(paths, destination ?? new DirectoryInfo(CurrentPath), false);

    public IReadOnlyList<BrowserItem> SelectedItems => m_selectedItems;

    public static string JoinPaths(IEnumerable<BrowserItem> items, bool namesOnly = false) =>
        string.Join(' ', items.Select(item => QuoteIfNeeded(namesOnly ? item.Name : item.FullPath)));

    public string GetSelectedPaths(bool namesOnly = false) => JoinPaths(m_selectedItems, namesOnly);

    public Task<string> GetMd5TextAsync() => GetHashTextAsync(HashAlgorithmName.MD5, "MD5");
    public Task<string> GetSha256TextAsync() => GetHashTextAsync(HashAlgorithmName.SHA256, "SHA-256");

    public async Task<string> GetBase64TextAsync()
    {
        var files = m_selectedItems.Where(item => !item.IsDirectory).ToArray();
        if (files.Length != 1)
        {
            StatusText = "Select one file to copy as Base64.";
            return null;
        }
        try
        {
            StatusText = $"Encoding {files[0].Name}…";
            var encoded = await m_fileOperationService.EncodeBase64Async((FileInfo)files[0].Info);
            StatusText = "Base64 copied to the clipboard.";
            return encoded;
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            return null;
        }
    }

    public void OpenSelected()
    {
        if (m_selectedItems.Count != 1)
            return;
        if (m_selectedItems[0].IsDirectory)
            _ = NavigateToAsync(m_selectedItems[0].FullPath);
        else
        {
            try
            {
                m_fileOperationService.Open(m_selectedItems[0]);
            }
            catch (Exception ex)
            {
                StatusText = ex is System.ComponentModel.Win32Exception { NativeErrorCode: 1223 }
                    ? "Open canceled."
                    : $"Could not open {m_selectedItems[0].Name}: {ex.Message}";
            }
        }
    }

    private async Task<string> GetHashTextAsync(HashAlgorithmName algorithm, string displayName)
    {
        var files = m_selectedItems.Where(item => !item.IsDirectory).ToArray();
        if (files.Length == 0)
            return null;
        try
        {
            StatusText = $"Calculating {displayName}…";
            var lines = new List<string>(files.Length);
            foreach (var item in files)
            {
                var hash = await m_fileOperationService.CalculateHashAsync((FileInfo)item.Info, algorithm);
                lines.Add(files.Length == 1 ? hash : $"{hash}  {item.Name}");
            }
            StatusText = $"{displayName} copied to the clipboard.";
            return string.Join(Environment.NewLine, lines);
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
            return null;
        }
    }

    public void OpenTerminal()
    {
        var path = m_selectedItems.Count == 1
            ? m_selectedItems[0].IsDirectory ? m_selectedItems[0].FullPath : Path.GetDirectoryName(m_selectedItems[0].FullPath)
            : CurrentPath;
        if (!string.IsNullOrWhiteSpace(path))
            OpenTerminal(new DirectoryInfo(path));
    }

    public void OpenTerminal(DirectoryInfo directory) =>
        m_fileOperationService.OpenTerminal(directory, Settings.TerminalCommand);

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
            FolderSize = bytes.ToSize();
            FolderSizeExact = $"{bytes:N0} bytes";
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

    public void BeginCreateFolder(DirectoryInfo destination)
    {
        m_newFolderDestination = destination ?? new DirectoryInfo(CurrentPath);
        NewFolderName = "New folder";
        IsNewFolderVisible = true;
    }

    public async Task CommitCreateFolderAsync()
    {
        IsNewFolderVisible = false;
        if (m_newFolderDestination == null || string.IsNullOrWhiteSpace(NewFolderName))
            return;
        try
        {
            var requestedPath = Path.Combine(m_newFolderDestination.FullName, NewFolderName.Trim());
            var path = FileOperationService.GetAvailablePath(requestedPath);
            await Task.Run(() => Directory.CreateDirectory(path));
            m_directoryService.Invalidate(m_newFolderDestination);
            await ReloadCurrentAsync();
            StatusText = $"Created {Path.GetFileName(path)}.";
        }
        catch (Exception ex)
        {
            StatusText = ex.Message;
        }
    }

    public void AddFavorite(string path)
    {
        if (!Directory.Exists(path) || Favorites.Any(entry => string.Equals(entry.Path, path, StringComparison.OrdinalIgnoreCase)))
            return;
        Favorites.Add(CreateFavorite(path));
        SaveFavoritePaths();
    }

    public void RemoveFavorite(SidebarEntryViewModel entry)
    {
        if (entry == null || !Favorites.Remove(entry))
            return;
        SaveFavoritePaths();
    }

    public void ResetFavorites()
    {
        Settings.FavoritePaths = GetDefaultFavoritePaths();
        PopulateFavorites();
        m_settingsService.Save(Settings);
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
        column.IsRefreshing = true;
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
            column.IsRefreshing = false;
        }
        StatusText = $"{column.Items.Count:N0} items";
    }

    private async Task UpdatePreviewAsync()
    {
        m_previewCancellation.Cancel();
        m_previewCancellation.Dispose();
        m_previewCancellation = new CancellationTokenSource();
        var cancellation = m_previewCancellation;
        var cancellationToken = cancellation.Token;
        try
        {
            var preview = await m_previewService.CreateAsync(m_selectedItems, cancellationToken);
            if (cancellationToken.IsCancellationRequested)
            {
                preview.Dispose();
                cancellationToken.ThrowIfCancellationRequested();
            }
            Preview = preview;
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
            Preview = new EmptyPreviewContent(
                m_selectedItems.Count == 1 ? m_selectedItems[0].Name : "Preview unavailable",
                details: $"Preview unavailable · {ex.Message}");
            HasSelection = m_selectedItems.Count > 0;
        }
    }

    private void PopulateSidebar()
    {
        PopulateFavorites();

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

    private void PopulateFavorites()
    {
        Favorites.Clear();
        var paths = Settings.FavoritePaths ?? GetDefaultFavoritePaths();
        foreach (var path in paths.Where(Directory.Exists).Distinct(StringComparer.OrdinalIgnoreCase))
            Favorites.Add(CreateFavorite(path));
    }

    private static string[] GetDefaultFavoritePaths()
    {
        var paths = new[]
        {
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
            GetApplicationsPath()
        };
        return paths.Where(path => !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)).ToArray();
    }

    private static SidebarEntryViewModel CreateFavorite(string path)
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var name = string.Equals(path, home, StringComparison.OrdinalIgnoreCase)
            ? "Home"
            : string.Equals(path, Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), StringComparison.OrdinalIgnoreCase)
                ? "Desktop"
                : string.Equals(path, Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), StringComparison.OrdinalIgnoreCase)
                    ? "Documents"
                    : string.Equals(path, GetApplicationsPath(), StringComparison.OrdinalIgnoreCase)
                        ? "Applications"
                        : new DirectoryInfo(path).Name;
        return new SidebarEntryViewModel(name, path);
    }

    private void SaveFavoritePaths()
    {
        Settings.FavoritePaths = Favorites.Select(entry => entry.Path).ToArray();
        m_settingsService.Save(Settings);
    }

    private void ClearPreview()
    {
        Preview = new EmptyPreviewContent();
        HasSelection = false;
        FolderSize = null;
        FolderSizeExact = null;
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
        m_preview.Dispose();
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
