using System.Runtime.InteropServices;
using Framecalm.Core;
using Microsoft.Win32;

namespace Framecalm.Discovery;

/// <summary>Reads hardware from the registry and documented Win32 calls. No drivers, no WMI, nothing installed.</summary>
public sealed class HardwareProbe : IHardwareProbe
{
    public HardwareInfo Probe()
    {
        var cpu = (Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", null) as string)?.Trim() ?? "Unknown CPU";
        var mem = new MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>() };
        GlobalMemoryStatusEx(ref mem);
        GetSystemPowerStatus(out var power);
        var dm = new DEVMODE { dmSize = (short)Marshal.SizeOf<DEVMODE>() };
        EnumDisplaySettings(null, -1, ref dm);
        return new HardwareInfo
        {
            Cpu = cpu,
            Threads = Environment.ProcessorCount,
            RamBytes = (long)mem.ullTotalPhys,
            Gpus = Gpus(),
            IsLaptop = power.BatteryFlag != 128 && power.BatteryFlag != 255,
            RefreshHz = dm.dmDisplayFrequency > 1 ? dm.dmDisplayFrequency : 60,
            ScreenWidth = dm.dmPelsWidth,
            ScreenHeight = dm.dmPelsHeight,
        };
    }

    const string DisplayClass = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}";

    /// <summary>Physical display adapters from the driver store (skips virtual/remote adapters).</summary>
    public static List<GpuInfo> Gpus()
    {
        var list = new List<GpuInfo>();
        using var cls = Registry.LocalMachine.OpenSubKey(DisplayClass);
        if (cls is null) return list;
        foreach (var sub in cls.GetSubKeyNames().Where(n => n.Length == 4 && n.All(char.IsDigit)))
        {
            using var k = cls.OpenSubKey(sub);
            if (k is null) continue;
            var dev = (k.GetValue("MatchingDeviceId") as string ?? "").ToLowerInvariant();
            if (!dev.StartsWith("pci\\ven_")) continue;
            var name = k.GetValue("DriverDesc") as string ?? "GPU";
            var vendor = dev[8..12] switch { "10de" => "NVIDIA", "1002" => "AMD", "8086" => "Intel", var v => v };
            long vram = k.GetValue("HardwareInformation.qwMemorySize") switch
            {
                long l => l,
                byte[] b when b.Length >= 8 => BitConverter.ToInt64(b),
                byte[] b when b.Length >= 4 => BitConverter.ToUInt32(b),
                int i => (uint)i,
                _ => k.GetValue("HardwareInformation.MemorySize") switch { int i => (uint)i, byte[] b when b.Length >= 4 => BitConverter.ToUInt32(b), _ => 0 },
            };
            var kind = Classify(vendor, name, vram);
            DateTime? date = null;
            if (k.GetValue("DriverDate") is string ds && DateTime.TryParseExact(ds, ["M-d-yyyy", "MM-dd-yyyy"], null, System.Globalization.DateTimeStyles.None, out var d)) date = d;
            if (list.Any(g => g.Name == name)) continue;
            list.Add(new GpuInfo(name, vendor, vram, kind, k.GetValue("DriverVersion") as string, date));
        }
        return list.OrderBy(g => g.Kind == GpuKind.Dedicated ? 0 : 1).ToList();
    }

    public static GpuKind Classify(string vendor, string name, long vram)
    {
        var n = name.ToLowerInvariant();
        if (vendor == "NVIDIA") return GpuKind.Dedicated;
        if (vendor == "Intel") return n.Contains("arc a") || n.Contains("arc b") || vram >= 2L << 30 ? GpuKind.Dedicated : GpuKind.Integrated;
        if (vendor == "AMD")
        {
            if (n.Contains("rx ") || n.Contains("radeon pro")) return GpuKind.Dedicated;
            return vram >= 2L << 30 ? GpuKind.Dedicated : GpuKind.Integrated;
        }
        return GpuKind.Unknown;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MEMORYSTATUSEX
    {
        public uint dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct SYSTEM_POWER_STATUS
    {
        public byte ACLineStatus, BatteryFlag, BatteryLifePercent, SystemStatusFlag;
        public int BatteryLifeTime, BatteryFullLifeTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct DEVMODE
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
        public short dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
        public int dmFields, dmPositionX, dmPositionY, dmDisplayOrientation, dmDisplayFixedOutput;
        public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
        public short dmLogPixels;
        public int dmBitsPerPel, dmPelsWidth, dmPelsHeight, dmDisplayFlags, dmDisplayFrequency;
        public int dmICMMethod, dmICMIntent, dmMediaType, dmDitherType, dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
    }

    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX m);
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out SYSTEM_POWER_STATUS s);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern bool EnumDisplaySettings(string? device, int mode, ref DEVMODE dm);
}
