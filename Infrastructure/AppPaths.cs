using System;
using System.IO;

namespace OptiMaster
{
    internal static class AppPaths
    {
        internal static readonly string DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            "OptiMaster");

        internal static readonly string BackupFile = Path.Combine(DataDirectory, "backup.json");
    }
}
