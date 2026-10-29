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
using Avalonia.Headless;
using Avalonia.Media;
using Avalonia.Threading;
using Browse.Models;
using Browse.Services;
using Browse.ViewModels;
using Browse.Views;
using Material.Icons.Avalonia;

namespace Browse.Tests;

[TestFixture]
public sealed class AppearanceTests
{
    [TestCase(false)]
    [TestCase(true)]
    public async Task CheckSavedAppearanceRendersMainWindowAndSettings(bool retro)
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        await session.Dispatch(() =>
        {
            var app = (App)Application.Current;
            var original = new BrowserSettings().UseRetroAppearance;
            using var model = new MainWindowViewModel(new DirectoryContentService(), new PreviewService(),
                new FileOperationService(), new SettingsService());
            MainWindow window = null;
            try
            {
                app.ApplyAppearance(retro);
                window = new MainWindow(model);
                window.Show();
                model.IsSettingsVisible = true;
                Dispatcher.UIThread.RunJobs();
                Assert.That(window.FontFamily.Name, Is.EqualTo(retro ? "PxPlus IBM VGA8" : "Inter"));
                Assert.That(((ISolidColorBrush)window.Background).Color,
                    Is.EqualTo(Color.Parse(retro ? "#0000AA" : "#17191D")));
                Assert.That(window.FindControl<MaterialIcon>("SettingsCog").IsVisible, Is.EqualTo(!retro));
                Assert.That(window.FindControl<TextBlock>("AsciiSettingsLabel").IsVisible, Is.EqualTo(retro));
                var initialAppearance = model.UseRetroAppearance;
                Assert.That(window.FindControl<Border>("AppearanceRestartPrompt").IsVisible, Is.False);
                // Saving a different preference must not replace templates on an open window.
                model.Settings.UseRetroAppearance = !initialAppearance;
                Dispatcher.UIThread.RunJobs();
                Assert.That(window.FontFamily.Name, Is.EqualTo(retro ? "PxPlus IBM VGA8" : "Inter"));
                Assert.That(window.FindControl<Border>("AppearanceRestartPrompt").IsVisible, Is.True);

                model.Settings.UseRetroAppearance = initialAppearance;
                Dispatcher.UIThread.RunJobs();
                Assert.That(window.FindControl<Border>("AppearanceRestartPrompt").IsVisible, Is.False);
            }
            finally
            {
                window?.Close();
                app.ApplyAppearance(original);
            }
            return true;
        }, CancellationToken.None);
    }

    [Test]
    public async Task CheckRetroMenuActionRowKeepsDarkBackgroundWhenHighlighted()
    {
        var session = HeadlessUnitTestSession.GetOrStartForAssembly(Assembly.GetExecutingAssembly());
        await session.Dispatch(() =>
        {
            var app = (App)Application.Current;
            var original = new BrowserSettings().UseRetroAppearance;
            using var model = new MainWindowViewModel(new DirectoryContentService(), new PreviewService(),
                new FileOperationService(), new SettingsService());
            MainWindow window = null;
            ContextMenu menu = null;
            try
            {
                app.ApplyAppearance(true);
                window = new MainWindow(model);
                window.Show();
                menu = (ContextMenu)window.Resources["ItemContextMenu"];
                menu.DataContext = new BrowserItem(new DirectoryInfo(Path.GetTempPath()));
                menu.Open(window);
                Dispatcher.UIThread.RunJobs();
                var row = menu.Items.OfType<MenuItem>().First(item => item.Classes.Contains("fileActions"));
                var root = Avalonia.VisualTree.VisualExtensions.GetVisualDescendants(row).OfType<Border>()
                    .First(border => border.Name == "PART_LayoutRoot");
                foreach (var selected in new[] { false, true })
                foreach (var hovered in new[] { false, true })
                {
                    ((IPseudoClasses)row.Classes).Set(":selected", selected);
                    ((IPseudoClasses)row.Classes).Set(":pointerover", hovered);
                    Dispatcher.UIThread.RunJobs();
                    Assert.That(((ISolidColorBrush)root.Background).Color, Is.EqualTo(Color.Parse("#00008A")));
                }

            }
            finally
            {
                menu?.Close();
                window?.Close();
                app.ApplyAppearance(original);
            }
            return true;
        }, CancellationToken.None);
    }

    [Test]
    public void CheckSharedAppearanceNotifiesAllWindows()
    {
        var service = new SettingsService();
        using var first = new MainWindowViewModel(new DirectoryContentService(), new PreviewService(),
            new FileOperationService(), service);
        using var second = new MainWindowViewModel(new DirectoryContentService(), new PreviewService(),
            new FileOperationService(), service);
        var notified = false;
        second.PropertyChanged += (_, e) => notified |= e.PropertyName == nameof(second.UseRetroAppearance);
        var original = first.UseRetroAppearance;
        // Change the shared setting without writing the test runner's preferences.
        first.Settings.UseRetroAppearance = !original;
        Assert.That(second.UseRetroAppearance, Is.EqualTo(!original));
        Assert.That(notified, Is.True);
        Assert.That(second.IsAppearanceRestartRequired, Is.True);
        using var third = new MainWindowViewModel(new DirectoryContentService(), new PreviewService(),
            new FileOperationService(), service);
        Assert.That(third.IsAppearanceRestartRequired, Is.True);
        first.Settings.UseRetroAppearance = original;
        Assert.That(second.IsAppearanceRestartRequired, Is.False);
        Assert.That(third.IsAppearanceRestartRequired, Is.False);
    }
}
