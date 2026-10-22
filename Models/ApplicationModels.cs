using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace OptiMaster
{
    [DataContract]
    public class RegistrySnapshot
    {
        [DataMember] public bool Exists { get; set; }
        [DataMember] public int Value { get; set; }
    }

    [DataContract]
    public class ServiceSnapshot
    {
        [DataMember] public string Name { get; set; }
        [DataMember] public int StartMode { get; set; }
        [DataMember] public bool WasRunning { get; set; }
    }

    [DataContract]
    public class AppBackup
    {
        [DataMember] public Dictionary<string, RegistrySnapshot> Registry = new Dictionary<string, RegistrySnapshot>();
        [DataMember] public string PowerScheme;
        [DataMember] public Dictionary<string, List<ServiceSnapshot>> Services = new Dictionary<string, List<ServiceSnapshot>>();
    }

    public class ComponentSpec
    {
        public string Id, Title, Description, Warning, ImpactText;
        public string[] Services;
        public int Impact;

        public ComponentSpec(string id, string title, string description, string warning, string impactText, int impact, params string[] services)
        {
            Id = id;
            Title = title;
            Description = description;
            Warning = warning;
            ImpactText = impactText;
            Impact = impact;
            Services = services;
        }
    }

    public class StartupEntry
    {
        public string Name;
        public string Command;
        public string Location;
        public bool IsHKLM;
    }

    public enum UninstallEntryType
    {
        Program,
        WindowsApp,
        BrowserExtension
    }

    public class UninstallEntry
    {
        public string Id;
        public string Name;
        public string Publisher;
        public string Version;
        public string UninstallCommand;
        public string InstallLocation;
        public string RegistryPath;
        public string PackageFullName;
        public UninstallEntryType Type;

        public string TypeLabel
        {
            get
            {
                if (Type == UninstallEntryType.WindowsApp) return "Aplicación de Windows";
                if (Type == UninstallEntryType.BrowserExtension) return "Complemento del navegador";
                return "Programa";
            }
        }

        public override string ToString()
        {
            string details = String.IsNullOrWhiteSpace(Version) ? TypeLabel : TypeLabel + " · " + Version;
            return Name + "\n" + details;
        }
    }
}
