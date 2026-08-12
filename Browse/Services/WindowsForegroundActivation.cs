// Code authored by Dean Edis (DeanTheCoder).
// Anyone is free to copy, modify, use, compile, or distribute this software,
// either in source code form or as a compiled binary, for any purpose.
//
// If you modify the code, please retain this copyright header,
// and consider contributing back to the repository or letting us know
// about your modifications. Your contributions are valued!
//
// THE SOFTWARE IS PROVIDED AS IS, WITHOUT WARRANTY OF ANY KIND.

using System.Runtime.InteropServices;

namespace Browse.Services;

/// <summary>
/// Transfers Windows foreground-activation permission to the running Browse process.
/// </summary>
/// <remarks>
/// Explorer launches a short-lived secondary process, so that process must authorize the existing instance before forwarding the request.
/// </remarks>
internal static class WindowsForegroundActivation
{
    public static bool TryAllowProcess(int processId)
    {
        if (!OperatingSystem.IsWindows() || processId <= 0)
            return false;
        return AllowSetForegroundWindow(processId);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool AllowSetForegroundWindow(int processId);
}
