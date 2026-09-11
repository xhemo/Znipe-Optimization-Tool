using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    public partial class MainWindowLogic {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool GetPhysicallyInstalledSystemMemory(out ulong totalMemoryInKilobytes);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplayDevices(string lpDevice, uint iDevNum, ref DISPLAY_DEVICE lpDisplayDevice, uint dwFlags);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DISPLAY_DEVICE {
            public int cb;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string DeviceName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceString;
            public int StateFlags;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string DeviceKey;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DEVMODE devMode);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DEVMODE {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string dmFormName;
            public short dmLogPixels;
            public short dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, ref DEVMODE lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Ansi, SetLastError = true)]
        private static extern int ChangeDisplaySettingsEx(string lpszDeviceName, IntPtr lpDevMode, IntPtr hwnd, uint dwflags, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct LUID {
            public uint LowPart;
            public int HighPart;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_RATIONAL {
            public uint Numerator;
            public uint Denominator;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_SOURCE_INFO {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_TARGET_INFO {
            public LUID adapterId;
            public uint id;
            public uint modeInfoIdx;
            public uint outputTechnology;
            public uint rotation;
            public uint scaling;
            public DISPLAYCONFIG_RATIONAL refreshRate;
            public uint scanLineOrdering;
            public bool targetAvailable;
            public uint statusFlags;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_PATH_INFO {
            public DISPLAYCONFIG_PATH_SOURCE_INFO sourceInfo;
            public DISPLAYCONFIG_PATH_TARGET_INFO targetInfo;
            public uint flags;
        }

        private enum DISPLAYCONFIG_MODE_INFO_TYPE : uint {
            SOURCE = 1,
            TARGET = 2,
            DESKTOP_IMAGE = 3
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINTL {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_SOURCE_MODE {
            public uint width;
            public uint height;
            public uint pixelFormat;
            public POINTL position;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_VIDEO_SIGNAL_INFO {
            public ulong pixelRate;
            public DISPLAYCONFIG_RATIONAL hSyncFreq;
            public DISPLAYCONFIG_RATIONAL vSyncFreq;
            public uint activeSizeX;
            public uint activeSizeY;
            public uint totalSizeX;
            public uint totalSizeY;
            public uint videoStandard;
            public uint scanLineOrdering;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_TARGET_MODE {
            public DISPLAYCONFIG_VIDEO_SIGNAL_INFO targetVideoSignalInfo;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct DISPLAYCONFIG_MODE_INFO_UNION {
            [FieldOffset(0)]
            public DISPLAYCONFIG_TARGET_MODE targetMode;
            [FieldOffset(0)]
            public DISPLAYCONFIG_SOURCE_MODE sourceMode;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_MODE_INFO {
            public DISPLAYCONFIG_MODE_INFO_TYPE infoType;
            public uint id;
            public LUID adapterId;
            public DISPLAYCONFIG_MODE_INFO_UNION modeInfo;
        }

        private enum DISPLAYCONFIG_DEVICE_INFO_TYPE : uint {
            GET_SOURCE_NAME = 1,
            GET_TARGET_NAME = 2
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct DISPLAYCONFIG_DEVICE_INFO_HEADER {
            public DISPLAYCONFIG_DEVICE_INFO_TYPE type;
            public uint size;
            public LUID adapterId;
            public uint id;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct DISPLAYCONFIG_SOURCE_DEVICE_NAME {
            public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
            public string viewGdiDeviceName;
        }

        [DllImport("user32.dll")]
        private static extern int GetDisplayConfigBufferSizes(uint flags, out uint numPathArrayElements, out uint numModeInfoArrayElements);

        [DllImport("user32.dll")]
        private static extern int QueryDisplayConfig(uint flags, ref uint numPathArrayElements, [Out] DISPLAYCONFIG_PATH_INFO[] pathInfoArray, ref uint numModeInfoArrayElements, [Out] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, IntPtr currentTopologyId);

        [DllImport("user32.dll")]
        private static extern int DisplayConfigGetDeviceInfo(ref DISPLAYCONFIG_SOURCE_DEVICE_NAME requestPacket);

        [DllImport("user32.dll")]
        private static extern int SetDisplayConfig(uint numPathArrayElements, [In] DISPLAYCONFIG_PATH_INFO[] pathArray, uint numModeInfoArrayElements, [In] DISPLAYCONFIG_MODE_INFO[] modeInfoArray, uint flags);

        private const uint QDC_ONLY_ACTIVE_PATHS = 0x00000002;
        private const uint SDC_APPLY = 0x00000080;
        private const uint SDC_USE_SUPPLIED_DISPLAY_CONFIG = 0x00000020;
        private const uint SDC_ALLOW_CHANGES = 0x00000400;

        public static bool ApplyMonitorRefreshRate(string deviceName, int targetHz, int width, int height) {
            try {
                // Method 1: ChangeDisplaySettingsEx
                var dm = new DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(DEVMODE));
                bool foundMode = false;
                for (int m = 0; EnumDisplaySettings(deviceName, m, ref dm); m++) {
                    if (dm.dmPelsWidth == width && dm.dmPelsHeight == height && dm.dmDisplayFrequency == targetHz) {
                        foundMode = true;
                        break;
                    }
                }
                if (!foundMode) {
                    if (EnumDisplaySettings(deviceName, -1, ref dm)) {
                        dm.dmDisplayFrequency = targetHz;
                        dm.dmFields |= 0x00400000; // DM_DISPLAYFREQUENCY
                        foundMode = true;
                    }
                }

                if (foundMode) {
                    int r1 = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, 0x00000001 /* CDS_UPDATEREGISTRY */, IntPtr.Zero);
                    if (r1 == 0) return true;

                    int r2 = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, 0x10000001 /* CDS_UPDATEREGISTRY | CDS_NORESET */, IntPtr.Zero);
                    if (r2 == 0) {
                        ChangeDisplaySettingsEx(null, IntPtr.Zero, IntPtr.Zero, 0, IntPtr.Zero);
                        return true;
                    }

                    int r0 = ChangeDisplaySettingsEx(deviceName, ref dm, IntPtr.Zero, 0, IntPtr.Zero);
                    if (r0 == 0) return true;
                }

                // Method 2: SetDisplayConfig (CCD API)
                uint pathCount, modeCount;
                if (GetDisplayConfigBufferSizes(QDC_ONLY_ACTIVE_PATHS, out pathCount, out modeCount) == 0 && pathCount > 0) {
                    var paths = new DISPLAYCONFIG_PATH_INFO[pathCount];
                    var modes = new DISPLAYCONFIG_MODE_INFO[modeCount];
                    if (QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS, ref pathCount, paths, ref modeCount, modes, IntPtr.Zero) == 0) {
                        for (int i = 0; i < pathCount; i++) {
                            var sName = new DISPLAYCONFIG_SOURCE_DEVICE_NAME();
                            sName.header.type = DISPLAYCONFIG_DEVICE_INFO_TYPE.GET_SOURCE_NAME;
                            sName.header.size = (uint)Marshal.SizeOf(sName);
                            sName.header.adapterId = paths[i].sourceInfo.adapterId;
                            sName.header.id = paths[i].sourceInfo.id;
                            if (DisplayConfigGetDeviceInfo(ref sName) == 0 && string.Equals(sName.viewGdiDeviceName, deviceName, StringComparison.OrdinalIgnoreCase)) {
                                paths[i].flags |= 0x00000001;
                                paths[i].targetInfo.refreshRate.Numerator = (uint)targetHz;
                                paths[i].targetInfo.refreshRate.Denominator = 1;
                                uint modeIdx = paths[i].targetInfo.modeInfoIdx;
                                if (modeIdx < modeCount) {
                                    modes[modeIdx].modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Numerator = (uint)targetHz;
                                    modes[modeIdx].modeInfo.targetMode.targetVideoSignalInfo.vSyncFreq.Denominator = 1;
                                }
                                int ccdRes = SetDisplayConfig(pathCount, paths, modeCount, modes, SDC_APPLY | SDC_USE_SUPPLIED_DISPLAY_CONFIG | SDC_ALLOW_CHANGES);
                                if (ccdRes == 0) return true;
                            }
                        }
                    }
                }
            } catch {}
            return false;
        }

        private static string ResolveManufacturer(string pnpId) {
            if (string.IsNullOrEmpty(pnpId)) return "";
            string code = pnpId.ToUpperInvariant().Trim();
            switch (code) {
                case "AUS":
                case "ASU":
                case "ACI":
                    return "ASUS";
                case "ACR":
                    return "Acer";
                case "AOC":
                    return "AOC";
                case "BNQ":
                    return "BenQ";
                case "COR":
                    return "Corsair";
                case "DEL":
                    return "Dell";
                case "EIZ":
                    return "Eizo";
                case "EVG":
                    return "EVGA";
                case "GBT":
                case "GIG":
                    return "Gigabyte";
                case "GSM":
                case "LGD":
                    return "LG";
                case "HWP":
                case "HPN":
                    return "HP";
                case "IIY":
                    return "Iiyama";
                case "LEN":
                    return "Lenovo";
                case "MSI":
                    return "MSI";
                case "NEC":
                    return "NEC";
                case "PHL":
                    return "Philips";
                case "SAM":
                case "SEC":
                    return "Samsung";
                case "SNY":
                    return "Sony";
                case "VSC":
                    return "ViewSonic";
                case "ZOW":
                    return "ZOWIE";
                case "APP":
                case "APL":
                    return "Apple";
                case "HUA":
                    return "Huawei";
                case "XMM":
                    return "Xiaomi";
                case "MED":
                    return "Medion";
                default:
                    return code;
            }
        }

        private static System.Collections.Generic.Dictionary<string, string> GetMonitorEdidNames() {
            var dict = new System.Collections.Generic.Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            try {
                using (var displayKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\DISPLAY")) {
                    if (displayKey != null) {
                        foreach (var model in displayKey.GetSubKeyNames()) {
                            using (var modelKey = displayKey.OpenSubKey(model)) {
                                if (modelKey != null) {
                                    foreach (var inst in modelKey.GetSubKeyNames()) {
                                        using (var devParams = modelKey.OpenSubKey(inst + @"\Device Parameters")) {
                                            if (devParams != null) {
                                                byte[] edid = devParams.GetValue("EDID") as byte[];
                                                if (edid != null && edid.Length >= 128) {
                                                    string mfg = "";
                                                    try {
                                                        int b8 = edid[8];
                                                        int b9 = edid[9];
                                                        char c1 = (char)('A' + ((b8 >> 2) & 0x1F) - 1);
                                                        char c2 = (char)('A' + (((b8 & 0x03) << 3) | ((b9 >> 5) & 0x07)) - 1);
                                                        char c3 = (char)('A' + (b9 & 0x1F) - 1);
                                                        if (c1 >= 'A' && c1 <= 'Z' && c2 >= 'A' && c2 <= 'Z' && c3 >= 'A' && c3 <= 'Z') {
                                                            mfg = ResolveManufacturer(new string(new char[] { c1, c2, c3 }));
                                                        }
                                                    } catch {}

                                                    if (string.IsNullOrEmpty(mfg) && model.Length >= 3) {
                                                        mfg = ResolveManufacturer(model.Substring(0, 3));
                                                    }

                                                    for (int i = 54; i <= 108; i += 18) {
                                                        if (edid[i] == 0 && edid[i + 1] == 0 && edid[i + 2] == 0 && edid[i + 3] == 0xFC) {
                                                            string name = System.Text.Encoding.ASCII.GetString(edid, i + 5, 13).Trim('\0', ' ', '\r', '\n');
                                                            if (!string.IsNullOrEmpty(name)) {
                                                                if (!string.IsNullOrEmpty(mfg) && !name.ToUpperInvariant().Contains(mfg.ToUpperInvariant())) {
                                                                    name = mfg + " " + name;
                                                                }
                                                                dict[model] = name;
                                                                dict[model + @"\" + inst] = name;
                                                            }
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } catch {}
            return dict;
        }

        // =========================================================================
        // DEUTSCHES DATUMSFORMAT HELPER (DD.MM.YYYY)
        // =========================================================================
        public static string FormatGermanDate(string input) {
            if (string.IsNullOrWhiteSpace(input)) return "Unbekannt";
            string s = input.Trim();
            DateTime dt;
            if (DateTime.TryParseExact(s.Length >= 8 ? s.Substring(0, 8) : s, "yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out dt) ||
                DateTime.TryParse(s, System.Globalization.CultureInfo.GetCultureInfo("de-DE"), System.Globalization.DateTimeStyles.None, out dt)) {
                return dt.ToString("dd.MM.yyyy");
            }
            return s;
        }

        private static string CleanGpuName(string h) {
            if (string.IsNullOrEmpty(h)) return "";
            string s = Regex.Replace(h, @"^((dGPU|iGPU|GPU|CPU)(\s*\[#\d+\])?\s*:\s*)", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"^(dGPU|iGPU|GPU|CPU)(\s*\[#\d+\])?\s*", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @":\s*Enhanced$", "", RegexOptions.IgnoreCase);
            return s.Trim();
        }

        private static string ResolveGpuPartnerModel(string baseName, string pnpDeviceId) {
            if (string.IsNullOrEmpty(baseName)) return "";
            string name = CleanGpuName(baseName);
            if (string.IsNullOrEmpty(pnpDeviceId)) return name;

            var match = Regex.Match(pnpDeviceId, @"SUBSYS_([0-9A-F]{4})([0-9A-F]{4})", RegexOptions.IgnoreCase);
            if (!match.Success) return name;

            string subId = match.Groups[1].Value.ToUpperInvariant();
            string vendorId = match.Groups[2].Value.ToUpperInvariant();

            string vendor = "";
            switch (vendorId) {
                case "1462": vendor = "MSI"; break;
                case "1043": vendor = "ASUS"; break;
                case "1458": vendor = "Gigabyte"; break;
                case "10DE": vendor = "NVIDIA"; break;
                case "3842": vendor = "EVGA"; break;
                case "10B0": vendor = "Gainward"; break;
                case "1569": vendor = "Palit"; break;
                case "19DA": vendor = "Zotac"; break;
                case "1EAE": vendor = "PNY"; break;
                case "1849": vendor = "ASRock"; break;
                case "1DA2": vendor = "Sapphire"; break;
                case "1682": vendor = "XFX"; break;
                case "1787": vendor = "PowerColor"; break;
                case "17AA": vendor = "Lenovo"; break;
                case "1028": vendor = "Dell"; break;
                case "103C": vendor = "HP"; break;
                case "1002": vendor = "AMD"; break;
            }

            string subModel = "";
            if (vendorId == "1462") {
                if (subId == "5100" || subId == "5102") subModel = "SUPRIM X";
                else if (subId == "5101" || subId == "5103") subModel = "GAMING X TRIO";
                else if (subId == "5104" || subId == "5105") subModel = "VENTUS 3X";
            } else if (vendorId == "1043") {
                if (subId.StartsWith("889B") || subId.StartsWith("889C") || subId.StartsWith("88D0")) subModel = "ROG STRIX";
                else if (subId.StartsWith("889D") || subId.StartsWith("889E") || subId.StartsWith("88D1")) subModel = "TUF GAMING";
                else if (subId.StartsWith("88A0") || subId.StartsWith("88D2")) subModel = "PROART";
            } else if (vendorId == "1458") {
                if (subId.StartsWith("409B") || subId.StartsWith("40A0") || subId.StartsWith("40C0")) subModel = "AORUS MASTER";
                else if (subId.StartsWith("409C") || subId.StartsWith("409D") || subId.StartsWith("40C1")) subModel = "GAMING OC";
                else if (subId.StartsWith("409E") || subId.StartsWith("40C2")) subModel = "WINDFORCE";
            } else if (vendorId == "10DE") {
                subModel = "Founders Edition";
            }

            if (!string.IsNullOrEmpty(vendor) && vendor != "NVIDIA" && vendor != "AMD") {
                if (name.StartsWith("NVIDIA ", StringComparison.OrdinalIgnoreCase)) {
                    name = name.Substring(7).Trim();
                } else if (name.StartsWith("AMD ", StringComparison.OrdinalIgnoreCase)) {
                    name = name.Substring(4).Trim();
                }
                name = vendor + " " + name;
            }

            if (!string.IsNullOrEmpty(subModel) && name.IndexOf(subModel, StringComparison.OrdinalIgnoreCase) < 0) {
                name = name + " " + subModel;
            }

            return name;
        }

        private static string CleanBoardVendor(string v) {
            if (string.IsNullOrEmpty(v)) return "";
            string upper = v.ToUpperInvariant();
            if (upper.Contains("ASUSTEK") || upper.Contains("ASUS")) return "ASUS";
            if (upper.Contains("MICRO-STAR") || upper.Contains("MSI")) return "MSI";
            if (upper.Contains("GIGABYTE")) return "Gigabyte";
            if (upper.Contains("ASROCK")) return "ASRock";
            if (upper.Contains("EVGA")) return "EVGA";
            if (upper.Contains("NZXT")) return "NZXT";
            if (upper.Contains("BIOSTAR")) return "Biostar";
            return v.Trim();
        }
        // =========================================================================
        // VOLLSTÄNDIGE HARDWARE- & SYSTEM-INFO SAMMLUNG & REPORT GENERATOR
        // =========================================================================
        public class HardwareReportData {
            // CPU
            public string CpuName = "Unbekannt";
            public int CpuCores = 0;
            public int CpuThreads = 0;
            public string CpuClock = "Unbekannt";
            public string CpuCache = "Unbekannt";
            public string CpuSocket = "Unbekannt";
            public string CpuArch = "64-Bit";
            public string CpuBadge = "CPU";

            // GPU
            public string GpuName = "Unbekannt";
            public string GpuDriver = "Unbekannt";
            public string GpuVram = "Unbekannt";
            public string GpuRes = "Unbekannt";
            public string GpuSubGpu = "Keine";
            public string GpuBadge = "PCIe";


            // Motherboard & BIOS
            public string BoardModel = "Unbekannt";
            public string BoardVendor = "Unbekannt";
            public string BoardChipset = "CHIPSET";
            public string BiosVersion = "Unbekannt";
            public string BiosDate = "Unbekannt";
            public string BoardSerial = "Unbekannt";

            // RAM
            public string RamTotal = "Unbekannt";
            public string RamSpeed = "DDR5";
            public string RamSlots = "Unbekannt";
            public string RamModules = "Unbekannt";
            public string RamClock = "Unbekannt";
            public string RamUsage = "Unbekannt";

            // Storage
            public string StorageSummary = "Unbekannt";
            public System.Collections.Generic.List<string> Drives = new System.Collections.Generic.List<string>();

            // OS
            public string OsCaption = "Windows";
            public string OsActivation = "Ermittle...";
            public bool OsIsActivated = false;
            public string OsProductKey = "";
            public string OsBuild = "Unbekannt";
            public string OsInstallDate = "Unbekannt";
            public string OsUptime = "Unbekannt";
            public string OsSecurity = "Unbekannt";
            public bool OsSecureBootActive = false;

            // Network & Audio
            public string NetAdapter = "Unbekannt";
            public string NetSpeed = "Unbekannt";
            public string NetMac = "Unbekannt";
            public string NetDriverVer = "Unbekannt";
            public string NetDriverDate = "Unbekannt";
            public string NetProvider = "";
            public string NetStatus = "Aktiv & Bereit";
            public string NetPnpId = "";
            public System.Collections.Generic.List<AudioDeviceEntry> AudioEndpoints = new System.Collections.Generic.List<AudioDeviceEntry>();

            // Monitors & Displays
            public System.Collections.Generic.List<MonitorReportData> Monitors = new System.Collections.Generic.List<MonitorReportData>();
        }

        public class AudioDeviceEntry {
            public string DeviceId = "";
            public string Name = "";
            public string Category = "Wiedergabe";
            public string Icon = "🔊";
            public bool IsDefault = false;
        }

        public class MonitorReportData {
            public string Name = "Monitor";
            public string DeviceName = "";
            public bool IsPrimary = false;
            public int Width = 1920;
            public int Height = 1080;
            public int RefreshRate = 60;
            public int BitsPerPel = 32;
            public string GpuAdapter = "";
            public System.Collections.Generic.List<int> SupportedRates = new System.Collections.Generic.List<int>();
            public int MaxRefreshRate = 60;
        }

        private void RefreshHardwareInfoUI(bool userTriggered = false) {
            try {
                if (window != null && !window.Dispatcher.CheckAccess()) {
                    window.Dispatcher.Invoke((Action)(() => RefreshHardwareInfoUI(userTriggered)));
                    return;
                }

                if (userTriggered) {
                    if (txtHwCpuClock != null) txtHwCpuClock.Text = "Ermittle...";
                    if (txtHwRamUsage != null) txtHwRamUsage.Text = "Ermittle...";
                    if (txtHwGpuSubGpu != null) txtHwGpuSubGpu.Text = "Ermittle...";
                    if (txtHwOsSecurity != null) {
                        txtHwOsSecurity.Text = "Ermittle...";
                        txtHwOsSecurity.Foreground = UIHelper.BrushGrayText;
                    }
                }

                Task.Run(() => {
                    var data = new HardwareReportData();

                    try {
                        // ============================================================
                        // 1. CPU (Hardware-Spezifikationen)
                        // ============================================================
                        int cpuMaxClock = 0;
                        WmiHelper.ForEach("SELECT Name, NumberOfCores, NumberOfLogicalProcessors, L2CacheSize, L3CacheSize, SocketDesignation, MaxClockSpeed FROM Win32_Processor", obj => {
                            if (string.IsNullOrEmpty(data.CpuName) || data.CpuName == "Unbekannt") {
                                if (obj["Name"] != null) data.CpuName = UIHelper.CleanCpuName(obj["Name"].ToString());
                            }
                            if (obj["NumberOfCores"] != null) int.TryParse(obj["NumberOfCores"].ToString(), out data.CpuCores);
                            if (obj["NumberOfLogicalProcessors"] != null) int.TryParse(obj["NumberOfLogicalProcessors"].ToString(), out data.CpuThreads);
                            if (obj["MaxClockSpeed"] != null) int.TryParse(obj["MaxClockSpeed"].ToString(), out cpuMaxClock);
                            
                            long l2Kb = 0, l3Kb = 0;
                            if (obj["L2CacheSize"] != null) long.TryParse(obj["L2CacheSize"].ToString(), out l2Kb);
                            if (obj["L3CacheSize"] != null) long.TryParse(obj["L3CacheSize"].ToString(), out l3Kb);
                            if (l3Kb > 0) {
                                double l3Mb = Math.Round(l3Kb / 1024.0, 0);
                                double l2Mb = Math.Round(l2Kb / 1024.0, 0);
                                double totalCache = l3Mb + l2Mb;
                                data.CpuCache = string.Format("{0:F0} MB L3 + {1:F0} MB L2 Cache ({2:F0} MB Total)", l3Mb, l2Mb, totalCache);
                            }
                            if (obj["SocketDesignation"] != null) {
                                string sock = obj["SocketDesignation"].ToString().Trim();
                                if (!string.IsNullOrEmpty(sock)) data.CpuSocket = string.Format("Socket {0} (64-Bit x86_64)", sock);
                            }
                        });

                        if (data.CpuThreads == 0) data.CpuThreads = Environment.ProcessorCount;
                        if (string.IsNullOrEmpty(data.CpuSocket) || data.CpuSocket == "Unbekannt") {
                            data.CpuSocket = Environment.Is64BitOperatingSystem ? "64-Bit x86_64 Prozessor" : "32-Bit x86 Prozessor";
                        }
                        if (string.IsNullOrEmpty(data.CpuCache) || data.CpuCache == "Unbekannt") {
                            data.CpuCache = "Hardware L1/L2/L3 Cache";
                        }
                        data.CpuBadge = data.CpuName.IndexOf("Ryzen", StringComparison.OrdinalIgnoreCase) >= 0 ? "AMD Ryzen" : (data.CpuName.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ? "Intel Core" : "CPU");

                        if (cpuMaxClock > 0) {
                            data.CpuClock = string.Format("{0:N0} MHz ({1:F2} GHz Base)", cpuMaxClock, cpuMaxClock / 1000.0);
                        } else {
                            data.CpuClock = "Standard Taktung";
                        }

                        // ============================================================
                        // 2. GPU (Grafikkarte, Treiber, 64-Bit VRAM & Monitor)
                        // ============================================================
                        long totalVramBytes = 0;
                        string regDriverVer = "";
                        string regDriverDate = "";
                        try {
                            for (int i = 0; i < 4; i++) {
                                string subkey = string.Format(@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{0:D4}", i);
                                using (var key = Registry.LocalMachine.OpenSubKey(subkey)) {
                                    if (key != null) {
                                        object memObj = key.GetValue("HardwareInformation.qwMemorySize");
                                        if (memObj != null) {
                                            long bytes = Convert.ToInt64(memObj);
                                            if (bytes > totalVramBytes) {
                                                totalVramBytes = bytes;
                                                object verObj = key.GetValue("DriverVersion");
                                                object dateObj = key.GetValue("DriverDate");
                                                if (verObj != null) regDriverVer = verObj.ToString();
                                                if (dateObj != null) regDriverDate = FormatGermanDate(dateObj.ToString());
                                            }
                                        }
                                    }
                                }
                            }
                        } catch {}

                        WmiHelper.ForEach("SELECT Name, DriverVersion, DriverDate, AdapterRAM, VideoModeDescription, PNPDeviceID FROM Win32_VideoController", obj => {
                            string gName = obj["Name"] != null ? obj["Name"].ToString().Trim() : "";
                            if (string.IsNullOrEmpty(gName) || gName.IndexOf("Remote Display", StringComparison.OrdinalIgnoreCase) >= 0) return;

                            string pnpId = obj["PNPDeviceID"] != null ? obj["PNPDeviceID"].ToString() : "";
                            string fullGpuName = ResolveGpuPartnerModel(gName, pnpId);

                            if (string.IsNullOrEmpty(data.GpuName) || data.GpuName == "Unbekannt" || (gName.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0 || gName.IndexOf("Radeon RX", StringComparison.OrdinalIgnoreCase) >= 0 || gName.IndexOf("Arc", StringComparison.OrdinalIgnoreCase) >= 0)) {
                                if (data.GpuName != "Unbekannt" && data.GpuName != fullGpuName) {
                                    data.GpuSubGpu = data.GpuName;
                                }
                                data.GpuName = fullGpuName;
                                if (string.IsNullOrEmpty(regDriverVer) && obj["DriverVersion"] != null) {
                                    regDriverVer = obj["DriverVersion"].ToString().Trim();
                                }
                                if (string.IsNullOrEmpty(regDriverDate) && obj["DriverDate"] != null) {
                                    regDriverDate = FormatGermanDate(obj["DriverDate"].ToString());
                                }
                                if (totalVramBytes == 0 && obj["AdapterRAM"] != null) {
                                    long b;
                                    if (long.TryParse(obj["AdapterRAM"].ToString(), out b) && b > 0) totalVramBytes = b;
                                }
                                if (obj["VideoModeDescription"] != null) {
                                    data.GpuRes = obj["VideoModeDescription"].ToString().Replace("4294967296 Farben", "32-Bit TrueColor").Replace("Farben", "").Trim();
                                }
                            } else {
                                data.GpuSubGpu = gName + " (iGPU)";
                            }
                        });

                        data.GpuBadge = data.GpuName.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 || data.GpuName.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0 || data.GpuName.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0 || data.GpuName.IndexOf("GTX", StringComparison.OrdinalIgnoreCase) >= 0 
                            ? "NVIDIA" 
                            : (data.GpuName.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || data.GpuName.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0 ? "AMD Radeon" : "GPU");

                        if (!string.IsNullOrEmpty(regDriverVer)) {
                            string dateFormatted = !string.IsNullOrEmpty(regDriverDate) ? " (vom " + regDriverDate + ")" : "";
                            data.GpuDriver = regDriverVer + dateFormatted;
                        } else {
                            data.GpuDriver = "Aktueller Grafiktreiber aktiv";
                        }

                        double vramTotalGb = totalVramBytes > 0 ? Math.Round(totalVramBytes / (1024.0 * 1024.0 * 1024.0), 0) : 0;
                        string vramTotalStr = vramTotalGb > 0 ? string.Format("{0:F0} GB Dedicated VRAM", vramTotalGb) : "Dedicated VRAM";
                        data.GpuVram = vramTotalStr;

                        if (string.IsNullOrEmpty(data.GpuRes) || data.GpuRes == "Unbekannt") {
                            try {
                                data.GpuRes = string.Format("{0:F0} x {1:F0} (32-Bit TrueColor)", SystemParameters.PrimaryScreenWidth, SystemParameters.PrimaryScreenHeight);
                            } catch {}
                        }

                        if (string.IsNullOrEmpty(data.GpuSubGpu) || data.GpuSubGpu == "Unbekannt") {
                            data.GpuSubGpu = "PCIe x16 Schnittstelle";
                        }



                        // ============================================================
                        // 3. MAINBOARD & BIOS (Dynamisch & Chipsatz via Regex)
                        // ============================================================
                        WmiHelper.ForEach("SELECT Manufacturer, Product, SerialNumber, Version FROM Win32_BaseBoard", obj => {
                            if (string.IsNullOrEmpty(data.BoardModel) || data.BoardModel == "Unbekannt") {
                                string rawProd = obj["Product"] != null ? obj["Product"].ToString().Trim() : "";
                                string rawVendor = obj["Manufacturer"] != null ? obj["Manufacturer"].ToString().Trim() : "";
                                string cleanVendor = CleanBoardVendor(rawVendor);

                                data.BoardVendor = !string.IsNullOrEmpty(cleanVendor) ? cleanVendor : rawVendor;

                                if (!string.IsNullOrEmpty(data.BoardVendor) && !string.IsNullOrEmpty(rawProd)) {
                                    if (rawProd.StartsWith(data.BoardVendor, StringComparison.OrdinalIgnoreCase)) {
                                        data.BoardModel = rawProd;
                                    } else {
                                        data.BoardModel = data.BoardVendor + " " + rawProd;
                                    }
                                } else if (!string.IsNullOrEmpty(rawProd)) {
                                    data.BoardModel = rawProd;
                                }
                                if (obj["SerialNumber"] != null) data.BoardSerial = obj["SerialNumber"].ToString().Trim();
                            }
                        });

                        WmiHelper.ForEach("SELECT Manufacturer, SMBIOSBIOSVersion, ReleaseDate FROM Win32_BIOS", obj => {
                            if (string.IsNullOrEmpty(data.BiosVersion) || data.BiosVersion == "Unbekannt") {
                                if (obj["SMBIOSBIOSVersion"] != null) data.BiosVersion = "v" + obj["SMBIOSBIOSVersion"].ToString().Trim();
                                if (obj["ReleaseDate"] != null) data.BiosDate = FormatGermanDate(obj["ReleaseDate"].ToString());
                            }
                        });

                        var chipMatch = Regex.Match(data.BoardModel, @"\b([A-Z]\d{2,3}[A-Z]?)\b", RegexOptions.IgnoreCase);
                        if (chipMatch.Success) {
                            string chipCode = chipMatch.Value.ToUpper();
                            string vendorPrefix = data.CpuName.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0 ? "Intel " : (data.CpuName.IndexOf("Ryzen", StringComparison.OrdinalIgnoreCase) >= 0 ? "AMD " : "");
                            data.BoardChipset = vendorPrefix + chipCode;
                        } else {
                            data.BoardChipset = !string.IsNullOrEmpty(data.BoardVendor) && data.BoardVendor != "Unbekannt" ? data.BoardVendor : "MAINBOARD";
                        }

                        // ============================================================
                        // 4. RAM (Physischer Speicher, echte Module, Taktung & DDR-Typ)
                        // ============================================================
                        double physRamGb = 0;
                        try {
                            ulong totalMemKb = 0;
                            if (GetPhysicallyInstalledSystemMemory(out totalMemKb) && totalMemKb > 0) {
                                physRamGb = Math.Round(totalMemKb / (1024.0 * 1024.0), 0);
                            }
                        } catch {}

                        int slotCount = 0;
                        int configuredSpeed = 0;
                        int smbiosType = 0;
                        string memVendor = "";
                        string partNum = "";
                        long totalWmiBytes = 0;

                        WmiHelper.ForEach("SELECT Manufacturer, PartNumber, Capacity, Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory", obj => {
                            slotCount++;
                            if (obj["Capacity"] != null) {
                                long cap;
                                if (long.TryParse(obj["Capacity"].ToString(), out cap)) totalWmiBytes += cap;
                            }
                            if (obj["ConfiguredClockSpeed"] != null) {
                                int spd;
                                if (int.TryParse(obj["ConfiguredClockSpeed"].ToString(), out spd) && spd > configuredSpeed) configuredSpeed = spd;
                            }
                            if (obj["Speed"] != null && configuredSpeed == 0) {
                                int spd;
                                if (int.TryParse(obj["Speed"].ToString(), out spd) && spd > configuredSpeed) configuredSpeed = spd;
                            }
                            if (string.IsNullOrEmpty(memVendor) && obj["Manufacturer"] != null) {
                                string v = obj["Manufacturer"].ToString().Trim();
                                if (v != "Unbekannt" && v != "0000") memVendor = v;
                            }
                            if (string.IsNullOrEmpty(partNum) && obj["PartNumber"] != null) {
                                partNum = obj["PartNumber"].ToString().Trim();
                            }
                            if (obj["SMBIOSMemoryType"] != null) {
                                int t;
                                if (int.TryParse(obj["SMBIOSMemoryType"].ToString(), out t) && t > 0) smbiosType = t;
                            }
                        });

                        if (physRamGb == 0 && totalWmiBytes > 0) {
                            physRamGb = Math.Round(totalWmiBytes / (1024.0 * 1024.0 * 1024.0), 0);
                        }

                        string ddrGen = "RAM";
                        if (smbiosType == 34 || configuredSpeed >= 4400) ddrGen = "DDR5";
                        else if (smbiosType == 26 || (configuredSpeed >= 2133 && configuredSpeed <= 4000)) ddrGen = "DDR4";
                        else if (smbiosType == 24) ddrGen = "DDR3";

                        data.RamSpeed = ddrGen;
                        data.RamTotal = string.Format("{0:F0} GB {1} High-Speed RAM", physRamGb > 0 ? physRamGb : 16, ddrGen);
                        data.RamSlots = slotCount > 0 ? string.Format("{0} Riegel installiert ({0}x {1:F0} GB Dual-Channel)", slotCount, physRamGb / slotCount) : string.Format("{0:F0} GB installiert", physRamGb);
                        data.RamModules = (!string.IsNullOrEmpty(memVendor) || !string.IsNullOrEmpty(partNum)) ? string.Format("{0} {1}", memVendor, partNum).Trim() : "Standard OEM Module";
                        data.RamClock = configuredSpeed > 0 ? string.Format("{0} MT/s / MHz ({1})", configuredSpeed, ddrGen) : "Standard Takt";

                        data.RamUsage = string.Format("288-Pin DIMM ({0})", ddrGen);

                        // ============================================================
                        // 5. STORAGE Win32_DiskDrive
                        // ============================================================
                        double totalDiskGb = 0;
                        int dIdx = 1;
                        WmiHelper.ForEach("SELECT Model, Size, MediaType, InterfaceType, SerialNumber FROM Win32_DiskDrive", obj => {
                            string model = obj["Model"] != null ? obj["Model"].ToString().Trim() : "Laufwerk " + dIdx;
                            string sizeStr = "";
                            if (obj["Size"] != null) {
                                long sBytes;
                                if (long.TryParse(obj["Size"].ToString(), out sBytes)) {
                                    double dGb = Math.Round(sBytes / (1000.0 * 1000.0 * 1000.0), 0);
                                    totalDiskGb += dGb;
                                    if (dGb >= 1000) {
                                        sizeStr = string.Format("{0:F0} TB ({1:F0} GB)", Math.Round(dGb / 1000.0, 0), dGb);
                                    } else {
                                        sizeStr = string.Format("{0:F0} GB", dGb);
                                    }
                                }
                            }
                            string interfaceType = obj["InterfaceType"] != null ? obj["InterfaceType"].ToString().Trim() : "NVMe / SSD";
                            data.Drives.Add(string.Format("Laufwerk {0}: {1} — {2} ({3})", dIdx, model, sizeStr, interfaceType.Replace("SCSI", "PCIe NVMe / High-Speed")));
                            dIdx++;
                        });
                        if (totalDiskGb >= 1000) {
                            data.StorageSummary = string.Format("{0:F0} TB Gesamtkapazität ({1} Laufwerke)", Math.Round(totalDiskGb / 1000.0, 0), data.Drives.Count);
                        } else {
                            data.StorageSummary = string.Format("{0:F0} GB Gesamtkapazität ({1} Laufwerke)", totalDiskGb, data.Drives.Count);
                        }

                        // ============================================================
                        // 6. OS & WINDOWS (Dynamische DisplayVersion aus Registry)
                        // ============================================================
                        string displayVer = RegistryHelper.GetString(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "DisplayVersion");
                        if (string.IsNullOrEmpty(displayVer)) {
                            displayVer = RegistryHelper.GetString(RegistryHive.LocalMachine, @"SOFTWARE\Microsoft\Windows NT\CurrentVersion", "ReleaseId");
                        }
                        string buildNum = "";
                        WmiHelper.ForEach("SELECT Caption, BuildNumber, InstallDate, LastBootUpTime FROM Win32_OperatingSystem", obj => {
                            if (obj["Caption"] != null) data.OsCaption = obj["Caption"].ToString().Trim() + " 64-Bit";
                            if (obj["BuildNumber"] != null) buildNum = obj["BuildNumber"].ToString().Trim();
                            if (obj["InstallDate"] != null) {
                                string rawDate = obj["InstallDate"].ToString();
                                if (rawDate.Length >= 14) {
                                    data.OsInstallDate = rawDate.Substring(6, 2) + "." + rawDate.Substring(4, 2) + "." + rawDate.Substring(0, 4) + " " + rawDate.Substring(8, 2) + ":" + rawDate.Substring(10, 2);
                                }
                            }
                            if (obj["LastBootUpTime"] != null) {
                                string rawDate = obj["LastBootUpTime"].ToString();
                                if (rawDate.Length >= 14) {
                                    DateTime boot = new DateTime(int.Parse(rawDate.Substring(0, 4)), int.Parse(rawDate.Substring(4, 2)), int.Parse(rawDate.Substring(6, 2)), int.Parse(rawDate.Substring(8, 2)), int.Parse(rawDate.Substring(10, 2)), int.Parse(rawDate.Substring(12, 2)));
                                    TimeSpan up = DateTime.Now - boot;
                                    data.OsUptime = string.Format("Seit {0:dd.MM.yyyy HH:mm} ({1} Std. {2} Min. aktiv)", boot, (int)up.TotalHours, up.Minutes);
                                }
                            }
                        });

                        data.OsBuild = !string.IsNullOrEmpty(displayVer) ? string.Format("Version {0} (OS Build {1})", displayVer, buildNum) : "OS Build " + buildNum;

                        // Activation Status
                        uint licenseStatus = 0;
                        string partialKey = "";
                        try {
                            WmiHelper.ForEach("SELECT LicenseStatus, PartialProductKey FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND ApplicationID = '55c92734-d682-4d71-983e-d6ec3f16059f'", obj => {
                                if (obj["LicenseStatus"] != null) {
                                    uint s;
                                    if (uint.TryParse(obj["LicenseStatus"].ToString(), out s) && s == 1) {
                                        licenseStatus = 1;
                                    }
                                }
                                if (obj["PartialProductKey"] != null && string.IsNullOrEmpty(partialKey)) {
                                    partialKey = obj["PartialProductKey"].ToString().Trim();
                                }
                            });
                        } catch {}

                        if (licenseStatus == 1) {
                            data.OsIsActivated = true;
                            data.OsActivation = "Aktiviert";
                        } else {
                            data.OsIsActivated = false;
                            data.OsActivation = "Nicht aktiviert";
                        }

                        // Product Key Decoding
                        string decodedKey = "";
                        try {
                            using (var rk = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion")) {
                                if (rk != null) {
                                    byte[] digitalProductId = rk.GetValue("DigitalProductId") as byte[];
                                    if (digitalProductId != null && digitalProductId.Length >= 67) {
                                        decodedKey = DecodeWindowsProductKey(digitalProductId);
                                    }
                                }
                            }
                        } catch {}

                        if (string.IsNullOrEmpty(decodedKey) || decodedKey.StartsWith("BBBBB")) {
                            try {
                                WmiHelper.ForEach("SELECT OA3xOriginalProductKey FROM SoftwareLicensingService", obj => {
                                    if (obj["OA3xOriginalProductKey"] != null) {
                                        string k = obj["OA3xOriginalProductKey"].ToString().Trim();
                                        if (!string.IsNullOrEmpty(k)) decodedKey = k;
                                    }
                                });
                            } catch {}
                        }

                        data.OsProductKey = !string.IsNullOrEmpty(decodedKey) ? decodedKey : "Kein Key gefunden";

                        bool secureBoot = RegistryHelper.GetDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled") == 1;
                        data.OsSecureBootActive = secureBoot;
                        data.OsSecurity = secureBoot ? "Secure Boot: Aktiv" : "Secure Boot: Deaktiviert";

                        // ============================================================
                        // 7. NETZWERK & LAN (Natives .NET NetworkInterface + WMI)
                        // ============================================================
                        try {
                            foreach (var nic in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
                                if (nic.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                                    nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                                    nic.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) {

                                    data.NetAdapter = nic.Description;
                                    long spd = nic.Speed;
                                    string netType = nic.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ? "WLAN" : "LAN";
                                    if (spd >= 1000000000) {
                                        data.NetSpeed = string.Format("{0:F1} Gbit/s Gigabit {1}", spd / 1000000000.0, netType);
                                    } else if (spd > 0) {
                                        data.NetSpeed = string.Format("{0} Mbit/s {1}", spd / 1000000, netType);
                                    } else {
                                        data.NetSpeed = "Verbunden";
                                    }

                                    byte[] macBytes = nic.GetPhysicalAddress().GetAddressBytes();
                                    if (macBytes != null && macBytes.Length > 0) {
                                        data.NetMac = BitConverter.ToString(macBytes).Replace("-", ":");
                                    }
                                    break;
                                }
                            }
                        } catch {}

                        if (data.NetAdapter == "Unbekannt" || string.IsNullOrEmpty(data.NetAdapter)) {
                            WmiHelper.ForEach("SELECT Name, Speed, MACAddress FROM Win32_NetworkAdapter WHERE NetEnabled = True", obj => {
                                if (data.NetAdapter == "Unbekannt" && obj["Name"] != null) {
                                    data.NetAdapter = obj["Name"].ToString().Trim();
                                    if (obj["Speed"] != null) {
                                        long s;
                                        if (long.TryParse(obj["Speed"].ToString(), out s) && s > 0) {
                                            data.NetSpeed = s >= 1000000000 ? string.Format("{0:F1} Gbit/s Gigabit LAN", s / 1000000000.0) : (s / 1000000) + " Mbit/s";
                                        }
                                    }
                                    if (obj["MACAddress"] != null) data.NetMac = obj["MACAddress"].ToString().Trim();
                                }
                            });
                        }

                        // Treiber-Details (Version, Datum, Provider, PNP-ID)
                        try {
                            string classPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";
                            using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(classPath)) {
                                if (baseKey != null) {
                                    foreach (string subName in baseKey.GetSubKeyNames()) {
                                        if (subName.Length == 4) {
                                            using (RegistryKey adapterKey = baseKey.OpenSubKey(subName)) {
                                                if (adapterKey != null) {
                                                    object descObj = adapterKey.GetValue("DriverDesc");
                                                    object verObj = adapterKey.GetValue("DriverVersion");
                                                    object dateObj = adapterKey.GetValue("DriverDate");
                                                    object provObj = adapterKey.GetValue("ProviderName");
                                                    object netCfgId = adapterKey.GetValue("NetCfgInstanceId");
                                                    object matchId = adapterKey.GetValue("MatchingDeviceId");

                                                    if (descObj != null && verObj != null) {
                                                        string desc = descObj.ToString();
                                                        if (!Regex.IsMatch(desc, @"WAN|Miniport|Virtual|Kernel|Bluetooth|Pacer|QoS|Loopback|Teredo|ISATAP|Direct", RegexOptions.IgnoreCase)) {
                                                            string pnpId = null;
                                                            if (netCfgId != null) {
                                                                string connPath = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}\" + netCfgId.ToString() + @"\Connection";
                                                                using (RegistryKey connKey = Registry.LocalMachine.OpenSubKey(connPath)) {
                                                                    if (connKey != null) {
                                                                        object pnpObj = connKey.GetValue("PnPInstanceId") ?? connKey.GetValue("PnPInstanceID");
                                                                        if (pnpObj != null) pnpId = pnpObj.ToString();
                                                                    }
                                                                }
                                                            }
                                                            if (string.IsNullOrEmpty(pnpId) && matchId != null) {
                                                                pnpId = matchId.ToString();
                                                            }

                                                            if (data.NetAdapter == "Unbekannt" || string.IsNullOrEmpty(data.NetAdapter)) {
                                                                data.NetAdapter = desc;
                                                            }
                                                            data.NetDriverVer = verObj != null ? verObj.ToString() : "Unbekannt";
                                                            data.NetDriverDate = dateObj != null ? FormatGermanDate(dateObj.ToString()) : "Unbekannt";
                                                            data.NetProvider = provObj != null ? provObj.ToString().ToUpper() : "WINDOWS";
                                                            data.NetStatus = "Aktiv & Bereit";
                                                            data.NetPnpId = pnpId;

                                                            currentLanAdapter = new LanAdapterInfo {
                                                                Description = desc,
                                                                DriverVersion = data.NetDriverVer,
                                                                DriverDate = data.NetDriverDate,
                                                                Provider = data.NetProvider,
                                                                PnpInstanceId = pnpId
                                                            };
                                                            break;
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        } catch {}

                        // ============================================================
                        // 8. AUDIO & SOUNDGERÄTE (Aktive Wiedergabe- & Aufnahme-Endgeräte)
                        // ============================================================
                        try {
                            data.AudioEndpoints.AddRange(AudioDeviceHelper.GetAudioEndpoints());

                            // Fallback auf Registry, falls CoreAudio COM 0 Geräte lieferte
                            if (data.AudioEndpoints.Count == 0) {
                                string defaultRenderId = AudioDeviceHelper.GetDefaultEndpointId(0);
                                string defaultCaptureId = AudioDeviceHelper.GetDefaultEndpointId(1);

                                using (var rKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Render")) {
                                    if (rKey != null) {
                                        foreach (var subName in rKey.GetSubKeyNames()) {
                                            using (var devKey = rKey.OpenSubKey(subName)) {
                                                if (devKey != null) {
                                                    object st = devKey.GetValue("DeviceState");
                                                    if (st != null && Convert.ToInt32(st) == 1) { // DEVICE_STATE_ACTIVE
                                                        using (var propKey = devKey.OpenSubKey("Properties")) {
                                                            if (propKey != null) {
                                                                string friendly = propKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string ?? "";
                                                                string device = propKey.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string ?? "";
                                                                string icon = "🔊";
                                                                if (friendly.IndexOf("Kopfhörer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                    friendly.IndexOf("Headset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                    friendly.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                    device.IndexOf("Headset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                    device.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0) {
                                                                    icon = "🎧";
                                                                } else if (device.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                           device.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                                           friendly.IndexOf("Display", StringComparison.OrdinalIgnoreCase) >= 0) {
                                                                    icon = "🖥️";
                                                                }
                                                                string display = (!string.IsNullOrEmpty(friendly) && !string.IsNullOrEmpty(device) && !friendly.Equals(device, StringComparison.OrdinalIgnoreCase))
                                                                    ? string.Format("{0} ({1})", friendly, device)
                                                                    : (!string.IsNullOrEmpty(friendly) ? friendly : device);
                                                                if (!string.IsNullOrEmpty(display)) {
                                                                    string fullDeviceId = "{0.0.0.00000000}." + subName;
                                                                    string cleanSub = subName.Trim('{', '}');
                                                                    bool isDef = !string.IsNullOrEmpty(defaultRenderId) && defaultRenderId.IndexOf(cleanSub, StringComparison.OrdinalIgnoreCase) >= 0;
                                                                    data.AudioEndpoints.Add(new AudioDeviceEntry {
                                                                        DeviceId = fullDeviceId,
                                                                        Name = display,
                                                                        Category = "Wiedergabe",
                                                                        Icon = icon,
                                                                        IsDefault = isDef
                                                                    });
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }

                                using (var cKey = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64).OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\MMDevices\Audio\Capture")) {
                                    if (cKey != null) {
                                        foreach (var subName in cKey.GetSubKeyNames()) {
                                            using (var devKey = cKey.OpenSubKey(subName)) {
                                                if (devKey != null) {
                                                    object st = devKey.GetValue("DeviceState");
                                                    if (st != null && Convert.ToInt32(st) == 1) { // DEVICE_STATE_ACTIVE
                                                        using (var propKey = devKey.OpenSubKey("Properties")) {
                                                            if (propKey != null) {
                                                                string friendly = propKey.GetValue("{a45c254e-df1c-4efd-8020-67d146a850e0},2") as string ?? "";
                                                                string device = propKey.GetValue("{b3f8fa53-0004-438e-9003-51a46e139bfc},6") as string ?? "";
                                                                string display = (!string.IsNullOrEmpty(friendly) && !string.IsNullOrEmpty(device) && !friendly.Equals(device, StringComparison.OrdinalIgnoreCase))
                                                                    ? string.Format("{0} ({1})", friendly, device)
                                                                    : (!string.IsNullOrEmpty(friendly) ? friendly : device);
                                                                if (!string.IsNullOrEmpty(display)) {
                                                                    string fullDeviceId = "{0.0.1.00000000}." + subName;
                                                                    string cleanSub = subName.Trim('{', '}');
                                                                    bool isDef = !string.IsNullOrEmpty(defaultCaptureId) && defaultCaptureId.IndexOf(cleanSub, StringComparison.OrdinalIgnoreCase) >= 0;
                                                                    data.AudioEndpoints.Add(new AudioDeviceEntry {
                                                                        DeviceId = fullDeviceId,
                                                                        Name = display,
                                                                        Category = "Aufnahme",
                                                                        Icon = "🎙️",
                                                                        IsDefault = isDef
                                                                    });
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        } catch {}

                        // Fallback auf Win32_SoundDevice, falls keine Endpunkte gefunden wurden
                        if (data.AudioEndpoints.Count == 0) {
                            WmiHelper.ForEach("SELECT Name, Manufacturer FROM Win32_SoundDevice", obj => {
                                if (obj["Name"] != null) {
                                    string sName = obj["Name"].ToString().Trim();
                                    data.AudioEndpoints.Add(new AudioDeviceEntry { Name = sName, Category = "Gerät", Icon = "🔊", IsDefault = false });
                                }
                            });
                        }

                        // ============================================================
                        // 9. MONITORE & DISPLAYS (NATIVES WIN32 & EDID-NAMEN)
                        // ============================================================
                        try {
                            var edidNames = GetMonitorEdidNames();
                            var dDev = new DISPLAY_DEVICE();
                            dDev.cb = Marshal.SizeOf(dDev);

                            for (uint i = 0; EnumDisplayDevices(null, i, ref dDev, 0); i++) {
                                if ((dDev.StateFlags & 0x1) != 0) { // ATTACHED_TO_DESKTOP
                                    bool isPrimary = (dDev.StateFlags & 0x4) != 0; // PRIMARY_DEVICE
                                    var monDev = new DISPLAY_DEVICE();
                                    monDev.cb = Marshal.SizeOf(monDev);
                                    string monName = dDev.DeviceString;
                                    if (EnumDisplayDevices(dDev.DeviceName, 0, ref monDev, 0)) {
                                        string monId = monDev.DeviceID ?? "";
                                        bool matched = false;
                                        foreach (var kvp in edidNames) {
                                            if (monId.IndexOf(kvp.Key, StringComparison.OrdinalIgnoreCase) >= 0) {
                                                monName = kvp.Value;
                                                matched = true;
                                                break;
                                            }
                                        }
                                        if (!matched) {
                                            if (!string.IsNullOrWhiteSpace(monDev.DeviceString) && !monDev.DeviceString.Equals("Generic PnP Monitor", StringComparison.OrdinalIgnoreCase)) {
                                                monName = monDev.DeviceString;
                                            }
                                            var mPnp = Regex.Match(monId, @"(?:DISPLAY|MONITOR)\\([A-Za-z]{3})", RegexOptions.IgnoreCase);
                                            if (mPnp.Success) {
                                                string mfg = ResolveManufacturer(mPnp.Groups[1].Value);
                                                if (!string.IsNullOrEmpty(mfg) && !monName.ToUpperInvariant().Contains(mfg.ToUpperInvariant())) {
                                                    monName = mfg + " " + monName;
                                                }
                                            }
                                        }
                                    }

                                    var dm = new DEVMODE();
                                    dm.dmSize = (short)Marshal.SizeOf(dm);
                                    int w = 1920, h = 1080, hz = 60, bpp = 32;
                                    if (EnumDisplaySettings(dDev.DeviceName, -1, ref dm)) {
                                        w = dm.dmPelsWidth;
                                        h = dm.dmPelsHeight;
                                        hz = dm.dmDisplayFrequency;
                                        bpp = dm.dmBitsPerPel;
                                    }

                                    var supportedRates = new System.Collections.Generic.List<int>();
                                    var dmMode = new DEVMODE();
                                    dmMode.dmSize = (short)Marshal.SizeOf(dmMode);
                                    for (int m = 0; EnumDisplaySettings(dDev.DeviceName, m, ref dmMode); m++) {
                                        if (dmMode.dmPelsWidth == w && dmMode.dmPelsHeight == h && dmMode.dmDisplayFrequency > 0) {
                                            if (!supportedRates.Contains(dmMode.dmDisplayFrequency)) {
                                                supportedRates.Add(dmMode.dmDisplayFrequency);
                                            }
                                        }
                                    }
                                    supportedRates.Sort(new Comparison<int>((a, b) => b.CompareTo(a)));
                                    int maxHz = supportedRates.Count > 0 ? supportedRates[0] : hz;

                                    data.Monitors.Add(new MonitorReportData {
                                        Name = monName,
                                        DeviceName = dDev.DeviceName,
                                        IsPrimary = isPrimary,
                                        Width = w,
                                        Height = h,
                                        RefreshRate = hz,
                                        BitsPerPel = bpp,
                                        GpuAdapter = dDev.DeviceString,
                                        SupportedRates = supportedRates,
                                        MaxRefreshRate = maxHz
                                    });
                                }
                                dDev.cb = Marshal.SizeOf(dDev);
                            }
                        } catch {}

                    } catch {}

                    // Update UI on Dispatcher
                    if (window != null) {
                        window.Dispatcher.Invoke((Action)(() => {
                            if (txtHwCpuName != null) txtHwCpuName.Text = data.CpuName;
                            if (txtHwCpuBadge != null) txtHwCpuBadge.Text = data.CpuBadge;
                            if (txtHwCpuCores != null) {
                                txtHwCpuCores.Text = data.CpuCores > 0 
                                    ? string.Format("{0} Kerne / {1} Threads", data.CpuCores, data.CpuThreads) 
                                    : string.Format("{0} Threads (Logisch)", data.CpuThreads);
                            }
                            if (txtHwCpuClock != null) {
                                txtHwCpuClock.Text = data.CpuClock;
                                txtHwCpuClock.Foreground = UIHelper.GetBrush("#F8FAFC");
                            }
                            if (txtHwCpuCache != null) txtHwCpuCache.Text = data.CpuCache;
                            if (txtHwCpuSocket != null) txtHwCpuSocket.Text = data.CpuSocket;

                            if (txtHwGpuName != null) txtHwGpuName.Text = data.GpuName;
                            if (txtHwGpuBadge != null) txtHwGpuBadge.Text = data.GpuBadge;
                            if (txtHwGpuDriver != null) txtHwGpuDriver.Text = data.GpuDriver;
                            if (txtHwGpuVram != null) txtHwGpuVram.Text = data.GpuVram;
                            if (txtHwGpuRes != null) txtHwGpuRes.Text = data.GpuRes;

                            if (txtHwGpuSubGpu != null) {
                                txtHwGpuSubGpu.Text = data.GpuSubGpu;
                                txtHwGpuSubGpu.Foreground = UIHelper.GetBrush("#F8FAFC");
                            }

                            if (txtHwBoardModel != null) txtHwBoardModel.Text = data.BoardModel;
                            if (txtHwBoardVendor != null) txtHwBoardVendor.Text = data.BoardVendor;
                            if (txtHwBoardChipset != null) txtHwBoardChipset.Text = data.BoardChipset;
                            if (txtHwBiosVersion != null) txtHwBiosVersion.Text = data.BiosVersion;
                            if (txtHwBiosDate != null) txtHwBiosDate.Text = data.BiosDate;
                            if (txtHwBoardSerial != null) txtHwBoardSerial.Text = data.BoardSerial;

                            if (txtHwRamTotal != null) txtHwRamTotal.Text = data.RamTotal;
                            if (txtHwRamSpeed != null) txtHwRamSpeed.Text = data.RamSpeed;
                            if (txtHwRamSlots != null) txtHwRamSlots.Text = data.RamSlots;
                            if (txtHwRamModules != null) txtHwRamModules.Text = data.RamModules;
                            if (txtHwRamClock != null) txtHwRamClock.Text = data.RamClock;
                            if (txtHwRamUsage != null) {
                                txtHwRamUsage.Text = data.RamUsage;
                                txtHwRamUsage.Foreground = UIHelper.GetBrush("#F8FAFC");
                            }

                            if (txtHwStorageSummary != null && !string.IsNullOrEmpty(data.StorageSummary)) {
                                txtHwStorageSummary.Text = data.StorageSummary;
                            }
                            RefreshAllDrivesUI();

                            if (txtHwOsCaption != null) txtHwOsCaption.Text = data.OsCaption;
                            if (txtHwOsActivation != null) {
                                txtHwOsActivation.Text = data.OsActivation;
                                txtHwOsActivation.Foreground = data.OsIsActivated ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                            }
                            cachedProductKey = data.OsProductKey;
                            if (txtHwOsProductKey != null) {
                                txtHwOsProductKey.Text = isProductKeyVisible ? (!string.IsNullOrEmpty(cachedProductKey) ? cachedProductKey : "Kein Key gefunden") : "•••••-•••••-•••••-•••••-•••••";
                            }
                            if (txtHwOsBuild != null) txtHwOsBuild.Text = data.OsBuild;
                            if (txtHwOsInstallDate != null) txtHwOsInstallDate.Text = data.OsInstallDate;
                            if (txtHwOsUptime != null) txtHwOsUptime.Text = data.OsUptime;
                            if (txtHwOsSecurity != null) {
                                txtHwOsSecurity.Text = data.OsSecurity;
                                txtHwOsSecurity.Foreground = data.OsSecureBootActive ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                            }

                            if (txtHwNetAdapter != null) txtHwNetAdapter.Text = data.NetAdapter;
                            if (txtHwNetSpeed != null) txtHwNetSpeed.Text = data.NetSpeed;
                            if (txtHwNetMac != null) txtHwNetMac.Text = data.NetMac;
                            if (txtHwNetDriverVer != null) txtHwNetDriverVer.Text = data.NetDriverVer;
                            if (txtHwNetDriverDate != null) txtHwNetDriverDate.Text = data.NetDriverDate;
                            if (txtHwNetStatus != null) txtHwNetStatus.Text = data.NetStatus;
                            if (badgeHwNetProvider != null && txtHwNetProvider != null) {
                                if (!string.IsNullOrEmpty(data.NetProvider)) {
                                    txtHwNetProvider.Text = data.NetProvider;
                                    badgeHwNetProvider.Visibility = Visibility.Visible;
                                } else {
                                    badgeHwNetProvider.Visibility = Visibility.Collapsed;
                                }
                            }

                            if (stackHwAudioList != null) {
                                stackHwAudioList.Children.Clear();
                                if (data.AudioEndpoints.Count == 0) {
                                    stackHwAudioList.Children.Add(new TextBlock {
                                        Text = "Keine aktiven Audiogeräte gefunden",
                                        FontSize = 11,
                                        Foreground = UIHelper.BrushGrayText
                                    });
                                } else {
                                    // Sort default devices first in their category
                                    var sortedEndpoints = new System.Collections.Generic.List<AudioDeviceEntry>();
                                    foreach (var ep in data.AudioEndpoints) if (ep.IsDefault) sortedEndpoints.Add(ep);
                                    foreach (var ep in data.AudioEndpoints) if (!ep.IsDefault) sortedEndpoints.Add(ep);

                                    foreach (var ep in sortedEndpoints) {
                                        var rowBorder = new Border {
                                            Background = ep.IsDefault ? UIHelper.GetBrush("#12232B") : Brushes.Transparent,
                                            BorderBrush = ep.IsDefault ? UIHelper.GetBrush("#0284C7") : Brushes.Transparent,
                                            BorderThickness = new Thickness(ep.IsDefault ? 1 : 0),
                                            CornerRadius = new CornerRadius(6),
                                            Padding = new Thickness(6, 4, 6, 4),
                                            Margin = new Thickness(0, 0, 0, 4)
                                        };

                                        var grid = new Grid();
                                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                                        var iconTb = new TextBlock {
                                            Text = ep.Icon + " ",
                                            FontSize = 11,
                                            VerticalAlignment = VerticalAlignment.Center,
                                            Margin = new Thickness(0, 0, 4, 0)
                                        };
                                        Grid.SetColumn(iconTb, 0);
                                        grid.Children.Add(iconTb);

                                        var nameSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                                        var tb = new TextBlock {
                                            Text = ep.Name,
                                            FontSize = 11,
                                            FontWeight = ep.IsDefault ? FontWeights.Bold : FontWeights.Normal,
                                            Foreground = ep.IsDefault ? UIHelper.GetBrush("#F8FAFC") : UIHelper.GetBrush("#E2E8F0"),
                                            VerticalAlignment = VerticalAlignment.Center,
                                            TextTrimming = TextTrimming.CharacterEllipsis,
                                            ToolTip = ep.Name
                                        };
                                        nameSp.Children.Add(tb);

                                        if (ep.IsDefault) {
                                            var defaultBadge = new Border {
                                                Background = UIHelper.GetBrush("#0C4A6E"),
                                                BorderBrush = UIHelper.GetBrush("#38BDF8"),
                                                BorderThickness = new Thickness(1),
                                                CornerRadius = new CornerRadius(3),
                                                Padding = new Thickness(5, 1, 5, 1),
                                                Margin = new Thickness(8, 0, 0, 0),
                                                VerticalAlignment = VerticalAlignment.Center
                                            };
                                            defaultBadge.Child = new TextBlock {
                                                Text = "★ STANDARD",
                                                FontSize = 9,
                                                FontWeight = FontWeights.Bold,
                                                Foreground = UIHelper.GetBrush("#38BDF8")
                                            };
                                            nameSp.Children.Add(defaultBadge);
                                        }
                                        Grid.SetColumn(nameSp, 1);
                                        grid.Children.Add(nameSp);

                                        var btnSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                                        if (!ep.IsDefault && !string.IsNullOrEmpty(ep.DeviceId)) {
                                            var btnSetDef = new Button {
                                                Content = "⭐ Als Standard",
                                                ToolTip = "Als Standard-Audiogerät für Windows festlegen",
                                                Style = (Style)window.FindResource("CardMiniBtn"),
                                                Height = 22,
                                                FontSize = 10,
                                                Padding = new Thickness(6, 1, 6, 1),
                                                Margin = new Thickness(6, 0, 0, 0),
                                                VerticalAlignment = VerticalAlignment.Center
                                            };
                                            string dId = ep.DeviceId;
                                            btnSetDef.Click += (s, e) => {
                                                Task.Run(() => {
                                                    bool ok = AudioDeviceHelper.SetDefaultEndpoint(dId);
                                                    if (!ok) {
                                                        try {
                                                            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
                                                                FileName = "control.exe",
                                                                Arguments = "mmsys.cpl sounds",
                                                                UseShellExecute = true
                                                            });
                                                        } catch {}
                                                    }
                                                    System.Threading.Thread.Sleep(300);
                                                    RefreshHardwareInfoUI(true);
                                                });
                                            };
                                            btnSp.Children.Add(btnSetDef);
                                        }

                                        var copyBtn = new Button {
                                            Content = "📋",
                                            Style = (Style)window.FindResource("CopyIconBtn"),
                                            Margin = new Thickness(6, 0, 0, 0),
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        string audName = ep.Name;
                                        SetupCopyButton(copyBtn, () => audName, "Audiogerät kopieren");
                                        btnSp.Children.Add(copyBtn);

                                        Grid.SetColumn(btnSp, 2);
                                        grid.Children.Add(btnSp);

                                        rowBorder.Child = grid;
                                        stackHwAudioList.Children.Add(rowBorder);
                                    }
                                }
                            }

                            // 9. Render Dynamic Monitor Hero Cards
                            if (gridHwMonitors != null) {
                                gridHwMonitors.Children.Clear();
                                gridHwMonitors.ColumnDefinitions.Clear();
                                gridHwMonitors.RowDefinitions.Clear();

                                int monCount = data.Monitors.Count;
                                if (monCount > 0) {
                                    // Primary display first, then by DeviceName
                                    data.Monitors.Sort(new Comparison<MonitorReportData>((a, b) => {
                                        if (a.IsPrimary && !b.IsPrimary) return -1;
                                        if (!a.IsPrimary && b.IsPrimary) return 1;
                                        return string.Compare(a.DeviceName, b.DeviceName, StringComparison.OrdinalIgnoreCase);
                                    }));

                                    if (monCount == 1) {
                                        gridHwMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                    } else {
                                        gridHwMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                        gridHwMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14, GridUnitType.Pixel) });
                                        gridHwMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                    }

                                    int curRow = 0;
                                    gridHwMonitors.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                                    for (int m = 0; m < monCount; m++) {
                                        var mon = data.Monitors[m];
                                        int col = (monCount == 1) ? 0 : ((m % 2 == 0) ? 0 : 2);
                                        if (m > 0 && m % 2 == 0) {
                                            curRow++;
                                            gridHwMonitors.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14, GridUnitType.Pixel) });
                                            gridHwMonitors.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                                        }

                                        var card = new Border {
                                            Style = (Style)window.FindResource("CardBorder"),
                                            Padding = new Thickness(18)
                                        };

                                        var stack = new StackPanel();

                                        var headerGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
                                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                                        var headerLeft = new StackPanel { Orientation = Orientation.Horizontal };
                                        var iconBox = new Border { Style = (Style)window.FindResource("IconBoxBorder") };
                                        iconBox.Child = new TextBlock { Text = "🖥️", FontSize = 15, Foreground = UIHelper.GetBrush("#F8FAFC"), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                                        headerLeft.Children.Add(iconBox);

                                        string roleTitle;
                                        string pillText;
                                        if (mon.IsPrimary || m == 0) {
                                            roleTitle = "HAUPTBILDSCHIRM (PRIMÄR)";
                                            pillText = "1. MONITOR";
                                        } else if (m == 1) {
                                            roleTitle = "ZWEITER MONITOR (ERWEITERT)";
                                            pillText = "2. MONITOR";
                                        } else if (m == 2) {
                                            roleTitle = "DRITTER MONITOR (ERWEITERT)";
                                            pillText = "3. MONITOR";
                                        } else if (m == 3) {
                                            roleTitle = "VIERTER MONITOR (ERWEITERT)";
                                            pillText = "4. MONITOR";
                                        } else {
                                            roleTitle = string.Format("{0}. MONITOR (ERWEITERT)", m + 1);
                                            pillText = string.Format("{0}. MONITOR", m + 1);
                                        }

                                        var titleStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                                        titleStack.Children.Add(new TextBlock { Text = roleTitle, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = UIHelper.GetBrush("#64748B") });
                                        titleStack.Children.Add(new TextBlock { Text = mon.Name, FontSize = 13.5, FontWeight = FontWeights.Bold, Foreground = UIHelper.GetBrush("#FFFFFF"), TextWrapping = TextWrapping.Wrap });
                                        headerLeft.Children.Add(titleStack);
                                        Grid.SetColumn(headerLeft, 0);
                                        headerGrid.Children.Add(headerLeft);

                                        var headerRight = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Top };
                                        var rolePill = new Border {
                                            Background = UIHelper.GetBrush("#1E293B"),
                                            BorderBrush = UIHelper.GetBrush("#334155"),
                                            BorderThickness = new Thickness(1),
                                            CornerRadius = new CornerRadius(4),
                                            Padding = new Thickness(7, 2, 7, 2),
                                            Margin = new Thickness(0, 0, 6, 0)
                                        };
                                        rolePill.Child = new TextBlock {
                                            Text = pillText,
                                            FontSize = 9.5,
                                            FontWeight = FontWeights.Bold,
                                            Foreground = UIHelper.GetBrush("#94A3B8")
                                        };
                                        headerRight.Children.Add(rolePill);

                                        var copyBtn = new Button {
                                            Content = "📋",
                                            Style = (Style)window.FindResource("CopyIconBtn")
                                        };
                                        string copyText = mon.Name;
                                        SetupCopyButton(copyBtn, () => copyText, "Monitor-Modell kopieren");
                                        headerRight.Children.Add(copyBtn);
                                        Grid.SetColumn(headerRight, 1);
                                        headerGrid.Children.Add(headerRight);

                                        stack.Children.Add(headerGrid);

                                        var sep = new Border { Style = (Style)window.FindResource("SeparatorLine"), Margin = new Thickness(0, 4, 0, 10) };
                                        stack.Children.Add(sep);

                                        Action<string, string, bool, Brush> addRow = (label, val, bold, fg) => {
                                            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                                            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                                            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                            row.Children.Add(new TextBlock { Text = label, Foreground = UIHelper.GetBrush("#94A3B8"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
                                            var valTb = new TextBlock { Text = val, Foreground = fg ?? UIHelper.GetBrush("#F8FAFC"), FontSize = 11.5, TextWrapping = TextWrapping.Wrap, VerticalAlignment = VerticalAlignment.Center };
                                            if (bold) valTb.FontWeight = FontWeights.Bold;
                                            Grid.SetColumn(valTb, 1);
                                            row.Children.Add(valTb);
                                            stack.Children.Add(row);
                                        };

                                        Action<string, UIElement> addElementRow = (label, element) => {
                                            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                                            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                                            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                            row.Children.Add(new TextBlock { Text = label, Foreground = UIHelper.GetBrush("#94A3B8"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
                                            Grid.SetColumn(element, 1);
                                            row.Children.Add(element);
                                            stack.Children.Add(row);
                                        };

                                        addRow("Aktive Auflösung:", string.Format("{0} x {1} Pixel{2}", mon.Width, mon.Height, mon.IsPrimary ? " (Primär)" : ""), false, null);

                                        if (mon.SupportedRates.Count > 0) {
                                            bool isMaxHz = mon.RefreshRate >= mon.MaxRefreshRate;

                                            var cmbHz = new ComboBox {
                                                Height = 26,
                                                Width = 250,
                                                HorizontalAlignment = HorizontalAlignment.Left,
                                                FontSize = 11,
                                                FontWeight = FontWeights.SemiBold,
                                                Background = UIHelper.GetBrush("#121620"),
                                                BorderBrush = isMaxHz ? UIHelper.GetBrush("#1E293B") : UIHelper.GetBrush("#EF4444"),
                                                Foreground = isMaxHz ? UIHelper.GetBrush("#22C55E") : UIHelper.GetBrush("#EF4444")
                                            };

                                            int selectedIdx = -1;
                                            for (int r = 0; r < mon.SupportedRates.Count; r++) {
                                                int rate = mon.SupportedRates[r];
                                                string itemText = string.Format("{0} Hz", rate);
                                                if (rate == mon.MaxRefreshRate) {
                                                    itemText += " (Empfohlen)";
                                                } else {
                                                    itemText += string.Format(" (Reduziert - {0} Hz möglich)", mon.MaxRefreshRate);
                                                }
                                                if (rate == mon.RefreshRate && selectedIdx < 0) selectedIdx = r;

                                                var cItem = new ComboBoxItem {
                                                    Content = itemText,
                                                    Tag = rate,
                                                    FontSize = 11,
                                                    Foreground = (rate == mon.MaxRefreshRate) ? UIHelper.GetBrush("#22C55E") : UIHelper.GetBrush("#EF4444"),
                                                    FontWeight = (rate == mon.MaxRefreshRate || rate == mon.RefreshRate) ? FontWeights.Bold : FontWeights.Normal
                                                };
                                                cmbHz.Items.Add(cItem);
                                            }

                                            bool isUpdatingHz = true;
                                            if (selectedIdx >= 0) cmbHz.SelectedIndex = selectedIdx;
                                            else if (cmbHz.Items.Count > 0) cmbHz.SelectedIndex = 0;
                                            isUpdatingHz = false;

                                            var currentMon = mon;
                                            cmbHz.SelectionChanged += (s, e) => {
                                                if (isUpdatingHz) return;
                                                var item = cmbHz.SelectedItem as ComboBoxItem;
                                                if (item == null) return;
                                                if (item.Tag == null || !(item.Tag is int)) return;
                                                int targetHz = (int)item.Tag;
                                                if (targetHz == currentMon.RefreshRate) return;

                                                bool success = ApplyMonitorRefreshRate(currentMon.DeviceName, targetHz, currentMon.Width, currentMon.Height);
                                                if (success) {
                                                    currentMon.RefreshRate = targetHz;
                                                    bool nowMax = (currentMon.RefreshRate >= currentMon.MaxRefreshRate);
                                                    cmbHz.BorderBrush = nowMax ? UIHelper.GetBrush("#1E293B") : UIHelper.GetBrush("#EF4444");
                                                    cmbHz.Foreground = nowMax ? UIHelper.GetBrush("#22C55E") : UIHelper.GetBrush("#EF4444");
                                                } else {
                                                    isUpdatingHz = true;
                                                    for (int i = 0; i < cmbHz.Items.Count; i++) {
                                                        var ci = cmbHz.Items[i] as ComboBoxItem;
                                                        if (ci != null && ci.Tag != null && (int)ci.Tag == currentMon.RefreshRate) {
                                                             cmbHz.SelectedIndex = i;
                                                             break;
                                                        }
                                                    }
                                                    isUpdatingHz = false;
                                                }
                                            };

                                            addElementRow("Verfügbare Hz:", cmbHz);
                                        }

                                        card.Child = stack;
                                        Grid.SetColumn(card, col);
                                        Grid.SetRow(card, curRow * 2);
                                        gridHwMonitors.Children.Add(card);
                                    }
                                }
                            }
                        }));
                    }
                });

            } catch {
            }
        }

        public static string DecodeWindowsProductKey(byte[] digitalProductId) {
            try {
                if (digitalProductId == null || digitalProductId.Length < 67) return "";
                const int keyOffset = 52;
                int isWin8 = (digitalProductId[66] / 6) & 1;
                digitalProductId[66] = (byte)((digitalProductId[66] & 0xF7) | ((isWin8 & 2) * 4));

                const string digits = "BCDFGHJKMPQRTVWXY2346789";
                int last = 0;
                char[] dChars = new char[25];
                byte[] hexMap = new byte[15];
                Array.Copy(digitalProductId, keyOffset, hexMap, 0, 15);

                for (int i = 24; i >= 0; i--) {
                    int current = 0;
                    for (int j = 14; j >= 0; j--) {
                        current = (current * 256) ^ hexMap[j];
                        hexMap[j] = (byte)(current / 24);
                        current %= 24;
                        last = current;
                    }
                    dChars[i] = digits[current];
                }

                string raw = new string(dChars);
                string key = raw;
                if (isWin8 != 0 && last < raw.Length) {
                    string part1 = raw.Substring(1, last);
                    string part2 = raw.Substring(last + 1);
                    key = part1 + "N" + part2;
                }

                StringBuilder formatted = new StringBuilder();
                for (int i = 0; i < 25 && i < key.Length; i++) {
                    formatted.Append(key[i]);
                    if ((i + 1) % 5 == 0 && i < 24) formatted.Append('-');
                }
                return formatted.ToString();
            } catch {
                return "";
            }
        }
    }

    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    [ComImport]
    public class MMDeviceEnumeratorComObject { }

    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator {
        [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);
        [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice endpoint);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice endpoint);
        [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr client);
        [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr client);
    }

    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }

    [Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice {
        [PreserveSig] int Activate(ref Guid id, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object interfacePointer);
        [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)]
    public struct PROPERTYKEY {
        public Guid fmtid;
        public uint pid;
    }

    [StructLayout(LayoutKind.Explicit)]
    public struct PROPVARIANT {
        [FieldOffset(0)] public ushort vt;
        [FieldOffset(8)] public IntPtr pwszVal;
    }

    [Guid("886d8eeb-8cf2-4446-8d02-cdba1dbdcf99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
        [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
        [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT pv);
        [PreserveSig] int Commit();
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    public class CPolicyConfigClient { }

    [Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPolicyConfig {
        [PreserveSig] int GetMixFormat(string pszDeviceName, IntPtr ppFormat);
        [PreserveSig] int GetDeviceFormat(string pszDeviceName, int bDefault, IntPtr ppFormat);
        [PreserveSig] int ResetDeviceFormat(string pszDeviceName);
        [PreserveSig] int SetDeviceFormat(string pszDeviceName, IntPtr pEndpointFormat, IntPtr pMixFormat);
        [PreserveSig] int GetProcessingPeriod(string pszDeviceName, int bDefault, IntPtr pmftDefaultPeriod, IntPtr pmftMinimumPeriod);
        [PreserveSig] int SetProcessingPeriod(string pszDeviceName, IntPtr pmftPeriod);
        [PreserveSig] int GetShareMode(string pszDeviceName, IntPtr pMode);
        [PreserveSig] int SetShareMode(string pszDeviceName, IntPtr mode);
        [PreserveSig] int GetPropertyValue(string pszDeviceName, IntPtr pKey, IntPtr pv);
        [PreserveSig] int SetPropertyValue(string pszDeviceName, IntPtr pKey, IntPtr pv);
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string pszDeviceName, int role);
        [PreserveSig] int SetEndpointVisibility(string pszDeviceName, int bVisible);
    }

    public static class AudioDeviceHelper {
        [DllImport("ole32.dll")]
        private static extern int PropVariantClear(ref PROPVARIANT pvar);

        public static string GetDefaultEndpointId(int dataFlow /*0 = Render, 1 = Capture*/) {
            try {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                if (enumerator != null) {
                    for (int role = 0; role <= 2; role++) {
                        IMMDevice dev;
                        if (enumerator.GetDefaultAudioEndpoint(dataFlow, role, out dev) == 0 && dev != null) {
                            string id;
                            int hr = dev.GetId(out id);
                            Marshal.ReleaseComObject(dev);
                            if (hr == 0 && !string.IsNullOrEmpty(id)) {
                                return id;
                            }
                        }
                    }
                }
            } catch {}
            return "";
        }

        public static System.Collections.Generic.List<MainWindowLogic.AudioDeviceEntry> GetAudioEndpoints() {
            var list = new System.Collections.Generic.List<MainWindowLogic.AudioDeviceEntry>();
            try {
                var enumerator = (IMMDeviceEnumerator)(new MMDeviceEnumeratorComObject());
                if (enumerator != null) {
                    for (int df = 0; df <= 1; df++) {
                        string cat = (df == 0) ? "Wiedergabe" : "Aufnahme";
                        string defId = GetDefaultEndpointId(df);
                        IMMDeviceCollection col;
                        if (enumerator.EnumAudioEndpoints(df, 1 /*DEVICE_STATE_ACTIVE*/, out col) == 0 && col != null) {
                            uint count;
                            col.GetCount(out count);
                            for (uint i = 0; i < count; i++) {
                                IMMDevice dev;
                                if (col.Item(i, out dev) == 0 && dev != null) {
                                    string id = "";
                                    dev.GetId(out id);
                                    string name = "";
                                    IPropertyStore props;
                                    if (dev.OpenPropertyStore(0, out props) == 0 && props != null) {
                                        var PKEY_Device_FriendlyName = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 14 };
                                        var PKEY_Device_DeviceDesc = new PROPERTYKEY { fmtid = new Guid("a45c254e-df1c-4efd-8020-67d146a850e0"), pid = 2 };
                                        var PKEY_DeviceInterface_FriendlyName = new PROPERTYKEY { fmtid = new Guid("026e516e-b814-414b-83cd-856d6fef4822"), pid = 2 };

                                        PROPVARIANT pv;
                                        props.GetValue(ref PKEY_Device_FriendlyName, out pv);
                                        if (pv.pwszVal != IntPtr.Zero) name = Marshal.PtrToStringUni(pv.pwszVal);
                                        PropVariantClear(ref pv);

                                        if (string.IsNullOrEmpty(name)) {
                                            props.GetValue(ref PKEY_DeviceInterface_FriendlyName, out pv);
                                            if (pv.pwszVal != IntPtr.Zero) name = Marshal.PtrToStringUni(pv.pwszVal);
                                            PropVariantClear(ref pv);
                                        }
                                        if (string.IsNullOrEmpty(name)) {
                                            props.GetValue(ref PKEY_Device_DeviceDesc, out pv);
                                            if (pv.pwszVal != IntPtr.Zero) name = Marshal.PtrToStringUni(pv.pwszVal);
                                            PropVariantClear(ref pv);
                                        }
                                        Marshal.ReleaseComObject(props);
                                    }

                                    if (!string.IsNullOrEmpty(name)) {
                                        string icon = (df == 0) ? "🔊" : "🎙️";
                                        if (df == 0) {
                                            if (name.IndexOf("Kopfhörer", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                name.IndexOf("Headset", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                name.IndexOf("Headphone", StringComparison.OrdinalIgnoreCase) >= 0) {
                                                icon = "🎧";
                                            } else if (name.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                       name.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                       name.IndexOf("Display", StringComparison.OrdinalIgnoreCase) >= 0) {
                                                icon = "🖥️";
                                            }
                                        }
                                        bool isDef = !string.IsNullOrEmpty(defId) && string.Equals(id, defId, StringComparison.OrdinalIgnoreCase);
                                        list.Add(new MainWindowLogic.AudioDeviceEntry {
                                            DeviceId = id,
                                            Name = name,
                                            Category = cat,
                                            Icon = icon,
                                            IsDefault = isDef
                                        });
                                    }
                                    Marshal.ReleaseComObject(dev);
                                }
                            }
                            Marshal.ReleaseComObject(col);
                        }
                    }
                }
            } catch {}
            return list;
        }

        public static bool SetDefaultEndpoint(string deviceId) {
            if (string.IsNullOrEmpty(deviceId)) return false;
            try {
                var policy = (IPolicyConfig)new CPolicyConfigClient();
                if (policy != null) {
                    policy.SetDefaultEndpoint(deviceId, 0); // eConsole
                    policy.SetDefaultEndpoint(deviceId, 1); // eMultimedia
                    policy.SetDefaultEndpoint(deviceId, 2); // eCommunications
                    return true;
                }
            } catch {}
            return false;
        }
    }
}
