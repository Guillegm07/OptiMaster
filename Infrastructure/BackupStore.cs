using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;

namespace OptiMaster
{
    internal sealed class BackupStore
    {
        private readonly string path;

        internal BackupStore(string path)
        {
            this.path = path;
        }

        internal AppBackup Load()
        {
            AppBackup backup = null;
            if (File.Exists(path))
            {
                try
                {
                    using (var stream = File.OpenRead(path))
                        backup = (AppBackup)new DataContractJsonSerializer(typeof(AppBackup)).ReadObject(stream);
                }
                catch { }
            }

            backup = backup ?? new AppBackup();
            backup.Registry = backup.Registry ?? new Dictionary<string, RegistrySnapshot>();
            backup.Services = backup.Services ?? new Dictionary<string, List<ServiceSnapshot>>();
            return backup;
        }

        internal void Save(AppBackup backup)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            using (var stream = File.Create(path))
                new DataContractJsonSerializer(typeof(AppBackup)).WriteObject(stream, backup);
        }

        internal void Clear()
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
