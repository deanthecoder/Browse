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
using System.Text;
using Avalonia.Input;
using Browse.Services;

namespace Browse.Tests;

[TestFixture]
[Platform("Win")]
[Apartment(ApartmentState.STA)]
public class WindowsFileDragTests
{
    [Test]
    public void ShellFileDataSupportsWpfMediaMaskAndPreservesPaths()
    {
        if (!OperatingSystem.IsWindows())
            return;
        Marshal.ThrowExceptionForHR(OleInitialize(IntPtr.Zero));
        var sourcePointer = Marshal.GetComInterfaceForObject(new WindowsFileDrag.DropSource(), typeof(WindowsFileDrag.IDropSource));
        Marshal.Release(sourcePointer);
        var directory = Path.Combine(Path.GetTempPath(), "Browse drag " + Guid.NewGuid());
        Directory.CreateDirectory(directory);
        var paths = new[] { Path.Combine(directory, "résumé 日本.pdf"), Path.Combine(directory, "second.pdf") };
        try
        {
            foreach (var path in paths)
                File.WriteAllText(path, "Test file payload");
            var data = WindowsFileDrag.CreateDataObject(paths);
            try
            {
                var format = new FORMATETC
                {
                    cfFormat = 15, dwAspect = DVASPECT.DVASPECT_CONTENT, lindex = -1,
                    tymed = (TYMED)0xD3 // WPF's HGLOBAL | ISTREAM | ENHMF | MFPICT | GDI.
                };
                Assert.That(data.QueryGetData(ref format), Is.Zero);
                data.GetData(ref format, out var medium);
                try
                {
                    Assert.That(medium.tymed, Is.EqualTo(TYMED.TYMED_HGLOBAL));
                    Assert.That(DragQueryFile(medium.unionmember, uint.MaxValue, null, 0), Is.EqualTo(paths.Length));
                    for (var i = 0; i < paths.Length; i++)
                    {
                        var buffer = new StringBuilder(1024);
                        DragQueryFile(medium.unionmember, (uint)i, buffer, (uint)buffer.Capacity);
                        Assert.That(buffer.ToString(), Is.EqualTo(paths[i]));
                    }
                }
                finally { ReleaseStgMedium(ref medium); }

                format.tymed = TYMED.TYMED_ISTREAM;
                Assert.That(data.QueryGetData(ref format), Is.Not.Zero);
                var marker = DataFormat.CreateStringApplicationFormat("Browse.ArchiveDrag");
                WindowsFileDrag.AddArchiveMarker(data, marker);
                format.cfFormat = unchecked((short)RegisterClipboardFormat(marker.ToSystemName("avn-app-fmt:")));
                format.tymed = (TYMED)0xD3;
                Assert.That(data.QueryGetData(ref format), Is.Zero);
            }
            finally { Marshal.ReleaseComObject(data); }
        }
        finally
        {
            Directory.Delete(directory, true);
            OleUninitialize();
        }
    }

    [DllImport("ole32.dll")]
    private static extern int OleInitialize(IntPtr reserved);
    [DllImport("ole32.dll")]
    private static extern void OleUninitialize();
    [DllImport("ole32.dll")]
    private static extern void ReleaseStgMedium(ref STGMEDIUM medium);
    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern uint DragQueryFile(IntPtr drop, uint index, StringBuilder path, uint size);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern uint RegisterClipboardFormat(string name);
}
