// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Browse.Services;
using Browse.ViewModels;
using Browse.Views;
using DTC.Core.Commands;
using DTC.Core.UI;

namespace Browse;

public class App : Application
{
    private readonly DirectoryContentService m_directoryService = new();
    private readonly PreviewService m_previewService = new();
    private readonly FileOperationService m_fileOperationService = new();
    private readonly SettingsService m_settingsService = new();
    private TrayIcon m_trayIcon;
    private WindowsGlobalHotKeyHost m_hotKeyHost;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public void OpenWindow(string path = null) => CreateWindow(path);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.ShutdownMode = ShutdownMode.OnExplicitShutdown;
            var backgroundOnly = desktop.Args?.Any(argument => argument.Equals("--background", StringComparison.OrdinalIgnoreCase)) == true;
            var requestedPath = desktop.Args?.FirstOrDefault(argument => !argument.StartsWith("--", StringComparison.Ordinal));
            if (!backgroundOnly)
            {
                desktop.MainWindow = CreateWindow(requestedPath, false);
                desktop.MainWindow.Show();
            }
            m_trayIcon = CreateTrayIcon(desktop);
            TrayIcon.SetIcons(this, [m_trayIcon]);
            if (OperatingSystem.IsWindows() && m_settingsService.Load().EnableGlobalShortcut)
            {
                m_hotKeyHost = new WindowsGlobalHotKeyHost(() => CreateWindow());
                m_hotKeyHost.Show();
            }
            desktop.Exit += (_, _) =>
            {
                m_hotKeyHost?.Close();
                TrayIcon.SetIcons(this, null);
                m_trayIcon?.Dispose();
            };
        }
        base.OnFrameworkInitializationCompleted();
    }

    private MainWindow CreateWindow(string requestedPath = null, bool show = true)
    {
        var viewModel = new MainWindowViewModel(
            m_directoryService,
            m_previewService,
            m_fileOperationService,
            m_settingsService);
        var window = new MainWindow(viewModel, requestedPath)
        {
            Icon = IconLoader.LoadWindowIcon()
        };
        window.Closed += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(viewModel.CurrentPath))
            {
                viewModel.Settings.DefaultPath = viewModel.CurrentPath;
                m_settingsService.Save(viewModel.Settings);
            }
            viewModel.Dispose();
        };
        if (show)
            window.Show();
        return window;
    }

    private TrayIcon CreateTrayIcon(IClassicDesktopStyleApplicationLifetime desktop)
    {
        var menu = new NativeMenu
        {
            new NativeMenuItem("New Browse window...")
            {
                Command = new RelayCommand(_ => CreateWindow())
            },
            new NativeMenuItem("About Browse")
            {
                Command = new RelayCommand(_ => ShowAbout())
            },
            new NativeMenuItemSeparator(),
            new NativeMenuItem("Exit")
            {
                Command = new RelayCommand(_ => desktop.Shutdown())
            }
        };
        return new TrayIcon
        {
            Icon = IconLoader.LoadWindowIcon(),
            IsVisible = true,
            Menu = menu,
            ToolTipText = "Browse"
        };
    }

    private static void ShowAbout()
    {
        var assembly = Assembly.GetEntryAssembly();
        var dialog = new AboutDialog(new AboutInfo
        {
            Title = "Browse",
            Version = assembly?.GetName().Version?.ToString(3) ?? "0.1.0",
            Copyright = "Copyright (c) 2026 Dean Edis (DeanTheCoder)",
            WebsiteUrl = "https://github.com/deanthecoder/Browse",
            Icon = IconLoader.LoadBitmap()
        })
        {
            Icon = IconLoader.LoadWindowIcon(),
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.CenterScreen
        };
        dialog.Show();
    }
}
