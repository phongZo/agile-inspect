using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace AgileMark
{
    public static class ProcessExtension
    {
        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool CreateProcessAsUser(
        IntPtr hToken,
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("advapi32.dll", SetLastError = true)]
        private static extern bool OpenProcessToken(
            IntPtr ProcessHandle,
            uint DesiredAccess,
            out IntPtr TokenHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        // Define required structures
        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public uint dwProcessId;
            public uint dwThreadId;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public uint dwX;
            public uint dwY;
            public uint dwXSize;
            public uint dwYSize;
            public uint dwXCountChars;
            public uint dwYCountChars;
            public uint dwFillAttribute;
            public uint dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        // Constants
        private const uint TOKEN_DUPLICATE = 0x0002;
        private const uint TOKEN_QUERY = 0x0008;
        private const uint TOKEN_ASSIGN_PRIMARY = 0x0001;
        private const uint NORMAL_PRIORITY_CLASS = 0x0020;

        // Using Windows API for more control over token inheritance
        public static uint StartProcessWithExplicitToken(string exePath, string arguments = "")
        {
            IntPtr tokenHandle = IntPtr.Zero;
            PROCESS_INFORMATION processInfo = new PROCESS_INFORMATION();

            try
            {
                // Get current process token
                if (!OpenProcessToken(GetCurrentProcess(), TOKEN_DUPLICATE | TOKEN_QUERY | TOKEN_ASSIGN_PRIMARY, out tokenHandle))
                {
                    return 0; // Return 0 to indicate failure
                }

                // Setup startup info
                STARTUPINFO startupInfo = new STARTUPINFO();
                startupInfo.cb = Marshal.SizeOf(typeof(STARTUPINFO));

                // Create process with token
                string commandLine = string.IsNullOrEmpty(arguments) ? null : $"\"{exePath}\" {arguments}";
                bool result = CreateProcessAsUser(
                    tokenHandle,
                    exePath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    true,  // Inherit handles
                    NORMAL_PRIORITY_CLASS,
                    IntPtr.Zero,
                    null,
                    ref startupInfo,
                    out processInfo);

                if (!result)
                {
                    return 0; // Return 0 to indicate failure
                }

                return processInfo.dwProcessId; // Return the process ID
            }
            catch (Exception ex)
            {
                return 0;
            }
            finally
            {
                // Clean up
                if (tokenHandle != IntPtr.Zero)
                    CloseHandle(tokenHandle);
                if (processInfo.hProcess != IntPtr.Zero)
                    CloseHandle(processInfo.hProcess);
                if (processInfo.hThread != IntPtr.Zero)
                    CloseHandle(processInfo.hThread);
            }
        }

    }
}
