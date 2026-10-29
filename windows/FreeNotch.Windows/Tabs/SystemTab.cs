using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace FreeNotch;

public sealed class SystemTab : IslandTab
{
    private static readonly Dictionary<string, Dictionary<string, string>> Text = new()
    {
        ["tr"] = new()
        {
            ["System"] = "Sistem",
            ["CPU"] = "İşlemci",
            ["Memory"] = "Bellek",
            ["Battery"] = "Pil",
            ["Disk"] = "Disk",
            ["Charging"] = "Şarj oluyor",
            ["On battery"] = "Pilde",
            ["Plugged in"] = "Fişe takılı",
            ["No battery"] = "Pil yok",
            ["GB free of {0} GB"] = "GB boş / {0} GB",
            ["Up {0}"] = "Açık kalma süresi {0}",
            ["{0} processes"] = "{0} işlem",
        },
        ["de"] = new()
        {
            ["System"] = "System",
            ["CPU"] = "CPU",
            ["Memory"] = "Speicher",
            ["Battery"] = "Akku",
            ["Disk"] = "Datenträger",
            ["Charging"] = "Lädt",
            ["On battery"] = "Akkubetrieb",
            ["Plugged in"] = "Angeschlossen",
            ["No battery"] = "Kein Akku",
            ["GB free of {0} GB"] = "GB frei von {0} GB",
            ["Up {0}"] = "Laufzeit {0}",
            ["{0} processes"] = "{0} Prozesse",
        },
        ["es"] = new()
        {
            ["System"] = "Sistema",
            ["CPU"] = "CPU",
            ["Memory"] = "Memoria",
            ["Battery"] = "Batería",
            ["Disk"] = "Disco",
            ["Charging"] = "Cargando",
            ["On battery"] = "Con batería",
            ["Plugged in"] = "Conectado",
            ["No battery"] = "Sin batería",
            ["GB free of {0} GB"] = "GB libres de {0} GB",
            ["Up {0}"] = "Activo {0}",
            ["{0} processes"] = "{0} procesos",
        },
        ["fr"] = new()
        {
            ["System"] = "Système",
            ["CPU"] = "CPU",
            ["Memory"] = "Mémoire",
            ["Battery"] = "Batterie",
            ["Disk"] = "Disque",
            ["Charging"] = "En charge",
            ["On battery"] = "Sur batterie",
            ["Plugged in"] = "Branché",
            ["No battery"] = "Pas de batterie",
            ["GB free of {0} GB"] = "Go libres sur {0} Go",
            ["Up {0}"] = "Actif depuis {0}",
            ["{0} processes"] = "{0} processus",
        },
    };

    private static string T(string key) => Strings.Pick(Text, key);

    [StructLayout(LayoutKind.Sequential)]
    private struct FileTimeValue
    {
        public uint Low;
        public uint High;

        public ulong Ticks => ((ulong)High << 32) | Low;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MemoryStatus
    {
        public uint Length;
        public uint MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PowerStatus
    {
        public byte AcLineStatus;
        public byte BatteryFlag;
        public byte BatteryLifePercent;
        public byte SystemStatusFlag;
        public uint BatteryLifeTime;
        public uint BatteryFullLifeTime;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemTimes(out FileTimeValue idle, out FileTimeValue kernel, out FileTimeValue user);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MemoryStatus status);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetSystemPowerStatus(out PowerStatus status);

    private sealed class StatBlock
    {
        private readonly TextBlock number;
        private readonly TextBlock detail;
        private readonly ColumnDefinition fillColumn = new();
        private readonly ColumnDefinition restColumn = new();

        public StatBlock(string label)
        {
            number = Ui.Text("–", 26, FontWeights.Bold);
            detail = Ui.Text(" ", 11, FontWeights.Normal, 130);

            var meter = new Grid { Height = 4, Margin = new Thickness(0, 6, 0, 0) };
            meter.ColumnDefinitions.Add(fillColumn);
            meter.ColumnDefinitions.Add(restColumn);
            meter.Children.Add(new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = Ui.White(40),
            });
            var fill = new Border
            {
                CornerRadius = new CornerRadius(2),
                Background = Ui.White(230),
            };
            Grid.SetColumn(fill, 0);
            meter.Children.Add(fill);
            SetFraction(0);

            Root = new StackPanel { Spacing = 0 };
            Root.Children.Add(Ui.Text(label, 11, FontWeights.SemiBold, 150));
            Root.Children.Add(number);
            Root.Children.Add(detail);
            Root.Children.Add(meter);
        }

        public StackPanel Root { get; }

        public void Set(string bigText, string detailText, double fraction)
        {
            number.Text = bigText;
            detail.Text = detailText;
            SetFraction(fraction);
        }

        private void SetFraction(double fraction)
        {
            var clamped = Math.Clamp(double.IsNaN(fraction) ? 0 : fraction, 0, 1);
            fillColumn.Width = new GridLength(clamped, GridUnitType.Star);
            restColumn.Width = new GridLength(1 - clamped, GridUnitType.Star);
        }
    }

    private readonly StatBlock cpuBlock;
    private readonly StatBlock memoryBlock;
    private readonly StatBlock batteryBlock;
    private readonly StatBlock diskBlock;
    private readonly TextBlock uptimeText;
    private readonly TextBlock processText;
    private readonly string systemRoot = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
    private DispatcherQueueTimer? timer;
    private bool hasPreviousTimes;
    private ulong previousIdle;
    private ulong previousTotal;
    private int tickCount;

    public SystemTab(TabContext context) : base(context)
    {
        Padding = new Thickness(22, 4, 22, 6);
        RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        cpuBlock = new StatBlock(T("CPU"));
        memoryBlock = new StatBlock(T("Memory"));
        batteryBlock = new StatBlock(T("Battery"));
        diskBlock = new StatBlock(T("Disk"));

        var blocks = new[] { cpuBlock, memoryBlock, batteryBlock, diskBlock };
        var row = new Grid { ColumnSpacing = 16, VerticalAlignment = VerticalAlignment.Center };
        for (var index = 0; index < blocks.Length; index++)
        {
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            Grid.SetColumn(blocks[index].Root, index);
            row.Children.Add(blocks[index].Root);
        }
        Grid.SetRow(row, 0);
        Children.Add(row);

        uptimeText = Ui.Text(" ", 12, FontWeights.Normal, 150);
        processText = Ui.Text(" ", 12, FontWeights.Normal, 150);
        var footer = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        footer.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        processText.HorizontalAlignment = HorizontalAlignment.Right;
        Grid.SetColumn(processText, 1);
        footer.Children.Add(uptimeText);
        footer.Children.Add(processText);
        Grid.SetRow(footer, 1);
        Children.Add(footer);
    }

    public override string Id => "system";
    public override string Glyph => "\uE950";
    public override string Title => T("System");
    public override double ContentHeight => 150;

    public override void OnShown()
    {
        hasPreviousTimes = false;
        tickCount = 0;
        Refresh();
        if (timer is null)
        {
            timer = Context.Queue.CreateTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += (_, _) => Refresh();
        }
        timer.Start();
    }

    public override void OnHidden()
    {
        timer?.Stop();
    }

    private void Refresh()
    {
        RefreshCpu();
        RefreshMemory();
        RefreshBattery();
        if (tickCount % 5 == 0)
        {
            RefreshDisk();
            RefreshProcesses();
        }
        RefreshUptime();
        tickCount++;
    }

    private void RefreshCpu()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return;
        var idleTicks = idle.Ticks;
        var totalTicks = kernel.Ticks + user.Ticks;
        if (hasPreviousTimes)
        {
            var totalDelta = totalTicks - previousTotal;
            var idleDelta = idleTicks - previousIdle;
            if (totalDelta > 0)
            {
                var load = 1.0 - (double)idleDelta / totalDelta;
                load = Math.Clamp(load, 0, 1);
                cpuBlock.Set(Percent(load), " ", load);
            }
        }
        previousIdle = idleTicks;
        previousTotal = totalTicks;
        hasPreviousTimes = true;
    }

    private void RefreshMemory()
    {
        var status = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        if (!GlobalMemoryStatusEx(ref status) || status.TotalPhys == 0) return;
        var used = status.TotalPhys - status.AvailPhys;
        var fraction = (double)used / status.TotalPhys;
        var detail = Gigabytes(used) + " / " + Gigabytes(status.TotalPhys) + " GB";
        memoryBlock.Set(Percent(fraction), detail, fraction);
    }

    private void RefreshBattery()
    {
        if (!GetSystemPowerStatus(out var power) || power.BatteryFlag == 128 || power.BatteryLifePercent > 100)
        {
            batteryBlock.Set("–", T("No battery"), 0);
            return;
        }
        var fraction = power.BatteryLifePercent / 100.0;
        string state;
        if ((power.BatteryFlag & 8) != 0) state = T("Charging");
        else if (power.AcLineStatus == 1) state = T("Plugged in");
        else state = T("On battery");
        batteryBlock.Set(Percent(fraction), state, fraction);
    }

    private void RefreshDisk()
    {
        try
        {
            var drive = new DriveInfo(systemRoot);
            var free = drive.AvailableFreeSpace;
            var total = drive.TotalSize;
            if (total <= 0) return;
            var usedFraction = 1.0 - (double)free / total;
            var freeText = ((double)free / 1073741824.0).ToString("0", CultureInfo.CurrentCulture);
            diskBlock.Set(freeText, string.Format(CultureInfo.CurrentCulture, T("GB free of {0} GB"), Gigabytes((ulong)total)), usedFraction);
        }
        catch (Exception)
        {
            diskBlock.Set("–", string.Empty, 0);
        }
    }

    private void RefreshProcesses()
    {
        try
        {
            var processes = Process.GetProcesses();
            var count = processes.Length;
            foreach (var process in processes) process.Dispose();
            processText.Text = string.Format(CultureInfo.CurrentCulture, T("{0} processes"), count);
        }
        catch (Exception)
        {
            processText.Text = string.Empty;
        }
    }

    private void RefreshUptime()
    {
        var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
        string value;
        if (uptime.TotalDays >= 1) value = (int)uptime.TotalDays + " d " + uptime.Hours + " h";
        else if (uptime.TotalHours >= 1) value = uptime.Hours + " h " + uptime.Minutes + " min";
        else value = uptime.Minutes + " min";
        uptimeText.Text = string.Format(CultureInfo.CurrentCulture, T("Up {0}"), value);
    }

    private static string Percent(double fraction) =>
        Math.Round(fraction * 100).ToString("0", CultureInfo.CurrentCulture) + "%";

    private static string Gigabytes(ulong bytes) =>
        (bytes / 1073741824.0).ToString("0.#", CultureInfo.CurrentCulture);
}
