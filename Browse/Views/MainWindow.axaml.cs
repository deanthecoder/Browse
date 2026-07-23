// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia.Controls;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Browse.Models;
using Browse.Services;
using Browse.ViewModels;

namespace Browse.Views;

/// <summary>
/// Hosts Browse's column-oriented desktop interface.
/// </summary>
/// <remarks>
/// Code-behind is limited to view concerns such as focus, selection, and keyboard routing.
/// </remarks>
public partial class MainWindow : Window
{
    private readonly string m_requestedPath;
    private Point? m_dragStart;
    private ListBox m_dragSource;
    private ListBox m_focusedColumn;
    private ContextMenu m_pendingContextMenu;
    private ContextMenu m_openContextMenu;
    private string[] m_externalClipboardPaths = [];
    private DirectoryInfo m_contextDestination;
    private bool m_suppressSelectionChanged;
    private string m_typeSearch = string.Empty;
    private DateTime m_lastTypeSearch;
    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    public MainWindow() : this(new MainWindowViewModel(
        new DirectoryContentService(),
        new PreviewService(),
        new FileOperationService(),
        new SettingsService()), null)
    {
    }

    public MainWindow(MainWindowViewModel viewModel, string requestedPath = null)
    {
        m_requestedPath = requestedPath;
        DataContext = viewModel;
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnColumnPointerPressed, RoutingStrategies.Tunnel, true);
        AddHandler(PointerMovedEvent, OnColumnPointerMoved, RoutingStrategies.Tunnel, true);
        AddHandler(PointerReleasedEvent, OnColumnPointerReleased, RoutingStrategies.Tunnel, true);
        AddHandler(KeyDownEvent, OnWindowKeyDown, RoutingStrategies.Tunnel, true);
        Deactivated += (_, _) => ClearPendingDrag();
        Opened += OnOpened;
    }

    private async void OnOpened(object sender, EventArgs e)
    {
        await ViewModel.InitializeAsync(m_requestedPath);
    }

    private async void OnSidebarClicked(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: SidebarEntryViewModel entry })
            await ViewModel.NavigateToAsync(entry.Path);
    }

    private async void OnColumnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (sender is not ListBox { DataContext: FolderColumnViewModel column } listBox)
            return;
        if (m_suppressSelectionChanged)
            return;
        if (column.IsRefreshing)
        {
            Dispatcher.UIThread.Post(() => RestoreColumnSelection(listBox, column), DispatcherPriority.Background);
            return;
        }
        var previousColumnCount = ViewModel.Columns.Count;
        var selection = listBox.SelectedItems.Cast<BrowserItem>().ToArray();
        await ViewModel.SelectAsync(column, selection);
        if (ViewModel.Columns.Count > previousColumnCount)
            Dispatcher.UIThread.Post(BringLastColumnIntoViewIfNeeded, DispatcherPriority.Background);
    }

    private void OnColumnDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File)
            ? e.KeyModifiers.HasFlag(KeyModifiers.Control) ? DragDropEffects.Copy : DragDropEffects.Move
            : DragDropEffects.None;
    }

    private void OnItemDragOver(object sender, DragEventArgs e)
    {
        if (sender is not Grid { Tag: BrowserItem { IsDirectory: true } } target ||
            !e.DataTransfer.Contains(DataFormat.File))
        {
            e.DragEffects = DragDropEffects.None;
            return;
        }
        if (!target.Classes.Contains("folderDropTarget"))
            target.Classes.Add("folderDropTarget");
        e.DragEffects = e.KeyModifiers.HasFlag(KeyModifiers.Control) ? DragDropEffects.Copy : DragDropEffects.Move;
        e.Handled = true;
    }

    private static void OnItemDragLeave(object sender, RoutedEventArgs e)
    {
        if (sender is Grid target)
            target.Classes.Remove("folderDropTarget");
    }

    private async void OnItemDrop(object sender, DragEventArgs e)
    {
        if (sender is not Grid { Tag: BrowserItem { IsDirectory: true } folder } target)
            return;
        target.Classes.Remove("folderDropTarget");
        var paths = e.DataTransfer.TryGetFiles()?
            .Select(file => file.TryGetLocalPath())
            .Where(path => path != null)
            .ToArray();
        if (paths is not { Length: > 0 })
            return;
        e.Handled = true;
        await ViewModel.ImportDroppedPathsAsync(
            paths,
            new DirectoryInfo(folder.FullPath),
            !e.KeyModifiers.HasFlag(KeyModifiers.Control));
    }

    private void OnColumnPointerPressed(object sender, PointerPressedEventArgs e)
    {
        if (e.Source is not Visual source)
            return;
        var listBox = source as ListBox ?? source.FindAncestorOfType<ListBox>();
        var itemContainer = source as ListBoxItem ?? source.FindAncestorOfType<ListBoxItem>();
        if (listBox == null)
            return;
        var updateKind = e.GetCurrentPoint(this).Properties.PointerUpdateKind;
        if (updateKind == PointerUpdateKind.RightButtonPressed)
        {
            var itemGrid = source.GetSelfAndVisualAncestors()
                .OfType<Grid>()
                .FirstOrDefault(grid => grid.Tag is BrowserItem);
            if (itemGrid?.Tag is BrowserItem contextItem)
            {
                if (!listBox.SelectedItems.Contains(contextItem))
                {
                    m_suppressSelectionChanged = true;
                    listBox.SelectedItem = contextItem;
                    m_suppressSelectionChanged = false;
                    ViewModel.SetContextSelection((FolderColumnViewModel)listBox.DataContext, [contextItem]);
                }
                SetFocusedColumn(listBox);
                m_contextDestination = contextItem.IsDirectory
                    ? new DirectoryInfo(contextItem.FullPath)
                    : ((FolderColumnViewModel)listBox.DataContext).Directory;
                m_pendingContextMenu = (ContextMenu)Resources["ItemContextMenu"];
                m_pendingContextMenu.DataContext = contextItem;
            }
            else
            {
                m_contextDestination = ((FolderColumnViewModel)listBox.DataContext).Directory;
                m_pendingContextMenu = (ContextMenu)Resources["ColumnContextMenu"];
            }
            e.Handled = true;
            return;
        }
        if (updateKind != PointerUpdateKind.LeftButtonPressed || itemContainer == null)
            return;
        var extendsSelection = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                               e.KeyModifiers.HasFlag(KeyModifiers.Meta) ||
                               e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (!extendsSelection && itemContainer.DataContext is BrowserItem item)
            listBox.SelectedItem = item;
        SetFocusedColumn(listBox);
        m_dragSource = listBox;
        m_dragStart = e.GetPosition(this);
    }

    private async void OnColumnPointerReleased(object sender, PointerReleasedEventArgs e)
    {
        ClearPendingDrag();
        if (e.GetCurrentPoint(this).Properties.PointerUpdateKind == PointerUpdateKind.RightButtonReleased &&
            m_pendingContextMenu is { } contextMenu)
        {
            m_pendingContextMenu = null;
            await UpdatePasteAvailabilityAsync(contextMenu);
            m_openContextMenu = contextMenu;
            contextMenu.Open(this);
            e.Handled = true;
        }
    }

    private void OnColumnGotFocus(object sender, GotFocusEventArgs e)
    {
        if (sender is ListBox listBox)
            SetFocusedColumn(listBox);
    }

    private async void OnColumnDrop(object sender, DragEventArgs e)
    {
        if (sender is not ListBox { DataContext: FolderColumnViewModel column })
            return;
        var files = e.DataTransfer.TryGetFiles();
        if (files == null)
            return;
        var paths = files.Select(file => file.TryGetLocalPath()).Where(path => path != null);
        var move = !e.KeyModifiers.HasFlag(KeyModifiers.Control);
        await ViewModel.ImportDroppedPathsAsync(paths, column.Directory, move);
    }

    private async void OnColumnPointerMoved(object sender, PointerEventArgs e)
    {
        var listBox = e.Source as ListBox ?? (e.Source as Visual)?.FindAncestorOfType<ListBox>();
        if (listBox == null || listBox != m_dragSource || m_dragStart == null)
            return;
        var position = e.GetPosition(this);
        if (Math.Abs(position.X - m_dragStart.Value.X) < 14 && Math.Abs(position.Y - m_dragStart.Value.Y) < 14)
            return;
        var draggedItems = listBox.SelectedItems.Cast<BrowserItem>().ToArray();
        ClearPendingDrag();
        if (draggedItems.Length == 0)
            return;
        var transfer = new DataTransfer();
        foreach (var item in draggedItems)
        {
            IStorageItem storageItem = item.IsDirectory
                ? await StorageProvider.TryGetFolderFromPathAsync(item.FullPath)
                : await StorageProvider.TryGetFileFromPathAsync(item.FullPath);
            if (storageItem != null)
                transfer.Add(DataTransferItem.CreateFile(storageItem));
        }
        if (transfer.Items.Count > 0)
        {
            await DragDrop.DoDragDropAsync(e, transfer, DragDropEffects.Copy | DragDropEffects.Move);
        }
    }

    private void ClearPendingDrag()
    {
        m_dragStart = null;
        m_dragSource = null;
    }

    private void OnItemDoubleTapped(object sender, TappedEventArgs e) => ViewModel.OpenSelected();

    private void OnSettingsClicked(object sender, RoutedEventArgs e) => ViewModel.IsSettingsVisible = true;
    private void OnSettingsCloseClicked(object sender, RoutedEventArgs e) => ViewModel.IsSettingsVisible = false;
    private void OnResetFavoritesClicked(object sender, RoutedEventArgs e) => ViewModel.ResetFavorites();
    private void OnTerminalClicked(object sender, RoutedEventArgs e) => ViewModel.OpenTerminal();
    private void OnColumnTerminalClicked(object sender, RoutedEventArgs e)
    {
        if (m_contextDestination != null)
            ViewModel.OpenTerminal(m_contextDestination);
    }
    private void OnOpenClicked(object sender, RoutedEventArgs e) => ViewModel.OpenSelected();
    private void OnExpandPreviewClicked(object sender, RoutedEventArgs e) => Dispatcher.UIThread.Post(() => new PreviewWindow(ViewModel).Show(this));
    private void OnOpenInNewWindowClicked(object sender, RoutedEventArgs e)
    {
        var selected = ViewModel.SelectedItems.Count == 1 ? ViewModel.SelectedItems[0] : null;
        var path = selected?.IsDirectory == true
            ? selected.FullPath
            : selected == null ? ViewModel.CurrentPath : Path.GetDirectoryName(selected.FullPath);
        (Application.Current as App)?.OpenWindow(path);
    }
    private async void OnCutClicked(object sender, RoutedEventArgs e)
    {
        await CopySelectionAsync(true);
        CloseContextMenu();
    }
    private async void OnCopyClicked(object sender, RoutedEventArgs e)
    {
        await CopySelectionAsync(false);
        CloseContextMenu();
    }
    private async void OnPasteClicked(object sender, RoutedEventArgs e)
    {
        await PasteSelectionAsync(m_contextDestination);
        CloseContextMenu();
    }
    private void OnNewFolderClicked(object sender, RoutedEventArgs e)
    {
        ViewModel.BeginCreateFolder(m_contextDestination);
        Dispatcher.UIThread.Post(() =>
        {
            NewFolderTextBox.Focus();
            NewFolderTextBox.SelectAll();
        });
    }
    private void OnRenameClicked(object sender, RoutedEventArgs e) => BeginRename();
    private async void OnCalculateSizeClicked(object sender, RoutedEventArgs e) => await ViewModel.CalculateFolderSizeAsync();
    private async void OnCreateZipClicked(object sender, RoutedEventArgs e) => await ViewModel.CreateZipAsync();
    private async void OnDeleteClicked(object sender, RoutedEventArgs e) => await ViewModel.DeleteSelectionAsync();
    private async void OnCopyPathsClicked(object sender, RoutedEventArgs e) => await CopyTextAsync(ViewModel.GetSelectedPaths());
    private async void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        var hasModalOverlay = ViewModel.IsGoToVisible || ViewModel.IsRenameVisible ||
                              ViewModel.IsSettingsVisible || ViewModel.IsNewFolderVisible;
        if (!hasModalOverlay && e.Key is Key.Left or Key.Right)
        {
            MoveColumnFocus(e.Key == Key.Left ? -1 : 1);
            e.Handled = true;
            return;
        }
        var primaryModifier = e.KeyModifiers.HasFlag(KeyModifiers.Control) ||
                              e.KeyModifiers.HasFlag(KeyModifiers.Meta);
        if (!hasModalOverlay && primaryModifier && e.Key == Key.G)
        {
            ViewModel.ShowGoTo();
            Dispatcher.UIThread.Post(() => GoToTextBox.Focus());
            e.Handled = true;
        }
        else if (!hasModalOverlay && primaryModifier && e.Key == Key.C)
        {
            await CopySelectionAsync(false);
            e.Handled = true;
        }
        else if (!hasModalOverlay && primaryModifier && e.Key == Key.X)
        {
            await CopySelectionAsync(true);
            e.Handled = true;
        }
        else if (!hasModalOverlay && primaryModifier && e.Key == Key.V)
        {
            await PasteSelectionAsync();
            e.Handled = true;
        }
        else if (!hasModalOverlay && e.Key == Key.F2)
        {
            BeginRename();
            e.Handled = true;
        }
        else if (!hasModalOverlay && e.Key == Key.Delete)
        {
            await ViewModel.DeleteSelectionAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.IsGoToVisible = false;
            ViewModel.IsRenameVisible = false;
            ViewModel.IsSettingsVisible = false;
            ViewModel.IsNewFolderVisible = false;
        }
        else if (!hasModalOverlay && e.KeyModifiers == KeyModifiers.None && e.Key is Key.Up or Key.Down)
        {
            MoveColumnSelection(e.Key == Key.Up ? -1 : 1);
            e.Handled = true;
        }
        else if (!hasModalOverlay && e.KeyModifiers == KeyModifiers.None && e.KeySymbol is { Length: 1 } symbol && !char.IsControl(symbol[0]))
        {
            SelectByTypedPrefix(symbol);
            e.Handled = true;
        }
    }

    private void MoveColumnFocus(int direction)
    {
        if (m_focusedColumn?.DataContext is not FolderColumnViewModel currentColumn)
            return;
        var targetIndex = ViewModel.Columns.IndexOf(currentColumn) + direction;
        if (targetIndex < 0 || targetIndex >= ViewModel.Columns.Count)
            return;
        var targetColumn = ViewModel.Columns[targetIndex];
        var targetList = this.GetVisualDescendants()
            .OfType<ListBox>()
            .FirstOrDefault(listBox => ReferenceEquals(listBox.DataContext, targetColumn));
        if (targetList == null)
            return;
        SetFocusedColumn(targetList);
        targetList.Focus();
        if (direction > 0 && targetList.SelectedIndex < 0 && targetList.ItemsView.Count > 0)
            targetList.SelectedIndex = 0;
        if (targetList.SelectedItem != null)
            targetList.ScrollIntoView(targetList.SelectedItem);
    }

    private void BringLastColumnIntoViewIfNeeded()
    {
        var viewportRight = ColumnScrollViewer.Offset.X + ColumnScrollViewer.Viewport.Width;
        if (ColumnScrollViewer.Extent.Width <= viewportRight + 0.5)
            return;
        ColumnScrollViewer.Offset = new Vector(
            ColumnScrollViewer.Extent.Width - ColumnScrollViewer.Viewport.Width,
            ColumnScrollViewer.Offset.Y);
    }

    private void MoveColumnSelection(int direction)
    {
        if (m_focusedColumn == null || m_focusedColumn.ItemsView.Count == 0)
            return;
        var currentIndex = m_focusedColumn.SelectedIndex;
        var targetIndex = Math.Clamp(currentIndex < 0 ? 0 : currentIndex + direction, 0, m_focusedColumn.ItemsView.Count - 1);
        m_focusedColumn.SelectedIndex = targetIndex;
        if (m_focusedColumn.SelectedItem != null)
            m_focusedColumn.ScrollIntoView(m_focusedColumn.SelectedItem);
    }

    private void SetFocusedColumn(ListBox listBox)
    {
        if (ReferenceEquals(m_focusedColumn, listBox))
            return;
        m_focusedColumn?.Classes.Remove("activeColumn");
        m_focusedColumn = listBox;
        m_focusedColumn?.Classes.Add("activeColumn");
    }

    private void SelectByTypedPrefix(string character)
    {
        if (m_focusedColumn == null)
            return;
        var now = DateTime.UtcNow;
        m_typeSearch = now - m_lastTypeSearch > TimeSpan.FromMilliseconds(700)
            ? character
            : m_typeSearch + character;
        m_lastTypeSearch = now;
        var item = m_focusedColumn.ItemsView
            .Cast<BrowserItem>()
            .FirstOrDefault(candidate => candidate.Name.StartsWith(m_typeSearch, StringComparison.CurrentCultureIgnoreCase));
        if (item == null)
            return;
        m_focusedColumn.SelectedItem = item;
        m_focusedColumn.ScrollIntoView(item);
    }

    private void RestoreColumnSelection(ListBox listBox, FolderColumnViewModel column)
    {
        m_suppressSelectionChanged = true;
        try
        {
            listBox.SelectedItems.Clear();
            foreach (var item in column.Items.Where(item => column.IsSelectedPath(item.FullPath)))
                listBox.SelectedItems.Add(item);
        }
        finally
        {
            m_suppressSelectionChanged = false;
        }
    }

    private async void OnGoToKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.SubmitGoToAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.IsGoToVisible = false;
            e.Handled = true;
        }
    }

    private async void OnRenameKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.CommitRenameAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.IsRenameVisible = false;
            e.Handled = true;
        }
    }

    private async void OnNewFolderKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            await ViewModel.CommitCreateFolderAsync();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            ViewModel.IsNewFolderVisible = false;
            e.Handled = true;
        }
    }

    private void BeginRename()
    {
        ViewModel.BeginRename();
        Dispatcher.UIThread.Post(() =>
        {
            RenameTextBox.Focus();
            var dotIndex = RenameTextBox.Text?.LastIndexOf('.') ?? -1;
            RenameTextBox.SelectionStart = 0;
            RenameTextBox.SelectionEnd = dotIndex > 0 ? dotIndex : RenameTextBox.Text?.Length ?? 0;
        });
    }

    private async Task CopyTextAsync(string text)
    {
        var clipboard = Clipboard;
        if (clipboard != null)
            await clipboard.SetTextAsync(text);
    }

    private async Task CopySelectionAsync(bool cut)
    {
        ViewModel.CopySelection(cut);
        if (Clipboard == null)
            return;
        var storageItems = new List<IStorageItem>();
        foreach (var item in ViewModel.SelectedItems)
        {
            IStorageItem storageItem = item.IsDirectory
                ? await StorageProvider.TryGetFolderFromPathAsync(item.FullPath)
                : await StorageProvider.TryGetFileFromPathAsync(item.FullPath);
            if (storageItem != null)
                storageItems.Add(storageItem);
        }
        if (storageItems.Count > 0)
        {
            await Clipboard.SetFilesAsync(storageItems);
            m_externalClipboardPaths = storageItems
                .Select(item => item.TryGetLocalPath())
                .Where(path => path != null)
                .ToArray();
        }
    }

    private async Task UpdatePasteAvailabilityAsync(ContextMenu contextMenu)
    {
        await RefreshClipboardPathsAsync();
        var pasteItem = contextMenu.Items
            .OfType<MenuItem>()
            .FirstOrDefault(item => string.Equals(item.Header?.ToString(), "Paste", StringComparison.Ordinal));
        if (pasteItem != null)
            pasteItem.IsEnabled = m_externalClipboardPaths.Length > 0;
        var pasteButton = contextMenu.Items
            .OfType<MenuItem>()
            .Select(item => item.Header)
            .OfType<Panel>()
            .SelectMany(panel => panel.Children)
            .OfType<Button>()
            .FirstOrDefault(button => string.Equals(button.Tag?.ToString(), "PasteAction", StringComparison.Ordinal));
        if (pasteButton != null)
            pasteButton.IsEnabled = m_externalClipboardPaths.Length > 0;
    }

    private async Task PasteSelectionAsync(DirectoryInfo destination = null)
    {
        await RefreshClipboardPathsAsync();
        if (ViewModel.CanPaste && ViewModel.ClipboardMatches(m_externalClipboardPaths))
            await (destination == null ? ViewModel.PasteAsync() : ViewModel.PasteAsync(destination));
        else
            await ViewModel.ImportClipboardPathsAsync(m_externalClipboardPaths, destination);
    }

    private void OnFavoritesDragOver(object sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.Contains(DataFormat.File) ? DragDropEffects.Link : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnFavoritesDrop(object sender, DragEventArgs e)
    {
        var paths = e.DataTransfer.TryGetFiles()?
            .Select(item => item.TryGetLocalPath())
            .Where(Directory.Exists)
            .ToArray() ?? [];
        foreach (var path in paths)
            ViewModel.AddFavorite(path);
        e.Handled = true;
    }

    private void OnRemoveFavoriteClicked(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem menuItem)
            return;
        var entry = menuItem.DataContext as SidebarEntryViewModel;
        if (entry == null && menuItem.Parent is ContextMenu { PlacementTarget: Button { Tag: SidebarEntryViewModel targetEntry } })
            entry = targetEntry;
        if (entry != null)
            ViewModel.RemoveFavorite(entry);
    }

    private async Task RefreshClipboardPathsAsync()
    {
        try
        {
            m_externalClipboardPaths = Clipboard == null
                ? []
                : (await Clipboard.TryGetFilesAsync())?
                    .Select(item => item.TryGetLocalPath())
                    .Where(path => path != null)
                    .ToArray() ?? [];
        }
        catch (Exception)
        {
            m_externalClipboardPaths = [];
        }
        if (ViewModel.CanPaste && !ViewModel.ClipboardMatches(m_externalClipboardPaths))
            ViewModel.ClearClipboard();
    }

    private void CloseContextMenu()
    {
        m_openContextMenu?.Close();
        m_openContextMenu = null;
    }
}
