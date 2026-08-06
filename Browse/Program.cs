// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using Avalonia;
using Browse.Services;
using Browse.Services.Previews;
using DTC.Core;

namespace Browse;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        if (PdfPreviewWorker.TryRun(args, out var exitCode))
        {
            Environment.ExitCode = exitCode;
            return;
        }

        using var singleInstance = SingleInstanceGuard.TryAcquire("Browse");
        if (singleInstance == null)
        {
            SingleInstanceLaunchServer.TrySend(args);
            return;
        }
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
