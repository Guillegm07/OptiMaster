using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace OptiMaster
{
    internal sealed class SystemMetricsSnapshot
    {
        internal float CpuUsage { get; set; }
        internal double RamUsage { get; set; }
        internal double UsedRamGb { get; set; }
        internal double TotalRamGb { get; set; }
        internal double DiskUsage { get; set; }
        internal double FreeDiskGb { get; set; }
    }

    internal sealed class SystemMetricsService : IDisposable
    {
        private readonly PerformanceCounter cpuCounter;

        internal SystemMetricsService()
        {
            try
            {
                cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                cpuCounter.NextValue();
            }
            catch { }
        }

        internal SystemMetricsSnapshot Read()
        {
            var snapshot = new SystemMetricsSnapshot();
            try { if (cpuCounter != null) snapshot.CpuUsage = Math.Max(0, Math.Min(100, cpuCounter.NextValue())); } catch { }

            var memory = new NativeMethods.MemoryStatus();
            memory.Length = (uint)Marshal.SizeOf(typeof(NativeMethods.MemoryStatus));
            NativeMethods.GlobalMemoryStatusEx(ref memory);
            snapshot.RamUsage = memory.TotalPhys == 0 ? 0 : (double)(memory.TotalPhys - memory.AvailPhys) * 100.0 / memory.TotalPhys;
            snapshot.UsedRamGb = (memory.TotalPhys - memory.AvailPhys) / 1073741824.0;
            snapshot.TotalRamGb = memory.TotalPhys / 1073741824.0;

            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                if (drive.IsReady)
                {
                    snapshot.DiskUsage = (double)(drive.TotalSize - drive.AvailableFreeSpace) * 100.0 / drive.TotalSize;
                    snapshot.FreeDiskGb = drive.AvailableFreeSpace / 1073741824.0;
                }
            }
            catch { }

            return snapshot;
        }

        public void Dispose()
        {
            if (cpuCounter != null) cpuCounter.Dispose();
        }
    }
}
