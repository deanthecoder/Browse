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
using Avalonia.Controls;

namespace Browse.Services;

/// <summary>
/// Owns the Windows message handle used by Browse's global shortcut.
/// </summary>
/// <remarks>
/// RegisterHotKey avoids an invasive low-level keyboard hook and reports conflicts cleanly.
/// </remarks>
public sealed class WindowsGlobalHotKeyHost : Window
{
    private const int HotKeyId = 0xB012;
    private const uint WmHotKey = 0x0312;
    private const uint ModAlt = 0x0001;
    private const uint ModControl = 0x0002;
    private const uint ModNoRepeat = 0x4000;
    private const uint VkB = 0x42;
    private readonly Action m_callback;
    private nint m_handle;
    private Win32Properties.CustomWndProcHookCallback m_hook;

    public WindowsGlobalHotKeyHost(Action callback)
    {
        m_callback = callback;
        Width = 1;
        Height = 1;
        Opacity = 0;
        ShowInTaskbar = false;
        CanResize = false;
        SystemDecorations = SystemDecorations.None;
        Opened += OnOpened;
        Closed += OnClosed;
    }

    public bool IsRegistered { get; private set; }

    private void OnOpened(object sender, EventArgs e)
    {
        m_handle = TryGetPlatformHandle()?.Handle ?? 0;
        if (m_handle == 0)
            return;
        m_hook = WndProc;
        Win32Properties.AddWndProcHookCallback(this, m_hook);
        IsRegistered = RegisterHotKey(m_handle, HotKeyId, ModControl | ModAlt | ModNoRepeat, VkB);
    }

    private nint WndProc(nint hWnd, uint message, nint wParam, nint lParam, ref bool handled)
    {
        if (message == WmHotKey && wParam == HotKeyId)
        {
            handled = true;
            m_callback();
        }
        return 0;
    }

    private void OnClosed(object sender, EventArgs e)
    {
        if (m_handle != 0 && IsRegistered)
            UnregisterHotKey(m_handle, HotKeyId);
        if (m_hook != null)
            Win32Properties.RemoveWndProcHookCallback(this, m_hook);
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool RegisterHotKey(nint windowHandle, int id, uint modifiers, uint virtualKey);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnregisterHotKey(nint windowHandle, int id);
}
