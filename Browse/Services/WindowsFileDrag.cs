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
using System.Runtime.InteropServices.ComTypes;
using System.Runtime.Versioning;
using Avalonia.Input;
using IDataObject = System.Runtime.InteropServices.ComTypes.IDataObject;

namespace Browse.Services;

/// <summary>
/// Uses the Windows shell's file data object so WPF can negotiate OLE storage media.
/// Avalonia 11 rejects WPF's combined TYMED mask (AvaloniaUI/Avalonia#22250).
/// </summary>
[SupportedOSPlatform("windows")]
public static class WindowsFileDrag
{
    internal static DragDropEffects Run(PointerEventArgs trigger, string[] paths, bool copyOnly,
        DataFormat<string> archiveFormat)
    {
        var data = CreateDataObject(paths);
        try
        {
            if (copyOnly)
                AddArchiveMarker(data, archiveFormat);
            trigger.Pointer.Capture(null);
            var allowed = copyOnly ? 1u : 1u | 2u | 4u;
            var result = DoDragDrop(data, new DropSource(), allowed, out var effect);
            Marshal.ThrowExceptionForHR(result);
            return result == 0x40100 ? (DragDropEffects)effect : DragDropEffects.None;
        }
        finally
        {
            Marshal.ReleaseComObject(data);
        }
    }

    internal static IDataObject CreateDataObject(string[] paths)
    {
        var iid = typeof(IDataObject).GUID;
        Marshal.ThrowExceptionForHR(SHCreateDataObject(IntPtr.Zero, 0, [],
            IntPtr.Zero, ref iid, out var data));
        try
        {
            // DROPFILES header followed by a double-null-terminated UTF-16 path list.
            var names = System.Text.Encoding.Unicode.GetBytes(string.Join('\0', paths) + "\0\0");
            var payload = new byte[20 + names.Length];
            BitConverter.GetBytes(20).CopyTo(payload, 0);
            BitConverter.GetBytes(1).CopyTo(payload, 16); // fWide
            names.CopyTo(payload, 20);
            SetData(data, 15, payload); // CF_HDROP
            return data;
        }
        catch
        {
            Marshal.ReleaseComObject(data);
            throw;
        }
    }

    internal static void AddArchiveMarker(IDataObject data, DataFormat<string> archiveFormat)
    {
        var id = RegisterClipboardFormat(archiveFormat.ToSystemName("avn-app-fmt:"));
        if (id == 0)
            throw new System.ComponentModel.Win32Exception();
        SetData(data, unchecked((short)id), new byte[2]); // Only marker presence matters.
    }

    private static void SetData(IDataObject data, short id, byte[] payload)
    {
        var handle = GlobalAlloc(0x42, (nuint)payload.Length);
        if (handle == IntPtr.Zero)
            throw new OutOfMemoryException();
        var memory = GlobalLock(handle);
        if (memory == IntPtr.Zero)
        {
            GlobalFree(handle);
            throw new System.ComponentModel.Win32Exception();
        }
        try { Marshal.Copy(payload, 0, memory, payload.Length); }
        finally { GlobalUnlock(handle); }
        var format = new FORMATETC
        {
            cfFormat = unchecked((short)id), dwAspect = DVASPECT.DVASPECT_CONTENT,
            lindex = -1, tymed = TYMED.TYMED_HGLOBAL
        };
        var medium = new STGMEDIUM { tymed = TYMED.TYMED_HGLOBAL, unionmember = handle };
        try
        {
            data.SetData(ref format, ref medium, true);
        }
        catch
        {
            ReleaseStgMedium(ref medium);
            throw;
        }
    }

    [ComVisible(true), Guid("00000121-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDropSource
    {
        [PreserveSig] int QueryContinueDrag([MarshalAs(UnmanagedType.Bool)] bool escapePressed, uint keyState);
        [PreserveSig] int GiveFeedback(uint effect);
    }

    [ComVisible(true), ClassInterface(ClassInterfaceType.None)]
    public sealed class DropSource : IDropSource
    {
        public int QueryContinueDrag(bool escapePressed, uint keyState)
        {
            if (escapePressed || (keyState & (2u | 16u)) != 0)
                return 0x40101; // Cancel on Escape or another mouse button.
            return (keyState & 1) == 0 ? 0x40100 : 0; // Drop when the left button is released.
        }

        public int GiveFeedback(uint effect) => 0x40102; // Let Windows choose the cursor.
    }

    [DllImport("shell32.dll")]
    private static extern int SHCreateDataObject(IntPtr parent, uint count,
        [MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] IntPtr[] children,
        IntPtr inner, ref Guid iid, [MarshalAs(UnmanagedType.Interface)] out IDataObject data);

    [DllImport("ole32.dll")]
    private static extern int DoDragDrop(IDataObject data, IDropSource source, uint allowed, out uint effect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint RegisterClipboardFormat(string name);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalAlloc(uint flags, nuint bytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern bool GlobalUnlock(IntPtr handle);
    [DllImport("kernel32.dll")]
    private static extern IntPtr GlobalFree(IntPtr handle);

    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
}

