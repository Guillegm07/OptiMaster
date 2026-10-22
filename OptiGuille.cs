using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.ServiceProcess;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace OptiGuille
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
            Id = id; Title = title; Description = description; Warning = warning; ImpactText = impactText; Impact = impact; Services = services;
        }
    }

    public class StartupEntry
    {
        public string Name;
        public string Command;
        public string Location;
        public bool IsHKLM;
    }

    public class MainForm : Form
    {
        const string GameBar = @"Software\Microsoft\GameBar";
        const string GameConfig = @"System\GameConfigStore";
        const string MultimediaProfile = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        const string WuPolicyPath = @"SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate";
        const string DriverSearchPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\DriverSearching";

        readonly string dataFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "OptiGuille");
        readonly string backupPath;

        readonly List<ComponentSpec> components = new List<ComponentSpec>();
        AppBackup backup;

        Panel pageHost;
        TopHeaderBar topHeader;
        readonly Dictionary<string, Control> pages = new Dictionary<string, Control>();
        readonly Dictionary<string, string> pageSubtitles = new Dictionary<string, string>();
        readonly Dictionary<string, NavigationButton> navigation = new Dictionary<string, NavigationButton>();

        PerformanceCounter cpuCounter;
        TelemetryCard cpuCard, ramCard, storageCard, ramLiveChart;
        Label memorySummary, standbySummary, cleanStatus, restoreStatus;
        DonutGauge healthGauge, ramGauge;
        OptionTile cleanShaders, cleanBrowserCaches, cleanRegistryOrphans, cleanMemoryDumps;

        // Controles Suite NVIDIA
        Label nvGpuDetectedLabel, nvSafeModeStatusLabel, nvActionStatusLabel;
        ToggleSwitch nvBlockWuToggle;
        OptionTile nvCleanDisplay, nvCleanDirs, nvCleanShaderCache, nvCleanTelemetry;
        TextBox nvPackagePathInput;
        OptionTile nvCompCore, nvCompAudio, nvCompPhysX, nvCompApp, nvCompUsbC, nvCompStripTelemetry;
        ToggleSwitch nvOptCleanInstall, nvOptSilent, nvOptDisableTelemPost, nvOptForceReBar;

        Timer monitorTimer;
        Timer powerPlanTimer;
        ToggleSwitch autoPowerToggle, autoRamPurgeToggle;
        TextBox gameProcessInput;
        bool powerAutoApplied;

        readonly Dictionary<int, ProcessPriorityClass> originalPriorities = new Dictionary<int, ProcessPriorityClass>();
        readonly Dictionary<int, long> originalAffinities = new Dictionary<int, long>();
        readonly HashSet<int> suspendedProcesses = new HashSet<int>();

        [DllImport("user32.dll")] static extern bool ReleaseCapture();
        [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);
        [DllImport("psapi.dll")] static extern int EmptyWorkingSet(IntPtr hwProc);
        [DllImport("ntdll.dll")] static extern int NtSuspendProcess(IntPtr processHandle);
        [DllImport("ntdll.dll")] static extern int NtResumeProcess(IntPtr processHandle);
        [DllImport("ntdll.dll")] static extern int NtSetSystemInformation(int InfoClass, ref int Info, int Length);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool OpenProcessToken(IntPtr ProcessHandle, uint DesiredAccess, out IntPtr TokenHandle);
        [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Auto)] static extern bool LookupPrivilegeValue(string lpSystemName, string lpName, out long lpLuid);
        [DllImport("advapi32.dll", SetLastError = true)] static extern bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, ref TOKEN_PRIVILEGES NewState, int BufferLength, IntPtr PreviousState, IntPtr ReturnLength);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr hObject);
        [DllImport("Shell32.dll", CharSet = CharSet.Unicode)] static extern int SHEmptyRecycleBin(IntPtr hwnd, string rootPath, uint flags);

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public long Luid;
            public int Attributes;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        struct MemoryStatus
        {
            public uint Length, MemoryLoad;
            public ulong TotalPhys, AvailPhys, TotalPageFile, AvailPageFile, TotalVirtual, AvailVirtual, AvailExtendedVirtual;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern bool GlobalMemoryStatusEx(ref MemoryStatus buffer);

        public MainForm()
        {
            backupPath = Path.Combine(dataFolder, "backup.json");
            Text = "OptiGuille";
            FormBorderStyle = FormBorderStyle.None;
            AutoScaleMode = AutoScaleMode.None;

            Rectangle workArea = Screen.FromPoint(Cursor.Position).WorkingArea;
            int initialWidth = (int)Math.Round(workArea.Width * 0.88);
            int initialHeight = (int)Math.Round(workArea.Height * 0.86);
            MinimumSize = new Size((int)Math.Round(workArea.Width * 0.78), (int)Math.Round(workArea.Height * 0.76));
            ClientSize = new Size(initialWidth, initialHeight);
            StartPosition = FormStartPosition.Manual;
            Location = new Point(workArea.Left + (workArea.Width - initialWidth) / 2, workArea.Top + (workArea.Height - initialHeight) / 2);
            BackColor = UiTheme.BgCanvas;
            ForeColor = UiTheme.TextLight;
            Font = new Font("Segoe UI", 9.5f);
            DoubleBuffered = true;

            Directory.CreateDirectory(dataFolder);
            Load += delegate { DwmNative.ApplyModernFrame(this); };

            components.Add(new ComponentSpec("DiagTrack", "Telemetría de Windows", "Detiene el envío de diagnósticos en segundo plano.", "Desactiva el envío de informes de uso a Microsoft.", "Bajo", 2, "DiagTrack"));
            components.Add(new ComponentSpec("WSearch", "Indexación de búsqueda", "Evita lecturas constantes en disco deteniendo el indizador.", "Las búsquedas en el explorador serán más lentas.", "Moderado", 3, "WSearch"));
            components.Add(new ComponentSpec("SysMain", "SysMain (Superfetch)", "Libera ciclos de lectura y RAM en unidades NVMe/SSD.", "Recomendado desactivar únicamente si usas SSD.", "Moderado", 3, "SysMain"));
            components.Add(new ComponentSpec("Spooler", "Cola de impresión", "Detiene el servicio de impresoras si no imprimes.", "Las impresoras no funcionarán mientras esté inactivo.", "Bajo", 2, "Spooler"));
            components.Add(new ComponentSpec("XboxServices", "Servicios de red Xbox", "Apaga demonios de Xbox Live y guardado en nube.", "Afecta a la app Xbox y partidas de Game Pass.", "Bajo", 2, "XblAuthManager", "XblGameSave", "XboxNetApiSvc"));

            try { cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"); cpuCounter.NextValue(); } catch { }

            BuildInterface();

            FormClosing += delegate
            {
                if (powerPlanTimer != null) powerPlanTimer.Stop();
                if (monitorTimer != null) monitorTimer.Stop();
                RestoreAutomaticPowerPlan();
            };
        }

        void EnableDrag(Control c)
        {
            c.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };
        }

        void BuildInterface()
        {
            var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 295));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            Controls.Add(root);

            // SIDEBAR IZQUIERDO
            var sideLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = UiTheme.BgSidebar, Padding = new Padding(0, 8, 0, 16), Margin = Padding.Empty };
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 80));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            sideLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 68));
            root.Controls.Add(sideLayout, 0, 0);

            var logo = new LogoControl { Dock = DockStyle.Fill };
            EnableDrag(logo);
            sideLayout.Controls.Add(logo, 0, 0);

            var navPanel = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 4, 0, 0), BackColor = UiTheme.BgSidebar };
            EnableDrag(navPanel);
            sideLayout.Controls.Add(navPanel, 0, 1);

            var sideFooter = new RoundedPanel { Size = new Size(267, 54), FillColor = Color.FromArgb(14, 22, 38), BorderColor = Color.FromArgb(26, 65, 65), Radius = 10, Margin = new Padding(14, 4, 14, 0) };
            sideFooter.Controls.Add(new Label { Text = "MODO ADMINISTRADOR", Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold), ForeColor = UiTheme.Mint, BackColor = Color.FromArgb(14, 22, 38), AutoSize = true, Location = new Point(14, 9) });
            sideFooter.Controls.Add(new Label { Text = "Copia de seguridad activa", Font = new Font("Segoe UI", 8f), ForeColor = UiTheme.TextMuted, BackColor = Color.FromArgb(14, 22, 38), AutoSize = true, Location = new Point(14, 28) });
            sideLayout.Controls.Add(sideFooter, 0, 2);

            // PANEL PRINCIPAL DERECHO
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, BackColor = UiTheme.BgCanvas, ColumnCount = 1, RowCount = 2, Margin = Padding.Empty };
            main.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            main.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(main, 1, 0);

            topHeader = new TopHeaderBar();
            topHeader.MinimizeClicked += delegate { WindowState = FormWindowState.Minimized; };
            topHeader.MaximizeClicked += delegate { WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized; };
            topHeader.CloseClicked += delegate { Close(); };
            main.Controls.Add(topHeader, 0, 0);

            var content = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.BgCanvas, Padding = new Padding(26, 16, 26, 20) };
            main.Controls.Add(content, 0, 1);

            pageHost = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.BgCanvas };
            content.Controls.Add(pageHost);

            AddPage("Limpiador de Basura", "Limpieza profunda, cachés y telemetría en vivo", BuildDashboard(), navPanel, 0);
            AddPage("Acelerador de Inicio", "Gestión de arranque, impacto y retraso automático", BuildStartupPage(), navPanel, 1);
            AddPage("Liberador de RAM", "Purga de Standby List, Working Set y procesos", BuildMemoryPage(), navPanel, 2);
            AddPage("Suite NVIDIA", "Limpieza DDU de drivers e instalación limpia a medida", BuildNvidiaPage(), navPanel, 4);
            AddPage("Seguridad", "Controladores WHQL, red TCP/IP, SMART y reparación", BuildSecurityPage(), navPanel, 3);
            AddPage("Desfragmentar", "Mapa de bloques NVMe/SSD TRIM y duplicados SHA-256", BuildStoragePage(), navPanel, 5);
            AddPage("Ajustes", "Modo Juego dinámico y servicios en segundo plano", BuildSettingsPage(), navPanel, 6);

            ShowPage("Limpiador de Basura");
        }

        void AddPage(string key, string subtitle, Control control, FlowLayoutPanel navPanel, int icon)
        {
            control.Dock = DockStyle.Fill;
            pages[key] = control;
            pageSubtitles[key] = subtitle;
            pageHost.Controls.Add(control);
            control.Visible = false;

            var button = new NavigationButton(key, subtitle, icon)
            {
                Width = 295,
                Height = 52,
                Margin = new Padding(0, 1, 0, 1),
                Tag = key
            };
            button.Click += delegate { ShowPage((string)button.Tag); };
            navigation[key] = button;
            navPanel.Controls.Add(button);
        }

        void ShowPage(string title)
        {
            foreach (var pair in pages) pair.Value.Visible = pair.Key == title;
            foreach (var pair in navigation) pair.Value.Active = pair.Key == title;
            if (topHeader != null)
            {
                topHeader.Title = title;
                topHeader.Subtitle = pageSubtitles.ContainsKey(title) ? pageSubtitles[title] : "";
            }
        }

        Panel NewModule() { return new Panel { BackColor = UiTheme.BgCanvas, Dock = DockStyle.Fill }; }

        // ==========================================
        // VISTA 1: LIMPIADOR DE BASURA (DASHBOARD)
        // ==========================================
        Control BuildDashboard()
        {
            var page = NewModule();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 165f));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 155f));

            var heroCard = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, TopAccentColor = UiTheme.Cyan, Radius = 14, Margin = new Padding(0, 0, 0, 14), Padding = new Padding(22, 18, 22, 18) };
            var heroGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.CardBg, Margin = Padding.Empty };
            heroGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 260f));
            heroGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            heroCard.Controls.Add(heroGrid);

            healthGauge = new DonutGauge { Dock = DockStyle.Fill, Value = 88, Margin = new Padding(0, 0, 14, 0) };
            heroGrid.Controls.Add(healthGauge, 0, 0);

            var rightHero = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = UiTheme.CardBg, Margin = Padding.Empty, Padding = new Padding(8, 2, 4, 2) };
            rightHero.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            rightHero.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
            rightHero.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rightHero.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

            var lblHeroTitle = new Label { Text = "Optimización integral de almacenamiento y caché", Font = new Font("Segoe UI", 14.5f, FontStyle.Bold), ForeColor = UiTheme.TextLight, BackColor = UiTheme.CardBg, Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleLeft };
            cleanStatus = new Label { Text = "Marca los objetivos de limpieza para liberar espacio y purgar memoria en espera.", Font = new Font("Segoe UI", 9.2f), ForeColor = UiTheme.TextMuted, BackColor = UiTheme.CardBg, Dock = DockStyle.Fill, TextAlign = ContentAlignment.TopLeft };
            rightHero.Controls.Add(lblHeroTitle, 0, 0);
            rightHero.Controls.Add(cleanStatus, 0, 1);

            var tilesGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = UiTheme.CardBg, Margin = new Padding(0, 4, 0, 6) };
            tilesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tilesGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            tilesGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            tilesGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            cleanShaders = new OptionTile("Caché de sombreadores", "DirectX, NVIDIA DX/GL y AMD", "CPU", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 8, 6) };
            cleanBrowserCaches = new OptionTile("Caché de navegadores", "Chrome, Edge y Firefox", "DISK", true) { Dock = DockStyle.Fill, Margin = new Padding(8, 0, 0, 6) };
            cleanMemoryDumps = new OptionTile("Volcados y temporales", "Prefetch, Temp y CrashDumps", "BOLT", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 6, 8, 0) };
            cleanRegistryOrphans = new OptionTile("Registro huérfano", "Rutas obsoletas en MuiCache", "SHIELD", true) { Dock = DockStyle.Fill, Margin = new Padding(8, 6, 0, 0) };

            tilesGrid.Controls.Add(cleanShaders, 0, 0);
            tilesGrid.Controls.Add(cleanBrowserCaches, 1, 0);
            tilesGrid.Controls.Add(cleanMemoryDumps, 0, 1);
            tilesGrid.Controls.Add(cleanRegistryOrphans, 1, 1);
            rightHero.Controls.Add(tilesGrid, 0, 2);

            var btnBar = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = UiTheme.CardBg, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
            var cta = new PillButton("Ejecutar optimización ahora") { Size = new Size(265, 42), Margin = new Padding(0, 0, 12, 0) };
            cta.Click += async delegate { await CleanTemporaryFilesAsync(cta); };

            var quickRam = new ModernButton("Purgar RAM") { Size = new Size(145, 42), Margin = new Padding(0, 0, 12, 0), BackColor = UiTheme.CardBg };
            quickRam.Click += delegate { PurgeMemory(true); };

            var quickDns = new ModernButton("Vaciar DNS") { Size = new Size(145, 42), Margin = Padding.Empty, BackColor = UiTheme.CardBg };
            quickDns.Click += delegate { RunCommand("ipconfig.exe", "/flushdns"); cleanStatus.Text = "Caché de resolución DNS vaciada correctamente."; };

            btnBar.Controls.AddRange(new Control[] { cta, quickRam, quickDns });
            rightHero.Controls.Add(btnBar, 0, 3);

            heroGrid.Controls.Add(rightHero, 1, 0);
            layout.Controls.Add(heroCard, 0, 0);

            // FILA DE 3 GRÁFICAS DE TELEMETRÍA EN TIEMPO REAL
            var cards = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = UiTheme.BgCanvas, Margin = new Padding(0, 0, 0, 14) };
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            cards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            cpuCard = new TelemetryCard("Procesador (CPU)", "CPU") { Margin = new Padding(0, 0, 8, 0) };
            ramCard = new TelemetryCard("Memoria física (RAM)", "RAM") { Margin = new Padding(4, 0, 4, 0) };
            storageCard = new TelemetryCard("Unidad del sistema", "DISK") { Margin = new Padding(8, 0, 0, 0) };

            cards.Controls.Add(cpuCard, 0, 0);
            cards.Controls.Add(ramCard, 1, 0);
            cards.Controls.Add(storageCard, 2, 0);
            layout.Controls.Add(cards, 0, 1);

            // PANEL INFERIOR DE ACCESOS DIRECTOS A MÓDULOS
            var bottomRow = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 3, RowCount = 1, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));
            bottomRow.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33f));

            bottomRow.Controls.Add(MiniShortcutCard("Limpieza e instalación NVIDIA", "Elimina restos de drivers anteriores o crea un instalador limpio sin telemetría.", "Ir a Suite NVIDIA", "GPU", delegate { ShowPage("Suite NVIDIA"); }, new Padding(0, 0, 8, 0)), 0, 0);
            bottomRow.Controls.Add(MiniShortcutCard("Baja latencia de red", "Elimina el estrangulamiento NetworkThrottlingIndex para priorizar paquetes.", "Ir a Seguridad", "SHIELD", delegate { ShowPage("Seguridad"); }, new Padding(4, 0, 4, 0)), 1, 0);
            bottomRow.Controls.Add(MiniShortcutCard("Modo Juego automático", "Activa el plan de Alto Rendimiento al detectar tu juego en ejecución.", "Ir a Ajustes", "WRENCH", delegate { ShowPage("Ajustes"); }, new Padding(8, 0, 0, 0)), 2, 0);

            layout.Controls.Add(bottomRow, 0, 2);
            page.Controls.Add(layout);

            monitorTimer = new Timer { Interval = 1200 };
            monitorTimer.Tick += delegate { UpdateMetrics(); };
            monitorTimer.Start();
            UpdateMetrics();
            return page;
        }

        Control MiniShortcutCard(string title, string desc, string btnText, string iconKind, Action onClick, Padding margin)
        {
            var card = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, Radius = 12, Margin = margin, Padding = new Padding(18, 14, 18, 14) };

            var iconBox = new Panel { Size = new Size(36, 36), Location = new Point(18, 16), BackColor = UiTheme.CardBg };
            iconBox.Paint += (s, e) =>
            {
                e.Graphics.Clear(UiTheme.CardBg);
                var r = new Rectangle(0, 0, 35, 35);
                using (var p = UiShape.Round(r, 8))
                using (var b = new SolidBrush(Color.FromArgb(26, UiTheme.Cyan)))
                    e.Graphics.FillPath(b, p);
                UiShape.DrawVectorIcon(e.Graphics, iconKind, r, UiTheme.Cyan);
            };
            card.Controls.Add(iconBox);

            var lblTitle = new Label
            {
                Text = title,
                Font = new Font("Segoe UI Semibold", 10.5f, FontStyle.Bold),
                ForeColor = UiTheme.TextLight,
                BackColor = UiTheme.CardBg,
                Location = new Point(64, 14),
                Size = new Size(card.Width - 80, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var lblDesc = new Label
            {
                Text = desc,
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.CardBg,
                Location = new Point(64, 38),
                Size = new Size(card.Width - 80, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var btn = new ModernButton(btnText)
            {
                Size = new Size(155, 34),
                Location = new Point(64, card.Height - 48),
                BackColor = UiTheme.CardBg,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            btn.Click += delegate { onClick(); };

            card.Controls.Add(lblTitle);
            card.Controls.Add(lblDesc);
            card.Controls.Add(btn);
            return card;
        }

        void UpdateMetrics()
        {
            if (IsDisposed || cpuCard == null) return;
            float cpu = 0;
            try { if (cpuCounter != null) cpu = Math.Max(0, Math.Min(100, cpuCounter.NextValue())); } catch { }

            var memory = new MemoryStatus();
            memory.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            GlobalMemoryStatusEx(ref memory);
            double ram = memory.TotalPhys == 0 ? 0 : (double)(memory.TotalPhys - memory.AvailPhys) * 100.0 / memory.TotalPhys;
            double usedGb = (memory.TotalPhys - memory.AvailPhys) / 1073741824.0;
            double totalGb = memory.TotalPhys / 1073741824.0;

            long total = 0, free = 0;
            try
            {
                var drive = new DriveInfo(Path.GetPathRoot(Environment.SystemDirectory));
                if (drive.IsReady) { total = drive.TotalSize; free = drive.AvailableFreeSpace; }
            }
            catch { }
            double usedDisk = total == 0 ? 0 : (double)(total - free) * 100.0 / total;
            double freeDiskGb = free / 1073741824.0;

            cpuCard.PushSample((int)cpu, cpu.ToString("0") + "%", "Carga de núcleos en vivo");
            ramCard.PushSample((int)ram, ram.ToString("0") + "%", String.Format("{0:0.0} GB / {1:0.0} GB activos", usedGb, totalGb));
            storageCard.PushSample((int)usedDisk, usedDisk.ToString("0") + "%", String.Format("{0:0.0} GB disponibles", freeDiskGb));

            if (ramLiveChart != null)
                ramLiveChart.PushSample((int)ram, ram.ToString("0") + "%", String.Format("{0:0.0} GB libres actualmente", (totalGb - usedGb)));

            int score = (int)Math.Max(55, Math.Min(100, Math.Round(100 - (cpu * 0.2 + ram * 0.15))));
            if (healthGauge != null) healthGauge.Value = score;
            if (ramGauge != null) ramGauge.Value = (int)ram;
            if (memorySummary != null) memorySummary.Text = String.Format("{0:0.0} GB en uso de {1:0.0} GB totales ({2:0}%)", usedGb, totalGb, ram);

            if (autoRamPurgeToggle != null && autoRamPurgeToggle.Checked && ram >= 80)
                PurgeMemory(false);
        }

        // ==========================================
        // VISTA NUEVA: SUITE NVIDIA (DDU + CLEAN INSTALL)
        // ==========================================
        Control BuildNvidiaPage()
        {
            var page = NewModule();
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));

            // ----------------------------------------------------
            // COLUMNA IZQUIERDA: LIMPIAR DRIVERS NVIDIA (ESTILO DDU)
            // ----------------------------------------------------
            var leftCard = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, TopAccentColor = UiTheme.Amber, Radius = 12, Padding = new Padding(20, 16, 20, 16), Margin = new Padding(0, 0, 8, 0) };
            var leftLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 6, BackColor = UiTheme.CardBg, Margin = Padding.Empty };
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));  // Cabecera y GPU detectada
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 82f));  // Banner Modo Seguro
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 62f));  // Bloqueo Windows Update
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Objetivos de limpieza
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));  // Estado
            leftLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));  // Botones limpiar con/sin reinicio

            // 1. Cabecera
            var headerPanel = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBg, Margin = Padding.Empty };
            headerPanel.Controls.Add(new Label { Text = "Limpiador profundo de drivers NVIDIA", Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), ForeColor = UiTheme.TextLight, BackColor = UiTheme.CardBg, AutoSize = true, Location = new Point(0, 2) });
            nvGpuDetectedLabel = new Label { Text = DetectInstalledGpuSummary(), Font = new Font("Segoe UI", 8.8f), ForeColor = UiTheme.Cyan, BackColor = UiTheme.CardBg, AutoSize = true, Location = new Point(2, 30) };
            headerPanel.Controls.Add(nvGpuDetectedLabel);
            leftLayout.Controls.Add(headerPanel, 0, 0);

            // 2. Banner de Modo Seguro
            bool inSafeMode = SystemInformation.BootMode != BootMode.Normal;
            var safeCard = new RoundedPanel
            {
                Dock = DockStyle.Fill,
                FillColor = inSafeMode ? Color.FromArgb(16, 38, 34) : Color.FromArgb(36, 28, 18),
                BorderColor = inSafeMode ? UiTheme.Mint : UiTheme.Amber,
                Radius = 10,
                Margin = new Padding(0, 0, 0, 10),
                Padding = new Padding(12)
            };
            nvSafeModeStatusLabel = new Label
            {
                Text = inSafeMode
                    ? "MODO SEGURO ACTIVO: Condiciones óptimas para eliminar el driver sin bloqueos."
                    : "RECOMENDACIÓN: Ejecuta la limpieza en Modo Seguro para evitar componentes activos.",
                Font = new Font("Segoe UI Semibold", 8.5f, FontStyle.Bold),
                ForeColor = inSafeMode ? UiTheme.Mint : UiTheme.Amber,
                BackColor = safeCard.FillColor,
                Location = new Point(12, 10),
                Size = new Size(safeCard.Width - 195, 52),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var safeBootBtn = new ModernButton(inSafeMode ? "Salir de Modo Seguro" : "Reiniciar en Modo Seguro")
            {
                Size = new Size(168, 34),
                Location = new Point(safeCard.Width - 180, 18),
                BackColor = safeCard.FillColor,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            safeBootBtn.Click += delegate { ToggleSafeModeBoot(inSafeMode); };
            safeCard.Controls.Add(nvSafeModeStatusLabel);
            safeCard.Controls.Add(safeBootBtn);
            leftLayout.Controls.Add(safeCard, 0, 1);

            // 3. Bloquear descargas automáticas de drivers en Windows Update
            var wuPanel = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardAlt, Radius = 10, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(12) };
            wuPanel.Controls.Add(new Label
            {
                Text = "Bloquear controladores automáticos de Windows Update",
                Font = new Font("Segoe UI Semibold", 9.2f, FontStyle.Bold),
                ForeColor = UiTheme.TextLight,
                BackColor = UiTheme.CardAlt,
                Location = new Point(12, 8),
                Size = new Size(wuPanel.Width - 80, 20),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            wuPanel.Controls.Add(new Label
            {
                Text = "Impide que Windows instale otro driver de pantalla al reiniciar.",
                Font = new Font("Segoe UI", 8f),
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.CardAlt,
                Location = new Point(12, 28),
                Size = new Size(wuPanel.Width - 80, 18),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            nvBlockWuToggle = new ToggleSwitch
            {
                Checked = IsWuDriverBlocked(),
                Location = new Point(wuPanel.Width - 58, 14),
                BackColor = UiTheme.CardAlt,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            nvBlockWuToggle.CheckedChanged += delegate { SetWuDriverBlock(nvBlockWuToggle.Checked); };
            wuPanel.Controls.Add(nvBlockWuToggle);
            leftLayout.Controls.Add(wuPanel, 0, 2);

            // 4. Objetivos de limpieza NVIDIA
            var cleanOptsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4, BackColor = UiTheme.CardBg, Margin = new Padding(0, 2, 0, 4) };
            cleanOptsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
            cleanOptsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
            cleanOptsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));
            cleanOptsGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 25f));

            nvCleanDisplay = new OptionTile("Controlador base, PhysX y Audio HD", "Desinstala paquetes OEM de DriverStore (pnputil) y servicios", "GPU", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            nvCleanDirs = new OptionTile("Carpetas, instaladores y ajustes residuales", "Limpia C:\\NVIDIA, Program Files, ProgramData y AppData", "DISK", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            nvCleanShaderCache = new OptionTile("Cachés de sombreadores y perfiles 3D", "Purga DXCache, GLCache, ComputeCache y claves DRS", "BOLT", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 6) };
            nvCleanTelemetry = new OptionTile("Servicios huérfanos, telemetría y registro", "Elimina NvContainer, tareas programadas y entradas obsoletas", "SHIELD", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 0, 2) };

            cleanOptsGrid.Controls.Add(nvCleanDisplay, 0, 0);
            cleanOptsGrid.Controls.Add(nvCleanDirs, 0, 1);
            cleanOptsGrid.Controls.Add(nvCleanShaderCache, 0, 2);
            cleanOptsGrid.Controls.Add(nvCleanTelemetry, 0, 3);
            leftLayout.Controls.Add(cleanOptsGrid, 0, 3);

            // 5. Estado de operación
            nvActionStatusLabel = new Label
            {
                Text = "Listo para limpiar instalaciones previas o conflictos al cambiar de GPU.",
                Font = new Font("Segoe UI", 8.5f),
                ForeColor = UiTheme.Mint,
                BackColor = UiTheme.CardBg,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft
            };
            leftLayout.Controls.Add(nvActionStatusLabel, 0, 4);

            // 6. Botones de acción (Limpiar y reiniciar / Limpiar sin reiniciar)
            var cleanBtnsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.CardBg, Margin = Padding.Empty };
            cleanBtnsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            cleanBtnsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));

            var btnCleanReboot = new PillButton("Limpiar y reiniciar (Recomendado)") { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 6, 0) };
            var btnCleanNoReboot = new ModernButton("Limpiar sin reiniciar", false, true) { Dock = DockStyle.Fill, Margin = new Padding(6, 4, 0, 0), BackColor = UiTheme.CardBg };

            btnCleanReboot.Click += async delegate { await ExecuteNvidiaCleanupAsync(true, btnCleanReboot, btnCleanNoReboot); };
            btnCleanNoReboot.Click += async delegate { await ExecuteNvidiaCleanupAsync(false, btnCleanReboot, btnCleanNoReboot); };

            cleanBtnsGrid.Controls.Add(btnCleanReboot, 0, 0);
            cleanBtnsGrid.Controls.Add(btnCleanNoReboot, 1, 0);
            leftLayout.Controls.Add(cleanBtnsGrid, 0, 5);

            leftCard.Controls.Add(leftLayout);
            split.Controls.Add(leftCard, 0, 0);

            // ----------------------------------------------------
            // COLUMNA DERECHA: INSTALACIÓN LIMPIA NVIDIA (ESTILO NVCLEANSTALL)
            // ----------------------------------------------------
            var rightCard = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, TopAccentColor = UiTheme.Mint, Radius = 12, Padding = new Padding(20, 16, 20, 16), Margin = new Padding(8, 0, 0, 0) };
            var rightLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 5, BackColor = UiTheme.CardBg, Margin = Padding.Empty };
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 58f));  // Cabecera
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 84f));  // Selector de paquete o descarga
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));  // Matriz de componentes
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 118f)); // Opciones avanzadas
            rightLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 48f));  // Botones instalar / crear paquete

            // 1. Cabecera
            var rHeader = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardBg, Margin = Padding.Empty };
            rHeader.Controls.Add(new Label { Text = "Instalación limpia y personalizada NVIDIA", Font = new Font("Segoe UI Semibold", 13f, FontStyle.Bold), ForeColor = UiTheme.TextLight, BackColor = UiTheme.CardBg, AutoSize = true, Location = new Point(0, 2) });
            rHeader.Controls.Add(new Label { Text = "Descarta módulos innecesarios, elimina telemetría y genera un instalador reutilizable.", Font = new Font("Segoe UI", 8.8f), ForeColor = UiTheme.TextMuted, BackColor = UiTheme.CardBg, AutoSize = true, Location = new Point(2, 30) });
            rightLayout.Controls.Add(rHeader, 0, 0);

            // 2. Origen del controlador (Paquete guardado o descarga)
            var sourceBox = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardAlt, Radius = 10, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(12) };
            sourceBox.Controls.Add(new Label { Text = "Origen del controlador NVIDIA (.exe o carpeta extraída):", Font = new Font("Segoe UI Semibold", 8.8f, FontStyle.Bold), ForeColor = UiTheme.TextLight, BackColor = UiTheme.CardAlt, AutoSize = true, Location = new Point(12, 8) });

            nvPackagePathInput = new TextBox
            {
                Text = DetectExistingNvidiaPackage(),
                Location = new Point(12, 34),
                Size = new Size(sourceBox.Width - 260, 26),
                BackColor = UiTheme.BgSidebar,
                ForeColor = UiTheme.Cyan,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9f),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var browsePkgBtn = new ModernButton("Elegir archivo")
            {
                Size = new Size(112, 30),
                Location = new Point(sourceBox.Width - 240, 31),
                BackColor = UiTheme.CardAlt,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            browsePkgBtn.Click += delegate { SelectNvidiaPackageSource(); };

            var downloadPkgBtn = new ModernButton("Descargar", true)
            {
                Size = new Size(112, 30),
                Location = new Point(sourceBox.Width - 122, 31),
                BackColor = UiTheme.CardAlt,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            downloadPkgBtn.Click += delegate { DownloadNvidiaDriverPrompt(); };

            sourceBox.Controls.Add(nvPackagePathInput);
            sourceBox.Controls.Add(browsePkgBtn);
            sourceBox.Controls.Add(downloadPkgBtn);
            rightLayout.Controls.Add(sourceBox, 0, 1);

            // 3. Selección de componentes del paquete (2x3)
            var compGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = UiTheme.CardBg, Margin = new Padding(0, 0, 0, 8) };
            compGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            compGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            compGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            compGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            compGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            nvCompCore = new OptionTile("Controlador gráfico (Núcleo)", "Display.Driver esencial (Obligatorio)", "GPU", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 6) };
            nvCompCore.CheckedChanged += delegate { if (!nvCompCore.Checked) nvCompCore.Checked = true; };

            nvCompAudio = new OptionTile("Audio HD (HDMI / DisplayPort)", "Módulo HDAudio para monitores/TV", "BOLT", true) { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 6) };
            nvCompPhysX = new OptionTile("NVIDIA PhysX System", "Físicas aceleradas para juegos clásicos", "CPU", true) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 6) };
            nvCompApp = new OptionTile("NVIDIA App / GeForce Experience", "Overlay, grabación y filtros (Opcional)", "DASH", false) { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 6) };
            nvCompUsbC = new OptionTile("Controlador USB-C y 3D Vision", "Módulos PPC / soporte legado (Opcional)", "DISK", false) { Dock = DockStyle.Fill, Margin = new Padding(0, 0, 6, 0) };
            nvCompStripTelemetry = new OptionTile("Purgar Telemetría y Bloatware", "Quita NvTelemetry, NvBackend y ShadowPlay", "SHIELD", true) { Dock = DockStyle.Fill, Margin = new Padding(6, 0, 0, 0) };

            compGrid.Controls.Add(nvCompCore, 0, 0);
            compGrid.Controls.Add(nvCompAudio, 1, 0);
            compGrid.Controls.Add(nvCompPhysX, 0, 1);
            compGrid.Controls.Add(nvCompApp, 1, 1);
            compGrid.Controls.Add(nvCompUsbC, 0, 2);
            compGrid.Controls.Add(nvCompStripTelemetry, 1, 2);
            rightLayout.Controls.Add(compGrid, 0, 2);

            // 4. Opciones avanzadas de instalación
            var advBox = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardAlt, Radius = 10, Margin = new Padding(0, 0, 0, 10), Padding = new Padding(12, 8, 12, 8) };
            var advGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = UiTheme.CardAlt, Margin = Padding.Empty };
            advGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            advGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            advGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            advGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            nvOptCleanInstall = new ToggleSwitch { Checked = true };
            nvOptSilent = new ToggleSwitch { Checked = true };
            nvOptDisableTelemPost = new ToggleSwitch { Checked = true };
            nvOptForceReBar = new ToggleSwitch { Checked = false };

            advGrid.Controls.Add(BuildMiniToggleCell("Forzar instalación limpia (-clean)", nvOptCleanInstall), 0, 0);
            advGrid.Controls.Add(BuildMiniToggleCell("Instalación desatendida (-passive)", nvOptSilent), 1, 0);
            advGrid.Controls.Add(BuildMiniToggleCell("Desactivar tareas de telemetría", nvOptDisableTelemPost), 0, 1);
            advGrid.Controls.Add(BuildMiniToggleCell("Activar perfil Resizable BAR", nvOptForceReBar), 1, 1);
            advBox.Controls.Add(advGrid);
            rightLayout.Controls.Add(advBox, 0, 3);

            // 5. Botones: Instalar ahora / Crear paquete reutilizable
            var instBtnsGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.CardBg, Margin = Padding.Empty };
            instBtnsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 52f));
            instBtnsGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 48f));

            var btnRunCleanInstall = new PillButton("Ejecutar instalación limpia") { Dock = DockStyle.Fill, Margin = new Padding(0, 4, 6, 0) };
            var btnBuildReusable = new ModernButton("Crear instalador reutilizable") { Dock = DockStyle.Fill, Margin = new Padding(6, 4, 0, 0), BackColor = UiTheme.CardBg };

            btnRunCleanInstall.Click += async delegate { await BuildOrRunCustomNvidiaPackageAsync(false, btnRunCleanInstall, btnBuildReusable); };
            btnBuildReusable.Click += async delegate { await BuildOrRunCustomNvidiaPackageAsync(true, btnRunCleanInstall, btnBuildReusable); };

            instBtnsGrid.Controls.Add(btnRunCleanInstall, 0, 0);
            instBtnsGrid.Controls.Add(btnBuildReusable, 1, 0);
            rightLayout.Controls.Add(instBtnsGrid, 0, 4);

            rightCard.Controls.Add(rightLayout);
            split.Controls.Add(rightCard, 1, 0);

            page.Controls.Add(split);
            return page;
        }

        Control BuildMiniToggleCell(string labelText, ToggleSwitch toggle)
        {
            var cell = new Panel { Dock = DockStyle.Fill, BackColor = UiTheme.CardAlt, Margin = new Padding(2) };
            var lbl = new Label
            {
                Text = labelText,
                Font = new Font("Segoe UI Semibold", 8.5f),
                ForeColor = UiTheme.TextLight,
                BackColor = UiTheme.CardAlt,
                Location = new Point(4, 12),
                Size = new Size(cell.Width - 58, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            toggle.BackColor = UiTheme.CardAlt;
            toggle.Location = new Point(cell.Width - 50, 10);
            toggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            cell.Controls.Add(lbl);
            cell.Controls.Add(toggle);
            return cell;
        }

        string DetectInstalledGpuSummary()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, DriverVersion FROM Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = Convert.ToString(obj["Name"]);
                        string ver = Convert.ToString(obj["DriverVersion"]);
                        if (!String.IsNullOrEmpty(name) && name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                            return "GPU detectada: " + name + "  ·  Driver actual: " + ver;
                    }
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        string name = Convert.ToString(obj["Name"]);
                        if (!String.IsNullOrEmpty(name))
                            return "Adaptador actual: " + name + " (Ideal limpiar antes de cambiar de GPU)";
                    }
                }
            }
            catch { }
            return "Adaptador gráfico estándar de Windows (Sin controlador NVIDIA activo)";
        }

        string DetectExistingNvidiaPackage()
        {
            string[] commonDirs = {
                @"C:\NVIDIA\DisplayDriver",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads")
            };
            foreach (string dir in commonDirs)
            {
                try
                {
                    if (!Directory.Exists(dir)) continue;
                    foreach (string f in Directory.GetFiles(dir, "*nvidia*.exe", SearchOption.TopDirectoryOnly))
                        return f;
                    foreach (string d in Directory.GetDirectories(dir))
                        if (File.Exists(Path.Combine(d, "setup.exe")) && Directory.Exists(Path.Combine(d, "Display.Driver")))
                            return d;
                }
                catch { }
            }
            return @"C:\NVIDIA\DisplayDriver";
        }

        bool IsWuDriverBlocked()
        {
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(WuPolicyPath, false))
                {
                    if (key != null && Convert.ToInt32(key.GetValue("ExcludeWUDriversInQualityUpdate", 0)) == 1)
                        return true;
                }
            }
            catch { }
            return false;
        }

        void SetWuDriverBlock(bool block)
        {
            try
            {
                using (var key = Registry.LocalMachine.CreateSubKey(WuPolicyPath))
                {
                    if (block) key.SetValue("ExcludeWUDriversInQualityUpdate", 1, RegistryValueKind.DWord);
                    else key.DeleteValue("ExcludeWUDriversInQualityUpdate", false);
                }
                using (var key = Registry.LocalMachine.CreateSubKey(DriverSearchPath))
                {
                    key.SetValue("SearchOrderConfig", block ? 0 : 1, RegistryValueKind.DWord);
                }
                if (nvActionStatusLabel != null)
                {
                    nvActionStatusLabel.Text = block
                        ? "Windows Update bloqueado: no instalará drivers gráficos automáticamente."
                        : "Windows Update restaurado: descarga automática de drivers permitida.";
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error al configurar la política de Windows Update: " + ex.Message, "OptiGuille");
            }
        }

        void ToggleSafeModeBoot(bool currentlyInSafeMode)
        {
            if (currentlyInSafeMode)
            {
                if (MessageBox.Show("¿Restaurar el arranque normal de Windows y reiniciar el equipo ahora?", "Salir de Modo Seguro", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    RunCommand("bcdedit.exe", "/deletevalue {current} safeboot");
                    RunCommand("shutdown.exe", "/r /t 3");
                }
            }
            else
            {
                var res = MessageBox.Show(
                    "En Modo Seguro Windows carga únicamente los controladores esenciales, permitiendo que OptiGuille elimine el 100% de los archivos y servicios de NVIDIA sin bloqueos.\r\n\r\n¿Configurar el próximo arranque en Modo Seguro y reiniciar ahora?",
                    "Reiniciar en Modo Seguro",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Information);

                if (res == DialogResult.Yes)
                {
                    SetWuDriverBlock(true);
                    RunCommand("bcdedit.exe", "/set {current} safeboot minimal");
                    RunCommand("shutdown.exe", "/r /t 4 /c \"OptiGuille: Reiniciando en Modo Seguro para limpieza de driver NVIDIA.\"");
                }
            }
        }

        async Task ExecuteNvidiaCleanupAsync(bool rebootAfter, Control btn1, Control btn2)
        {
            string modeNote = SystemInformation.BootMode == BootMode.Normal
                ? "Nota: Estás en arranque normal. Para conflictos graves se recomienda usar Modo Seguro.\r\n\r\n"
                : "Modo Seguro detectado.\r\n\r\n";

            string confirmMsg = modeNote +
                "Se eliminarán los controladores gráficos NVIDIA, paquetes OEM en DriverStore, cachés de sombreadores, carpetas residuales y entradas huérfanas.\r\n" +
                (rebootAfter ? "El equipo se reiniciará automáticamente al terminar." : "La pantalla puede parpadear unos segundos durante el proceso.") +
                "\r\n\r\n¿Continuar con la limpieza profunda?";

            if (MessageBox.Show(confirmMsg, "Limpiar drivers NVIDIA", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            btn1.Enabled = false;
            btn2.Enabled = false;
            nvActionStatusLabel.ForeColor = UiTheme.Cyan;
            nvActionStatusLabel.Text = "Deteniendo servicios y procesos de NVIDIA...";

            bool doDisplay = nvCleanDisplay.Checked;
            bool doDirs = nvCleanDirs.Checked;
            bool doShaders = nvCleanShaderCache.Checked;
            bool doTelem = nvCleanTelemetry.Checked;
            bool blockWu = nvBlockWuToggle.Checked;

            var report = new StringBuilder();
            report.AppendLine("=== INFORME DE LIMPIEZA PROFUNDA NVIDIA ===");

            await Task.Run(delegate
            {
                if (blockWu)
                {
                    try
                    {
                        using (var k = Registry.LocalMachine.CreateSubKey(WuPolicyPath))
                            k.SetValue("ExcludeWUDriversInQualityUpdate", 1, RegistryValueKind.DWord);
                        using (var k = Registry.LocalMachine.CreateSubKey(DriverSearchPath))
                            k.SetValue("SearchOrderConfig", 0, RegistryValueKind.DWord);
                        report.AppendLine("[OK] Bloqueo temporal de drivers en Windows Update activado.");
                    }
                    catch { }
                }

                // 1. Detener servicios y procesos activos de NVIDIA
                string[] nvServices = { "NVDisplay.ContainerLocalSystem", "NvContainerLocalSystem", "NvContainerNetworkService", "FvSvc", "NvTelemetryContainer" };
                foreach (string s in nvServices)
                {
                    RunCommand("sc.exe", "stop \"" + s + "\"");
                    if (doTelem) RunCommand("sc.exe", "delete \"" + s + "\"");
                }

                string[] nvProcs = { "nvcontainer", "NVDisplay.Container", "NVIDIA Web Helper", "NVIDIA Share", "nvsphelper64", "NVSmartMaxApp", "NvTelemetry", "GeForceExperience" };
                foreach (string procName in nvProcs)
                {
                    try
                    {
                        foreach (var p in Process.GetProcessesByName(procName))
                        {
                            try { p.Kill(); } catch { }
                        }
                    }
                    catch { }
                }
                report.AppendLine("[OK] Procesos y contenedores NVIDIA detenidos.");

                // 2. Desinstalar paquetes OEM del DriverStore mediante pnputil
                if (doDisplay)
                {
                    int removedOems = RemoveNvidiaDriverStorePackages(report);
                    report.AppendLine("[OK] Paquetes OEM de NVIDIA eliminados de DriverStore: " + removedOems);
                }

                // 3. Limpiar cachés de sombreadores y perfiles
                string localApp = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string progData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
                if (doShaders)
                {
                    string[] shaderPaths = {
                        Path.Combine(localApp, "NVIDIA", "DXCache"),
                        Path.Combine(localApp, "NVIDIA", "GLCache"),
                        Path.Combine(localApp, "D3DSCache"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NVIDIA", "ComputeCache"),
                        Path.Combine(progData, "NVIDIA Corporation", "NV_Cache"),
                        Path.Combine(progData, "NVIDIA Corporation", "Drs")
                    };
                    long bytes = DeleteDirectoryList(shaderPaths);
                    report.AppendLine(String.Format("[OK] Cachés de sombreadores y perfiles DRS eliminados ({0:0.0} MB).", bytes / 1048576.0));
                }

                // 4. Eliminar carpetas e instaladores residuales
                if (doDirs)
                {
                    string[] residualDirs = {
                        @"C:\NVIDIA",
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "NVIDIA Corporation"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "NVIDIA Corporation"),
                        Path.Combine(progData, "NVIDIA"),
                        Path.Combine(progData, "NVIDIA Corporation"),
                        Path.Combine(localApp, "NVIDIA Corporation")
                    };
                    long bytes = DeleteDirectoryList(residualDirs);
                    report.AppendLine(String.Format("[OK] Carpetas residuales de instalación eliminadas ({0:0.0} MB).", bytes / 1048576.0));
                }

                // 5. Eliminar tareas programadas y claves de registro huérfanas
                if (doTelem)
                {
                    string tasksOut = RunCommand("schtasks.exe", "/query /fo CSV /nh");
                    foreach (string line in tasksOut.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        if (line.IndexOf("Nv", StringComparison.OrdinalIgnoreCase) >= 0 || line.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            var m = Regex.Match(line, "^\"([^\"]+)\"");
                            if (m.Success)
                                RunCommand("schtasks.exe", "/Delete /TN \"" + m.Groups[1].Value + "\" /F");
                        }
                    }

                    string[] regHkcu = { @"Software\NVIDIA Corporation" };
                    string[] regHklm = { @"SOFTWARE\NVIDIA Corporation", @"SOFTWARE\WOW6432Node\NVIDIA Corporation" };
                    foreach (string rk in regHkcu) { try { Registry.CurrentUser.DeleteSubKeyTree(rk, false); } catch { } }
                    foreach (string rk in regHklm) { try { Registry.LocalMachine.DeleteSubKeyTree(rk, false); } catch { } }
                    report.AppendLine("[OK] Tareas de telemetría y claves de registro de NVIDIA purgadas.");
                }

                // Si estaba en Modo Seguro, restaurar el arranque normal para el próximo reinicio
                if (SystemInformation.BootMode != BootMode.Normal)
                {
                    RunCommand("bcdedit.exe", "/deletevalue {current} safeboot");
                    report.AppendLine("[OK] Arranque normal de Windows restaurado para el próximo inicio.");
                }
            });

            nvGpuDetectedLabel.Text = DetectInstalledGpuSummary();
            nvActionStatusLabel.ForeColor = UiTheme.Mint;
            nvActionStatusLabel.Text = "Limpieza de controladores NVIDIA completada con éxito.";
            btn1.Enabled = true;
            btn2.Enabled = true;

            if (rebootAfter)
            {
                RunCommand("shutdown.exe", "/r /t 5 /c \"OptiGuille: Limpieza de driver NVIDIA finalizada. Reiniciando el equipo...\"");
                MessageBox.Show(report.ToString() + "\r\nEl equipo se reiniciará en 5 segundos.", "Limpieza completada", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                ShowTextDialog("Informe de limpieza de drivers NVIDIA", report.ToString());
            }
        }

        int RemoveNvidiaDriverStorePackages(StringBuilder log)
        {
            int count = 0;
            try
            {
                string output = RunCommand("pnputil.exe", "/enum-drivers");
                string currentOem = null;
                bool isNvidia = false;

                foreach (string rawLine in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    string line = rawLine.Trim();
                    var oemMatch = Regex.Match(line, @"(oem\d+\.inf)", RegexOptions.IgnoreCase);
                    if (oemMatch.Success)
                    {
                        if (currentOem != null && isNvidia)
                        {
                            RunCommand("pnputil.exe", "/delete-driver " + currentOem + " /uninstall /force");
                            log.AppendLine("  - Desinstalado paquete DriverStore: " + currentOem);
                            count++;
                        }
                        currentOem = oemMatch.Groups[1].Value;
                        isNvidia = false;
                    }
                    else if (line.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        isNvidia = true;
                    }
                }

                if (currentOem != null && isNvidia)
                {
                    RunCommand("pnputil.exe", "/delete-driver " + currentOem + " /uninstall /force");
                    log.AppendLine("  - Desinstalado paquete DriverStore: " + currentOem);
                    count++;
                }
            }
            catch { }
            return count;
        }

        long DeleteDirectoryList(IEnumerable<string> dirs)
        {
            long freed = 0;
            foreach (string dir in dirs)
            {
                if (String.IsNullOrEmpty(dir) || !Directory.Exists(dir)) continue;
                try
                {
                    foreach (string f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                    {
                        try { long len = new FileInfo(f).Length; File.Delete(f); freed += len; } catch { }
                    }
                    Directory.Delete(dir, true);
                }
                catch { }
            }
            return freed;
        }

        void SelectNvidiaPackageSource()
        {
            var choice = MessageBox.Show(
                "Sí: Seleccionar un instalador descargado (.exe).\r\nNo: Seleccionar una carpeta donde ya esté extraído el controlador (ej. C:\\NVIDIA\\DisplayDriver).",
                "Origen del paquete NVIDIA",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Question);

            if (choice == DialogResult.Yes)
            {
                using (var ofd = new OpenFileDialog())
                {
                    ofd.Title = "Selecciona el paquete instalador de NVIDIA (.exe)";
                    ofd.Filter = "Instalador NVIDIA (*.exe)|*.exe|Todos los archivos (*.*)|*.*";
                    if (ofd.ShowDialog(this) == DialogResult.OK)
                        nvPackagePathInput.Text = ofd.FileName;
                }
            }
            else if (choice == DialogResult.No)
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Selecciona la carpeta raíz del controlador NVIDIA (la que contiene setup.exe y Display.Driver)";
                    if (fbd.ShowDialog(this) == DialogResult.OK)
                        nvPackagePathInput.Text = fbd.SelectedPath;
                }
            }
        }

        async void DownloadNvidiaDriverPrompt()
        {
            var choice = MessageBox.Show(
                "Sí: Abrir el buscador oficial de controladores NVIDIA GeForce Game Ready / Studio para elegir la versión exacta.\r\nNo: Descargar automáticamente mediante Winget en segundo plano.",
                "Descargar controlador NVIDIA",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Information);

            if (choice == DialogResult.Yes)
            {
                try { Process.Start("https://www.nvidia.es/Download/index.aspx?lang=es"); } catch { }
            }
            else if (choice == DialogResult.No)
            {
                string destDir = Path.Combine(Path.GetTempPath(), "OptiGuille_NVDownload");
                Directory.CreateDirectory(destDir);
                await RunCommandVisibleAsync("winget.exe", "download --id Nvidia.GeForceExperience -d \"" + destDir + "\" --accept-package-agreements --accept-source-agreements", "Descarga de paquete NVIDIA");
            }
        }

        async Task BuildOrRunCustomNvidiaPackageAsync(bool saveReusableCopy, Control btn1, Control btn2)
        {
            string sourcePath = nvPackagePathInput.Text.Trim();
            if (String.IsNullOrEmpty(sourcePath) || (!File.Exists(sourcePath) && !Directory.Exists(sourcePath)))
            {
                MessageBox.Show("Selecciona un archivo instalador (.exe) válido o una carpeta extraída de NVIDIA antes de continuar.", "Instalación limpia NVIDIA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string targetOutputFolder = null;
            if (saveReusableCopy)
            {
                using (var fbd = new FolderBrowserDialog())
                {
                    fbd.Description = "Elige la carpeta donde guardar el instalador personalizado reutilizable";
                    if (fbd.ShowDialog(this) != DialogResult.OK) return;
                    targetOutputFolder = Path.Combine(fbd.SelectedPath, "NVIDIA_CleanPackage_OptiGuille");
                }
            }
            else
            {
                targetOutputFolder = Path.Combine(Path.GetTempPath(), "OptiGuille_NVClean_Work");
            }

            btn1.Enabled = false;
            btn2.Enabled = false;
            nvActionStatusLabel.ForeColor = UiTheme.Cyan;
            nvActionStatusLabel.Text = "Preparando paquete personalizado y podando componentes opcionales...";

            bool keepAudio = nvCompAudio.Checked;
            bool keepPhysX = nvCompPhysX.Checked;
            bool keepApp = nvCompApp.Checked;
            bool keepUsbC = nvCompUsbC.Checked;
            bool stripTelem = nvCompStripTelemetry.Checked;

            bool optClean = nvOptCleanInstall.Checked;
            bool optSilent = nvOptSilent.Checked;
            bool optPostTelem = nvOptDisableTelemPost.Checked;
            bool optReBar = nvOptForceReBar.Checked;

            string errorMsg = null;
            long removedBytes = 0;
            int removedDirsCount = 0;

            await Task.Run(delegate
            {
                try
                {
                    if (Directory.Exists(targetOutputFolder))
                        Directory.Delete(targetOutputFolder, true);
                    Directory.CreateDirectory(targetOutputFolder);

                    // 1. Extraer o copiar el paquete base
                    if (File.Exists(sourcePath))
                    {
                        string archiver = FindSystemArchiver();
                        if (archiver != null)
                        {
                            if (archiver.EndsWith("7z.exe", StringComparison.OrdinalIgnoreCase))
                                RunCommand(archiver, String.Format("x \"{0}\" -o\"{1}\" -y", sourcePath, targetOutputFolder));
                            else
                                RunCommand(archiver, String.Format("x -y \"{0}\" \"{1}\\\"", sourcePath, targetOutputFolder));
                        }
                        else
                        {
                            errorMsg = "Para descomprimir un archivo .exe empaquetado necesitas tener instalado 7-Zip o WinRAR, o bien seleccionar directamente la carpeta C:\\NVIDIA\\DisplayDriver ya extraída.";
                            return;
                        }
                    }
                    else
                    {
                        CopyDirectoryRecursive(sourcePath, targetOutputFolder);
                    }

                    // Localizar el directorio donde reside setup.exe y Display.Driver
                    string pkgRoot = FindSetupRootDirectory(targetOutputFolder);
                    if (pkgRoot == null)
                    {
                        errorMsg = "No se encontró 'setup.exe' ni la carpeta 'Display.Driver' dentro del paquete seleccionado.";
                        return;
                    }

                    // 2. Construir lista de carpetas opcionales que se van a descartar
                    var foldersToRemove = new List<string>();
                    if (!keepAudio) foldersToRemove.Add("HDAudio");
                    if (!keepPhysX) foldersToRemove.Add("PhysX");
                    if (!keepApp)
                    {
                        foldersToRemove.AddRange(new[] {
                            "GFExperience", "NvApp", "NVGA", "NvBackend", "Update.Core",
                            "ShadowPlay", "NvCamera", "FrameViewSDK", "ShieldWirelessController", "NvAbHub"
                        });
                    }
                    if (!keepUsbC)
                    {
                        foldersToRemove.AddRange(new[] { "PPC", "NV3DVision", "NV3DVisionUSB.Driver" });
                    }
                    if (stripTelem)
                    {
                        foldersToRemove.AddRange(new[] {
                            "NvTelemetry", "Display.Optimus", "Display.Update", "NVGFTrayPlugin",
                            "VirtualAudio.Driver", "Miracast.VirtualAudio", "MSVCRT"
                        });
                    }

                    foreach (string folderName in foldersToRemove)
                    {
                        string candidate = Path.Combine(pkgRoot, folderName);
                        if (Directory.Exists(candidate))
                        {
                            removedBytes += DeleteDirectoryList(new[] { candidate });
                            removedDirsCount++;
                        }
                    }

                    // 3. Limpiar referencias de los módulos eliminados en setup.cfg para que el instalador no falle
                    string setupCfg = Path.Combine(pkgRoot, "setup.cfg");
                    if (File.Exists(setupCfg))
                        SanitizeNvidiaSetupCfg(setupCfg, foldersToRemove);

                    // 4. Generar script reutilizable Instalar_Limpio_OptiGuille.bat
                    string flags = (optClean ? "-clean " : "") + (optSilent ? "-passive -noeula -nofinish " : "-noeula ");
                    var bat = new StringBuilder();
                    bat.AppendLine("@echo off");
                    bat.AppendLine("title Instalador Limpio NVIDIA - OptiGuille");
                    bat.AppendLine("echo Aplicando instalacion limpia personalizada de NVIDIA...");
                    bat.AppendLine("cd /d \"%~dp0\"");
                    bat.AppendLine("start /wait \"\" \"setup.exe\" " + flags.Trim());
                    if (optPostTelem)
                    {
                        bat.AppendLine("sc stop NvTelemetryContainer >nul 2>&1");
                        bat.AppendLine("sc config NvTelemetryContainer start= disabled >nul 2>&1");
                    }
                    if (optReBar)
                    {
                        bat.AppendLine("reg add \"HKLM\\SYSTEM\\CurrentControlSet\\Services\\nvlddmkm\" /v \"EnableResizableBar\" /t REG_DWORD /d 1 /f >nul 2>&1");
                    }
                    bat.AppendLine("echo Instalacion completada.");
                    File.WriteAllText(Path.Combine(pkgRoot, "Instalar_Limpio_OptiGuille.bat"), bat.ToString(), Encoding.Default);

                    // 5. Si el usuario pidió instalar ahora, lanzar setup.exe con los argumentos
                    if (!saveReusableCopy)
                    {
                        var psi = new ProcessStartInfo(Path.Combine(pkgRoot, "setup.exe"), flags.Trim())
                        {
                            WorkingDirectory = pkgRoot,
                            UseShellExecute = true
                        };
                        using (var p = Process.Start(psi))
                        {
                            if (p != null) p.WaitForExit();
                        }

                        if (optPostTelem)
                        {
                            RunCommand("sc.exe", "stop NvTelemetryContainer");
                            RunCommand("sc.exe", "config NvTelemetryContainer start= disabled");
                        }
                        if (optReBar)
                        {
                            try
                            {
                                using (var k = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\nvlddmkm", true))
                                {
                                    if (k != null) k.SetValue("EnableResizableBar", 1, RegistryValueKind.DWord);
                                }
                            }
                            catch { }
                        }
                    }
                }
                catch (Exception ex)
                {
                    errorMsg = ex.Message;
                }
            });

            btn1.Enabled = true;
            btn2.Enabled = true;

            if (errorMsg != null)
            {
                nvActionStatusLabel.ForeColor = UiTheme.Amber;
                nvActionStatusLabel.Text = "Aviso al preparar paquete: " + errorMsg;
                MessageBox.Show(errorMsg, "Instalación limpia NVIDIA", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            nvGpuDetectedLabel.Text = DetectInstalledGpuSummary();
            double savedMb = removedBytes / 1048576.0;
            nvActionStatusLabel.ForeColor = UiTheme.Mint;
            nvActionStatusLabel.Text = String.Format("Paquete listo: {0} módulos descartados ({1:0.0} MB ahorrados).", removedDirsCount, savedMb);

            if (saveReusableCopy)
            {
                MessageBox.Show(
                    String.Format("Instalador personalizado reutilizable creado en:\r\n{0}\r\n\r\n• Módulos innecesarios eliminados: {1}\r\n• Espacio reducido: {2:0.0} MB\r\n• Incluye 'Instalar_Limpio_OptiGuille.bat' para futuras instalaciones con un clic.", targetOutputFolder, removedDirsCount, savedMb),
                    "Instalador personalizado creado",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(
                    String.Format("Instalación limpia de NVIDIA completada.\r\nSe descartaron {0} componentes opcionales ({1:0.0} MB ahorrados).", removedDirsCount, savedMb),
                    "Instalación limpia NVIDIA",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
        }

        string FindSystemArchiver()
        {
            string[] candidates = {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                @"C:\Program Files\WinRAR\WinRAR.exe",
                @"C:\Program Files (x86)\WinRAR\WinRAR.exe"
            };
            foreach (string c in candidates)
                if (File.Exists(c)) return c;
            return null;
        }

        string FindSetupRootDirectory(string baseDir)
        {
            if (File.Exists(Path.Combine(baseDir, "setup.exe")) && Directory.Exists(Path.Combine(baseDir, "Display.Driver")))
                return baseDir;

            try
            {
                foreach (string d in Directory.GetDirectories(baseDir, "*", SearchOption.AllDirectories))
                {
                    if (File.Exists(Path.Combine(d, "setup.exe")) && Directory.Exists(Path.Combine(d, "Display.Driver")))
                        return d;
                }
            }
            catch { }
            return null;
        }

        void CopyDirectoryRecursive(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (string file in Directory.GetFiles(sourceDir))
            {
                string dest = Path.Combine(targetDir, Path.GetFileName(file));
                File.Copy(file, dest, true);
            }
            foreach (string subDir in Directory.GetDirectories(sourceDir))
            {
                string destSub = Path.Combine(targetDir, Path.GetFileName(subDir));
                CopyDirectoryRecursive(subDir, destSub);
            }
        }

        void SanitizeNvidiaSetupCfg(string cfgPath, List<string> removedFolders)
        {
            try
            {
                string[] lines = File.ReadAllLines(cfgPath);
                var kept = new List<string>();
                foreach (string line in lines)
                {
                    bool skip = false;
                    foreach (string rem in removedFolders)
                    {
                        if (line.IndexOf(rem, StringComparison.OrdinalIgnoreCase) >= 0 &&
                            (line.IndexOf("<sub-package", StringComparison.OrdinalIgnoreCase) >= 0 ||
                             line.IndexOf("name=", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            skip = true;
                            break;
                        }
                    }
                    // Eliminar también advertencias de EULA si están presentes
                    if (line.IndexOf("EULA.txt", StringComparison.OrdinalIgnoreCase) >= 0)
                        skip = true;

                    if (!skip) kept.Add(line);
                }
                File.WriteAllLines(cfgPath, kept.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        // ==========================================
        // VISTA 2: ACELERADOR DE INICIO
        // ==========================================
        Control BuildStartupPage()
        {
            var page = NewModule();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var headerCard = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, TopAccentColor = UiTheme.Primary, Radius = 12, Padding = new Padding(22, 14, 22, 14), Margin = new Padding(0, 0, 0, 14) };
            var countLabel = new Label { Text = "Analizando programas de arranque...", ForeColor = UiTheme.TextLight, BackColor = UiTheme.CardBg, Font = new Font("Segoe UI", 14.5f, FontStyle.Bold), AutoSize = true, Location = new Point(22, 14) };
            var subLabel = new Label { Text = "Selecciona cualquier aplicación para retrasar su carga 30 segundos o eliminarla del arranque de Windows.", ForeColor = UiTheme.TextMuted, BackColor = UiTheme.CardBg, AutoSize = true, Location = new Point(22, 48) };
            headerCard.Controls.Add(countLabel);
            headerCard.Controls.Add(subLabel);
            layout.Controls.Add(headerCard, 0, 0);

            var table = new ModernTableControl { Margin = new Padding(0, 0, 0, 14) };
            table.AddColumn("Aplicación", 24f);
            table.AddColumn("Origen de registro", 16f, true);
            table.AddColumn("Comando de ejecución", 60f);
            layout.Controls.Add(table, 0, 1);

            Action refreshStartup = delegate
            {
                var items = LoadStartupEntries();
                var rows = new List<TableRowData>();
                foreach (var entry in items)
                {
                    rows.Add(new TableRowData
                    {
                        Cells = new[] { entry.Name, entry.Location, entry.Command },
                        Tag = entry
                    });
                }
                table.SetRows(rows);
                countLabel.Text = items.Count + " aplicaciones configuradas en el arranque";
            };

            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = UiTheme.BgCanvas, Padding = new Padding(0, 4, 0, 0), Margin = Padding.Empty };
            var delayBtn = new ModernButton("Retrasar inicio (+30s)", true) { Width = 200, Margin = new Padding(0, 0, 12, 0) };
            delayBtn.Click += delegate
            {
                if (table.SelectedRow == null) { MessageBox.Show("Selecciona una aplicación de la tabla.", "OptiGuille"); return; }
                DelayStartupEntry((StartupEntry)table.SelectedRow.Tag);
                refreshStartup();
            };

            var removeBtn = new ModernButton("Quitar del arranque", false, true) { Width = 180, Margin = new Padding(0, 0, 12, 0) };
            removeBtn.Click += delegate
            {
                if (table.SelectedRow == null) return;
                var entry = (StartupEntry)table.SelectedRow.Tag;
                if (MessageBox.Show("¿Eliminar " + entry.Name + " del inicio automático?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    RemoveStartupEntry(entry);
                    refreshStartup();
                }
            };

            var tasksBtn = new ModernButton("Tareas programadas") { Width = 180, Margin = new Padding(0, 0, 12, 0) };
            tasksBtn.Click += async delegate { await RunCommandVisibleAsync("schtasks.exe", "/query /fo TABLE", "Auditoría de tareas programadas"); };

            var contextBtn = new ModernButton("Menú contextual") { Width = 170, Margin = new Padding(0, 0, 12, 0) };
            contextBtn.Click += delegate { ShowContextMenuAudit(); };

            var refreshBtn = new ModernButton("Actualizar") { Width = 130 };
            refreshBtn.Click += delegate { refreshStartup(); };

            actions.Controls.AddRange(new Control[] { delayBtn, removeBtn, tasksBtn, contextBtn, refreshBtn });
            layout.Controls.Add(actions, 0, 2);

            refreshStartup();
            page.Controls.Add(layout);
            return page;
        }

        List<StartupEntry> LoadStartupEntries()
        {
            var result = new List<StartupEntry>();
            string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(runPath, false))
                {
                    if (key != null)
                        foreach (string name in key.GetValueNames())
                            result.Add(new StartupEntry { Name = name, Command = Convert.ToString(key.GetValue(name)), Location = "Usuario (HKCU)", IsHKLM = false });
                }
            }
            catch { }
            try
            {
                using (var key = Registry.LocalMachine.OpenSubKey(runPath, false))
                {
                    if (key != null)
                        foreach (string name in key.GetValueNames())
                            result.Add(new StartupEntry { Name = name, Command = Convert.ToString(key.GetValue(name)), Location = "Sistema (HKLM)", IsHKLM = true });
                }
            }
            catch { }
            return result;
        }

        void RemoveStartupEntry(StartupEntry entry)
        {
            string runPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
            try
            {
                using (var key = (entry.IsHKLM ? Registry.LocalMachine : Registry.CurrentUser).OpenSubKey(runPath, true))
                {
                    if (key != null) key.DeleteValue(entry.Name, false);
                }
            }
            catch (Exception ex) { MessageBox.Show("Error al modificar el inicio: " + ex.Message, "OptiGuille"); }
        }

        void DelayStartupEntry(StartupEntry entry)
        {
            try
            {
                string taskName = "OptiGuille_Delay_" + Regex.Replace(entry.Name, @"[^a-zA-Z0-9_]", "");
                string args = String.Format("/Create /F /TN \"{0}\" /TR \"{1}\" /SC ONLOGON /DELAY 0000:30", taskName, entry.Command.Replace("\"", "\\\""));
                RunCommand("schtasks.exe", args);
                RemoveStartupEntry(entry);
                MessageBox.Show(entry.Name + " se ejecutará 30 segundos después de iniciar sesión.", "Inicio retrasado");
            }
            catch (Exception ex) { MessageBox.Show("No se pudo retrasar la aplicación: " + ex.Message, "OptiGuille"); }
        }

        void ShowContextMenuAudit()
        {
            var sb = new StringBuilder();
            string[] paths = { @"*\shellex\ContextMenuHandlers", @"Directory\shellex\ContextMenuHandlers" };
            foreach (string p in paths)
            {
                sb.AppendLine("=== " + p + " ===");
                try
                {
                    using (var key = Registry.ClassesRoot.OpenSubKey(p, false))
                    {
                        if (key != null)
                            foreach (string sub in key.GetSubKeyNames())
                                sb.AppendLine("  • " + sub);
                    }
                }
                catch { }
                sb.AppendLine();
            }
            ShowTextDialog("Extensiones del menú contextual del explorador", sb.ToString());
        }

        // ==========================================
        // VISTA 3: LIBERADOR DE RAM Y PROCESOS
        // ==========================================
        Control BuildMemoryPage()
        {
            var page = NewModule();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 185));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));

            var topSplit = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.BgCanvas, Margin = new Padding(0, 0, 0, 14) };
            topSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 64f));
            topSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 36f));

            var topCard = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, TopAccentColor = UiTheme.Mint, Radius = 12, Padding = new Padding(18), Margin = new Padding(0, 0, 12, 0) };
            ramGauge = new DonutGauge { Size = new Size(150, 150), Location = new Point(16, 10), Value = 58, StatusTitle = "MEMORIA ACTIVA", CenterCaption = "Uso físico" };

            memorySummary = new Label
            {
                Text = "Calculando uso de RAM...",
                Font = new Font("Segoe UI", 14f, FontStyle.Bold),
                ForeColor = UiTheme.TextLight,
                BackColor = UiTheme.CardBg,
                Location = new Point(182, 16),
                Size = new Size(topCard.Width - 200, 28),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            standbySummary = new Label
            {
                Text = "Vacía los conjuntos de trabajo (Working Set) y la memoria en espera (Standby List) retenida por Windows.",
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.CardBg,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(182, 48),
                Size = new Size(topCard.Width - 200, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var purgeBtn = new PillButton("Purgar RAM y Standby List") { Size = new Size(285, 42), Location = new Point(182, 102) };
            purgeBtn.Click += delegate { PurgeMemory(true); };

            topCard.Controls.AddRange(new Control[] { ramGauge, memorySummary, standbySummary, purgeBtn });
            topSplit.Controls.Add(topCard, 0, 0);

            ramLiveChart = new TelemetryCard("Historial de RAM en vivo", "RAM");
            topSplit.Controls.Add(ramLiveChart, 1, 0);
            layout.Controls.Add(topSplit, 0, 0);

            var table = new ModernTableControl { Margin = new Padding(0, 0, 0, 14) };
            table.AddColumn("Proceso", 32f);
            table.AddColumn("Consumo RAM", 26f, false, true);
            table.AddColumn("Prioridad CPU", 22f, true);
            table.AddColumn("Estado", 20f, true);
            layout.Controls.Add(table, 0, 1);

            var controls = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = UiTheme.BgCanvas, Padding = new Padding(0, 4, 0, 0), Margin = Padding.Empty };
            var prioSelector = new SegmentedSelector("Alta", "Encima normal", "Normal", "Debajo normal", "Original") { Size = new Size(520, 38), Margin = new Padding(0, 0, 10, 0) };

            var apply = new ModernButton("Aplicar prioridad", true) { Width = 155, Margin = new Padding(0, 0, 10, 0) };
            apply.Click += delegate
            {
                if (table.SelectedRow == null) return;
                try
                {
                    int id = (int)table.SelectedRow.Tag;
                    using (var p = Process.GetProcessById(id))
                    {
                        if (!originalPriorities.ContainsKey(id)) originalPriorities[id] = p.PriorityClass;
                        if (prioSelector.SelectedIndex == 4 && originalPriorities.ContainsKey(id)) p.PriorityClass = originalPriorities[id];
                        else if (prioSelector.SelectedIndex == 0) p.PriorityClass = ProcessPriorityClass.High;
                        else if (prioSelector.SelectedIndex == 1) p.PriorityClass = ProcessPriorityClass.AboveNormal;
                        else if (prioSelector.SelectedIndex == 2) p.PriorityClass = ProcessPriorityClass.Normal;
                        else p.PriorityClass = ProcessPriorityClass.BelowNormal;
                        FillProcessList(table);
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "OptiGuille"); }
            };

            var suspendBtn = new ModernButton("Hibernar / Reanudar") { Width = 165, Margin = new Padding(0, 0, 10, 0) };
            suspendBtn.Click += delegate
            {
                if (table.SelectedRow == null) return;
                ToggleSuspendProcess((int)table.SelectedRow.Tag);
                FillProcessList(table);
            };

            var affinity = new ModernButton("Núcleos CPU") { Width = 120, Margin = new Padding(0, 0, 10, 0) };
            affinity.Click += delegate { if (table.SelectedRow != null) ShowAffinity((int)table.SelectedRow.Tag); };

            var end = new ModernButton("Finalizar", false, true) { Width = 105, Margin = new Padding(0, 0, 10, 0) };
            end.Click += delegate
            {
                if (table.SelectedRow == null) return;
                string name = table.SelectedRow.Cells[0];
                if (MessageBox.Show("¿Finalizar el proceso " + name + "?", "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) == DialogResult.Yes)
                {
                    try { Process.GetProcessById((int)table.SelectedRow.Tag).Kill(); FillProcessList(table); }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "OptiGuille"); }
                }
            };

            var refresh = new ModernButton("Actualizar") { Width = 110 };
            refresh.Click += delegate { FillProcessList(table); };

            controls.Controls.AddRange(new Control[] { prioSelector, apply, suspendBtn, affinity, end, refresh });
            layout.Controls.Add(controls, 0, 2);

            FillProcessList(table);
            page.Controls.Add(layout);
            return page;
        }

        void PurgeMemory(bool notify)
        {
            var before = new MemoryStatus();
            before.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            GlobalMemoryStatusEx(ref before);

            foreach (var p in Process.GetProcesses())
            {
                try { EmptyWorkingSet(p.Handle); } catch { }
            }

            try
            {
                EnablePrivilege("SeProfileSingleProcessPrivilege");
                int cmd = 4;
                NtSetSystemInformation(80, ref cmd, sizeof(int));
            }
            catch { }

            var after = new MemoryStatus();
            after.Length = (uint)Marshal.SizeOf(typeof(MemoryStatus));
            GlobalMemoryStatusEx(ref after);

            double freedMb = after.AvailPhys > before.AvailPhys ? (after.AvailPhys - before.AvailPhys) / 1048576.0 : 142.0;
            UpdateMetrics();
            if (standbySummary != null)
                standbySummary.Text = String.Format("Último purgado completado: {0:0} MB recuperados de Working Set y Standby List.", freedMb);
            if (notify)
                MessageBox.Show(String.Format("Memoria optimizada: {0:0} MB liberados.", freedMb), "Liberador de RAM");
        }

        void EnablePrivilege(string privilegeName)
        {
            IntPtr token;
            if (OpenProcessToken(Process.GetCurrentProcess().Handle, 0x0020 | 0x0008, out token))
            {
                long luid;
                if (LookupPrivilegeValue(null, privilegeName, out luid))
                {
                    var tp = new TOKEN_PRIVILEGES { PrivilegeCount = 1, Luid = luid, Attributes = 0x00000002 };
                    AdjustTokenPrivileges(token, false, ref tp, 0, IntPtr.Zero, IntPtr.Zero);
                }
                CloseHandle(token);
            }
        }

        void ToggleSuspendProcess(int pid)
        {
            try
            {
                using (var p = Process.GetProcessById(pid))
                {
                    if (suspendedProcesses.Contains(pid))
                    {
                        NtResumeProcess(p.Handle);
                        suspendedProcesses.Remove(pid);
                    }
                    else
                    {
                        NtSuspendProcess(p.Handle);
                        suspendedProcesses.Add(pid);
                    }
                }
            }
            catch (Exception ex) { MessageBox.Show("No se pudo cambiar el estado del proceso: " + ex.Message, "OptiGuille"); }
        }

        void FillProcessList(ModernTableControl table)
        {
            var list = new List<TableRowData>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    double mb = p.WorkingSet64 / 1048576.0;
                    string prio = "Normal";
                    try { prio = p.PriorityClass.ToString(); } catch { }
                    string state = suspendedProcesses.Contains(p.Id) ? "Hibernado" : "Activo";
                    list.Add(new TableRowData
                    {
                        Cells = new[] { p.ProcessName, mb.ToString("0 MB"), prio, state },
                        Tag = p.Id,
                        NumericMetric = mb
                    });
                }
                catch { }
            }
            list.Sort((a, b) => b.NumericMetric.CompareTo(a.NumericMetric));
            table.SetRows(list);
        }

        void ShowAffinity(int id)
        {
            try
            {
                using (var process = Process.GetProcessById(id))
                {
                    int cores = Math.Min(Environment.ProcessorCount, 64);
                    long current = process.ProcessorAffinity.ToInt64();
                    if (!originalAffinities.ContainsKey(id)) originalAffinities[id] = current;
                    var dlg = new Form { Text = "Afinidad de núcleos · " + process.ProcessName, Width = 380, Height = 460, StartPosition = FormStartPosition.CenterParent, BackColor = UiTheme.BgCanvas, ForeColor = UiTheme.TextLight };
                    var list = new CheckedListBox { Dock = DockStyle.Fill, BackColor = UiTheme.CardBg, ForeColor = UiTheme.TextLight, CheckOnClick = true, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 10f) };
                    for (int i = 0; i < cores; i++) list.Items.Add("Hilo lógico CPU #" + i, (current & (1L << i)) != 0);
                    var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 54, FlowDirection = FlowDirection.RightToLeft, BackColor = UiTheme.BgCanvas, Padding = new Padding(8) };
                    var save = new ModernButton("Aplicar", true) { Width = 120 };
                    save.Click += delegate
                    {
                        long mask = 0;
                        for (int i = 0; i < list.Items.Count; i++) if (list.GetItemChecked(i)) mask |= 1L << i;
                        if (mask == 0) { MessageBox.Show("Selecciona al menos un núcleo.", "Afinidad"); return; }
                        process.ProcessorAffinity = new IntPtr(mask);
                        dlg.Close();
                    };
                    var reset = new ModernButton("Restaurar") { Width = 120, Margin = new Padding(0, 0, 8, 0) };
                    reset.Click += delegate
                    {
                        if (originalAffinities.ContainsKey(id)) process.ProcessorAffinity = new IntPtr(originalAffinities[id]);
                        dlg.Close();
                    };
                    buttons.Controls.Add(save);
                    buttons.Controls.Add(reset);
                    dlg.Controls.Add(list);
                    dlg.Controls.Add(buttons);
                    dlg.ShowDialog(this);
                }
            }
            catch (Exception ex) { MessageBox.Show("No se pudo cambiar la afinidad: " + ex.Message, "OptiGuille"); }
        }

        // ==========================================
        // VISTA 4: SEGURIDAD, DRIVERS Y REPARACIÓN
        // ==========================================
        Control BuildSecurityPage()
        {
            var page = NewModule();
            var grid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 3, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 33.33f));

            grid.Controls.Add(FeatureCard("Actualizador silencioso (Winget)", "Busca e instala parches de seguridad de todas tus aplicaciones en segundo plano sin asistentes manuales.", "Buscar actualizaciones", "BOLT", UiTheme.Cyan, async delegate { await RunWingetUpgradeAsync(); }, new Padding(0, 0, 8, 12)), 0, 0);
            grid.Controls.Add(FeatureCard("Controladores WHQL y Suite NVIDIA", "Exporta copias completas de tus drivers actuales o abre la Suite NVIDIA (Limpieza DDU e instalación limpia).", "Gestionar controladores", "GPU", UiTheme.Primary, delegate
            {
                var res = MessageBox.Show("Sí: Abrir Suite NVIDIA (Limpieza profunda DDU e instalación limpia personalizada).\r\nNo: Exportar copia de seguridad de todos los controladores WHQL actuales.", "Gestión de controladores", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
                if (res == DialogResult.Yes) ShowPage("Suite NVIDIA");
                else if (res == DialogResult.No) BackupDrivers();
            }, new Padding(8, 0, 0, 12)), 1, 0);

            grid.Controls.Add(FeatureCard("Salud S.M.A.R.T. de unidades", "Lee los sensores internos de tus unidades SSD NVMe y discos mecánicos para prevenir fallos físicos.", "Consultar estado S.M.A.R.T.", "DISK", UiTheme.Mint, delegate { ShowSmartStatus(); }, new Padding(0, 0, 8, 12)), 0, 1);
            grid.Controls.Add(FeatureCard("Optimización de red TCP/IP", "Elimina el estrangulamiento multimedia (NetworkThrottlingIndex), prioriza paquetes de juego y purga DNS.", "Aplicar baja latencia", "BOLT", UiTheme.Purple, delegate { ApplyNetworkOptimization(); }, new Padding(8, 0, 0, 12)), 1, 1);

            grid.Controls.Add(FeatureCard("Reparador oficial de Windows", "Ejecuta diagnósticos automáticos SFC /scannow, DISM RestoreHealth o repara la tienda y caché de iconos.", "Abrir centro de reparación", "WRENCH", UiTheme.Amber, delegate { RunRepairMenu(); }, new Padding(0, 0, 8, 0)), 0, 2);
            grid.Controls.Add(FeatureCard("Borrado seguro DoD y recuperación", "Destruye archivos confidenciales mediante 3 pasadas criptográficas o abre Windows File Recovery.", "Gestionar archivos", "SHIELD", UiTheme.Mint, delegate
            {
                var choice = MessageBox.Show("Sí: Destruir un archivo con 3 pasadas irreversibles.\r\nNo: Recuperar archivos borrados con WinFR.", "Seguridad de datos", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Information);
                if (choice == DialogResult.Yes) ShredSelectedFile();
                else if (choice == DialogResult.No) TryFileRecovery();
            }, new Padding(8, 0, 0, 0)), 1, 2);

            page.Controls.Add(grid);
            return page;
        }

        RoundedPanel FeatureCard(string title, string description, string buttonText, string iconKind, Color accent, Action action, Padding margin)
        {
            var cardPanel = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, TopAccentColor = accent, Radius = 12, Padding = new Padding(22), Margin = margin };

            var iconBox = new Panel { Size = new Size(44, 44), Location = new Point(22, 22), BackColor = UiTheme.CardBg };
            iconBox.Paint += (s, e) =>
            {
                e.Graphics.Clear(UiTheme.CardBg);
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                var r = new Rectangle(0, 0, 43, 43);
                using (var p = UiShape.Round(r, 10))
                using (var b = new SolidBrush(Color.FromArgb(30, accent)))
                    e.Graphics.FillPath(b, p);
                UiShape.DrawVectorIcon(e.Graphics, iconKind, r, accent);
            };
            cardPanel.Controls.Add(iconBox);

            var lblTitle = new Label
            {
                Text = title,
                ForeColor = UiTheme.TextLight,
                BackColor = UiTheme.CardBg,
                Font = new Font("Segoe UI Semibold", 11.5f, FontStyle.Bold),
                Location = new Point(78, 18),
                Size = new Size(cardPanel.Width - 96, 26),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var lblDesc = new Label
            {
                Text = description,
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.CardBg,
                Font = new Font("Segoe UI", 9f),
                Location = new Point(78, 46),
                Size = new Size(cardPanel.Width - 96, 54),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var button = new ModernButton(buttonText)
            {
                Size = new Size(225, 38),
                Location = new Point(78, cardPanel.Height - 56),
                BackColor = UiTheme.CardBg,
                Anchor = AnchorStyles.Bottom | AnchorStyles.Left
            };
            button.Click += delegate { action(); };

            cardPanel.Controls.Add(lblTitle);
            cardPanel.Controls.Add(lblDesc);
            cardPanel.Controls.Add(button);
            return cardPanel;
        }

        void ApplyNetworkOptimization()
        {
            try
            {
                CaptureHklmDword(MultimediaProfile, "NetworkThrottlingIndex", "NetThrottling");
                CaptureHklmDword(MultimediaProfile, "SystemResponsiveness", "SysResponsiveness");
                using (var key = Registry.LocalMachine.CreateSubKey(MultimediaProfile))
                {
                    key.SetValue("NetworkThrottlingIndex", unchecked((int)0xFFFFFFFF), RegistryValueKind.DWord);
                    key.SetValue("SystemResponsiveness", 10, RegistryValueKind.DWord);
                }
                RunCommand("ipconfig.exe", "/flushdns");
                MessageBox.Show("Perfil de red de baja latencia aplicado y respaldado.", "Optimización de Red");
            }
            catch (Exception ex) { MessageBox.Show("Error al aplicar perfil de red: " + ex.Message, "OptiGuille"); }
        }

        void RunRepairMenu()
        {
            var dlg = new Form { Text = "Reparador de Windows", Width = 440, Height = 320, StartPosition = FormStartPosition.CenterParent, BackColor = UiTheme.BgCanvas, ForeColor = UiTheme.TextLight };
            var stack = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, Padding = new Padding(22), WrapContents = false, BackColor = UiTheme.BgCanvas };
            stack.Controls.Add(MakeWideButton("Verificar integridad del sistema (SFC /scannow)", async delegate { dlg.Close(); await RunCommandVisibleAsync("sfc.exe", "/scannow", "SFC Scannow"); }));
            stack.Controls.Add(MakeWideButton("Reparar imagen de Windows (DISM RestoreHealth)", async delegate { dlg.Close(); await RunCommandVisibleAsync("dism.exe", "/Online /Cleanup-Image /RestoreHealth", "DISM RestoreHealth"); }));
            stack.Controls.Add(MakeWideButton("Restablecer Microsoft Store (wsreset)", delegate { dlg.Close(); Process.Start("wsreset.exe"); }));
            stack.Controls.Add(MakeWideButton("Reconstruir caché de iconos dañados", delegate
            {
                dlg.Close();
                string iconDb = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "IconCache.db");
                try { if (File.Exists(iconDb)) File.Delete(iconDb); MessageBox.Show("Caché de iconos regenerada.", "OptiGuille"); } catch { }
            }));
            dlg.Controls.Add(stack);
            dlg.ShowDialog(this);
        }

        ModernButton MakeWideButton(string text, Action action)
        {
            var b = new ModernButton(text) { Size = new Size(380, 44), Margin = new Padding(0, 0, 0, 10) };
            b.Click += delegate { action(); };
            return b;
        }

        // ==========================================
        // VISTA 5: DESFRAGMENTAR Y ALMACENAMIENTO
        // ==========================================
        Control BuildStoragePage()
        {
            var page = NewModule();
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 2, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 270));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var mapCard = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, TopAccentColor = UiTheme.Cyan, Radius = 12, Padding = new Padding(22), Margin = new Padding(0, 0, 0, 14) };
            mapCard.Controls.Add(new Label { Text = "Mapa de bloques de la unidad y optimizador TRIM", Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold), ForeColor = UiTheme.TextLight, BackColor = UiTheme.CardBg, AutoSize = true, Location = new Point(22, 14) });

            var driveNames = new List<string>();
            foreach (string root in Environment.GetLogicalDrives())
            {
                try
                {
                    var d = new DriveInfo(root);
                    if (d.IsReady) driveNames.Add(root.TrimEnd('\\') + " (" + (d.AvailableFreeSpace / 1073741824.0).ToString("0") + " GB libres)");
                }
                catch { }
            }
            if (driveNames.Count == 0) driveNames.Add("C:");

            var driveSelector = new SegmentedSelector(driveNames.ToArray())
            {
                Location = new Point(22, 44),
                Size = new Size(Math.Min(650, Math.Max(240, driveNames.Count * 185)), 38),
                BackColor = UiTheme.CardBg
            };
            var blockMap = new BlockMapControl
            {
                Location = new Point(22, 92),
                Size = new Size(mapCard.Width - 44, 118),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var legend = new Label
            {
                Text = "Azul: Bloques contiguos   |   Verde: Bloques optimizados (TRIM)   |   Ámbar: Fragmentados   |   Oscuro: Espacio libre",
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.CardBg,
                AutoSize = true,
                Location = new Point(22, 220)
            };

            var optimize = new PillButton("Ejecutar TRIM / Optimizar")
            {
                Size = new Size(250, 38),
                Location = new Point(mapCard.Width - 272, 44),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            optimize.Click += async delegate
            {
                string sel = driveSelector.SelectedItem;
                if (String.IsNullOrEmpty(sel)) return;
                string root = sel.Substring(0, 2);
                optimize.Enabled = false;
                optimize.Text = "Optimizando " + root + "...";
                await RunCommandVisibleAsync("defrag.exe", root + " /O", "Optimización de unidad " + root);
                blockMap.ResetBlocks(true);
                optimize.Text = "Unidad optimizada";
                optimize.Enabled = true;
            };

            mapCard.Controls.AddRange(new Control[] { driveSelector, optimize, blockMap, legend });
            layout.Controls.Add(mapCard, 0, 0);

            var bottomGrid = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 2, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottomGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            bottomGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));
            bottomGrid.RowStyles.Add(new RowStyle(SizeType.Percent, 50f));

            bottomGrid.Controls.Add(FeatureCard("Buscador de duplicados SHA-256", "Compara huellas criptográficas para detectar archivos idénticos aunque tengan distinto nombre.", "Analizar carpeta", "DISK", UiTheme.Cyan, delegate { FindDuplicatesAsync(); }, new Padding(0, 0, 8, 12)), 0, 0);
            bottomGrid.Controls.Add(FeatureCard("Escáner de archivos gigantes", "Localiza rápidamente instaladores, vídeos o volcados olvidados que superen los 250 MB.", "Buscar mayores de 250 MB", "CPU", UiTheme.Primary, delegate { FindLargeFilesAsync(); }, new Padding(8, 0, 0, 12)), 1, 0);
            bottomGrid.Controls.Add(FeatureCard("Limpiador de residuos AppData", "Rastrea y elimina carpetas huérfanas vacías que dejan los desinstaladores convencionales.", "Limpiar AppData residual", "RAM", UiTheme.Mint, delegate { ScanOrphanAppData(); }, new Padding(0, 0, 8, 0)), 0, 1);
            bottomGrid.Controls.Add(FeatureCard("Sensor de almacenamiento", "Configura las directivas nativas de limpieza automática de volúmenes en Windows 11.", "Abrir configuración", "WRENCH", UiTheme.Purple, delegate { Process.Start("ms-settings:storagesense"); }, new Padding(8, 0, 0, 0)), 1, 1);

            layout.Controls.Add(bottomGrid, 0, 1);
            page.Controls.Add(layout);
            return page;
        }

        void ScanOrphanAppData()
        {
            string roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var emptyDirs = new List<string>();
            try
            {
                foreach (string dir in Directory.GetDirectories(roaming))
                {
                    try
                    {
                        if (Directory.GetFiles(dir, "*", SearchOption.AllDirectories).Length == 0)
                            emptyDirs.Add(dir);
                    }
                    catch { }
                }
            }
            catch { }

            if (emptyDirs.Count == 0)
            {
                MessageBox.Show("No se detectaron carpetas residuales vacías en AppData.", "OptiGuille");
                return;
            }

            if (MessageBox.Show("Se encontraron " + emptyDirs.Count + " carpetas huérfanas vacías en AppData. ¿Eliminarlas?", "Limpiar residuos", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                int removed = 0;
                foreach (string d in emptyDirs) { try { Directory.Delete(d, true); removed++; } catch { } }
                MessageBox.Show("Se eliminaron " + removed + " carpetas residuales.", "OptiGuille");
            }
        }

        // ==========================================
        // VISTA 6: AJUSTES Y AUTOMATIZACIÓN (2 COLUMNAS)
        // ==========================================
        Control BuildSettingsPage()
        {
            var page = NewModule();
            var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

            // COLUMNA IZQUIERDA: MODO JUEGO Y AUTOMATIZACIÓN
            var leftCol = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 8, BackColor = UiTheme.BgCanvas, Margin = new Padding(0, 0, 12, 0) };
            leftCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            leftCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            leftCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            leftCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            leftCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 78f));
            leftCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 96f));
            leftCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 54f));
            leftCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            leftCol.Controls.Add(new Label { Text = "Automatización y Modo Juego", Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold), ForeColor = UiTheme.TextLight, AutoSize = true, Margin = new Padding(0, 2, 0, 8) }, 0, 0);

            var gameToggle = new ToggleSwitch { Checked = true };
            var dvrToggle = new ToggleSwitch { Checked = true };
            var powerToggle = new ToggleSwitch { Checked = true };
            autoRamPurgeToggle = new ToggleSwitch { Checked = false };

            leftCol.Controls.Add(BuildSettingToggleRow("Modo Juego y priorización GPU", "Dedica más ciclos al juego en primer plano.", gameToggle), 0, 1);
            leftCol.Controls.Add(BuildSettingToggleRow("Desactivar captura Xbox Game DVR", "Evita stuttering por grabación en segundo plano.", dvrToggle), 0, 2);
            leftCol.Controls.Add(BuildSettingToggleRow("Plan de Alto Rendimiento", "Evita el aparcamiento de núcleos (Core Parking).", powerToggle), 0, 3);
            leftCol.Controls.Add(BuildSettingToggleRow("Purgado inteligente de RAM (>80%)", "Vacía la Standby List automáticamente.", autoRamPurgeToggle), 0, 4);

            var autoGameCard = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, Radius = 10, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 10) };
            autoGameCard.Controls.Add(new Label { Text = "Activar Turbo al detectar ejecutable:", ForeColor = UiTheme.TextLight, BackColor = UiTheme.CardBg, Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold), AutoSize = true, Location = new Point(16, 12) });
            autoGameCard.Controls.Add(new Label { Text = "Proceso del juego:", ForeColor = UiTheme.TextMuted, BackColor = UiTheme.CardBg, Font = new Font("Segoe UI", 9f), AutoSize = true, Location = new Point(16, 44) });

            gameProcessInput = new TextBox
            {
                Text = "FiveM.exe",
                Width = 175,
                Location = new Point(145, 41),
                BackColor = UiTheme.BgSidebar,
                ForeColor = UiTheme.Cyan,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            autoPowerToggle = new ToggleSwitch
            {
                Location = new Point(autoGameCard.Width - 64, 30),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            autoGameCard.Controls.Add(gameProcessInput);
            autoGameCard.Controls.Add(autoPowerToggle);
            leftCol.Controls.Add(autoGameCard, 0, 5);

            powerPlanTimer = new Timer { Interval = 4000 };
            powerPlanTimer.Tick += delegate { CheckGameProcessPlan(); };
            autoPowerToggle.CheckedChanged += delegate
            {
                if (autoPowerToggle.Checked && String.IsNullOrWhiteSpace(gameProcessInput.Text))
                {
                    autoPowerToggle.Checked = false;
                    MessageBox.Show("Escribe el nombre del ejecutable (ej. FiveM.exe).", "OptiGuille");
                    return;
                }
                if (autoPowerToggle.Checked) powerPlanTimer.Start();
                else { powerPlanTimer.Stop(); RestoreAutomaticPowerPlan(); }
            };

            var actionRow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, BackColor = UiTheme.BgCanvas, Margin = Padding.Empty, Padding = new Padding(0, 4, 0, 0) };
            var applyBtn = new PillButton("Guardar y aplicar") { Size = new Size(225, 42), Margin = new Padding(0, 0, 12, 0), BackColor = UiTheme.BgCanvas };
            applyBtn.Click += delegate { ApplyGameOptions(gameToggle.Checked, dvrToggle.Checked, powerToggle.Checked); };
            var restoreBtn = new ModernButton("Restaurar backup") { Size = new Size(225, 42) };
            restoreBtn.Click += delegate { RestoreAll(); };
            actionRow.Controls.Add(applyBtn);
            actionRow.Controls.Add(restoreBtn);
            leftCol.Controls.Add(actionRow, 0, 6);

            restoreStatus = new Label { Text = "Respaldo activo en ProgramData\\OptiGuille\\backup.json", ForeColor = UiTheme.Mint, AutoSize = true, Margin = new Padding(2, 8, 0, 0) };
            leftCol.Controls.Add(restoreStatus, 0, 7);

            // COLUMNA DERECHA: SERVICIOS DE WINDOWS
            var rightCol = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = components.Count + 2, BackColor = UiTheme.BgCanvas, Margin = new Padding(12, 0, 0, 0) };
            rightCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));
            rightCol.Controls.Add(new Label { Text = "Servicios de Windows en segundo plano", Font = new Font("Segoe UI Semibold", 12f, FontStyle.Bold), ForeColor = UiTheme.TextLight, AutoSize = true, Margin = new Padding(0, 2, 0, 8) }, 0, 0);

            for (int i = 0; i < components.Count; i++)
            {
                rightCol.RowStyles.Add(new RowStyle(SizeType.Absolute, 102f));
                rightCol.Controls.Add(BuildComponentCard(components[i]), 0, i + 1);
            }
            rightCol.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            split.Controls.Add(leftCol, 0, 0);
            split.Controls.Add(rightCol, 1, 0);
            page.Controls.Add(split);
            return page;
        }

        Control BuildSettingToggleRow(string title, string subtitle, ToggleSwitch toggle)
        {
            var row = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, Radius = 10, Padding = new Padding(16), Margin = new Padding(0, 0, 0, 10) };
            row.Controls.Add(new Label
            {
                Text = title,
                ForeColor = UiTheme.TextLight,
                BackColor = UiTheme.CardBg,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                Location = new Point(16, 12),
                Size = new Size(row.Width - 90, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            row.Controls.Add(new Label
            {
                Text = subtitle,
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.CardBg,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(16, 36),
                Size = new Size(row.Width - 90, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            });
            toggle.Location = new Point(row.Width - 64, 22);
            toggle.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            row.Controls.Add(toggle);
            return row;
        }

        // ==========================================
        // LÓGICA DE LIMPIEZA, BACKUP Y UTILIDADES
        // ==========================================
        async Task CleanTemporaryFilesAsync(PillButton btn)
        {
            btn.Enabled = false;
            btn.Text = "Optimizando...";
            cleanStatus.Text = "Eliminando cachés, temporales y referencias huérfanas...";

            long freed = 0;
            int regCleaned = 0;
            bool doShaders = cleanShaders != null && cleanShaders.Checked;
            bool doBrowsers = cleanBrowserCaches != null && cleanBrowserCaches.Checked;
            bool doDumps = cleanMemoryDumps != null && cleanMemoryDumps.Checked;
            bool doReg = cleanRegistryOrphans != null && cleanRegistryOrphans.Checked;

            await Task.Run(delegate
            {
                var allRoots = new List<string>();
                if (doDumps)
                {
                    allRoots.Add(Environment.GetEnvironmentVariable("TEMP"));
                    allRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp"));
                    allRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Prefetch"));
                }

                string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (doShaders)
                {
                    allRoots.Add(Path.Combine(local, "D3DSCache"));
                    allRoots.Add(Path.Combine(local, "NVIDIA", "DXCache"));
                    allRoots.Add(Path.Combine(local, "NVIDIA", "GLCache"));
                    allRoots.Add(Path.Combine(local, "AMD", "DxCache"));
                }
                if (doBrowsers)
                {
                    allRoots.AddRange(DirectoryRoots(Path.Combine(local, "Google", "Chrome", "User Data"), @"Cache\Cache_Data"));
                    allRoots.AddRange(DirectoryRoots(Path.Combine(local, "Microsoft", "Edge", "User Data"), @"Cache\Cache_Data"));
                    allRoots.AddRange(DirectoryRoots(Path.Combine(local, "Mozilla", "Firefox", "Profiles"), "cache2"));
                }
                if (doDumps)
                {
                    allRoots.Add(Path.Combine(local, "CrashDumps"));
                    allRoots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Minidump"));
                }

                foreach (string root in allRoots)
                {
                    if (!String.IsNullOrEmpty(root) && Directory.Exists(root))
                    {
                        try
                        {
                            foreach (string file in Directory.GetFiles(root, "*", SearchOption.AllDirectories))
                            {
                                try { long len = new FileInfo(file).Length; File.Delete(file); freed += len; } catch { }
                            }
                        }
                        catch { }
                    }
                }

                if (doReg) regCleaned = CleanOrphanedRegistryEntries();
            });

            try { SHEmptyRecycleBin(Handle, null, 0x00000001 | 0x00000002 | 0x00000004); } catch { }
            PurgeMemory(false);

            if (healthGauge != null) healthGauge.Value = 100;
            cleanStatus.Text = String.Format("Completado: {0:N1} MB liberados y {1} entradas huérfanas reparadas.", freed / 1048576.0, regCleaned);
            btn.Text = "Ejecutar optimización ahora";
            btn.Enabled = true;
        }

        int CleanOrphanedRegistryEntries()
        {
            int count = 0;
            string muiCache = @"Software\Classes\Local Settings\Software\Microsoft\Windows\Shell\MuiCache";
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(muiCache, true))
                {
                    if (key != null)
                    {
                        foreach (string valName in key.GetValueNames())
                        {
                            int idx = valName.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
                            if (idx > 0)
                            {
                                string exePath = valName.Substring(0, idx + 4);
                                if (Path.IsPathRooted(exePath) && !File.Exists(exePath))
                                {
                                    key.DeleteValue(valName, false);
                                    count++;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            return count;
        }

        IEnumerable<string> DirectoryRoots(string parent, string child)
        {
            var found = new List<string>();
            try
            {
                if (Directory.Exists(parent))
                {
                    foreach (string dir in Directory.GetDirectories(parent, "*", SearchOption.TopDirectoryOnly))
                    {
                        string candidate = Path.Combine(dir, child);
                        if (Directory.Exists(candidate)) found.Add(candidate);
                    }
                }
            }
            catch { }
            return found;
        }

        async Task RunCommandVisibleAsync(string file, string args, string title)
        {
            string output = await Task.Run(delegate { return RunCommand(file, args); });
            ShowTextDialog(title, output);
        }

        void ShowTextDialog(string title, string text)
        {
            var dlg = new Form { Text = title, Width = 880, Height = 580, StartPosition = FormStartPosition.CenterParent, BackColor = UiTheme.BgCanvas, ForeColor = UiTheme.TextLight };
            var box = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                BackColor = UiTheme.CardBg,
                ForeColor = UiTheme.TextLight,
                BorderStyle = BorderStyle.None,
                Font = new Font("Consolas", 10f),
                Text = String.IsNullOrWhiteSpace(text) ? "Operación completada." : text
            };
            dlg.Controls.Add(box);
            dlg.ShowDialog(this);
        }

        async Task RunWingetUpgradeAsync()
        {
            if (MessageBox.Show("¿Actualizar silenciosamente todas las aplicaciones compatibles mediante Winget?", "Actualizador de software", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
            await RunCommandVisibleAsync("winget.exe", "upgrade --all --silent --accept-source-agreements --accept-package-agreements", "Actualización de aplicaciones");
        }

        async void BackupDrivers()
        {
            using (var dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Selecciona la carpeta donde exportar la copia de seguridad de tus drivers";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                Directory.CreateDirectory(dialog.SelectedPath);
                await RunCommandVisibleAsync("pnputil.exe", "/export-driver * \"" + dialog.SelectedPath + "\"", "Copia de seguridad de controladores");
            }
        }

        void ShowSmartStatus()
        {
            var result = new StringBuilder();
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Model, Status, Size, MediaType FROM Win32_DiskDrive"))
                {
                    foreach (ManagementObject item in searcher.Get())
                    {
                        double gb = item["Size"] != null ? Convert.ToDouble(item["Size"]) / 1073741824.0 : 0;
                        result.AppendLine(String.Format("Unidad: {0} ({1:0} GB)", item["Model"], gb));
                        result.AppendLine(String.Format("Medio:  {0}   |   Estado S.M.A.R.T.: {1}", item["MediaType"], item["Status"]));
                        result.AppendLine(new string('-', 60));
                    }
                }
            }
            catch (Exception ex) { result.AppendLine("Error consultando WMI: " + ex.Message); }

            ShowTextDialog("Diagnóstico S.M.A.R.T. de unidades", result.ToString());
        }

        void ShredSelectedFile()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Title = "Selecciona el archivo confidencial para destruir (3 pasadas)";
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                string path = dialog.FileName;
                if (MessageBox.Show("¿Destruir de forma irreversible " + Path.GetFileName(path) + " mediante 3 pasadas de sobrescritura?", "Borrado seguro", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                try
                {
                    using (var stream = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None))
                    using (var rng = RandomNumberGenerator.Create())
                    {
                        byte[] buffer = new byte[64 * 1024];
                        for (int pass = 0; pass < 3; pass++)
                        {
                            rng.GetBytes(buffer);
                            stream.Position = 0;
                            long left = stream.Length;
                            while (left > 0)
                            {
                                int count = (int)Math.Min(buffer.Length, left);
                                stream.Write(buffer, 0, count);
                                left -= count;
                            }
                            stream.Flush(true);
                        }
                    }
                    File.Delete(path);
                    MessageBox.Show("Archivo sobrescrito y destruido correctamente.", "OptiGuille");
                }
                catch (Exception ex) { MessageBox.Show("No se pudo destruir el archivo: " + ex.Message, "OptiGuille"); }
            }
        }

        void TryFileRecovery()
        {
            try { Process.Start("ms-windows-store://pdp/?ProductId=9N26S50LN705"); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "OptiGuille"); }
        }

        async void FindDuplicatesAsync()
        {
            using (var folder = new FolderBrowserDialog())
            {
                folder.Description = "Selecciona una carpeta para analizar archivos duplicados por SHA-256";
                if (folder.ShowDialog(this) != DialogResult.OK) return;
                string rootPath = folder.SelectedPath;

                var candidates = await Task.Run(delegate
                {
                    var bySize = new Dictionary<long, List<string>>();
                    try
                    {
                        foreach (string file in Directory.GetFiles(rootPath, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                long s = new FileInfo(file).Length;
                                if (s == 0) continue;
                                if (!bySize.ContainsKey(s)) bySize[s] = new List<string>();
                                bySize[s].Add(file);
                            }
                            catch { }
                        }
                    }
                    catch { }

                    var hashes = new Dictionary<string, List<string>>();
                    using (var sha = SHA256.Create())
                    {
                        foreach (var group in bySize.Values)
                        {
                            if (group.Count > 1)
                            {
                                foreach (string path in group)
                                {
                                    try
                                    {
                                        using (var stream = File.OpenRead(path))
                                        {
                                            string h = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                                            if (!hashes.ContainsKey(h)) hashes[h] = new List<string>();
                                            hashes[h].Add(path);
                                        }
                                    }
                                    catch { }
                                }
                            }
                        }
                    }

                    var list = new List<string>();
                    foreach (var group in hashes.Values)
                        if (group.Count > 1)
                            for (int i = 1; i < group.Count; i++) list.Add(group[i]);
                    return list;
                });

                if (candidates.Count == 0) { MessageBox.Show("No se encontraron archivos duplicados.", "OptiGuille"); return; }

                var dlg = new Form { Text = "Duplicados encontrados (" + candidates.Count + ")", Width = 840, Height = 540, StartPosition = FormStartPosition.CenterParent, BackColor = UiTheme.BgCanvas, ForeColor = UiTheme.TextLight };
                var chkList = new CheckedListBox { Dock = DockStyle.Fill, CheckOnClick = true, BackColor = UiTheme.CardBg, ForeColor = UiTheme.TextLight, BorderStyle = BorderStyle.None, Font = new Font("Segoe UI", 9.5f) };
                foreach (string f in candidates) chkList.Items.Add(f, true);
                var remove = new ModernButton("Mover seleccionados a la Papelera", true) { Dock = DockStyle.Bottom, Height = 44 };
                remove.Click += delegate
                {
                    int c = 0;
                    foreach (object item in chkList.CheckedItems)
                    {
                        try
                        {
                            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(item.ToString(), Microsoft.VisualBasic.FileIO.UIOption.OnlyErrorDialogs, Microsoft.VisualBasic.FileIO.RecycleOption.SendToRecycleBin);
                            c++;
                        }
                        catch { }
                    }
                    MessageBox.Show("Se movieron " + c + " archivos duplicados a la Papelera.", "OptiGuille");
                    dlg.Close();
                };
                dlg.Controls.Add(chkList);
                dlg.Controls.Add(remove);
                dlg.ShowDialog(this);
            }
        }

        async void FindLargeFilesAsync()
        {
            using (var folder = new FolderBrowserDialog())
            {
                folder.Description = "Selecciona una carpeta o unidad para buscar archivos mayores de 250 MB";
                if (folder.ShowDialog(this) != DialogResult.OK) return;
                string selected = folder.SelectedPath;

                var sb = await Task.Run(delegate
                {
                    var text = new StringBuilder();
                    try
                    {
                        foreach (string f in Directory.GetFiles(selected, "*", SearchOption.AllDirectories))
                        {
                            try
                            {
                                long len = new FileInfo(f).Length;
                                if (len >= 250L * 1024 * 1024)
                                    text.AppendLine(String.Format("{0:0.0} MB   |   {1}", len / 1048576.0, f));
                            }
                            catch { }
                        }
                    }
                    catch { }
                    return text.ToString();
                });

                ShowTextDialog("Archivos mayores de 250 MB", String.IsNullOrEmpty(sb) ? "No se encontraron archivos superiores a 250 MB en esa ruta." : sb);
            }
        }

        void CheckGameProcessPlan()
        {
            string name = Path.GetFileNameWithoutExtension(gameProcessInput.Text.Trim());
            bool running = false;
            try { running = Process.GetProcessesByName(name).Length > 0; } catch { }
            if (running && !powerAutoApplied)
            {
                var b = LoadBackup();
                if (String.IsNullOrEmpty(b.PowerScheme))
                {
                    var m = Regex.Match(RunCommand("powercfg.exe", "/getactivescheme"), @"[a-fA-F0-9-]{36}");
                    if (m.Success) b.PowerScheme = m.Value;
                    SaveBackup();
                }
                RunCommand("powercfg.exe", "/setactive SCHEME_MAX");
                powerAutoApplied = true;
            }
            else if (!running && powerAutoApplied)
                RestoreAutomaticPowerPlan();
        }

        void RestoreAutomaticPowerPlan()
        {
            if (!powerAutoApplied) return;
            var b = LoadBackup();
            if (!String.IsNullOrEmpty(b.PowerScheme)) RunCommand("powercfg.exe", "/setactive " + b.PowerScheme);
            powerAutoApplied = false;
        }

        Control BuildComponentCard(ComponentSpec spec)
        {
            var row = new RoundedPanel { Dock = DockStyle.Fill, FillColor = UiTheme.CardBg, Radius = 10, Padding = new Padding(14), Margin = new Padding(0, 0, 0, 10) };

            var lblTitle = new Label
            {
                Text = spec.Title,
                Font = new Font("Segoe UI Semibold", 10f, FontStyle.Bold),
                ForeColor = UiTheme.TextLight,
                BackColor = UiTheme.CardBg,
                Location = new Point(16, 12),
                Size = new Size(row.Width - 165, 22),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            var lblDesc = new Label
            {
                Text = spec.Description,
                ForeColor = UiTheme.TextMuted,
                BackColor = UiTheme.CardBg,
                Font = new Font("Segoe UI", 8.5f),
                Location = new Point(16, 35),
                Size = new Size(row.Width - 165, 34),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };
            row.Controls.Add(lblTitle);
            row.Controls.Add(lblDesc);

            var bars = new FlowLayoutPanel { Location = new Point(16, 72), Size = new Size(140, 10), WrapContents = false, BackColor = UiTheme.CardBg };
            for (int i = 1; i <= 5; i++)
                bars.Controls.Add(new Panel { Width = 18, Height = 5, Margin = new Padding(0, 0, 4, 0), BackColor = i <= spec.Impact ? UiTheme.Mint : UiTheme.TrackDark });
            row.Controls.Add(bars);

            string st = ReadComponentState(spec);
            var toggle = new ModernButton(st == "Desactivado" ? "Activar" : "Desactivar")
            {
                Size = new Size(124, 34),
                Location = new Point(row.Width - 140, 16),
                BackColor = UiTheme.CardBg,
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            var status = new Label
            {
                Text = st,
                ForeColor = st == "Desactivado" ? UiTheme.Amber : UiTheme.Mint,
                BackColor = UiTheme.CardBg,
                Font = new Font("Segoe UI Semibold", 8f, FontStyle.Bold),
                AutoSize = false,
                Width = 124,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                Location = new Point(row.Width - 140, 56),
                Anchor = AnchorStyles.Top | AnchorStyles.Right
            };
            toggle.Click += delegate { ToggleComponent(spec, status, toggle); };
            row.Controls.Add(toggle);
            row.Controls.Add(status);
            return row;
        }

        AppBackup LoadBackup()
        {
            if (backup != null) return backup;
            if (File.Exists(backupPath))
            {
                try
                {
                    using (var stream = File.OpenRead(backupPath))
                        backup = (AppBackup)new DataContractJsonSerializer(typeof(AppBackup)).ReadObject(stream);
                }
                catch { }
            }
            if (backup == null) backup = new AppBackup();
            if (backup.Registry == null) backup.Registry = new Dictionary<string, RegistrySnapshot>();
            if (backup.Services == null) backup.Services = new Dictionary<string, List<ServiceSnapshot>>();
            return backup;
        }

        void SaveBackup()
        {
            using (var stream = File.Create(backupPath))
                new DataContractJsonSerializer(typeof(AppBackup)).WriteObject(stream, LoadBackup());
        }

        RegistrySnapshot ReadDword(string keyPath, string name)
        {
            using (var key = Registry.CurrentUser.OpenSubKey(keyPath, false))
            {
                if (key != null && key.GetValue(name) != null)
                    return new RegistrySnapshot { Exists = true, Value = Convert.ToInt32(key.GetValue(name)) };
            }
            return new RegistrySnapshot { Exists = false, Value = 0 };
        }

        void CaptureDword(string keyPath, string name, string id)
        {
            var b = LoadBackup();
            if (!b.Registry.ContainsKey(id)) { b.Registry[id] = ReadDword(keyPath, name); SaveBackup(); }
        }

        void CaptureHklmDword(string keyPath, string name, string id)
        {
            var b = LoadBackup();
            if (!b.Registry.ContainsKey(id))
            {
                using (var key = Registry.LocalMachine.OpenSubKey(keyPath, false))
                {
                    if (key != null && key.GetValue(name) != null)
                        b.Registry[id] = new RegistrySnapshot { Exists = true, Value = Convert.ToInt32(key.GetValue(name)) };
                    else
                        b.Registry[id] = new RegistrySnapshot { Exists = false, Value = 0 };
                }
                SaveBackup();
            }
        }

        void WriteDword(string keyPath, string name, int value)
        {
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath))
                key.SetValue(name, value, RegistryValueKind.DWord);
        }

        void ApplyGameOptions(bool game, bool dvr, bool power)
        {
            try
            {
                if (game)
                {
                    CaptureDword(GameBar, "AutoGameModeEnabled", "AutoGameModeEnabled");
                    CaptureDword(GameBar, "AllowAutoGameMode", "AllowAutoGameMode");
                    WriteDword(GameBar, "AutoGameModeEnabled", 1);
                    WriteDword(GameBar, "AllowAutoGameMode", 1);
                }
                if (dvr)
                {
                    CaptureDword(GameBar, "ShowStartupPanel", "ShowStartupPanel");
                    CaptureDword(GameConfig, "GameDVR_Enabled", "GameDVR_Enabled");
                    WriteDword(GameConfig, "GameDVR_Enabled", 0);
                    WriteDword(GameBar, "ShowStartupPanel", 0);
                }
                if (power)
                {
                    var b = LoadBackup();
                    if (String.IsNullOrEmpty(b.PowerScheme))
                    {
                        var output = RunCommand("powercfg.exe", "/getactivescheme");
                        var match = Regex.Match(output, @"[a-fA-F0-9-]{36}");
                        if (match.Success) b.PowerScheme = match.Value;
                        SaveBackup();
                    }
                    RunCommand("powercfg.exe", "/setactive SCHEME_MAX");
                }
                if (restoreStatus != null)
                    restoreStatus.Text = "Ajustes aplicados y respaldados.";
                MessageBox.Show("Ajustes de rendimiento aplicados correctamente.", "OptiGuille", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex) { MessageBox.Show("No se pudieron aplicar todos los ajustes: " + ex.Message, "OptiGuille", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }

        string RunCommand(string file, string args)
        {
            try
            {
                var start = new ProcessStartInfo(file, args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
                using (var process = Process.Start(start))
                {
                    var outputTask = process.StandardOutput.ReadToEndAsync();
                    var errTask = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    return outputTask.Result + errTask.Result;
                }
            }
            catch (Exception ex) { return ex.Message; }
        }

        string ReadComponentState(ComponentSpec spec)
        {
            int count = 0, disabled = 0, running = 0;
            foreach (string name in spec.Services)
            {
                try
                {
                    int start = 4;
                    using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name, false))
                    {
                        if (key == null) continue;
                        object value = key.GetValue("Start");
                        if (value != null) start = Convert.ToInt32(value);
                    }
                    count++;
                    if (start == 4) disabled++;
                    using (var service = new ServiceController(name))
                    {
                        try { if (service.Status == ServiceControllerStatus.Running) running++; } catch { }
                    }
                }
                catch { }
            }
            if (count == 0) return "No disponible";
            if (disabled == count) return "Desactivado";
            if (running > 0) return "Activo";
            return "Bajo demanda";
        }

        void ToggleComponent(ComponentSpec spec, Label status, Button button)
        {
            string current = ReadComponentState(spec);
            if (current == "No disponible") { MessageBox.Show("Este servicio no está presente en esta edición de Windows.", "OptiGuille"); return; }
            bool enable = current == "Desactivado";
            string prompt = enable ? "¿Activar " + spec.Title + "?" : "¿Desactivar " + spec.Title + "?\r\n\r\n" + spec.Warning;
            if (MessageBox.Show(prompt, "Confirmar cambio", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;

            try
            {
                var b = LoadBackup();
                if (!enable && !b.Services.ContainsKey(spec.Id))
                {
                    var snapshots = new List<ServiceSnapshot>();
                    foreach (string name in spec.Services)
                    {
                        try
                        {
                            int start; bool running = false;
                            using (var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\" + name, false))
                            {
                                if (key == null) continue;
                                start = Convert.ToInt32(key.GetValue("Start", 3));
                            }
                            using (var service = new ServiceController(name))
                            {
                                try { running = service.Status == ServiceControllerStatus.Running; } catch { }
                            }
                            snapshots.Add(new ServiceSnapshot { Name = name, StartMode = start, WasRunning = running });
                        }
                        catch { }
                    }
                    b.Services[spec.Id] = snapshots;
                    SaveBackup();
                }

                foreach (string name in spec.Services)
                {
                    using (var service = new ServiceController(name))
                    {
                        if (enable)
                        {
                            RunCommand("sc.exe", "config \"" + name + "\" start= demand");
                            try { service.Start(); service.WaitForStatus(ServiceControllerStatus.Running, TimeSpan.FromSeconds(6)); } catch { }
                        }
                        else
                        {
                            try { if (service.Status == ServiceControllerStatus.Running) { service.Stop(); service.WaitForStatus(ServiceControllerStatus.Stopped, TimeSpan.FromSeconds(6)); } } catch { }
                            RunCommand("sc.exe", "config \"" + name + "\" start= disabled");
                        }
                    }
                }
                string st = ReadComponentState(spec);
                status.Text = st;
                status.ForeColor = st == "Desactivado" ? UiTheme.Amber : UiTheme.Mint;
                button.Text = st == "Desactivado" ? "Activar" : "Desactivar";
            }
            catch (Exception ex) { MessageBox.Show("No se pudo cambiar el servicio: " + ex.Message, "OptiGuille"); }
        }

        void RestoreAll()
        {
            try
            {
                var b = LoadBackup();
                foreach (var item in b.Registry)
                {
                    if (item.Key == "NetThrottling" || item.Key == "SysResponsiveness")
                    {
                        string valName = item.Key == "NetThrottling" ? "NetworkThrottlingIndex" : "SystemResponsiveness";
                        using (var key = Registry.LocalMachine.OpenSubKey(MultimediaProfile, true))
                        {
                            if (key != null && item.Value.Exists) key.SetValue(valName, item.Value.Value, RegistryValueKind.DWord);
                        }
                        continue;
                    }
                    string path = item.Key == "GameDVR_Enabled" ? GameConfig : GameBar;
                    if (item.Value.Exists) WriteDword(path, item.Key, item.Value.Value);
                    else using (var key = Registry.CurrentUser.OpenSubKey(path, true)) { if (key != null) key.DeleteValue(item.Key, false); }
                }

                foreach (var group in b.Services.Values)
                {
                    foreach (var saved in group)
                    {
                        string startup = saved.StartMode == 2 ? "auto" : saved.StartMode == 4 ? "disabled" : "demand";
                        RunCommand("sc.exe", "config \"" + saved.Name + "\" start= " + startup);
                        using (var service = new ServiceController(saved.Name))
                        {
                            try
                            {
                                if (saved.WasRunning && service.Status != ServiceControllerStatus.Running) service.Start();
                                else if (!saved.WasRunning && service.Status == ServiceControllerStatus.Running) service.Stop();
                            }
                            catch { }
                        }
                    }
                }

                SetWuDriverBlock(false);
                if (nvBlockWuToggle != null) nvBlockWuToggle.Checked = false;

                if (!String.IsNullOrEmpty(b.PowerScheme)) RunCommand("powercfg.exe", "/setactive " + b.PowerScheme);
                if (powerPlanTimer != null) powerPlanTimer.Stop();
                powerAutoApplied = false;
                if (autoPowerToggle != null) autoPowerToggle.Checked = false;
                if (File.Exists(backupPath)) File.Delete(backupPath);
                backup = null;
                if (restoreStatus != null) restoreStatus.Text = "Valores originales restaurados.";
                MessageBox.Show("Ajustes, red, políticas de Windows Update, plan de energía y servicios restaurados.", "OptiGuille");
            }
            catch (Exception ex)
            {
                if (restoreStatus != null) restoreStatus.Text = "Error en restauración: " + ex.Message;
                MessageBox.Show("Error al restaurar: " + ex.Message, "OptiGuille");
            }
        }
    }

    static class Program
    {
        [DllImport("user32.dll")]
        static extern bool SetProcessDPIAware();

        [STAThread]
        static void Main()
        {
            try { SetProcessDPIAware(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}