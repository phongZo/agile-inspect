using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Drawing;

namespace AgileInspect.Code.MonitorManagement
{
    public class MultipleMonitors
    {
        public static List<(Rectangle bounds, string deviceName)> GetMonitors()
        {
            var screens = new List<(Rectangle, string)>();

            MonitorEnumProc callback = (IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData) =>
            {
                MONITORINFOEX mi = new MONITORINFOEX();
                mi.cbSize = Marshal.SizeOf(typeof(MONITORINFOEX));

                if (GetMonitorInfo(hMonitor, ref mi))
                {
                    int width = mi.rcMonitor.Right - mi.rcMonitor.Left;
                    int height = mi.rcMonitor.Bottom - mi.rcMonitor.Top;
                    Rectangle bounds = new Rectangle(mi.rcMonitor.Left, mi.rcMonitor.Top, width, height);
                    screens.Add((bounds, mi.szDevice));
                }

                return true;
            };

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, callback, IntPtr.Zero);
            return screens;
        }

        public static List<(string deviceName, int brightness)> GetBrightnessMonitors(int fallbackBrightness)
        {
            var result = new List<(string, int)>();

            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero,
                (IntPtr hMonitor, IntPtr hdc, ref RECT rect, IntPtr data) =>
                {
                    uint numberOfMonitors = 0;
                    if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref numberOfMonitors))
                        return true;

                    var physicalMonitors = new PHYSICAL_MONITOR[numberOfMonitors];
                    if (GetPhysicalMonitorsFromHMONITOR(hMonitor, numberOfMonitors, physicalMonitors))
                    {
                        foreach (var pm in physicalMonitors)
                        {
                            int brightness = fallbackBrightness;

                            if (GetMonitorBrightness(pm.hPhysicalMonitor, out uint min, out uint current, out uint max))
                            {
                                brightness = (int)current;
                            }

                            result.Add((pm.szPhysicalMonitorDescription, brightness));
                            DestroyPhysicalMonitor(pm.hPhysicalMonitor);
                        }
                    }

                    return true;
                }, IntPtr.Zero);

            return result;
        }

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref RECT lprcMonitor, IntPtr dwData);
        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFOEX lpmi);
        [StructLayout(LayoutKind.Sequential)]
        struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MONITORINFOEX
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string szDevice;
        }

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool GetMonitorBrightness(
            IntPtr hMonitor,
            out uint pdwMinimumBrightness,
            out uint pdwCurrentBrightness,
            out uint pdwMaximumBrightness
        );

        [DllImport("dxva2.dll", SetLastError = true)]
        static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }
    }
}
