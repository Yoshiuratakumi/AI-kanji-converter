using System.Runtime.InteropServices;

namespace AiImeShell.Interop;

internal static class NativeMethods
{
    // CLSID for TF_ThreadMgr
    internal static readonly Guid CLSID_TF_ThreadMgr =
        new("529A9E6B-6587-4F23-AB9E-9C7D683E3C50");

    // CLSID for TF_InputProcessorProfiles
    internal static readonly Guid CLSID_TF_InputProcessorProfiles =
        new("33C53A50-F456-4884-B049-85FD643ECFED");

    [DllImport("ole32.dll", PreserveSig = false)]
    internal static extern void CoCreateInstance(
        in Guid rclsid,
        IntPtr pUnkOuter,
        uint dwClsContext,
        in Guid riid,
        [MarshalAs(UnmanagedType.IUnknown)] out object ppv);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetFocus();

    [DllImport("user32.dll")]
    internal static extern IntPtr GetAncestor(IntPtr hwnd, uint gaFlags);

    [StructLayout(LayoutKind.Sequential)]
    internal struct POINT { public int X; public int Y; }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetCaretPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool ClientToScreen(IntPtr hWnd, ref POINT lpPoint);

    // Virtual key codes
    internal const int VK_SPACE     = 0x20;
    internal const int VK_RETURN    = 0x0D;
    internal const int VK_ESCAPE    = 0x1B;
    internal const int VK_BACK      = 0x08;
    internal const int VK_UP        = 0x26;
    internal const int VK_DOWN      = 0x28;
    internal const int VK_LEFT      = 0x25;
    internal const int VK_RIGHT     = 0x27;
    internal const int VK_SHIFT     = 0x10;
    internal const int VK_CONTROL   = 0x11;

    internal const uint CLSCTX_INPROC_SERVER = 0x1;
}
