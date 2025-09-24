using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace BrightnessChangePlugin.Code
{
    public sealed class PhysicalMonitor : IDisposable
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        public struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;

            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        private IntPtr _handle;
        private string _description;
        private bool _disposed;

        internal PhysicalMonitor(IntPtr handle, string description)
        {
            _handle = handle;
            _description = description;
        }

        public string Description => _description;

        public uint MinBrightness
        {
            get
            {
                GetBrightness(out uint min, out _, out _);
                return min;
            }
        }

        public uint MaxBrightness
        {
            get
            {
                GetBrightness(out _, out _, out uint max);
                return max;
            }
        }

        public uint CurrentBrightness
        {
            get
            {
                GetBrightness(out _, out uint current, out _);
                return current;
            }
        }

        public void SetBrightness(uint brightness)
        {
            if (!Native.SetMonitorBrightness(_handle, brightness))
                throw new InvalidOperationException("Failed to set monitor brightness.");
        }

        private void GetBrightness(out uint min, out uint current, out uint max)
        {
            if (!Native.GetMonitorBrightness(_handle, out min, out current, out max))
                throw new InvalidOperationException("Failed to get monitor brightness.");
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Native.DestroyPhysicalMonitor(_handle);
                _disposed = true;
            }
        }

        private static class Native
        {
            [DllImport("dxva2.dll", SetLastError = true)]
            public static extern bool GetMonitorBrightness(
                IntPtr hMonitor,
                out uint pdwMinimumBrightness,
                out uint pdwCurrentBrightness,
                out uint pdwMaximumBrightness);

            [DllImport("dxva2.dll", SetLastError = true)]
            public static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

            [DllImport("dxva2.dll", SetLastError = true)]
            public static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);
        }
    }

    public sealed class MonitorCollection : IEnumerable<PhysicalMonitor>, IDisposable
    {
        private readonly List<PhysicalMonitor> _monitors = new();

        internal void Add(PhysicalMonitor monitor) => _monitors.Add(monitor);

        public IEnumerator<PhysicalMonitor> GetEnumerator() => _monitors.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public void Dispose()
        {
            foreach (var m in _monitors)
                m.Dispose();
            _monitors.Clear();
        }
    }

    public static class MonitorManager
    {
        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);

        [StructLayout(LayoutKind.Sequential)]
        private struct Rect
        {
            public int left, top, right, bottom;
        }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, out uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll", SetLastError = true)]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(
            IntPtr hMonitor,
            uint dwPhysicalMonitorArraySize,
            [Out] PhysicalMonitor.PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        private static readonly MonitorEnumProc _monitorEnumProc = MonitorEnumCallback;
        private static MonitorCollection? _currentCollection;

        public static MonitorCollection GetAllMonitors()
        {
            _currentCollection = new MonitorCollection();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, _monitorEnumProc, IntPtr.Zero);
            return _currentCollection;
        }

        private static bool MonitorEnumCallback(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData)
        {
            if (GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out uint count) && count > 0)
            {
                var array = new PhysicalMonitor.PHYSICAL_MONITOR[count];
                if (GetPhysicalMonitorsFromHMONITOR(hMonitor, count, array))
                {
                    foreach (var pm in array)
                    {
                        _currentCollection!.Add(new PhysicalMonitor(pm.hPhysicalMonitor, pm.szPhysicalMonitorDescription));
                    }
                }
            }
            return true; // continue enumeration
        }
    }
}
