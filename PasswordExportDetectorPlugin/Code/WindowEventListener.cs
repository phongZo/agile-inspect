using System;
using System.Runtime.InteropServices;
using System.Text;

namespace PasswordExportDetectorPlugin.Code
{
    public class WindowEventListener : IDisposable
    {
        private delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);

        [DllImport("user32.dll")]
        private static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

        [DllImport("user32.dll")]
        private static extern bool UnhookWinEvent(IntPtr hWinEventHook);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private const uint WINEVENT_OUTOFCONTEXT = 0;
        private const uint EVENT_OBJECT_CREATE = 0x8000;
        private const uint EVENT_OBJECT_DESTROY = 0x8001;
        private const uint EVENT_OBJECT_SHOW = 0x8002;
        private const uint EVENT_OBJECT_NAMECHANGE = 0x800C;

        private IntPtr _hookHandle;
        private WinEventDelegate _procDelegate;
        private Action<IntPtr, uint> _callback;

        public WindowEventListener(Action<IntPtr, uint> callback)
        {
            _callback = callback;
        }

        public void Start()
        {
            _procDelegate = new WinEventDelegate(WinEventProc);
            _hookHandle = SetWinEventHook(EVENT_OBJECT_CREATE, EVENT_OBJECT_NAMECHANGE, IntPtr.Zero, _procDelegate, 0, 0, WINEVENT_OUTOFCONTEXT);
        }

        public void Stop()
        {
            if (_hookHandle != IntPtr.Zero)
            {
                UnhookWinEvent(_hookHandle);
                _hookHandle = IntPtr.Zero;
            }
        }

        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            if (idObject != 0 || idChild != 0) return;
            if (hwnd == IntPtr.Zero) return;
            
            if (eventType == EVENT_OBJECT_CREATE || eventType == EVENT_OBJECT_SHOW || eventType == EVENT_OBJECT_NAMECHANGE || eventType == EVENT_OBJECT_DESTROY)
            {
                _callback?.Invoke(hwnd, eventType);
            }
        }

        public void Dispose()
        {
            Stop();
        }
        
        public static (uint pid, string title) GetWindowInfo(IntPtr hwnd)
        {
            uint pid = 0;
            try { GetWindowThreadProcessId(hwnd, out pid); } catch {}
            StringBuilder sb = new StringBuilder(512);
            try { GetWindowText(hwnd, sb, 512); } catch {}
            return (pid, sb.ToString());
        }
    }
}
