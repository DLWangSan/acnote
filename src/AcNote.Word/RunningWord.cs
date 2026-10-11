using System.Runtime.InteropServices;

namespace AcNote.Word;

public static class RunningWord
{
    public static object? TryGet()
    {
        var type = Type.GetTypeFromProgID("Word.Application");
        if (type is null)
            return null;

        try
        {
            var clsid = type.GUID;
            NativeMethods.GetActiveObject(ref clsid, IntPtr.Zero, out var instance);
            return instance;
        }
        catch (COMException)
        {
            return null;
        }
    }

    private static class NativeMethods
    {
        [DllImport("oleaut32.dll", PreserveSig = false)]
        public static extern void GetActiveObject(ref Guid rclsid, IntPtr reserved, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
    }
}
