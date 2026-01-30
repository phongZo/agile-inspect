using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ClipboardMonitorPlugin.Code
{
    public sealed class ClipboardWindow : IDisposable
    {
        private const int WM_CLIPBOARDUPDATE = 0x031D;

        private readonly Action _onUpdated;
        private readonly HwndSource _source;

        public ClipboardWindow(Action onUpdated)
        {
            _onUpdated = onUpdated;

            var parameters = new HwndSourceParameters("ClipboardMonitorHiddenWindow")
            {
                Width = 0,
                Height = 0,
                PositionX = 0,
                PositionY = 0,
                WindowStyle = 0x800000, // WS_OVERLAPPED
            };

            _source = new HwndSource(parameters);
            _source.AddHook(WndProc);

            if (!AddClipboardFormatListener(_source.Handle))
                throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
        }

        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == WM_CLIPBOARDUPDATE)
            {
                _onUpdated();
                // handled = false;
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            try
            {
                RemoveClipboardFormatListener(_source.Handle);
            }
            catch { /* ignore */ }

            try
            {
                _source.RemoveHook(WndProc);
                _source.Dispose();
            }
            catch { /* ignore */ }
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool AddClipboardFormatListener(IntPtr hwnd);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool RemoveClipboardFormatListener(IntPtr hwnd);
    }
}
