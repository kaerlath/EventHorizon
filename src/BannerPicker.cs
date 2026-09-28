using System.Diagnostics;
using System.Runtime.InteropServices;

namespace EventHorizon;

internal static class BannerPicker
{
    public static Task<string?> Choose(bool font = false)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("The image picker requires Windows.");
        var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var owner = Process.GetCurrentProcess().MainWindowHandle;
        var thread = new Thread(() =>
        {
            var buffer = IntPtr.Zero;
            try
            {
                buffer = Marshal.AllocHGlobal(32768 * sizeof(char));
                Marshal.WriteInt16(buffer, 0);
                var dialog = new OpenFileName { Size = Marshal.SizeOf<OpenFileName>(), Owner = owner,
                    Filter = font ? "Font files (TTF, OTF, WOFF, WOFF2)\0*.ttf;*.otf;*.woff;*.woff2\0\0" : "Banner images (PNG, JPEG)\0*.png;*.jpg;*.jpeg\0\0", File = buffer, MaxFile = 32768,
                    Title = font ? "Import a font for Event Horizon" : "Choose an Event Horizon banner", Flags = 0x00080000 | 0x00001000 | 0x00000800 | 0x00000008 };
                if (GetOpenFileName(ref dialog)) completion.SetResult(Marshal.PtrToStringUni(buffer));
                else if (CommDlgExtendedError() is var error && error != 0) completion.SetException(new IOException($"Windows could not open the image picker ({error})."));
                else completion.SetResult(null);
            }
            catch (Exception ex) { completion.TrySetException(ex); }
            finally { if (buffer != IntPtr.Zero) Marshal.FreeHGlobal(buffer); }
        }) { IsBackground = true, Name = "Event Horizon image picker" };
        thread.SetApartmentState(ApartmentState.STA); thread.Start();
        return completion.Task;
    }
    [DllImport("comdlg32.dll", EntryPoint = "GetOpenFileNameW", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool GetOpenFileName(ref OpenFileName value);
    [DllImport("comdlg32.dll")] private static extern uint CommDlgExtendedError();
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct OpenFileName
    {
        public int Size; public IntPtr Owner, Instance; public string Filter; public IntPtr CustomFilter;
        public int MaxCustomFilter, FilterIndex; public IntPtr File; public int MaxFile;
        public IntPtr FileTitle; public int MaxFileTitle; public string? InitialDirectory; public string Title;
        public int Flags; public short FileOffset, FileExtension; public string? DefaultExtension;
        public IntPtr CustomData, Hook, TemplateName, Reserved; public int ReservedValue, FlagsEx;
    }
}
