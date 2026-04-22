using System.Runtime.InteropServices;
using System.Text;
using AgileInspect.Code.Rules;
using AgileInspect.Code.Settings;
using Newtonsoft.Json.Linq;

namespace UnknownBluetoothPlugin
{
    public class UnknownBluetoothDetector
    {
        #region Singleton
        public static UnknownBluetoothDetector Instance { get; set; }
        public UnknownBluetoothDetector()
        {
            Instance = this;
        }
        #endregion

        private const int ErrorNoMoreItems = 259;

        private readonly string pluginName = "UnknownBluetoothPlugin";

        public void Check()
        {
            // Enumerate paired/remembered devices and their connection status (Settings-like list).
            // Note: This is classic Bluetooth enumeration (BluetoothApis.dll), not WinRT.
            var devices = GetDevices(out var connectedCount, out var detail);
            bool detected = connectedCount > 0;

            PluginContext.Log(pluginName, $"[UnknownBluetooth] Devices={devices.Count}, Connected={connectedCount}, Detail={detail}");

            var resultObj = new JObject
            {
                // User-requested payload shape:
                // { "unknown_bluetooth": bool, "devices": [{name, connected, paired}, ...], "connectedCount": n }
                [StoreCfgLoader.mapPluginNameToEventType(pluginName)] = detected,
                ["devices"] = new JArray(devices),
                ["connectedCount"] = connectedCount
            };

            RuleService.Save(StoreCfgLoader.mapPluginNameToEventType(pluginName), resultObj);
            PluginContext.SendDetectionResult(pluginName, resultObj);
        }

        private List<JObject> GetDevices(out int connectedCount, out string detail)
        {
            connectedCount = 0;
            detail = "bluetooth_apis";
            var results = new List<JObject>();

            try
            {
                var pr = new BLUETOOTH_FIND_RADIO_PARAMS
                {
                    dwSize = (uint)Marshal.SizeOf<BLUETOOTH_FIND_RADIO_PARAMS>(),
                };

                IntPtr hFind = BluetoothNative.BluetoothFindFirstRadio(ref pr, out IntPtr hRadio);
                if (hFind == IntPtr.Zero)
                {
                    int err = Marshal.GetLastWin32Error();
                    detail = err == ErrorNoMoreItems ? "no_bluetooth_radio" : $"radio_find_failed_{err}";
                    return results;
                }

                try
                {
                    if (hRadio == IntPtr.Zero)
                    {
                        detail = "no_bluetooth_radio";
                        return results;
                    }

                    // Search for known devices (paired/remembered), and include connected ones.
                    var sp = new BLUETOOTH_DEVICE_SEARCH_PARAMS
                    {
                        dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_SEARCH_PARAMS>(),
                        fReturnAuthenticated = true,
                        fReturnRemembered = true,
                        fReturnConnected = true,
                        fReturnUnknown = false,
                        fIssueInquiry = false,
                        cTimeoutMultiplier = 0,
                        hRadio = hRadio
                    };

                    var di = new BLUETOOTH_DEVICE_INFO
                    {
                        dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>()
                    };

                    IntPtr hDevFind = BluetoothNative.BluetoothFindFirstDevice(ref sp, ref di);
                    if (hDevFind == IntPtr.Zero)
                    {
                        int err = Marshal.GetLastWin32Error();
                        if (err == ErrorNoMoreItems)
                        {
                            detail = "no_devices";
                            return results;
                        }

                        detail = $"device_find_failed_{err}";
                        return results;
                    }

                    try
                    {
                        while (true)
                        {
                            var obj = new JObject
                            {
                                ["name"] = (di.szName ?? string.Empty).Trim(),
                                ["connected"] = di.fConnected,
                                ["paired"] = di.fAuthenticated
                            };
                            results.Add(obj);
                            if (di.fConnected) connectedCount++;

                            // Prepare for next call.
                            di = new BLUETOOTH_DEVICE_INFO { dwSize = (uint)Marshal.SizeOf<BLUETOOTH_DEVICE_INFO>() };

                            bool ok = BluetoothNative.BluetoothFindNextDevice(hDevFind, ref di);
                            if (!ok)
                            {
                                int err = Marshal.GetLastWin32Error();
                                if (err == ErrorNoMoreItems)
                                    break;

                                detail = $"device_next_failed_{err}";
                                break;
                            }
                        }
                    }
                    finally
                    {
                        BluetoothNative.BluetoothFindDeviceClose(hDevFind);
                    }

                    return results;
                }
                finally
                {
                    if (hRadio != IntPtr.Zero)
                        BluetoothNative.CloseHandle(hRadio);
                    if (hFind != IntPtr.Zero)
                        BluetoothNative.BluetoothFindRadioClose(hFind);
                }
            }
            catch (Exception ex)
            {
                detail = "error";
                PluginContext.Log(pluginName, $"[UnknownBluetooth] Enumeration error: {ex.Message}");
                return results;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_FIND_RADIO_PARAMS
        {
            public uint dwSize;
        }

        // https://learn.microsoft.com/windows/win32/api/bluetoothapis/ns-bluetoothapis-bluetooth_device_search_params
        [StructLayout(LayoutKind.Sequential)]
        private struct BLUETOOTH_DEVICE_SEARCH_PARAMS
        {
            public uint dwSize;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnAuthenticated;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnRemembered;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnUnknown;
            [MarshalAs(UnmanagedType.Bool)] public bool fReturnConnected;
            [MarshalAs(UnmanagedType.Bool)] public bool fIssueInquiry;
            public byte cTimeoutMultiplier;
            public IntPtr hRadio;
        }

        // https://learn.microsoft.com/windows/win32/api/bluetoothapis/ns-bluetoothapis-bluetooth_device_info
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct BLUETOOTH_DEVICE_INFO
        {
            public uint dwSize;
            public ulong Address;
            public uint ulClassofDevice;
            [MarshalAs(UnmanagedType.Bool)] public bool fConnected;
            [MarshalAs(UnmanagedType.Bool)] public bool fRemembered;
            [MarshalAs(UnmanagedType.Bool)] public bool fAuthenticated;
            public SYSTEMTIME stLastSeen;
            public SYSTEMTIME stLastUsed;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 248)]
            public string szName;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEMTIME
        {
            public ushort wYear;
            public ushort wMonth;
            public ushort wDayOfWeek;
            public ushort wDay;
            public ushort wHour;
            public ushort wMinute;
            public ushort wSecond;
            public ushort wMilliseconds;
        }

        private static class BluetoothNative
        {
            [DllImport("BluetoothApis.dll", SetLastError = true)]
            public static extern IntPtr BluetoothFindFirstRadio(ref BLUETOOTH_FIND_RADIO_PARAMS pbtfrp, out IntPtr phRadio);

            [DllImport("BluetoothApis.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool BluetoothFindRadioClose(IntPtr hFind);

            [DllImport("BluetoothApis.dll", SetLastError = true)]
            public static extern IntPtr BluetoothFindFirstDevice(ref BLUETOOTH_DEVICE_SEARCH_PARAMS pbtsp, ref BLUETOOTH_DEVICE_INFO pbtdi);

            [DllImport("BluetoothApis.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool BluetoothFindNextDevice(IntPtr hFind, ref BLUETOOTH_DEVICE_INFO pbtdi);

            [DllImport("BluetoothApis.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool BluetoothFindDeviceClose(IntPtr hFind);

            [DllImport("kernel32.dll", SetLastError = true)]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);
        }
    }
}
