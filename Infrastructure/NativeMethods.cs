using System;
using System.Runtime.InteropServices;

namespace OptiMaster
{
    internal static class NativeMethods
    {
        [DllImport("user32.dll")]
        internal static extern bool ReleaseCapture();

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(IntPtr hWnd, int message, int wParam, int lParam);

        [DllImport("psapi.dll")]
        internal static extern int EmptyWorkingSet(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        internal static extern int NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        internal static extern int NtResumeProcess(IntPtr processHandle);

        [DllImport("ntdll.dll")]
        internal static extern int NtSetSystemInformation(int informationClass, ref int command, int length);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool OpenProcessToken(IntPtr processHandle, uint desiredAccess, out IntPtr tokenHandle);

        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        internal static extern bool LookupPrivilegeValue(string systemName, string name, out long luid);

        [DllImport("advapi32.dll", SetLastError = true)]
        internal static extern bool AdjustTokenPrivileges(IntPtr tokenHandle, bool disableAllPrivileges, ref TokenPrivileges newState, int bufferLength, IntPtr previousState, IntPtr returnLength);

        [DllImport("kernel32.dll")]
        internal static extern bool CloseHandle(IntPtr handle);

        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)]
        static extern int SHEmptyRecycleBin(IntPtr hwnd, string rootPath, uint flags);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

        internal static void EmptyRecycleBin(IntPtr windowHandle)
        {
            const uint noConfirmation = 0x00000001;
            const uint noProgress = 0x00000002;
            const uint noSound = 0x00000004;
            SHEmptyRecycleBin(windowHandle, null, noConfirmation | noProgress | noSound);
        }

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        internal struct TokenPrivileges
        {
            internal int PrivilegeCount;
            internal long Luid;
            internal int Attributes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal struct MemoryStatus
        {
            internal uint Length;
            internal uint MemoryLoad;
            internal ulong TotalPhys;
            internal ulong AvailPhys;
            internal ulong TotalPageFile;
            internal ulong AvailPageFile;
            internal ulong TotalVirtual;
            internal ulong AvailVirtual;
            internal ulong AvailExtendedVirtual;
        }
    }
}
