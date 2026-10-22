using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace OptiMaster
{
    internal sealed class UninstallerService
    {
        private const string UninstallPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private readonly IProcessRunner processRunner;

        internal UninstallerService(IProcessRunner processRunner)
        {
            this.processRunner = processRunner;
        }

        internal List<UninstallEntry> GetEntries()
        {
            var entries = new List<UninstallEntry>();
            var known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            ReadRegistryEntries(entries, known, RegistryHive.LocalMachine, RegistryView.Registry64);
            ReadRegistryEntries(entries, known, RegistryHive.LocalMachine, RegistryView.Registry32);
            ReadRegistryEntries(entries, known, RegistryHive.CurrentUser, RegistryView.Default);
            ReadWindowsApps(entries, known);
            ReadBrowserExtensions(entries, known);
            entries.Sort(delegate (UninstallEntry a, UninstallEntry b) { return String.Compare(a.Name, b.Name, StringComparison.CurrentCultureIgnoreCase); });
            return entries;
        }

        private void ReadRegistryEntries(List<UninstallEntry> entries, HashSet<string> known, RegistryHive hive, RegistryView view)
        {
            try
            {
                using (var baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (var key = baseKey.OpenSubKey(UninstallPath, false))
                {
                    if (key == null) return;
                    foreach (string subName in key.GetSubKeyNames())
                    {
                        using (var app = key.OpenSubKey(subName, false))
                        {
                            string name = Convert.ToString(app.GetValue("DisplayName"));
                            string uninstall = Convert.ToString(app.GetValue("UninstallString"));
                            if (String.IsNullOrWhiteSpace(name) || String.IsNullOrWhiteSpace(uninstall)) continue;
                            string id = hive + "|" + view + "|" + subName;
                            if (!known.Add(id)) continue;
                            entries.Add(new UninstallEntry
                            {
                                Id = id,
                                Name = name,
                                Publisher = Convert.ToString(app.GetValue("Publisher")),
                                Version = Convert.ToString(app.GetValue("DisplayVersion")),
                                UninstallCommand = uninstall,
                                InstallLocation = Convert.ToString(app.GetValue("InstallLocation")),
                                RegistryPath = UninstallPath + "\\" + subName,
                                Type = UninstallEntryType.Program
                            });
                        }
                    }
                }
            }
            catch { }
        }

        private void ReadWindowsApps(List<UninstallEntry> entries, HashSet<string> known)
        {
            string script = "Get-AppxPackage | ForEach-Object { $_.Name + '\t' + $_.PackageFullName + '\t' + $_.Version }";
            string output = processRunner.Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"" + script + "\"");
            foreach (string line in output.Replace("\r", "").Split('\n'))
            {
                string[] parts = line.Split('\t');
                if (parts.Length < 2 || String.IsNullOrWhiteSpace(parts[0]) || String.IsNullOrWhiteSpace(parts[1])) continue;
                string id = "uwp|" + parts[1];
                if (!known.Add(id)) continue;
                entries.Add(new UninstallEntry
                {
                    Id = id,
                    Name = parts[0],
                    PackageFullName = parts[1],
                    Version = parts.Length > 2 ? parts[2] : "",
                    Type = UninstallEntryType.WindowsApp
                });
            }
        }

        private void ReadBrowserExtensions(List<UninstallEntry> entries, HashSet<string> known)
        {
            AddExtensionFolders(entries, known, "Chrome", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google", "Chrome", "User Data"));
            AddExtensionFolders(entries, known, "Microsoft Edge", Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Edge", "User Data"));
        }

        private static void AddExtensionFolders(List<UninstallEntry> entries, HashSet<string> known, string browser, string userDataPath)
        {
            try
            {
                foreach (string profile in Directory.GetDirectories(userDataPath))
                {
                    string extensions = Path.Combine(profile, "Extensions");
                    if (!Directory.Exists(extensions)) continue;
                    foreach (string extension in Directory.GetDirectories(extensions))
                    {
                        string id = "extension|" + extension;
                        if (!known.Add(id)) continue;
                        entries.Add(new UninstallEntry
                        {
                            Id = id,
                            Name = browser + " · " + Path.GetFileName(extension),
                            InstallLocation = extension,
                            Type = UninstallEntryType.BrowserExtension
                        });
                    }
                }
            }
            catch { }
        }

        internal void LaunchUninstaller(UninstallEntry entry)
        {
            string command = entry.UninstallCommand;
            if (command.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0)
                command = command.Replace("/I", "/X").Replace("/i", "/X");
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + command) { UseShellExecute = true });
        }

        internal string RemoveWindowsApp(UninstallEntry entry)
        {
            return processRunner.Run("powershell.exe", "-NoProfile -ExecutionPolicy Bypass -Command \"Remove-AppxPackage -Package '" + entry.PackageFullName.Replace("'", "''") + "'\"");
        }

        internal int RemoveBrowserExtension(UninstallEntry entry)
        {
            if (String.IsNullOrWhiteSpace(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation)) return 0;
            Directory.Delete(entry.InstallLocation, true);
            return 1;
        }

        internal int ForceRemove(UninstallEntry entry)
        {
            int removed = 0;
            if (!String.IsNullOrWhiteSpace(entry.InstallLocation) && Directory.Exists(entry.InstallLocation))
            {
                Directory.Delete(entry.InstallLocation, true);
                removed++;
            }
            if (!String.IsNullOrWhiteSpace(entry.RegistryPath))
            {
                DeleteRegistryEntry(entry.RegistryPath);
                removed++;
            }
            return removed;
        }

        internal int RemoveEmptyResidualFolders(IEnumerable<UninstallEntry> entries)
        {
            int removed = 0;
            foreach (var entry in entries)
            {
                if (String.IsNullOrWhiteSpace(entry.InstallLocation) || !Directory.Exists(entry.InstallLocation)) continue;
                try
                {
                    if (Directory.GetFiles(entry.InstallLocation, "*", SearchOption.AllDirectories).Length == 0)
                    {
                        Directory.Delete(entry.InstallLocation, true);
                        removed++;
                    }
                }
                catch { }
            }
            return removed;
        }

        private static void DeleteRegistryEntry(string path)
        {
            try { Registry.LocalMachine.DeleteSubKeyTree(path, false); return; } catch { }
            try { Registry.CurrentUser.DeleteSubKeyTree(path, false); } catch { }
        }
    }
}
