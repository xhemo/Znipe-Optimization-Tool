using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Security.AccessControl;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Globalization;
using Microsoft.Win32;
using System.Management;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace ZnipeOptimizationTool {
    public class PowerPlanItem {
        public string Guid { get; set; }
        public string Name { get; set; }
        public bool IsActive { get; set; }
        public override string ToString() { return Name; }
    }

    public class LanAdapterInfo {
        public string Description { get; set; }
        public string DriverVersion { get; set; }
        public string DriverDate { get; set; }
        public string Provider { get; set; }
        public string PnpInstanceId { get; set; }
    }

    public partial class MainWindowLogic {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
        private const int DWMWA_BORDER_COLOR = 34;
        private const int DWMWA_CAPTION_COLOR = 35;
        private const int DWMWCP_ROUND = 2;



        private Window window;
        private ToggleButton toggleQueue;
        private TextBlock txtSwitchSubtitle;
        private TextBlock txtMouseQueueReboot;
        private Button btnAdjustMouseQueue;
        private Popup popupMouseQueue;
        private Slider sliderMouseQueue;
        private TextBlock txtMouseQueueSliderVal;
        private Button btnMouseQueuePreset16;
        private Button btnMouseQueueApply;
        private Button btnOpenRegedit;

        private Border bannerAdminWarning;
        private Button btnRelaunchAdmin;
        private Button btnGlobalRestart;
        private Button btnGlobalShutdown;

        // Restart Modal Controls
        private Grid overlayRestartModal;
        private Button btnRestartModalClose;
        private Button btnRestartModalCancel;
        private Border btnRestartNormal;
        private Border btnRestartUefiOption;
        private Border btnRestartSafeMode;

        // Custom Window Chrome & Breadcrumb Controls
        private FrameworkElement headerDragArea;
        private FrameworkElement sidebarBrandHeader;
        private FrameworkElement sidebarTopDragArea;
        private Button btnWinMinimize;
        private Button btnWinMaximize;
        private Button btnWinClose;
        private TextBlock txtBreadcrumbIcon;
        private TextBlock txtBreadcrumbPath;

        // Responsive Layout Elements
        private ColumnDefinition colSidebar;
        private Grid gridHomeTopCards;
        private Grid panelCpuGpuBlock;
        private Grid gridCpuGpuRow;
        private Border cardHomeCpu;
        private Border cardHomeGpu;
        private Border cardHomeRam;
        private Border cardHomeGpuLimits;
        private Grid gridHomeMonitors;
        private Border cardMonitorTemp;
        private Border cardMonitorLoad;
        private Border cardMonitorPower;
        private Grid gridHwHeroCards;
        private Border cardHwCpu;
        private Border cardHwGpu;
        private Border cardHwBoard;
        private Border cardHwRam;
        private Grid gridHwNetAudio;
        private Border cardHwNet;
        private Border cardHwAudio;
        private Grid gridBbMain;
        private Border cardBbGrade;
        private Grid gridBbMetrics;
        private Border cardBbIdle;
        private Border cardBbDl;
        private Border cardBbUl;
        private Grid gridMouseTweaks;
        private Border cardMouseQueue;
        private Border cardMousePriority;
        private Border cardMouseCoreIso;
        private Border cardMousePowerPlan;
        private Border cardLanEnergy;
        private Border cardPagingExecutive;
        private Border cardSystemResponsiveness;
        private Border cardNetworkThrottling;
        private Border cardMmcssGames;
        private Border cardGameDvr;
        private Border cardUsbSelectiveSuspend;
        private Border cardRealtekRss;
        private TextBox txtSearchMouseTweaks;
        private TextBlock txtSearchMousePlaceholder;
        private Button btnClearSearchMouseTweaks;
        private Border panelMouseNoResults;
        private RadioButton radFilterMouseAll;
        private RadioButton radFilterMouseActive;
        private RadioButton radFilterMouseInactive;
        private int currentResponsiveTier = -1;

        // Tabs (Active Sidebar Tabs)
        private RadioButton tabBtnHome;
        private RadioButton tabBtnHardwareInfo;
        private RadioButton tabBtnDrives;
        private RadioButton tabBtnBufferbloat;
        private RadioButton tabBtnMouse;
        private RadioButton tabBtnTimerRes;
        private RadioButton tabBtnBcd;
        private RadioButton tabBtnOffice;
        private RadioButton tabBtnChipset;
        private RadioButton tabBtnGamebar;
        private RadioButton tabBtnApps;
        private RadioButton tabBtnUpdates;
        private RadioButton tabBtnBios;
        private RadioButton tabBtnUsbDirect;
        private RadioButton tabBtnAioCooler;

        private Border tabContentHome;
        private Border tabContentHardwareInfo;
        private Border tabContentDrives;
        private Border tabContentBufferbloat;
        private Border tabContentMouse;
        private Border tabContentTimerRes;
        private Border tabContentBcd;
        private Border tabContentOffice;
        private Border tabContentChipset;
        private Border tabContentGamebar;
        private Border tabContentApps;
        private Border tabContentUpdates;
        private Border tabContentBios;
        private Border tabContentUsbDirect;
        private Border tabContentAioCooler;

        // Sidebar Activity Spinners (only tabs with background operations)
        private Viewbox spinnerTabHome;
        private Viewbox spinnerTabBufferbloat;
        private Viewbox spinnerTabOffice;
        private Viewbox spinnerTabChipset;
        private Viewbox spinnerTabApps;
        private Viewbox spinnerTabUsbDirect;
        private Viewbox spinnerTabAioCooler;

        // Modular Tab Lifecycle & Activity Registry
        private class TabDescriptor {
            public string Id { get; set; }
            public RadioButton Button { get; set; }
            public Border Content { get; set; }
            public Viewbox Spinner { get; set; }
            public System.Windows.Shapes.Ellipse DoneDot { get; set; }
            public bool WasBusy { get; set; }
            public bool HasCompletedNotice { get; set; }
            public string Icon { get; set; }
            public string Title { get; set; }
            public Action OnActivate { get; set; }
            public Action OnDeactivate { get; set; }
            public Func<TabActivityState> CheckActivity { get; set; }
        }

        private struct TabActivityState {
            public bool IsActive;
            public string ColorHex;
            public string Tooltip;

            public TabActivityState(bool isActive, string colorHex = "#38BDF8", string tooltip = null) {
                IsActive = isActive;
                ColorHex = colorHex;
                Tooltip = tooltip;
            }
        }

        private readonly List<TabDescriptor> registeredTabs = new List<TabDescriptor>();
        private TabDescriptor currentActiveTab = null;

        // USB Direct Console Controls
        private Border pillUsbStatus;
        private TextBlock txtPillUsbStatus;
        private Button btnUsbRun;
        private TextBlock txtUsbRunIcon;
        private TextBlock txtUsbRunLabel;
        private Button btnUsbCopy;
        private Button btnUsbClear;
        private RichTextBox txtUsbConsoleOutput;
        private Paragraph paraUsbConsoleOutput;
        private Process usbDirectProcess;
        private object usbProcessLock = new object();
        private Border panelUsbHardwareEmpty;
        private StackPanel panelUsbHardwareContent;
        private TextBlock txtUsbDeviceCount;
        private StackPanel stackUsbDevicesChip0;
        private StackPanel stackUsbDevicesChip1;
        private StackPanel stackUsbDevicesChip2;

        public class UsbDetectedItem {
            public string Name;
            public int ChipCount;
            public string Vid;
            public string Pid;
            public string Note;
        }

        // Home Dashboard Controls
        // Home Telemetry Controls: 3 grouped tiles per hardware card
        private TextBlock txtCpuTempRow;
        private ColumnDefinition colCpuTempBarFill;
        private ColumnDefinition colCpuTempBarEmpty;
        private TextBlock txtCpuLoadRow;
        private ColumnDefinition colCpuLoadBarFill;
        private ColumnDefinition colCpuLoadBarEmpty;
        private TextBlock txtCpuPowerRow;
        private ColumnDefinition colCpuPowerBarFill;
        private ColumnDefinition colCpuPowerBarEmpty;

        private TextBlock txtGpuTempRow;
        private ColumnDefinition colGpuTempBarFill;
        private ColumnDefinition colGpuTempBarEmpty;
        private TextBlock txtGpuLoadRow;
        private ColumnDefinition colGpuLoadBarFill;
        private ColumnDefinition colGpuLoadBarEmpty;
        private TextBlock txtGpuPowerRow;
        private ColumnDefinition colGpuPowerBarFill;
        private ColumnDefinition colGpuPowerBarEmpty;

        private TextBlock txtRamLoadRow;
        private ColumnDefinition colRamLoadBarFill;
        private ColumnDefinition colRamLoadBarEmpty;
        private TextBlock txtRamUsedRow;
        private ColumnDefinition colRamMemBarFill;
        private ColumnDefinition colRamMemBarEmpty;
        private TextBlock txtRamFreeRow;
        private ColumnDefinition colRamFreeBarFill;
        private ColumnDefinition colRamFreeBarEmpty;
        private TextBlock txtRamExpoTitle;
        private TextBlock txtRamExpoSub;
        private Border pillRamExpoStatus;
        private TextBlock txtRamExpoVal;
        private bool isRamExpoDetected = false;
        private bool isRamExpoQueryRunning = false;
        private string cachedRamExpoTitle = "EXPO / XMP";
        private string cachedRamExpoSub = "lädt...";
        private string cachedRamExpoVal = "LÄDT...";
        private bool cachedRamExpoIsActive = false;
        private TextBlock txtTotalPowerRow;
        private TextBlock txtTotalPowerSubtitle;
        private ColumnDefinition colTotalPowerBarFill;
        private ColumnDefinition colTotalPowerBarEmpty;

        private TextBlock txtGpuLimitPowerVal;
        private TextBlock txtGpuLimitPowerSub;
        private ColumnDefinition colGpuLimitBarFill;
        private ColumnDefinition colGpuLimitBarEmpty;
        private Border barGpuLimitFill;
        private TextBlock txtGpuLimitTempVal;
        private TextBlock txtGpuLimitTempSub;
        private ColumnDefinition colGpuTempLimitBarFill;
        private ColumnDefinition colGpuTempLimitBarEmpty;
        private Border barGpuTempLimitFill;
        private TextBlock txtGpuLimitStatusSummary;
        private Border pillGpuLimitSummary;
        private Border pillGpuPcieStatus;
        private TextBlock txtGpuPcieVal;
        private TextBlock txtGpuPcieSub;

        // GPU Limits / Drossel Switch & Modal Controls
        private Border pillToggleGpuLimit;
        private TextBlock txtToggleGpuLimitDot;
        private TextBlock txtToggleGpuLimitStatus;

        private Grid overlayGpuLimitsModal;
        private Button btnCloseGpuLimitsModal;
        private Button btnCancelGpuLimits;
        private Button btnApplyGpuLimits;
        private Button btnResetGpuLimits;
        private Slider sliderGpuClock;
        private TextBlock txtGpuClockTargetVal;
        private TextBlock txtGpuClockTargetRange;
        private TextBlock txtGpuClockCurrentInfo;

        // 3 Dedicated Simultaneous Real-Time Monitors
        private Canvas canvasTempGraph;
        private System.Windows.Shapes.Path pathTempCpu;
        private System.Windows.Shapes.Path pathTempGpu;

        private Canvas canvasLoadGraph;
        private System.Windows.Shapes.Path pathLoadCpu;
        private System.Windows.Shapes.Path pathLoadGpu;

        private Canvas canvasPowerGraph;
        private System.Windows.Shapes.Path pathPowerCpu;
        private System.Windows.Shapes.Path pathPowerGpu;
        private TextBlock txtGraphPowerYMax;
        private TextBlock txtGraphPowerY75;
        private TextBlock txtGraphPowerY50;
        private TextBlock txtGraphPowerY25;

        // Monitor Filter & Hover Elements
        private Button btnFilterMonitorCycle;
        private int activeMonitorFilter = 0; // 0 = Both, 1 = CPU Only, 2 = GPU Only

        private System.Windows.Shapes.Line lineHoverTemp;
        private System.Windows.Shapes.Ellipse dotHoverTempCpu;
        private System.Windows.Shapes.Ellipse dotHoverTempGpu;
        private Border badgeHoverTemp;
        private TextBlock txtHoverTempTime;
        private TextBlock txtHoverTempCpu;
        private TextBlock txtHoverTempGpu;
        private StackPanel panelHoverTempCpu;
        private StackPanel panelHoverTempGpu;
        private bool isHoverTemp = false;
        private Point lastPosTemp = new Point();

        private System.Windows.Shapes.Line lineHoverLoad;
        private System.Windows.Shapes.Ellipse dotHoverLoadCpu;
        private System.Windows.Shapes.Ellipse dotHoverLoadGpu;
        private Border badgeHoverLoad;
        private TextBlock txtHoverLoadTime;
        private TextBlock txtHoverLoadCpu;
        private TextBlock txtHoverLoadGpu;
        private StackPanel panelHoverLoadCpu;
        private StackPanel panelHoverLoadGpu;
        private bool isHoverLoad = false;
        private Point lastPosLoad = new Point();

        private System.Windows.Shapes.Line lineHoverPower;
        private System.Windows.Shapes.Ellipse dotHoverPowerCpu;
        private System.Windows.Shapes.Ellipse dotHoverPowerGpu;
        private Border badgeHoverPower;
        private TextBlock txtHoverPowerTime;
        private TextBlock txtHoverPowerCpu;
        private TextBlock txtHoverPowerGpu;
        private TextBlock txtHoverPowerTotal;
        private StackPanel panelHoverPowerCpu;
        private StackPanel panelHoverPowerGpu;
        private StackPanel panelHoverPowerTotal;
        private Border sepHoverPowerTotal;
        private bool isHoverPower = false;
        private Point lastPosPower = new Point();
        private double lastMaxWattScale = 150.0;

        // Hardware Stress Test Controls
        private Border pillStressStatus;
        private TextBlock txtStressStatus;
        private TextBlock txtStressDuration;
        private Button btnStressToggle;
        private TextBlock txtStressBtnIcon;
        private TextBlock txtStressBtnLabel;
        private TextBlock txtStressLiveFeedback;
        private RadioButton radStressBoth;
        private RadioButton radStressCpu;
        private RadioButton radStressGpu;
        private DispatcherTimer stressDurationTimer;


        private Button btnViewDetailsCpu;
        private Button btnViewDetailsGpu;
        private Button btnViewDetailsRam;

        // Hardware Details Modal Overlay
        private Grid overlayHardwareDetails;
        private TextBlock txtHwDetailIcon;
        private TextBlock txtHwDetailTitle;
        private TextBlock txtHwDetailSubtitle;
        private Button btnCloseHwDetails;
        private Button btnDismissHwDetails;
        private WrapPanel panelHwDetailMetrics;
        private string activeHwDetailMode = null;

        // Modern In-App Dialog Modal
        private Grid overlayModal;
        private Border borderModalIcon;
        private TextBlock txtModalIcon;
        private TextBlock txtModalTitle;
        private TextBlock txtModalCategory;
        private Button btnModalClose;
        private TextBlock txtModalMessage;
        private Button btnModalCancel;
        private Button btnModalCustom;
        private Button btnModalConfirm;
        private TaskCompletionSource<bool> modalTcs;
        private TaskCompletionSource<ModalChoice> modalChoiceTcs;

        // Telemetry State & Timer
        private DispatcherTimer telemetryTimer;
        private DispatcherTimer smoothGraphTimer;
        private DateTime lastTelemetryTimestamp = DateTime.MinValue;
        private double _measuredTelemetryIntervalMs = 500.0;
        private DispatcherTimer sidebarActivityTimer;
        private bool isHomeActive = true;
        private bool isInitialTelemetryLoaded = false;
        private double latestCpuLoad = 0.0;
        private double latestCpuTemp = 0.0;
        private double latestGpuLoad = 0.0;
        private double latestGpuTemp = 0.0;
        private double latestCpuPower = 0.0;
        private double latestGpuPower = 0.0;
        private double latestGpuPowerLimit = 0.0;
        private double latestGpuPowerDefault = 0.0;
        private double latestGpuPowerMin = 0.0;
        private double latestGpuPowerMax = 0.0;
        private double latestGpuThrottleTemp = 83.0;
        private double latestGpuTempLimit = 0.0;
        private double latestGpuClock = 0.0;
        private double latestGpuMemoryClock = 0.0;
        private double latestGpuFanPercent = 0.0;
        private double latestGpuFanRpm = 0.0;
        private double latestGpuVramUsedMb = 0.0;
        private double latestGpuHotspot = 0.0;
        private double latestGpuTempMemory = 0.0;
        private string latestGpuName = "";
        private List<double> cpuTempHistory = new List<double>();
        private List<double> gpuTempHistory = new List<double>();
        private List<double> cpuLoadHistory = new List<double>();
        private List<double> gpuLoadHistory = new List<double>();
        private List<double> cpuPowerHistory = new List<double>();
        private List<double> gpuPowerHistory = new List<double>();
        private int _isTelemetryQueryRunning = 0;

        // NVML P/Invoke for dynamic NVIDIA GPU power limits and thermal targets
        [DllImport("nvml.dll", EntryPoint = "nvmlInit_v2")]
        private static extern int nvmlInit();

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetHandleByIndex_v2")]
        private static extern int nvmlDeviceGetHandleByIndex(uint index, out IntPtr device);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetEnforcedPowerLimit")]
        private static extern int nvmlDeviceGetEnforcedPowerLimit(IntPtr device, out uint limitMw);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerManagementDefaultLimit")]
        private static extern int nvmlDeviceGetPowerManagementDefaultLimit(IntPtr device, out uint defaultLimitMw);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerManagementLimitConstraints")]
        private static extern int nvmlDeviceGetPowerManagementLimitConstraints(IntPtr device, out uint minLimitMw, out uint maxLimitMw);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperatureThreshold")]
        private static extern int nvmlDeviceGetTemperatureThreshold(IntPtr device, int thresholdType, out uint temp);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetCurrPcieLinkWidth")]
        private static extern int nvmlDeviceGetCurrPcieLinkWidth(IntPtr device, out uint width);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetMaxPcieLinkWidth")]
        private static extern int nvmlDeviceGetMaxPcieLinkWidth(IntPtr device, out uint width);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetCurrPcieLinkGeneration")]
        private static extern int nvmlDeviceGetCurrPcieLinkGeneration(IntPtr device, out uint currLinkGen);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetMaxPcieLinkGeneration")]
        private static extern int nvmlDeviceGetMaxPcieLinkGeneration(IntPtr device, out uint maxLinkGen);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetTemperature")]
        private static extern int nvmlDeviceGetTemperature(IntPtr device, int sensorType, out uint temp);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetPowerUsage")]
        private static extern int nvmlDeviceGetPowerUsage(IntPtr device, out uint powerMw);

        [StructLayout(LayoutKind.Sequential)]
        public struct NvmlUtilization {
            public uint gpu;
            public uint memory;
        }

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetUtilizationRates")]
        private static extern int nvmlDeviceGetUtilizationRates(IntPtr device, out NvmlUtilization utilization);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetClockInfo")]
        private static extern int nvmlDeviceGetClockInfo(IntPtr device, int clockType, out uint clock);

        [StructLayout(LayoutKind.Sequential)]
        public struct NvmlMemory {
            public ulong total;
            public ulong free;
            public ulong used;
        }

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetMemoryInfo")]
        private static extern int nvmlDeviceGetMemoryInfo(IntPtr device, out NvmlMemory memory);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetFanSpeed")]
        private static extern int nvmlDeviceGetFanSpeed(IntPtr device, out uint speed);

        [StructLayout(LayoutKind.Sequential)]
        public struct NvmlBAR1Memory {
            public ulong total;
            public ulong free;
            public ulong used;
        }

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetBAR1MemoryInfo")]
        private static extern int nvmlDeviceGetBAR1MemoryInfo(IntPtr device, out NvmlBAR1Memory bar1Memory);

        [DllImport("nvml.dll", EntryPoint = "nvmlDeviceGetName")]
        private static extern int nvmlDeviceGetName(IntPtr device, byte[] name, uint length);

        private bool isNvmlInitialized = false;
        private IntPtr nvmlDeviceHandle = IntPtr.Zero;
        private bool hasGpu = false;

        [DllImport("cfgmgr32.dll", EntryPoint = "CM_Locate_DevNodeW", CharSet = CharSet.Unicode)]
        private static extern int CM_Locate_DevNode(out uint pdnDevInst, string pDeviceID, int ulFlags);

        [StructLayout(LayoutKind.Sequential)]
        private struct DEVPROPKEY {
            public Guid fmtid;
            public uint pid;
        }

        [DllImport("cfgmgr32.dll", EntryPoint = "CM_Get_DevNode_PropertyW", CharSet = CharSet.Unicode)]
        private static extern int CM_Get_DevNode_Property(
            uint dnDevInst,
            ref DEVPROPKEY PropertyKey,
            out uint PropertyType,
            byte[] PropertyBuffer,
            ref uint PropertyBufferSize,
            int ulFlags
        );

        private static readonly Guid GUID_DEVPROPKEY_PCI = new Guid("3ab22e31-8264-4b4e-9af5-a8d2d8e33e62");
        private static string cachedGpuPnpId = null;
        private static bool isGpuPnpIdScanStarted = false;

        private static void EnsureGpuPnpIdDiscovered() {
            if (isGpuPnpIdScanStarted) return;
            isGpuPnpIdScanStarted = true;
            Task.Run(() => {
                try {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, PNPDeviceID, AdapterRAM FROM Win32_VideoController")) {
                        string bestPnpId = null;
                        long bestVram = -1;
                        foreach (ManagementObject obj in searcher.Get()) {
                            string pnpId = obj["PNPDeviceID"] != null ? obj["PNPDeviceID"].ToString() : "";
                            if (string.IsNullOrEmpty(pnpId) || !pnpId.StartsWith("PCI\\", StringComparison.OrdinalIgnoreCase)) continue;

                            string name = obj["Name"] != null ? obj["Name"].ToString() : "";
                            long vram = 0;
                            if (obj["AdapterRAM"] != null) long.TryParse(obj["AdapterRAM"].ToString(), out vram);

                            bool isDiscrete = name.IndexOf("RTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             name.IndexOf("GTX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             name.IndexOf("Radeon RX", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             name.IndexOf("Arc", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                             name.IndexOf("GeForce", StringComparison.OrdinalIgnoreCase) >= 0;

                            if (isDiscrete || vram > bestVram || bestPnpId == null) {
                                bestPnpId = pnpId;
                                if (vram > bestVram) bestVram = vram;
                                if (isDiscrete) bestVram = Math.Max(bestVram, 1024L * 1024L * 1024L);
                            }
                        }
                        cachedGpuPnpId = bestPnpId ?? string.Empty;
                    }
                } catch {
                    cachedGpuPnpId = string.Empty;
                }
            });
        }

        private static uint ReadPciPropertyUInt32(uint devInst, uint pid) {
            try {
                DEVPROPKEY key = new DEVPROPKEY { fmtid = GUID_DEVPROPKEY_PCI, pid = pid };
                uint type;
                byte[] buf = new byte[4];
                uint size = (uint)buf.Length;
                int cr = CM_Get_DevNode_Property(devInst, ref key, out type, buf, ref size, 0);
                if (cr == 0 && size == 4) {
                    return BitConverter.ToUInt32(buf, 0);
                }
            } catch {}
            return 0;
        }

        private bool GetRealGpuPcieLinkInfo(out uint curGen, out uint curWidth, out uint maxGen, out uint maxWidth) {
            curGen = 0;
            curWidth = 0;
            maxGen = 0;
            maxWidth = 0;

            // 1. Direct NVML query for NVIDIA GPUs
            if (isNvmlInitialized && nvmlDeviceHandle != IntPtr.Zero) {
                try {
                    uint cWidth = 0, mWidth = 0, cGen = 0, mGen = 0;
                    if (nvmlDeviceGetCurrPcieLinkWidth(nvmlDeviceHandle, out cWidth) == 0 &&
                        nvmlDeviceGetMaxPcieLinkWidth(nvmlDeviceHandle, out mWidth) == 0 &&
                        mWidth > 0) {
                        nvmlDeviceGetCurrPcieLinkGeneration(nvmlDeviceHandle, out cGen);
                        nvmlDeviceGetMaxPcieLinkGeneration(nvmlDeviceHandle, out mGen);
                        curWidth = cWidth;
                        maxWidth = mWidth;
                        curGen = cGen;
                        maxGen = mGen;
                        return true;
                    }
                } catch {}
            }

            // 2. Windows PnP Device Property API via cfgmgr32.dll (Universal fallback for AMD, Intel, etc.)
            try {
                EnsureGpuPnpIdDiscovered();
                if (!string.IsNullOrEmpty(cachedGpuPnpId)) {
                    uint devInst;
                    if (CM_Locate_DevNode(out devInst, cachedGpuPnpId, 0) == 0) {
                        curGen = ReadPciPropertyUInt32(devInst, 9);
                        curWidth = ReadPciPropertyUInt32(devInst, 10);
                        maxGen = ReadPciPropertyUInt32(devInst, 11);
                        maxWidth = ReadPciPropertyUInt32(devInst, 12);
                        if (curWidth > 0 || maxWidth > 0) return true;
                    }
                }
            } catch {}

            return false;
        }

        private void DetectRamExpoProfileAsync() {
            if (isRamExpoQueryRunning) return;
            isRamExpoQueryRunning = true;

            Task.Run(() => {
                try {
                    // 1. CPU Vendor check (AMD vs. Intel)
                    string cpuName = "";
                    try {
                        cpuName = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "") as string ?? "";
                    } catch {}
                    bool isAmd = cpuName.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || cpuName.IndexOf("Ryzen", StringComparison.OrdinalIgnoreCase) >= 0;
                    string profileName = isAmd ? "EXPO" : "XMP";

                    // 2. RAM WMI Win32_PhysicalMemory scan
                    uint maxConfigured = 0;
                    uint baseSpeed = 0;
                    int smbiosType = 0;
                    int stickCount = 0;

                    WmiHelper.ForEach("SELECT Speed, ConfiguredClockSpeed, SMBIOSMemoryType FROM Win32_PhysicalMemory", obj => {
                        stickCount++;
                        uint s = 0, c = 0;
                        if (obj["Speed"] != null) uint.TryParse(obj["Speed"].ToString(), out s);
                        if (obj["ConfiguredClockSpeed"] != null) uint.TryParse(obj["ConfiguredClockSpeed"].ToString(), out c);

                        if (c > maxConfigured) maxConfigured = c;
                        if (s > baseSpeed) baseSpeed = s;

                        if (obj["SMBIOSMemoryType"] != null) {
                            int t;
                            if (int.TryParse(obj["SMBIOSMemoryType"].ToString(), out t) && t > 0) smbiosType = t;
                        }
                    });

                    string ddrGen = "RAM";
                    if (smbiosType == 34 || maxConfigured >= 4400 || baseSpeed >= 4400) ddrGen = "DDR5";
                    else if (smbiosType == 26 || (maxConfigured >= 2133 && maxConfigured <= 4000)) ddrGen = "DDR4";
                    else if (smbiosType == 24) ddrGen = "DDR3";

                    bool isExpoAn = false;
                    if (maxConfigured > 0) {
                        if (maxConfigured > baseSpeed) {
                            isExpoAn = true;
                        } else if (ddrGen == "DDR5" && maxConfigured >= 5600) {
                            isExpoAn = true;
                        } else if (ddrGen == "DDR4" && maxConfigured >= 3000) {
                            isExpoAn = true;
                        }
                    }

                    string channelInfo = stickCount >= 4 ? "Quad-DIMM" : (stickCount == 2 ? "Dual-Channel" : (stickCount == 1 ? "Single-Channel" : ""));
                    string subText;
                    if (maxConfigured > 0) {
                        string genChan = !string.IsNullOrEmpty(channelInfo) ? string.Format("{0} {1}", ddrGen, channelInfo) : ddrGen;
                        if (isExpoAn) {
                            subText = string.Format("{0} MT/s ({1})", maxConfigured, genChan);
                        } else {
                            subText = string.Format("{0} MT/s ({1} Basistakt)", maxConfigured, ddrGen);
                        }
                    } else {
                        subText = "Kein Profil aktiv";
                    }

                    string badge = isExpoAn ? string.Format("{0} AN", profileName) : string.Format("{0} AUS", profileName);

                    cachedRamExpoTitle = string.Format("{0}-Profil", profileName);
                    cachedRamExpoSub = subText;
                    cachedRamExpoVal = badge;
                    cachedRamExpoIsActive = isExpoAn;
                    isRamExpoDetected = true;

                    if (window != null) {
                        window.Dispatcher.Invoke(() => {
                            try {
                                if (txtRamExpoTitle != null) txtRamExpoTitle.Text = cachedRamExpoTitle;
                                if (txtRamExpoSub != null) {
                                    txtRamExpoSub.Text = cachedRamExpoSub;
                                    txtRamExpoSub.Foreground = cachedRamExpoIsActive ? UIHelper.BrushMuted : UIHelper.BrushRedText;
                                }
                                if (pillRamExpoStatus != null && txtRamExpoVal != null) {
                                    if (cachedRamExpoIsActive) {
                                        UIHelper.SetPill(pillRamExpoStatus, txtRamExpoVal, true, cachedRamExpoVal);
                                    } else {
                                        UIHelper.SetPillDanger(pillRamExpoStatus, txtRamExpoVal, cachedRamExpoVal);
                                    }
                                }
                            } catch {}
                        });
                    }
                } catch {
                } finally {
                    isRamExpoQueryRunning = false;
                }
            });
        }

        // Kernel Timer Controls
        private ToggleButton toggleKernelTimer;
        private TextBlock txtKernelSubtitle;
        private Border pillKernel;
        private TextBlock txtPillKernel;
        private Button btnOpenKernelRegedit;

        // Win32PrioritySeparation Controls
        private ToggleButton togglePriority;
        private TextBlock txtPrioritySubtitle;
        private TextBlock txtPriorityReboot;
        private ComboBox cmbPriority;
        private Button btnOpenPriorityRegedit;

        // BCDedit Controls
        private Border borderRowClock;
        private Border pillClock;
        private TextBlock txtPillClock;
        private ComboBox cmbClock;

        private Border borderRowTick;
        private Border pillTick;
        private TextBlock txtPillTick;
        private ComboBox cmbTick;

        private Border borderRowTsc;
        private Border pillTsc;
        private TextBlock txtPillTsc;
        private ComboBox cmbTsc;

        private Border borderRowDynamic;
        private Border pillDynamic;
        private TextBlock txtPillDynamic;
        private ComboBox cmbDynamic;

        private Button btnOpenBcdCmd;
        private Button btnResetBcd;

        // Timer Resolution Controls
        private TextBlock txtTimerResCurrent;

        // MeasureSleep Controls
        private Border pillMeasureSleepState;
        private TextBlock txtMeasureSleepState;
        private Button btnToggleMeasureSleep;
        private Button btnClearMeasureSleep;
        private Border borderMeasureSleepVerdict;
        private TextBlock txtMeasureSleepVerdictIcon;
        private TextBlock txtMeasureSleepVerdictTitle;
        private Border pillMeasureSleepVerdict;
        private TextBlock txtPillMeasureSleepVerdict;
        private Border borderMeasureSleepLastSlept;
        private TextBlock txtMeasureSleepLastSlept;
        private Border borderMeasureSleepDelta;
        private TextBlock txtMeasureSleepDelta;
        private ScrollViewer scrollMeasureSleep;
        private TextBox txtMeasureSleepLog;
        private bool isMeasureSleepPaused = false;
        private bool isTimerResTabActive = false;

        // Jitter Live Graph Controls
        private Canvas canvasJitterGraph;
        private System.Windows.Shapes.Path pathJitter;
        private System.Windows.Shapes.Line lineJitterTarget;
        private TextBlock txtJitterTargetLabel;
        private TextBlock txtJitterLiveVal;
        private TextBlock txtJitterYMax;
        private TextBlock txtJitterYMid;
        private TextBlock txtJitterYMin;
        private TextBlock txtJitterMin;
        private TextBlock txtJitterAvg;
        private TextBlock txtJitterMax;
        private List<double> jitterHistory = new List<double>();
        private DateTime lastJitterTimestamp = DateTime.MinValue;
        private double _measuredJitterIntervalMs = 1000.0;
        private System.Windows.Shapes.Line lineJitterHover;
        private System.Windows.Shapes.Ellipse dotJitterHover;
        private Border badgeJitterHover;
        private TextBlock txtJitterHoverTime;
        private TextBlock txtJitterHover;
        private bool isHoverJitter = false;
        private Point lastPosJitter = new Point();

        // Power Plan Controls
        private TextBlock txtPowerSubtitle;
        private ComboBox cmbPowerPlans;
        private ToggleButton togglePowerPlan;
        private Button btnOpenPowerCpl;

        // Kernisolierung Controls
        private ToggleButton toggleCoreIso;
        private TextBlock txtCoreIsoSubtitle;
        private TextBlock txtCoreIsoReboot;
        private Button btnOpenDefenderCoreIso;

        // LAN Energiespar Controls
        private ToggleButton toggleLanEnergy;
        private TextBlock txtLanEnergySubtitle;
        private TextBlock txtLanEnergyAdapter;
        private Button btnOpenLanDevMgmt;

        // Allgemeine Tweaks Neue Controls (Cards 6 - 10)
        private ToggleButton togglePagingExecutive;
        private TextBlock txtPagingSubtitle;
        private TextBlock txtPagingReboot;
        private Button btnOpenPagingRegedit;

        private ToggleButton toggleSystemResponsiveness;
        private TextBlock txtSystemResponsivenessSubtitle;
        private TextBlock txtSystemResponsivenessReboot;
        private Button btnOpenSystemResponsivenessRegedit;

        private ToggleButton toggleNetworkThrottling;
        private TextBlock txtNetworkThrottlingSubtitle;
        private TextBlock txtNetworkThrottlingReboot;
        private Button btnOpenNetworkThrottlingRegedit;

        private ToggleButton toggleMmcssGames;
        private TextBlock txtMmcssGamesSubtitle;
        private Button btnOpenMmcssGamesRegedit;

        private ToggleButton toggleGameDvr;
        private TextBlock txtGameDvrSubtitle;
        private Button btnOpenGameDvrRegedit;

        private ToggleButton toggleUsbSelectiveSuspend;
        private TextBlock txtUsbSelectiveSuspendSubtitle;
        private Button btnOpenUsbDevMgmt;

        private ToggleButton toggleRealtekRss;
        private TextBlock txtRealtekRssSubtitle;
        private TextBlock txtRealtekRssAdapter;
        private TextBlock txtRealtekRssReboot;
        private Button btnRealtekRssDriver;

        // Mouse / Allgemeine Tweaks Live Technical Inspectors
        private TextBlock txtMouseQueueLiveVal;
        private TextBlock txtPriorityLiveVal;
        private TextBlock txtCoreIsoLiveVal;
        private TextBlock txtHypervisorLiveVal;
        private TextBlock txtPowerPlanLiveVal;
        private TextBlock txtLanEeeLiveVal;
        private TextBlock txtLanPowerLiveVal;
        private TextBlock txtLanInterruptLiveVal;
        private TextBlock txtPagingLiveVal;
        private TextBlock txtSystemResponsivenessLiveVal;
        private TextBlock txtNetworkThrottlingLiveVal;
        private TextBlock txtMmcssPrioLiveVal;
        private TextBlock txtMmcssCategoryLiveVal;
        private TextBlock txtGameDvrLiveVal;
        private TextBlock txtFseModeLiveVal;
        private TextBlock txtAllowGameDvrLiveVal;
        private TextBlock txtUsbRegLiveVal;
        private TextBlock txtUsbPowerLiveVal;
        private TextBlock txtRealtekRssEnabledLiveVal;
        private TextBlock txtRealtekRssProcLiveVal;

        // Chipset Controls
        private Border borderChipsetStatus;
        private TextBlock txtChipsetStatusTitle;
        private TextBlock txtChipsetStatusDesc;
        private Border pillChipsetStatus;
        private TextBlock txtPillChipset;
        private TextBlock txtChipsetPlatform;
        private TextBlock txtChipsetInstalledVer;
        private TextBlock txtChipsetLatestVer;
        private TextBlock txtChipsetReleaseDate;
        private Button btnInstallChipsetDriver;
        private Button btnDownloadOnlyChipset;
        private Button btnCheckChipsetUpdate;
        private Button btnOpenChipsetFolder;
        private CheckBox chkCleanInstallChipset;
        private Border borderChipsetProgress;
        private TextBlock txtChipsetProgressStatus;
        private TextBlock txtChipsetDownloadSize;
        private TextBlock txtChipsetDownloadSpeed;
        private TextBlock txtChipsetDownloadPercent;
        private ProgressBar progChipsetDownload;
        private Button btnCancelChipsetDownload;
        private TextBlock txtChipsetVcacheVer;
        private TextBlock txtChipsetPpmVer;
        private TextBlock txtChipsetSmbusVer;
        private TextBlock txtChipsetPspVer;
        private TextBlock txtChipsetGpioVer;
        private TextBlock txtChipsetI2cVer;
        private Button btnOpenDevMgmtChipset;

        // Office Controls
        private CheckBox chkOfficeSelectAll;
        private RadioButton radFilterOfficeAll;
        private RadioButton radFilterOfficeInstalled;
        private RadioButton radFilterOfficeNotInstalled;
        private Border rowOfficeWord;
        private Border rowOfficeExcel;
        private Border rowOfficePowerPoint;
        private Border rowOfficeOutlook;
        private Border rowOfficeOneNote;
        private Border rowOfficeAccess;
        private Border rowOfficePublisher;
        private Border rowOfficeTeams;
        private CheckBox chkOfficeWord;
        private CheckBox chkOfficeExcel;
        private CheckBox chkOfficePowerPoint;
        private CheckBox chkOfficeOutlook;
        private CheckBox chkOfficeOneNote;
        private CheckBox chkOfficeAccess;
        private CheckBox chkOfficePublisher;
        private CheckBox chkOfficeLync;
        private CheckBox chkOfficeTeams;
        private CheckBox chkOfficeOneDrive;
        private Border borderOfficeProgress;
        private TextBlock txtOfficeStatus;
        private TextBlock txtOfficePercent;
        private ProgressBar progOfficeDownload;
        private TextBlock txtOfficeDownloadDetails;
        private TextBlock txtOfficeEta;
        private TextBlock txtOfficeSubStatus;
        private Button btnCancelOfficeAction;
        private Button btnInstallSelectedApps;
        private Button btnUninstallSelectedApps;
        private Button btnOhookActivate;
        private Border pillOfficeActivation;
        private TextBlock txtPillOfficeActivation;
        private TextBlock txtOfficeActivationInfo;
        // Per-App Office Status Pills & Descriptions
        private Border pillOfficeWord;
        private TextBlock txtPillOfficeWord;
        private TextBlock txtDescOfficeWord;
        private Border pillOfficeExcel;
        private TextBlock txtPillOfficeExcel;
        private TextBlock txtDescOfficeExcel;
        private Border pillOfficePowerPoint;
        private TextBlock txtPillOfficePowerPoint;
        private TextBlock txtDescOfficePowerPoint;
        private Border pillOfficeOutlook;
        private TextBlock txtPillOfficeOutlook;
        private TextBlock txtDescOfficeOutlook;
        private Border pillOfficeOneNote;
        private TextBlock txtPillOfficeOneNote;
        private TextBlock txtDescOfficeOneNote;
        private Border pillOfficeAccess;
        private TextBlock txtPillOfficeAccess;
        private TextBlock txtDescOfficeAccess;
        private Border pillOfficePublisher;
        private TextBlock txtPillOfficePublisher;
        private TextBlock txtDescOfficePublisher;
        private Border pillOfficeLync;
        private TextBlock txtPillOfficeLync;
        private Border pillOfficeTeams;
        private TextBlock txtPillOfficeTeams;
        private TextBlock txtDescOfficeTeams;
        private Border pillOfficeOneDrive;
        private TextBlock txtPillOfficeOneDrive;

        // Apps & Downloads Controls
        private TextBox txtSearchApps;
        private TextBlock txtSearchAppsPlaceholder;
        private Button btnClearSearchApps;
        private Border panelAppsNoResults;
        private CheckBox chkAppsSelectAll;
        private RadioButton radFilterAppsAll;
        private RadioButton radFilterAppsInstalled;
        private RadioButton radFilterAppsUpdates;
        private RadioButton radFilterAppsNotInstalled;
        private Button btnActionSelectedApps;
        private Border rowAppSteam;
        private Border rowAppDiscord;
        private Border rowAppStreamDeck;
        private Border rowAppBleachBit;
        private Border rowAppAntigravity;
        private Border rowAppEvga;
        private Border rowAppHasleo;
        private CheckBox chkSteam;
        private CheckBox chkDiscord;
        private CheckBox chkStreamDeck;
        private CheckBox chkBleachBit;
        private CheckBox chkAntigravity;
        private CheckBox chkEvga;
        private CheckBox chkHasleo;
        private Border pillAppSteam;
        private TextBlock txtPillAppSteam;
        private TextBlock txtDescAppSteam;
        private Border pillAppDiscord;
        private TextBlock txtPillAppDiscord;
        private TextBlock txtDescAppDiscord;
        private Border pillAppStreamDeck;
        private TextBlock txtPillAppStreamDeck;
        private TextBlock txtDescAppStreamDeck;
        private Border pillAppBleachBit;
        private TextBlock txtPillAppBleachBit;
        private TextBlock txtDescAppBleachBit;
        private Border pillAppAntigravity;
        private TextBlock txtPillAppAntigravity;
        private TextBlock txtDescAppAntigravity;
        private Border pillAppEvga;
        private TextBlock txtPillAppEvga;
        private TextBlock txtDescAppEvga;
        private Border pillAppHasleo;
        private TextBlock txtPillAppHasleo;
        private TextBlock txtDescAppHasleo;
        private Button btnRefreshApps;
        private Button btnOpenDownloadFolder;
        private Border borderAppProgress;
        private TextBlock txtAppStatus;
        private TextBlock txtAppDownloadSize;
        private TextBlock txtAppDownloadSpeed;
        private TextBlock txtAppDownloadPercent;
        private ProgressBar progAppDownload;
        private Button btnSkipAppDownload;
        private Button btnCancelAppDownloads;

        // Gamebar Controls
        private TextBlock txtKglLoaded;
        private TextBlock txtKglService;
        private Border borderKglSync;
        private TextBlock txtKglSyncTitle;
        private TextBlock txtKglSyncDesc;
        private Border pillKglStatus;
        private TextBlock txtKglPill;
        private TextBlock txtGamebarVersion;
        private TextBlock txtAmd3dStatus;
        private Button btnOpenGamebar;

        // Updates Controls
        private Button btnRunWindowsUpdate;
        private Button btnRunStoreUpdate;

        // BIOS Controls
        private TextBlock txtLiveSecureBoot;
        private Border pillLiveSecureBoot;
        private TextBlock txtPillLiveSecureBoot;
        private TextBlock txtLiveAcPower;
        private TextBlock txtLiveWlan;
        private Border pillLiveWlan;
        private TextBlock txtPillLiveWlan;
        private TextBlock txtDescWlan;
        private TextBlock txtLiveBt;
        private Border pillLiveBt;
        private TextBlock txtPillLiveBt;
        private TextBlock txtDescBt;
        private TextBlock txtLiveRebar;
        private Border pillLiveRebar;
        private TextBlock txtPillLiveRebar;
        private TextBlock txtLiveIgpu;
        private Border pillLiveIgpu;
        private TextBlock txtPillLiveIgpu;
        private TextBlock txtDescIgpu;

        // Hardware-Info Tab Controls
        private TextBlock txtHwCpuName;
        private TextBlock txtHwCpuBadge;
        private TextBlock txtHwCpuCores;
        private TextBlock txtHwCpuClock;
        private TextBlock txtHwCpuCache;
        private TextBlock txtHwCpuSocket;

        private TextBlock txtHwGpuName;
        private TextBlock txtHwGpuBadge;
        private TextBlock txtHwGpuDriver;
        private TextBlock txtHwGpuVram;
        private TextBlock txtHwGpuRes;

        private TextBlock txtHwGpuSubGpu;

        private TextBlock txtHwBoardModel;
        private TextBlock txtHwBoardVendor;
        private TextBlock txtHwBoardChipset;
        private TextBlock txtHwBiosVersion;
        private TextBlock txtHwBiosDate;
        private TextBlock txtHwBoardSerial;

        private TextBlock txtHwRamTotal;
        private TextBlock txtHwRamSpeed;
        private TextBlock txtHwRamSlots;
        private TextBlock txtHwRamModules;
        private TextBlock txtHwRamClock;
        private TextBlock txtHwRamUsage;

        private TextBlock txtHwStorageSummary;
        private StackPanel stackHwDrivesList;

        private TextBlock txtStorageSummary;
        private StackPanel stackDrivesList;
        private Button btnOpenDiskMgmt;
        private Button btnOpenCleanMgr;

        private TextBlock txtHwOsCaption;
        private TextBlock txtHwOsActivation;
        private TextBlock txtHwOsProductKey;
        private Button btnToggleHwOsKey;
        private Button btnCopyHwOsKey;
        private string cachedProductKey = "";
        private bool isProductKeyVisible = false;
        private TextBlock txtHwOsBuild;
        private TextBlock txtHwOsInstallDate;
        private TextBlock txtHwOsUptime;
        private TextBlock txtHwOsSecurity;

        private TextBlock txtHwNetAdapter;
        private TextBlock txtHwNetSpeed;
        private TextBlock txtHwNetMac;
        private TextBlock txtHwNetDriverVer;
        private Border badgeHwNetProvider;
        private TextBlock txtHwNetProvider;
        private TextBlock txtHwNetDriverDate;
        private TextBlock txtHwNetStatus;
        private Button btnCopyHwNetMac;
        private Button btnOpenDevMgmtLan;
        private Button btnOpenSoundSettings;
        private StackPanel stackHwAudioList;
        private Grid gridHwMonitors;

        private Button btnCopyHwCpu;
        private Button btnCopyHwGpu;
        private Button btnCopyHwBoard;
        private Button btnCopyHwRam;
        private Button btnCopyHwRamModules;
        private Button btnCopyHwNetAdapter;

        private LanAdapterInfo currentLanAdapter;
        private bool isAdmin;
        private int updateUIRefCount = 0;
        private bool isUpdatingUI { get { return updateUIRefCount > 0; } }
        private bool isNavigatingTab = false;

        public Window Initialize(string xamlContent) {
            try {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | (SecurityProtocolType)12288 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                ServicePointManager.DefaultConnectionLimit = 64;
                ServicePointManager.Expect100Continue = false;
            } catch {}

            using (StringReader sr = new StringReader(xamlContent))
            using (System.Xml.XmlReader xmlReader = System.Xml.XmlReader.Create(sr)) {
                window = (Window)XamlReader.Load(xmlReader);
            }

            window.SourceInitialized += (s, e) => {
                try {
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;

                    int darkMode = 1;
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref darkMode, sizeof(int));
                    DwmSetWindowAttribute(hwnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref darkMode, sizeof(int));

                    int cornerPreference = DWMWCP_ROUND;
                    DwmSetWindowAttribute(hwnd, DWMWA_WINDOW_CORNER_PREFERENCE, ref cornerPreference, sizeof(int));

                    int borderColor = 0x00110D0B; // #0B0D11 in BGR
                    DwmSetWindowAttribute(hwnd, DWMWA_BORDER_COLOR, ref borderColor, sizeof(int));

                    int captionColor = 0x00110D0B;
                    DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref captionColor, sizeof(int));

                    UpdateChromeResizeBorder();
                } catch {}
            };

            isAdmin = TestIsAdmin();

            // Set Application & Window Icon from embedded ZLogo
            try {
                using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream("app_icon.png")) {
                    if (s != null) {
                        BitmapImage bi = new BitmapImage();
                        bi.BeginInit();
                        bi.StreamSource = s;
                        bi.CacheOption = BitmapCacheOption.OnLoad;
                        bi.EndInit();
                        bi.Freeze();
                        window.Icon = bi;
                        var imgLogo = (Image)window.FindName("ImgHeaderLogo");
                        if (imgLogo != null) imgLogo.Source = bi;
                    }
                }
            } catch {}

            // Responsive Elements
            colSidebar          = (ColumnDefinition)window.FindName("ColSidebar");
            gridHomeTopCards    = (Grid)window.FindName("GridHomeTopCards");
            panelCpuGpuBlock    = (Grid)window.FindName("PanelCpuGpuBlock");
            gridCpuGpuRow       = (Grid)window.FindName("GridCpuGpuRow");
            cardHomeCpu         = (Border)window.FindName("CardHomeCpu");
            cardHomeGpu         = (Border)window.FindName("CardHomeGpu");
            cardHomeRam         = (Border)window.FindName("CardHomeRam");
            cardHomeGpuLimits   = (Border)window.FindName("CardHomeGpuLimits");
            gridHomeMonitors    = (Grid)window.FindName("GridHomeMonitors");
            cardMonitorTemp     = (Border)window.FindName("CardMonitorTemp");
            cardMonitorLoad     = (Border)window.FindName("CardMonitorLoad");
            cardMonitorPower    = (Border)window.FindName("CardMonitorPower");
            gridHwHeroCards     = (Grid)window.FindName("GridHwHeroCards");
            cardHwCpu           = (Border)window.FindName("CardHwCpu");
            cardHwGpu           = (Border)window.FindName("CardHwGpu");
            cardHwBoard         = (Border)window.FindName("CardHwBoard");
            cardHwRam           = (Border)window.FindName("CardHwRam");
            gridHwNetAudio      = (Grid)window.FindName("GridHwNetAudio");
            cardHwNet           = (Border)window.FindName("CardHwNet");
            cardHwAudio         = (Border)window.FindName("CardHwAudio");
            gridBbMain          = (Grid)window.FindName("GridBbMain");
            cardBbGrade         = (Border)window.FindName("CardBbGrade");
            gridBbMetrics       = (Grid)window.FindName("GridBbMetrics");
            cardBbIdle          = (Border)window.FindName("CardBbIdle");
            cardBbDl            = (Border)window.FindName("CardBbDl");
            cardBbUl            = (Border)window.FindName("CardBbUl");
            gridMouseTweaks     = (Grid)window.FindName("GridMouseTweaks");
            cardMouseQueue      = (Border)window.FindName("CardMouseQueue");
            cardMousePriority   = (Border)window.FindName("CardMousePriority");
            cardMouseCoreIso    = (Border)window.FindName("CardMouseCoreIso");
            cardMousePowerPlan  = (Border)window.FindName("CardMousePowerPlan");
            cardLanEnergy       = (Border)window.FindName("CardLanEnergy");
            cardPagingExecutive = (Border)window.FindName("CardPagingExecutive");
            cardSystemResponsiveness = (Border)window.FindName("CardSystemResponsiveness");
            cardNetworkThrottling    = (Border)window.FindName("CardNetworkThrottling");
            cardMmcssGames      = (Border)window.FindName("CardMmcssGames");
            cardGameDvr         = (Border)window.FindName("CardGameDvr");
            cardUsbSelectiveSuspend = (Border)window.FindName("CardUsbSelectiveSuspend");
            cardRealtekRss      = (Border)window.FindName("CardRealtekRss");
            txtSearchMouseTweaks       = (TextBox)window.FindName("TxtSearchMouseTweaks");
            txtSearchMousePlaceholder  = (TextBlock)window.FindName("TxtSearchMousePlaceholder");
            btnClearSearchMouseTweaks  = (Button)window.FindName("BtnClearSearchMouseTweaks");
            panelMouseNoResults        = (Border)window.FindName("PanelMouseNoResults");
            radFilterMouseAll          = (RadioButton)window.FindName("RadFilterMouseAll");
            radFilterMouseActive       = (RadioButton)window.FindName("RadFilterMouseActive");
            radFilterMouseInactive     = (RadioButton)window.FindName("RadFilterMouseInactive");

            // Header & Admin & Restart & Window Controls
            headerDragArea     = (FrameworkElement)window.FindName("HeaderDragArea");
            sidebarBrandHeader = (FrameworkElement)window.FindName("SidebarBrandHeader");
            sidebarTopDragArea = (FrameworkElement)window.FindName("SidebarTopDragArea");
            btnWinMinimize     = (Button)window.FindName("BtnWinMinimize");
            btnWinMaximize     = (Button)window.FindName("BtnWinMaximize");
            btnWinClose        = (Button)window.FindName("BtnWinClose");
            txtBreadcrumbIcon  = (TextBlock)window.FindName("TxtBreadcrumbIcon");
            txtBreadcrumbPath  = (TextBlock)window.FindName("TxtBreadcrumbPath");

            bannerAdminWarning = (Border)window.FindName("BannerAdminWarning");
            btnRelaunchAdmin   = (Button)window.FindName("BtnRelaunchAdmin");
            btnGlobalRestart   = (Button)window.FindName("BtnGlobalRestart");
            btnGlobalShutdown  = (Button)window.FindName("BtnGlobalShutdown");

            // Tabs
            tabBtnHome          = (RadioButton)window.FindName("TabBtnHome");
            tabBtnHardwareInfo  = (RadioButton)window.FindName("TabBtnHardwareInfo");
            tabBtnDrives        = (RadioButton)window.FindName("TabBtnDrives");
            tabBtnBufferbloat   = (RadioButton)window.FindName("TabBtnBufferbloat");
            tabBtnMouse         = (RadioButton)window.FindName("TabBtnMouse");
            tabBtnTimerRes      = (RadioButton)window.FindName("TabBtnTimerRes");
            tabBtnBcd           = (RadioButton)window.FindName("TabBtnBcd");
            tabBtnOffice        = (RadioButton)window.FindName("TabBtnOffice");
            tabBtnChipset       = (RadioButton)window.FindName("TabBtnChipset");
            tabBtnGamebar       = (RadioButton)window.FindName("TabBtnGamebar");
            tabBtnApps          = (RadioButton)window.FindName("TabBtnApps");
            tabBtnUpdates       = (RadioButton)window.FindName("TabBtnUpdates");
            tabBtnBios          = (RadioButton)window.FindName("TabBtnBios");
            tabBtnUsbDirect     = (RadioButton)window.FindName("TabBtnUsbDirect");
            tabBtnAioCooler     = (RadioButton)window.FindName("TabBtnAioCooler");

            tabContentHome          = (Border)window.FindName("TabContentHome");
            tabContentHardwareInfo  = (Border)window.FindName("TabContentHardwareInfo");
            tabContentDrives        = (Border)window.FindName("TabContentDrives");
            tabContentBufferbloat   = (Border)window.FindName("TabContentBufferbloat");
            tabContentMouse         = (Border)window.FindName("TabContentMouse");
            tabContentTimerRes      = (Border)window.FindName("TabContentTimerRes");
            tabContentBcd           = (Border)window.FindName("TabContentBcd");
            tabContentOffice        = (Border)window.FindName("TabContentOffice");
            tabContentChipset       = (Border)window.FindName("TabContentChipset");
            tabContentGamebar       = (Border)window.FindName("TabContentGamebar");
            tabContentApps          = (Border)window.FindName("TabContentApps");
            tabContentUpdates       = (Border)window.FindName("TabContentUpdates");
            tabContentBios          = (Border)window.FindName("TabContentBios");
            tabContentUsbDirect     = (Border)window.FindName("TabContentUsbDirect");
            tabContentAioCooler     = (Border)window.FindName("TabContentAioCooler");

            // Sidebar Spinners (only tabs with background operations)
            spinnerTabHome         = (Viewbox)window.FindName("SpinnerTabHome");
            spinnerTabBufferbloat  = (Viewbox)window.FindName("SpinnerTabBufferbloat");
            spinnerTabOffice       = (Viewbox)window.FindName("SpinnerTabOffice");
            spinnerTabChipset      = (Viewbox)window.FindName("SpinnerTabChipset");
            spinnerTabApps         = (Viewbox)window.FindName("SpinnerTabApps");
            spinnerTabUsbDirect    = (Viewbox)window.FindName("SpinnerTabUsbDirect");
            spinnerTabAioCooler    = (Viewbox)window.FindName("SpinnerTabAioCooler");

            HardwareStressTester.StateChanged += (s, e) => UpdateSidebarActivitySpinners();

            pillUsbStatus       = (Border)window.FindName("PillUsbStatus");
            txtPillUsbStatus    = (TextBlock)window.FindName("TxtPillUsbStatus");
            btnUsbRun           = (Button)window.FindName("BtnUsbRun");
            txtUsbRunIcon       = (TextBlock)window.FindName("TxtUsbRunIcon");
            txtUsbRunLabel      = (TextBlock)window.FindName("TxtUsbRunLabel");
            btnUsbCopy          = (Button)window.FindName("BtnUsbCopy");
            btnUsbClear         = (Button)window.FindName("BtnUsbClear");
            txtUsbConsoleOutput = (RichTextBox)window.FindName("TxtUsbConsoleOutput");
            paraUsbConsoleOutput = (Paragraph)window.FindName("ParaUsbConsoleOutput");
            panelUsbHardwareEmpty   = (Border)window.FindName("PanelUsbHardwareEmpty");
            panelUsbHardwareContent = (StackPanel)window.FindName("PanelUsbHardwareContent");
            txtUsbDeviceCount       = (TextBlock)window.FindName("TxtUsbDeviceCount");
            stackUsbDevicesChip0    = (StackPanel)window.FindName("StackUsbDevicesChip0");
            stackUsbDevicesChip1    = (StackPanel)window.FindName("StackUsbDevicesChip1");
            stackUsbDevicesChip2    = (StackPanel)window.FindName("StackUsbDevicesChip2");

            // Home Telemetry Controls (3 grouped tiles per hardware card)
            txtCpuTempRow          = (TextBlock)window.FindName("TxtCpuTempRow");
            colCpuTempBarFill      = (ColumnDefinition)window.FindName("ColCpuTempBarFill");
            colCpuTempBarEmpty     = (ColumnDefinition)window.FindName("ColCpuTempBarEmpty");
            txtCpuLoadRow          = (TextBlock)window.FindName("TxtCpuLoadRow");
            colCpuLoadBarFill      = (ColumnDefinition)window.FindName("ColCpuLoadBarFill");
            colCpuLoadBarEmpty     = (ColumnDefinition)window.FindName("ColCpuLoadBarEmpty");
            txtCpuPowerRow         = (TextBlock)window.FindName("TxtCpuPowerRow");
            colCpuPowerBarFill     = (ColumnDefinition)window.FindName("ColCpuPowerBarFill");
            colCpuPowerBarEmpty    = (ColumnDefinition)window.FindName("ColCpuPowerBarEmpty");

            txtGpuTempRow          = (TextBlock)window.FindName("TxtGpuTempRow");
            colGpuTempBarFill      = (ColumnDefinition)window.FindName("ColGpuTempBarFill");
            colGpuTempBarEmpty     = (ColumnDefinition)window.FindName("ColGpuTempBarEmpty");
            txtGpuLoadRow          = (TextBlock)window.FindName("TxtGpuLoadRow");
            colGpuLoadBarFill      = (ColumnDefinition)window.FindName("ColGpuLoadBarFill");
            colGpuLoadBarEmpty     = (ColumnDefinition)window.FindName("ColGpuLoadBarEmpty");
            txtGpuPowerRow         = (TextBlock)window.FindName("TxtGpuPowerRow");
            colGpuPowerBarFill     = (ColumnDefinition)window.FindName("ColGpuPowerBarFill");
            colGpuPowerBarEmpty    = (ColumnDefinition)window.FindName("ColGpuPowerBarEmpty");

            txtRamLoadRow          = (TextBlock)window.FindName("TxtRamLoadRow");
            colRamLoadBarFill      = (ColumnDefinition)window.FindName("ColRamLoadBarFill");
            colRamLoadBarEmpty     = (ColumnDefinition)window.FindName("ColRamLoadBarEmpty");
            txtRamUsedRow          = (TextBlock)window.FindName("TxtRamUsedRow");
            colRamMemBarFill       = (ColumnDefinition)window.FindName("ColRamMemBarFill");
            colRamMemBarEmpty      = (ColumnDefinition)window.FindName("ColRamMemBarEmpty");
            txtRamFreeRow          = (TextBlock)window.FindName("TxtRamFreeRow");
            colRamFreeBarFill      = (ColumnDefinition)window.FindName("ColRamFreeBarFill");
            colRamFreeBarEmpty     = (ColumnDefinition)window.FindName("ColRamFreeBarEmpty");
            txtRamExpoTitle        = (TextBlock)window.FindName("TxtRamExpoTitle");
            txtRamExpoSub          = (TextBlock)window.FindName("TxtRamExpoSub");
            pillRamExpoStatus      = (Border)window.FindName("PillRamExpoStatus");
            txtRamExpoVal          = (TextBlock)window.FindName("TxtRamExpoVal");
            txtTotalPowerRow       = (TextBlock)window.FindName("TxtTotalPowerRow");
            txtTotalPowerSubtitle  = (TextBlock)window.FindName("TxtTotalPowerSubtitle");
            colTotalPowerBarFill   = (ColumnDefinition)window.FindName("ColTotalPowerBarFill");
            colTotalPowerBarEmpty  = (ColumnDefinition)window.FindName("ColTotalPowerBarEmpty");

            txtGpuLimitPowerVal    = (TextBlock)window.FindName("TxtGpuLimitPowerVal");
            txtGpuLimitPowerSub    = (TextBlock)window.FindName("TxtGpuLimitPowerSub");
            colGpuLimitBarFill     = (ColumnDefinition)window.FindName("ColGpuLimitBarFill");
            colGpuLimitBarEmpty    = (ColumnDefinition)window.FindName("ColGpuLimitBarEmpty");
            barGpuLimitFill        = (Border)window.FindName("BarGpuLimitFill");
            txtGpuLimitTempVal     = (TextBlock)window.FindName("TxtGpuLimitTempVal");
            txtGpuLimitTempSub     = (TextBlock)window.FindName("TxtGpuLimitTempSub");
            colGpuTempLimitBarFill = (ColumnDefinition)window.FindName("ColGpuTempLimitBarFill");
            colGpuTempLimitBarEmpty = (ColumnDefinition)window.FindName("ColGpuTempLimitBarEmpty");
            barGpuTempLimitFill    = (Border)window.FindName("BarGpuTempLimitFill");
            txtGpuLimitStatusSummary = (TextBlock)window.FindName("TxtGpuLimitStatusSummary");
            pillGpuLimitSummary      = (Border)window.FindName("PillGpuLimitSummary");
            txtGpuPcieVal            = (TextBlock)window.FindName("TxtGpuPcieVal");
            txtGpuPcieSub            = (TextBlock)window.FindName("TxtGpuPcieSub");
            pillGpuPcieStatus        = (Border)window.FindName("PillGpuPcieStatus");

            pillToggleGpuLimit     = (Border)window.FindName("PillToggleGpuLimit");
            txtToggleGpuLimitDot   = (TextBlock)window.FindName("TxtToggleGpuLimitDot");
            txtToggleGpuLimitStatus = (TextBlock)window.FindName("TxtToggleGpuLimitStatus");

            overlayGpuLimitsModal  = (Grid)window.FindName("OverlayGpuLimitsModal");
            btnCloseGpuLimitsModal = (Button)window.FindName("BtnCloseGpuLimitsModal");
            btnCancelGpuLimits     = (Button)window.FindName("BtnCancelGpuLimits");
            btnApplyGpuLimits      = (Button)window.FindName("BtnApplyGpuLimits");
            btnResetGpuLimits      = (Button)window.FindName("BtnResetGpuLimits");
            sliderGpuClock         = (Slider)window.FindName("SliderGpuClock");
            txtGpuClockTargetVal   = (TextBlock)window.FindName("TxtGpuClockTargetVal");
            txtGpuClockTargetRange = (TextBlock)window.FindName("TxtGpuClockTargetRange");
            txtGpuClockCurrentInfo = (TextBlock)window.FindName("TxtGpuClockCurrentInfo");


            // 3 Dedicated Real-Time Live Monitors Binding
            canvasTempGraph     = (Canvas)window.FindName("CanvasTempGraph");
            pathTempCpu         = (System.Windows.Shapes.Path)window.FindName("PathTempCpu");
            pathTempGpu         = (System.Windows.Shapes.Path)window.FindName("PathTempGpu");

            canvasLoadGraph     = (Canvas)window.FindName("CanvasLoadGraph");
            pathLoadCpu         = (System.Windows.Shapes.Path)window.FindName("PathLoadCpu");
            pathLoadGpu         = (System.Windows.Shapes.Path)window.FindName("PathLoadGpu");

            canvasPowerGraph    = (Canvas)window.FindName("CanvasPowerGraph");
            pathPowerCpu        = (System.Windows.Shapes.Path)window.FindName("PathPowerCpu");
            pathPowerGpu        = (System.Windows.Shapes.Path)window.FindName("PathPowerGpu");
            txtGraphPowerYMax   = (TextBlock)window.FindName("TxtGraphPowerYMax");
            txtGraphPowerY75    = (TextBlock)window.FindName("TxtGraphPowerY75");
            txtGraphPowerY50    = (TextBlock)window.FindName("TxtGraphPowerY50");
            txtGraphPowerY25    = (TextBlock)window.FindName("TxtGraphPowerY25");

            // Master Monitor Filter Button
            btnFilterMonitorCycle = (Button)window.FindName("BtnFilterMonitorCycle");
            if (btnFilterMonitorCycle != null) btnFilterMonitorCycle.Click += delegate(object s, RoutedEventArgs e) { CycleMonitorFilter(); };

            // Hover Tracking Elements - Temp
            lineHoverTemp = (System.Windows.Shapes.Line)window.FindName("LineHoverTemp");
            dotHoverTempCpu = (System.Windows.Shapes.Ellipse)window.FindName("DotHoverTempCpu");
            dotHoverTempGpu = (System.Windows.Shapes.Ellipse)window.FindName("DotHoverTempGpu");
            badgeHoverTemp = (Border)window.FindName("BadgeHoverTemp");
            txtHoverTempTime = (TextBlock)window.FindName("TxtHoverTempTime");
            txtHoverTempCpu = (TextBlock)window.FindName("TxtHoverTempCpu");
            txtHoverTempGpu = (TextBlock)window.FindName("TxtHoverTempGpu");
            panelHoverTempCpu = (StackPanel)window.FindName("PanelHoverTempCpu");
            panelHoverTempGpu = (StackPanel)window.FindName("PanelHoverTempGpu");

            if (canvasTempGraph != null) {
                canvasTempGraph.MouseMove += delegate(object s, MouseEventArgs e) {
                    Point p = e.GetPosition(canvasTempGraph);
                    isHoverTemp = true;
                    lastPosTemp = p;
                    UpdateTempHover(p);
                };
                canvasTempGraph.MouseLeave += delegate(object s, MouseEventArgs e) {
                    isHoverTemp = false;
                    HideGraphHover(lineHoverTemp, dotHoverTempCpu, dotHoverTempGpu, badgeHoverTemp);
                };
            }

            // Hover Tracking Elements - Load
            lineHoverLoad = (System.Windows.Shapes.Line)window.FindName("LineHoverLoad");
            dotHoverLoadCpu = (System.Windows.Shapes.Ellipse)window.FindName("DotHoverLoadCpu");
            dotHoverLoadGpu = (System.Windows.Shapes.Ellipse)window.FindName("DotHoverLoadGpu");
            badgeHoverLoad = (Border)window.FindName("BadgeHoverLoad");
            txtHoverLoadTime = (TextBlock)window.FindName("TxtHoverLoadTime");
            txtHoverLoadCpu = (TextBlock)window.FindName("TxtHoverLoadCpu");
            txtHoverLoadGpu = (TextBlock)window.FindName("TxtHoverLoadGpu");
            panelHoverLoadCpu = (StackPanel)window.FindName("PanelHoverLoadCpu");
            panelHoverLoadGpu = (StackPanel)window.FindName("PanelHoverLoadGpu");

            if (canvasLoadGraph != null) {
                canvasLoadGraph.MouseMove += delegate(object s, MouseEventArgs e) {
                    Point p = e.GetPosition(canvasLoadGraph);
                    isHoverLoad = true;
                    lastPosLoad = p;
                    UpdateLoadHover(p);
                };
                canvasLoadGraph.MouseLeave += delegate(object s, MouseEventArgs e) {
                    isHoverLoad = false;
                    HideGraphHover(lineHoverLoad, dotHoverLoadCpu, dotHoverLoadGpu, badgeHoverLoad);
                };
            }

            // Hover Tracking Elements - Power
            lineHoverPower = (System.Windows.Shapes.Line)window.FindName("LineHoverPower");
            dotHoverPowerCpu = (System.Windows.Shapes.Ellipse)window.FindName("DotHoverPowerCpu");
            dotHoverPowerGpu = (System.Windows.Shapes.Ellipse)window.FindName("DotHoverPowerGpu");
            badgeHoverPower = (Border)window.FindName("BadgeHoverPower");
            txtHoverPowerTime = (TextBlock)window.FindName("TxtHoverPowerTime");
            txtHoverPowerCpu = (TextBlock)window.FindName("TxtHoverPowerCpu");
            txtHoverPowerGpu = (TextBlock)window.FindName("TxtHoverPowerGpu");
            panelHoverPowerCpu = (StackPanel)window.FindName("PanelHoverPowerCpu");
            panelHoverPowerGpu = (StackPanel)window.FindName("PanelHoverPowerGpu");
            txtHoverPowerTotal = (TextBlock)window.FindName("TxtHoverPowerTotal");
            panelHoverPowerTotal = (StackPanel)window.FindName("PanelHoverPowerTotal");
            sepHoverPowerTotal = (Border)window.FindName("SepHoverPowerTotal");

            if (canvasPowerGraph != null) {
                canvasPowerGraph.MouseMove += delegate(object s, MouseEventArgs e) {
                    Point p = e.GetPosition(canvasPowerGraph);
                    isHoverPower = true;
                    lastPosPower = p;
                    UpdatePowerHover(p);
                };
                canvasPowerGraph.MouseLeave += delegate(object s, MouseEventArgs e) {
                    isHoverPower = false;
                    HideGraphHover(lineHoverPower, dotHoverPowerCpu, dotHoverPowerGpu, badgeHoverPower);
                };
            }

            stackDrivesList     = (StackPanel)window.FindName("StackDrivesList");

            pillStressStatus       = (Border)window.FindName("PillStressStatus");
            txtStressStatus        = (TextBlock)window.FindName("TxtStressStatus");
            txtStressDuration      = (TextBlock)window.FindName("TxtStressDuration");
            btnStressToggle        = (Button)window.FindName("BtnStressToggle");
            txtStressBtnIcon       = (TextBlock)window.FindName("TxtStressBtnIcon");
            txtStressBtnLabel      = (TextBlock)window.FindName("TxtStressBtnLabel");
            txtStressLiveFeedback  = (TextBlock)window.FindName("TxtStressLiveFeedback");
            radStressBoth          = (RadioButton)window.FindName("RadStressBoth");
            radStressCpu           = (RadioButton)window.FindName("RadStressCpu");
            radStressGpu           = (RadioButton)window.FindName("RadStressGpu");

            if (btnStressToggle != null) {
                btnStressToggle.Click += (s, e) => ToggleHardwareStressTest();
            }

            stressDurationTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            stressDurationTimer.Tick += (s, e) => {
                if (HardwareStressTester.IsRunning && txtStressDuration != null) {
                    txtStressDuration.Text = string.Format("⏱ {0:mm\\:ss}", HardwareStressTester.Elapsed);
                }
            };

            HardwareStressTester.StateChanged += (s, e) => {
                try {
                    window.Dispatcher.InvokeAsync(UpdateHardwareStressUI);
                } catch {}
            };

            btnViewDetailsCpu   = (Button)window.FindName("BtnViewDetailsCpu");
            btnViewDetailsGpu   = (Button)window.FindName("BtnViewDetailsGpu");
            btnViewDetailsRam   = (Button)window.FindName("BtnViewDetailsRam");

            overlayHardwareDetails = (Grid)window.FindName("OverlayHardwareDetails");
            txtHwDetailIcon        = (TextBlock)window.FindName("TxtHwDetailIcon");
            txtHwDetailTitle       = (TextBlock)window.FindName("TxtHwDetailTitle");
            txtHwDetailSubtitle    = (TextBlock)window.FindName("TxtHwDetailSubtitle");
            btnCloseHwDetails      = (Button)window.FindName("BtnCloseHwDetails");
            btnDismissHwDetails    = (Button)window.FindName("BtnDismissHwDetails");
            panelHwDetailMetrics   = (WrapPanel)window.FindName("PanelHwDetailMetrics");

            if (btnCloseHwDetails != null) btnCloseHwDetails.Click += (s, e) => HideHardwareDetails();
            if (btnDismissHwDetails != null) btnDismissHwDetails.Click += (s, e) => HideHardwareDetails();

            // Overlay Modal (Modern In-App Dialog)
            overlayModal      = (Grid)window.FindName("OverlayModal");
            borderModalIcon   = (Border)window.FindName("BorderModalIcon");
            txtModalIcon      = (TextBlock)window.FindName("TxtModalIcon");
            txtModalTitle     = (TextBlock)window.FindName("TxtModalTitle");
            txtModalCategory  = (TextBlock)window.FindName("TxtModalCategory");
            btnModalClose     = (Button)window.FindName("BtnModalClose");
            txtModalMessage   = (TextBlock)window.FindName("TxtModalMessage");
            btnModalCancel    = (Button)window.FindName("BtnModalCancel");
            btnModalCustom    = (Button)window.FindName("BtnModalCustom");
            btnModalConfirm   = (Button)window.FindName("BtnModalConfirm");

            if (btnModalClose != null) btnModalClose.Click += (s, e) => CloseModalWithChoice(ModalChoice.Cancel);
            if (btnModalCancel != null) btnModalCancel.Click += (s, e) => CloseModalWithChoice(ModalChoice.Cancel);
            if (btnModalCustom != null) btnModalCustom.Click += (s, e) => CloseModalWithChoice(ModalChoice.Custom);
            if (btnModalConfirm != null) btnModalConfirm.Click += (s, e) => CloseModalWithChoice(ModalChoice.Confirm);

            // Overlay Restart Modal Setup
            overlayRestartModal    = (Grid)window.FindName("OverlayRestartModal");
            btnRestartModalClose   = (Button)window.FindName("BtnRestartModalClose");
            btnRestartModalCancel  = (Button)window.FindName("BtnRestartModalCancel");
            btnRestartNormal       = (Border)window.FindName("BtnRestartNormal");
            btnRestartUefiOption   = (Border)window.FindName("BtnRestartUefiOption");
            btnRestartSafeMode     = (Border)window.FindName("BtnRestartSafeMode");

            if (btnRestartModalClose != null) btnRestartModalClose.Click += (s, e) => { if (overlayRestartModal != null) overlayRestartModal.Visibility = Visibility.Collapsed; };
            if (btnRestartModalCancel != null) btnRestartModalCancel.Click += (s, e) => { if (overlayRestartModal != null) overlayRestartModal.Visibility = Visibility.Collapsed; };

            Action<Border> setupRestartCardHover = (b) => {
                if (b == null) return;
                b.MouseEnter += (s, e) => {
                    b.Background = UIHelper.GetBrush("#1E2433");
                    b.BorderBrush = UIHelper.GetBrush("#3B82F6");
                };
                b.MouseLeave += (s, e) => {
                    b.Background = UIHelper.GetBrush("#161B26");
                    b.BorderBrush = UIHelper.GetBrush("#262D3D");
                };
            };

            setupRestartCardHover(btnRestartNormal);
            setupRestartCardHover(btnRestartUefiOption);
            setupRestartCardHover(btnRestartSafeMode);

            if (btnRestartNormal != null) {
                btnRestartNormal.MouseLeftButtonUp += (s, e) => {
                    if (overlayRestartModal != null) overlayRestartModal.Visibility = Visibility.Collapsed;
                    if (!ProcessRunner.Start("shutdown.exe", "/r /t 0")) {
                        ShowAlertModal("Fehler beim Neustart", "Der Neustartbefehl konnte nicht ausgeführt werden.", "OK", "❌", "#EF4444");
                    }
                };
            }

            if (btnRestartUefiOption != null) {
                btnRestartUefiOption.MouseLeftButtonUp += (s, e) => {
                    if (overlayRestartModal != null) overlayRestartModal.Visibility = Visibility.Collapsed;
                    if (!ProcessRunner.Start("shutdown.exe", "/r /fw /t 0")) {
                        ShowAlertModal("Fehler beim BIOS-Neustart", "Der direkte Neustartbefehl ins UEFI/BIOS konnte nicht ausgeführt werden. Möglicherweise unterstützt dein Mainboard oder Windows keinen direkten Firmware-Reboot.", "OK", "❌", "#EF4444");
                    }
                };
            }

            if (btnRestartSafeMode != null) {
                btnRestartSafeMode.MouseLeftButtonUp += (s, e) => {
                    if (overlayRestartModal != null) overlayRestartModal.Visibility = Visibility.Collapsed;
                    try {
                        Registry.SetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\RunOnce", "*RemoveSafeBoot", "bcdedit.exe /deletevalue {current} safeboot", RegistryValueKind.String);
                    } catch {}

                    string bcdRes = ProcessRunner.RunAndGetOutput("bcdedit.exe", "/set {current} safeboot minimal");
                    bool setSafe = bcdRes.IndexOf("erfolgreich", StringComparison.OrdinalIgnoreCase) >= 0 || bcdRes.IndexOf("success", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (!setSafe) {
                        ProcessRunner.Start("shutdown.exe", "/r /o /t 0");
                    } else {
                        if (!ProcessRunner.Start("shutdown.exe", "/r /t 0")) {
                            ShowAlertModal("Fehler beim Neustart", "Der Neustartbefehl in den abgesicherten Modus konnte nicht ausgeführt werden.", "OK", "❌", "#EF4444");
                        }
                    }
                };
            }

            try {
                if (nvmlInit() == 0) {
                    isNvmlInitialized = true;
                    IntPtr dev;
                    if (nvmlDeviceGetHandleByIndex(0, out dev) == 0) {
                        nvmlDeviceHandle = dev;
                    }
                }
            } catch {}
            EnsureGpuPnpIdDiscovered();

            telemetryTimer = new DispatcherTimer {
                Interval = TimeSpan.FromMilliseconds(500)
            };
            telemetryTimer.Tick += TelemetryTimer_Tick;
            telemetryTimer.Start();

            smoothGraphTimer = new DispatcherTimer(DispatcherPriority.Render) {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            smoothGraphTimer.Tick += delegate(object s, EventArgs e) {
                if (isBenchmarkRunning) return; // Benchmark-Modus: 100% GUI-Timer Stopp für Null Interferenz
                if (isHomeActive) {
                    UpdateAllMonitorGraphs();
                    UpdateSmoothBars();
                }
                if (isTimerResTabActive) UpdateSmoothJitterGraph();
            };
            smoothGraphTimer.Start();

            sidebarActivityTimer = new DispatcherTimer(DispatcherPriority.Background) {
                Interval = TimeSpan.FromSeconds(1)
            };
            sidebarActivityTimer.Tick += (s, e) => {
                if (isBenchmarkRunning) return;
                UpdateSidebarActivitySpinners();
            };
            sidebarActivityTimer.Start();

            // Tab 1: Mouse
            toggleQueue        = (ToggleButton)window.FindName("ToggleQueue");
            txtSwitchSubtitle  = (TextBlock)window.FindName("TxtSwitchSubtitle");
            txtMouseQueueReboot= (TextBlock)window.FindName("TxtMouseQueueReboot");
            btnAdjustMouseQueue= (Button)window.FindName("BtnAdjustMouseQueue");
            popupMouseQueue    = (Popup)window.FindName("PopupMouseQueue");
            sliderMouseQueue   = (Slider)window.FindName("SliderMouseQueue");
            txtMouseQueueSliderVal = (TextBlock)window.FindName("TxtMouseQueueSliderVal");
            btnMouseQueuePreset16  = (Button)window.FindName("BtnMouseQueuePreset16");
            btnMouseQueueApply = (Button)window.FindName("BtnMouseQueueApply");
            btnOpenRegedit     = (Button)window.FindName("BtnOpenRegedit");

            // Tab 2: Kernel Timer
            toggleKernelTimer    = (ToggleButton)window.FindName("ToggleKernelTimer");
            txtKernelSubtitle    = (TextBlock)window.FindName("TxtKernelSubtitle");
            pillKernel           = (Border)window.FindName("PillKernel");
            txtPillKernel        = (TextBlock)window.FindName("TxtPillKernel");
            btnOpenKernelRegedit = (Button)window.FindName("BtnOpenKernelRegedit");

            // Tab 3: Win32 Priority
            togglePriority         = (ToggleButton)window.FindName("TogglePriority");
            txtPrioritySubtitle    = (TextBlock)window.FindName("TxtPrioritySubtitle");
            txtPriorityReboot      = (TextBlock)window.FindName("TxtPriorityReboot");
            cmbPriority            = (ComboBox)window.FindName("CmbPriority");
            btnOpenPriorityRegedit = (Button)window.FindName("BtnOpenPriorityRegedit");

            // Tab 4: BCDedit (HPET)
            borderRowClock = (Border)window.FindName("BorderRowClock");
            pillClock      = (Border)window.FindName("PillClock");
            txtPillClock   = (TextBlock)window.FindName("TxtPillClock");
            cmbClock       = (ComboBox)window.FindName("CmbClock");

            borderRowTick = (Border)window.FindName("BorderRowTick");
            pillTick      = (Border)window.FindName("PillTick");
            txtPillTick   = (TextBlock)window.FindName("TxtPillTick");
            cmbTick       = (ComboBox)window.FindName("CmbTick");

            borderRowTsc = (Border)window.FindName("BorderRowTsc");
            pillTsc      = (Border)window.FindName("PillTsc");
            txtPillTsc   = (TextBlock)window.FindName("TxtPillTsc");
            cmbTsc       = (ComboBox)window.FindName("CmbTsc");

            borderRowDynamic = (Border)window.FindName("BorderRowDynamic");
            pillDynamic      = (Border)window.FindName("PillDynamic");
            txtPillDynamic   = (TextBlock)window.FindName("TxtPillDynamic");
            cmbDynamic       = (ComboBox)window.FindName("CmbDynamic");

            btnOpenBcdCmd = (Button)window.FindName("BtnOpenBcdCmd");
            btnResetBcd   = (Button)window.FindName("BtnResetBcd");

            // Tab Timer Resolution
            txtTimerResCurrent     = (TextBlock)window.FindName("TxtTimerResCurrent");

            // MeasureSleep
            pillMeasureSleepState       = (Border)window.FindName("PillMeasureSleepState");
            txtMeasureSleepState        = (TextBlock)window.FindName("TxtMeasureSleepState");
            btnToggleMeasureSleep       = (Button)window.FindName("BtnToggleMeasureSleep");
            btnClearMeasureSleep        = (Button)window.FindName("BtnClearMeasureSleep");
            borderMeasureSleepVerdict   = (Border)window.FindName("BorderMeasureSleepVerdict");
            txtMeasureSleepVerdictIcon  = (TextBlock)window.FindName("TxtMeasureSleepVerdictIcon");
            txtMeasureSleepVerdictTitle = (TextBlock)window.FindName("TxtMeasureSleepVerdictTitle");
            pillMeasureSleepVerdict     = (Border)window.FindName("PillMeasureSleepVerdict");
            txtPillMeasureSleepVerdict  = (TextBlock)window.FindName("TxtPillMeasureSleepVerdict");
            borderMeasureSleepLastSlept = (Border)window.FindName("BorderMeasureSleepLastSlept");
            txtMeasureSleepLastSlept    = (TextBlock)window.FindName("TxtMeasureSleepLastSlept");
            borderMeasureSleepDelta     = (Border)window.FindName("BorderMeasureSleepDelta");
            txtMeasureSleepDelta        = (TextBlock)window.FindName("TxtMeasureSleepDelta");
            scrollMeasureSleep          = (ScrollViewer)window.FindName("ScrollMeasureSleep");
            txtMeasureSleepLog          = (TextBox)window.FindName("TxtMeasureSleepLog");
            canvasJitterGraph           = (Canvas)window.FindName("CanvasJitterGraph");
            pathJitter                  = (System.Windows.Shapes.Path)window.FindName("PathJitter");
            lineJitterTarget            = (System.Windows.Shapes.Line)window.FindName("LineJitterTarget");
            txtJitterTargetLabel        = (TextBlock)window.FindName("TxtJitterTargetLabel");
            lineJitterHover             = (System.Windows.Shapes.Line)window.FindName("LineJitterHover");
            dotJitterHover              = (System.Windows.Shapes.Ellipse)window.FindName("DotJitterHover");
            badgeJitterHover            = (Border)window.FindName("BadgeJitterHover");
            txtJitterHoverTime          = (TextBlock)window.FindName("TxtJitterHoverTime");
            txtJitterHover              = (TextBlock)window.FindName("TxtJitterHover");

            if (canvasJitterGraph != null) {
                canvasJitterGraph.MouseMove += (s, e) => {
                    Point p = e.GetPosition(canvasJitterGraph);
                    isHoverJitter = true;
                    lastPosJitter = p;
                    UpdateJitterHover(p);
                };
                canvasJitterGraph.MouseLeave += (s, e) => {
                    isHoverJitter = false;
                    HideJitterHover();
                };
            }
            txtJitterLiveVal            = (TextBlock)window.FindName("TxtJitterLiveVal");
            txtJitterYMax               = (TextBlock)window.FindName("TxtJitterYMax");
            txtJitterYMid               = (TextBlock)window.FindName("TxtJitterYMid");
            txtJitterYMin               = (TextBlock)window.FindName("TxtJitterYMin");
            txtJitterMin                = (TextBlock)window.FindName("TxtJitterMin");
            txtJitterAvg                = (TextBlock)window.FindName("TxtJitterAvg");
            txtJitterMax                = (TextBlock)window.FindName("TxtJitterMax");

            // Power Plan
            txtPowerSubtitle       = (TextBlock)window.FindName("TxtPowerSubtitle");
            cmbPowerPlans          = (ComboBox)window.FindName("CmbPowerPlans");
            togglePowerPlan        = (ToggleButton)window.FindName("TogglePowerPlan");
            btnOpenPowerCpl        = (Button)window.FindName("BtnOpenPowerCpl");

            // Tab 7: Kernisolierung
            toggleCoreIso            = (ToggleButton)window.FindName("ToggleCoreIso");
            txtCoreIsoSubtitle       = (TextBlock)window.FindName("TxtCoreIsoSubtitle");
            txtCoreIsoReboot         = (TextBlock)window.FindName("TxtCoreIsoReboot");
            btnOpenDefenderCoreIso   = (Button)window.FindName("BtnOpenDefenderCoreIso");

            // LAN Energiesparen
            toggleLanEnergy          = (ToggleButton)window.FindName("ToggleLanEnergy");
            txtLanEnergySubtitle     = (TextBlock)window.FindName("TxtLanEnergySubtitle");
            txtLanEnergyAdapter      = (TextBlock)window.FindName("TxtLanEnergyAdapter");
            btnOpenLanDevMgmt        = (Button)window.FindName("BtnOpenLanDevMgmt");

            togglePagingExecutive    = (ToggleButton)window.FindName("TogglePagingExecutive");
            txtPagingSubtitle        = (TextBlock)window.FindName("TxtPagingSubtitle");
            txtPagingReboot          = (TextBlock)window.FindName("TxtPagingReboot");
            btnOpenPagingRegedit     = (Button)window.FindName("BtnOpenPagingRegedit");

            toggleSystemResponsiveness = (ToggleButton)window.FindName("ToggleSystemResponsiveness");
            txtSystemResponsivenessSubtitle = (TextBlock)window.FindName("TxtSystemResponsivenessSubtitle");
            txtSystemResponsivenessReboot   = (TextBlock)window.FindName("TxtSystemResponsivenessReboot");
            btnOpenSystemResponsivenessRegedit = (Button)window.FindName("BtnOpenSystemResponsivenessRegedit");

            toggleNetworkThrottling = (ToggleButton)window.FindName("ToggleNetworkThrottling");
            txtNetworkThrottlingSubtitle = (TextBlock)window.FindName("TxtNetworkThrottlingSubtitle");
            txtNetworkThrottlingReboot   = (TextBlock)window.FindName("TxtNetworkThrottlingReboot");
            btnOpenNetworkThrottlingRegedit = (Button)window.FindName("BtnOpenNetworkThrottlingRegedit");

            toggleMmcssGames         = (ToggleButton)window.FindName("ToggleMmcssGames");
            txtMmcssGamesSubtitle    = (TextBlock)window.FindName("TxtMmcssGamesSubtitle");
            btnOpenMmcssGamesRegedit = (Button)window.FindName("BtnOpenMmcssGamesRegedit");

            toggleGameDvr            = (ToggleButton)window.FindName("ToggleGameDvr");
            txtGameDvrSubtitle       = (TextBlock)window.FindName("TxtGameDvrSubtitle");
            btnOpenGameDvrRegedit    = (Button)window.FindName("BtnOpenGameDvrRegedit");

            toggleUsbSelectiveSuspend = (ToggleButton)window.FindName("ToggleUsbSelectiveSuspend");
            txtUsbSelectiveSuspendSubtitle = (TextBlock)window.FindName("TxtUsbSelectiveSuspendSubtitle");
            btnOpenUsbDevMgmt        = (Button)window.FindName("BtnOpenUsbDevMgmt");

            toggleRealtekRss         = (ToggleButton)window.FindName("ToggleRealtekRss");
            txtRealtekRssSubtitle    = (TextBlock)window.FindName("TxtRealtekRssSubtitle");
            txtRealtekRssAdapter     = (TextBlock)window.FindName("TxtRealtekRssAdapter");
            txtRealtekRssReboot      = (TextBlock)window.FindName("TxtRealtekRssReboot");
            btnRealtekRssDriver      = (Button)window.FindName("BtnRealtekRssDriver");

            // Allgemeine Tweaks Live Technical Inspector TextBlocks
            txtMouseQueueLiveVal             = (TextBlock)window.FindName("TxtMouseQueueLiveVal");
            txtPriorityLiveVal               = (TextBlock)window.FindName("TxtPriorityLiveVal");
            txtCoreIsoLiveVal                = (TextBlock)window.FindName("TxtCoreIsoLiveVal");
            txtHypervisorLiveVal             = (TextBlock)window.FindName("TxtHypervisorLiveVal");
            txtPowerPlanLiveVal              = (TextBlock)window.FindName("TxtPowerPlanLiveVal");
            txtLanEeeLiveVal                 = (TextBlock)window.FindName("TxtLanEeeLiveVal");
            txtLanPowerLiveVal               = (TextBlock)window.FindName("TxtLanPowerLiveVal");
            txtLanInterruptLiveVal           = (TextBlock)window.FindName("TxtLanInterruptLiveVal");
            txtPagingLiveVal                 = (TextBlock)window.FindName("TxtPagingLiveVal");
            txtSystemResponsivenessLiveVal   = (TextBlock)window.FindName("TxtSystemResponsivenessLiveVal");
            txtNetworkThrottlingLiveVal      = (TextBlock)window.FindName("TxtNetworkThrottlingLiveVal");
            txtMmcssPrioLiveVal              = (TextBlock)window.FindName("TxtMmcssPrioLiveVal");
            txtMmcssCategoryLiveVal          = (TextBlock)window.FindName("TxtMmcssCategoryLiveVal");
            txtGameDvrLiveVal                = (TextBlock)window.FindName("TxtGameDvrLiveVal");
            txtFseModeLiveVal                = (TextBlock)window.FindName("TxtFseModeLiveVal");
            txtAllowGameDvrLiveVal           = (TextBlock)window.FindName("TxtAllowGameDvrLiveVal");
            txtUsbRegLiveVal                 = (TextBlock)window.FindName("TxtUsbRegLiveVal");
            txtUsbPowerLiveVal               = (TextBlock)window.FindName("TxtUsbPowerLiveVal");
            txtRealtekRssEnabledLiveVal      = (TextBlock)window.FindName("TxtRealtekRssEnabledLiveVal");
            txtRealtekRssProcLiveVal         = (TextBlock)window.FindName("TxtRealtekRssProcLiveVal");

            // Tab 7: Chipset
            borderChipsetStatus      = (Border)window.FindName("BorderChipsetStatus");
            txtChipsetStatusTitle    = (TextBlock)window.FindName("TxtChipsetStatusTitle");
            txtChipsetStatusDesc     = (TextBlock)window.FindName("TxtChipsetStatusDesc");
            pillChipsetStatus        = (Border)window.FindName("PillChipsetStatus");
            txtPillChipset           = (TextBlock)window.FindName("TxtPillChipset");
            txtChipsetPlatform       = (TextBlock)window.FindName("TxtChipsetPlatform");
            txtChipsetInstalledVer   = (TextBlock)window.FindName("TxtChipsetInstalledVer");
            txtChipsetLatestVer      = (TextBlock)window.FindName("TxtChipsetLatestVer");
            txtChipsetReleaseDate    = (TextBlock)window.FindName("TxtChipsetReleaseDate");
            btnInstallChipsetDriver  = (Button)window.FindName("BtnInstallChipsetDriver");
            btnDownloadOnlyChipset   = (Button)window.FindName("BtnDownloadOnlyChipset");
            btnCheckChipsetUpdate    = (Button)window.FindName("BtnCheckChipsetUpdate");
            btnOpenChipsetFolder     = (Button)window.FindName("BtnOpenChipsetFolder");
            chkCleanInstallChipset   = (CheckBox)window.FindName("ChkCleanInstallChipset");
            borderChipsetProgress    = (Border)window.FindName("BorderChipsetProgress");
            txtChipsetProgressStatus = (TextBlock)window.FindName("TxtChipsetProgressStatus");
            txtChipsetDownloadSize   = (TextBlock)window.FindName("TxtChipsetDownloadSize");
            txtChipsetDownloadSpeed  = (TextBlock)window.FindName("TxtChipsetDownloadSpeed");
            txtChipsetDownloadPercent= (TextBlock)window.FindName("TxtChipsetDownloadPercent");
            progChipsetDownload      = (ProgressBar)window.FindName("ProgChipsetDownload");
            btnCancelChipsetDownload = (Button)window.FindName("BtnCancelChipsetDownload");
            txtChipsetVcacheVer      = (TextBlock)window.FindName("TxtChipsetVcacheVer");
            txtChipsetPpmVer         = (TextBlock)window.FindName("TxtChipsetPpmVer");
            txtChipsetSmbusVer       = (TextBlock)window.FindName("TxtChipsetSmbusVer");
            txtChipsetPspVer         = (TextBlock)window.FindName("TxtChipsetPspVer");
            txtChipsetGpioVer        = (TextBlock)window.FindName("TxtChipsetGpioVer");
            txtChipsetI2cVer         = (TextBlock)window.FindName("TxtChipsetI2cVer");
            btnOpenDevMgmtChipset    = (Button)window.FindName("BtnOpenDevMgmtChipset");

            // Office
            chkOfficeSelectAll         = (CheckBox)window.FindName("ChkOfficeSelectAll");
            radFilterOfficeAll         = (RadioButton)window.FindName("RadFilterOfficeAll");
            radFilterOfficeInstalled   = (RadioButton)window.FindName("RadFilterOfficeInstalled");
            radFilterOfficeNotInstalled = (RadioButton)window.FindName("RadFilterOfficeNotInstalled");
            rowOfficeWord              = (Border)window.FindName("RowOfficeWord");
            rowOfficeExcel             = (Border)window.FindName("RowOfficeExcel");
            rowOfficePowerPoint        = (Border)window.FindName("RowOfficePowerPoint");
            rowOfficeOutlook           = (Border)window.FindName("RowOfficeOutlook");
            rowOfficeOneNote           = (Border)window.FindName("RowOfficeOneNote");
            rowOfficeAccess            = (Border)window.FindName("RowOfficeAccess");
            rowOfficePublisher         = (Border)window.FindName("RowOfficePublisher");
            rowOfficeTeams             = (Border)window.FindName("RowOfficeTeams");
            chkOfficeWord              = (CheckBox)window.FindName("ChkOfficeWord");
            chkOfficeExcel             = (CheckBox)window.FindName("ChkOfficeExcel");
            chkOfficePowerPoint        = (CheckBox)window.FindName("ChkOfficePowerPoint");
            chkOfficeOutlook           = (CheckBox)window.FindName("ChkOfficeOutlook");
            chkOfficeOneNote           = (CheckBox)window.FindName("ChkOfficeOneNote");
            chkOfficeAccess            = (CheckBox)window.FindName("ChkOfficeAccess");
            chkOfficePublisher         = (CheckBox)window.FindName("ChkOfficePublisher");
            chkOfficeLync              = (CheckBox)window.FindName("ChkOfficeLync");
            chkOfficeTeams             = (CheckBox)window.FindName("ChkOfficeTeams");
            chkOfficeOneDrive          = (CheckBox)window.FindName("ChkOfficeOneDrive");
            borderOfficeProgress       = (Border)window.FindName("BorderOfficeProgress");
            txtOfficeStatus      = (TextBlock)window.FindName("TxtOfficeStatus");
            txtOfficePercent     = (TextBlock)window.FindName("TxtOfficePercent");
            progOfficeDownload   = (ProgressBar)window.FindName("ProgOfficeDownload");
            txtOfficeDownloadDetails = (TextBlock)window.FindName("TxtOfficeDownloadDetails");
            txtOfficeEta             = (TextBlock)window.FindName("TxtOfficeEta");
            txtOfficeSubStatus       = (TextBlock)window.FindName("TxtOfficeSubStatus");
            btnCancelOfficeAction   = (Button)window.FindName("BtnCancelOfficeAction");
            btnInstallSelectedApps = (Button)window.FindName("BtnInstallSelectedApps");
            btnUninstallSelectedApps = (Button)window.FindName("BtnUninstallSelectedApps");
            btnOhookActivate       = (Button)window.FindName("BtnOhookActivate");
            pillOfficeActivation    = (Border)window.FindName("PillOfficeActivation");
            txtPillOfficeActivation = (TextBlock)window.FindName("TxtPillOfficeActivation");
            txtOfficeActivationInfo = (TextBlock)window.FindName("TxtOfficeActivationInfo");
            pillOfficeWord             = (Border)window.FindName("PillOfficeWord");
            txtPillOfficeWord          = (TextBlock)window.FindName("TxtPillOfficeWord");
            txtDescOfficeWord          = (TextBlock)window.FindName("TxtDescOfficeWord");
            pillOfficeExcel            = (Border)window.FindName("PillOfficeExcel");
            txtPillOfficeExcel         = (TextBlock)window.FindName("TxtPillOfficeExcel");
            txtDescOfficeExcel         = (TextBlock)window.FindName("TxtDescOfficeExcel");
            pillOfficePowerPoint       = (Border)window.FindName("PillOfficePowerPoint");
            txtPillOfficePowerPoint    = (TextBlock)window.FindName("TxtPillOfficePowerPoint");
            txtDescOfficePowerPoint    = (TextBlock)window.FindName("TxtDescOfficePowerPoint");
            pillOfficeOutlook          = (Border)window.FindName("PillOfficeOutlook");
            txtPillOfficeOutlook       = (TextBlock)window.FindName("TxtPillOfficeOutlook");
            txtDescOfficeOutlook       = (TextBlock)window.FindName("TxtDescOfficeOutlook");
            pillOfficeOneNote          = (Border)window.FindName("PillOfficeOneNote");
            txtPillOfficeOneNote       = (TextBlock)window.FindName("TxtPillOfficeOneNote");
            txtDescOfficeOneNote       = (TextBlock)window.FindName("TxtDescOfficeOneNote");
            pillOfficeAccess           = (Border)window.FindName("PillOfficeAccess");
            txtPillOfficeAccess        = (TextBlock)window.FindName("TxtPillOfficeAccess");
            txtDescOfficeAccess        = (TextBlock)window.FindName("TxtDescOfficeAccess");
            pillOfficePublisher        = (Border)window.FindName("PillOfficePublisher");
            txtPillOfficePublisher     = (TextBlock)window.FindName("TxtPillOfficePublisher");
            txtDescOfficePublisher     = (TextBlock)window.FindName("TxtDescOfficePublisher");
            pillOfficeLync             = (Border)window.FindName("PillOfficeLync");
            txtPillOfficeLync          = (TextBlock)window.FindName("TxtPillOfficeLync");
            pillOfficeTeams            = (Border)window.FindName("PillOfficeTeams");
            txtPillOfficeTeams         = (TextBlock)window.FindName("TxtPillOfficeTeams");
            txtDescOfficeTeams         = (TextBlock)window.FindName("TxtDescOfficeTeams");
            pillOfficeOneDrive         = (Border)window.FindName("PillOfficeOneDrive");
            txtPillOfficeOneDrive      = (TextBlock)window.FindName("TxtPillOfficeOneDrive");

            // Tab 12: Apps & Downloads
            txtSearchApps        = (TextBox)window.FindName("TxtSearchApps");
            txtSearchAppsPlaceholder = (TextBlock)window.FindName("TxtSearchAppsPlaceholder");
            btnClearSearchApps   = (Button)window.FindName("BtnClearSearchApps");
            panelAppsNoResults   = (Border)window.FindName("PanelAppsNoResults");
            chkAppsSelectAll     = (CheckBox)window.FindName("ChkAppsSelectAll");
            radFilterAppsAll     = (RadioButton)window.FindName("RadFilterAppsAll");
            radFilterAppsInstalled = (RadioButton)window.FindName("RadFilterAppsInstalled");
            radFilterAppsUpdates = (RadioButton)window.FindName("RadFilterAppsUpdates");
            radFilterAppsNotInstalled = (RadioButton)window.FindName("RadFilterAppsNotInstalled");
            btnActionSelectedApps= (Button)window.FindName("BtnActionSelectedApps");
            rowAppSteam          = (Border)window.FindName("RowAppSteam");
            rowAppDiscord        = (Border)window.FindName("RowAppDiscord");
            rowAppStreamDeck     = (Border)window.FindName("RowAppStreamDeck");
            rowAppBleachBit      = (Border)window.FindName("RowAppBleachBit");
            rowAppAntigravity    = (Border)window.FindName("RowAppAntigravity");
            rowAppEvga           = (Border)window.FindName("RowAppEvga");
            rowAppHasleo         = (Border)window.FindName("RowAppHasleo");
            chkSteam             = (CheckBox)window.FindName("ChkSteam");
            chkDiscord           = (CheckBox)window.FindName("ChkDiscord");
            chkStreamDeck        = (CheckBox)window.FindName("ChkStreamDeck");
            chkBleachBit         = (CheckBox)window.FindName("ChkBleachBit");
            chkAntigravity       = (CheckBox)window.FindName("ChkAntigravity");
            chkEvga              = (CheckBox)window.FindName("ChkEvga");
            chkHasleo            = (CheckBox)window.FindName("ChkHasleo");
            pillAppSteam         = (Border)window.FindName("PillAppSteam");
            txtPillAppSteam      = (TextBlock)window.FindName("TxtPillAppSteam");
            txtDescAppSteam      = (TextBlock)window.FindName("TxtDescAppSteam");
            pillAppDiscord       = (Border)window.FindName("PillAppDiscord");
            txtPillAppDiscord    = (TextBlock)window.FindName("TxtPillAppDiscord");
            txtDescAppDiscord    = (TextBlock)window.FindName("TxtDescAppDiscord");
            pillAppStreamDeck    = (Border)window.FindName("PillAppStreamDeck");
            txtPillAppStreamDeck = (TextBlock)window.FindName("TxtPillAppStreamDeck");
            txtDescAppStreamDeck = (TextBlock)window.FindName("TxtDescAppStreamDeck");
            pillAppBleachBit     = (Border)window.FindName("PillAppBleachBit");
            txtPillAppBleachBit  = (TextBlock)window.FindName("TxtPillAppBleachBit");
            txtDescAppBleachBit  = (TextBlock)window.FindName("TxtDescAppBleachBit");
            pillAppAntigravity   = (Border)window.FindName("PillAppAntigravity");
            txtPillAppAntigravity= (TextBlock)window.FindName("TxtPillAppAntigravity");
            txtDescAppAntigravity= (TextBlock)window.FindName("TxtDescAppAntigravity");
            pillAppEvga          = (Border)window.FindName("PillAppEvga");
            txtPillAppEvga       = (TextBlock)window.FindName("TxtPillAppEvga");
            txtDescAppEvga       = (TextBlock)window.FindName("TxtDescAppEvga");
            pillAppHasleo        = (Border)window.FindName("PillAppHasleo");
            txtPillAppHasleo     = (TextBlock)window.FindName("TxtPillAppHasleo");
            txtDescAppHasleo     = (TextBlock)window.FindName("TxtDescAppHasleo");
            btnRefreshApps       = (Button)window.FindName("BtnRefreshApps");
            btnOpenDownloadFolder= (Button)window.FindName("BtnOpenDownloadFolder");
            borderAppProgress    = (Border)window.FindName("BorderAppProgress");
            txtAppStatus         = (TextBlock)window.FindName("TxtAppStatus");
            txtAppDownloadSize   = (TextBlock)window.FindName("TxtAppDownloadSize");
            txtAppDownloadSpeed  = (TextBlock)window.FindName("TxtAppDownloadSpeed");
            txtAppDownloadPercent= (TextBlock)window.FindName("TxtAppDownloadPercent");
            progAppDownload      = (ProgressBar)window.FindName("ProgAppDownload");
            btnSkipAppDownload   = (Button)window.FindName("BtnSkipAppDownload");
            btnCancelAppDownloads= (Button)window.FindName("BtnCancelAppDownloads");

            // Tab 13: Gamebar
            txtKglLoaded      = (TextBlock)window.FindName("TxtKglLoaded");
            txtKglService     = (TextBlock)window.FindName("TxtKglService");
            borderKglSync     = (Border)window.FindName("BorderKglSync");
            txtKglSyncTitle   = (TextBlock)window.FindName("TxtKglSyncTitle");
            txtKglSyncDesc    = (TextBlock)window.FindName("TxtKglSyncDesc");
            pillKglStatus     = (Border)window.FindName("PillKglStatus");
            txtKglPill        = (TextBlock)window.FindName("TxtKglPill");
            txtGamebarVersion = (TextBlock)window.FindName("TxtGamebarVersion");
            txtAmd3dStatus    = (TextBlock)window.FindName("TxtAmd3dStatus");
            btnOpenGamebar    = (Button)window.FindName("BtnOpenGamebar");

            // Tab 15: Updates
            btnRunWindowsUpdate = (Button)window.FindName("BtnRunWindowsUpdate");
            btnRunStoreUpdate   = (Button)window.FindName("BtnRunStoreUpdate");

            // Tab 16: BIOS
            txtLiveSecureBoot     = (TextBlock)window.FindName("TxtLiveSecureBoot");
            pillLiveSecureBoot    = (Border)window.FindName("PillLiveSecureBoot");
            txtPillLiveSecureBoot = (TextBlock)window.FindName("TxtPillLiveSecureBoot");
            txtLiveAcPower        = (TextBlock)window.FindName("TxtLiveAcPower");
            txtLiveWlan           = (TextBlock)window.FindName("TxtLiveWlan");
            pillLiveWlan          = (Border)window.FindName("PillLiveWlan");
            txtPillLiveWlan       = (TextBlock)window.FindName("TxtPillLiveWlan");
            txtDescWlan           = (TextBlock)window.FindName("TxtDescWlan");
            txtLiveBt             = (TextBlock)window.FindName("TxtLiveBt");
            pillLiveBt            = (Border)window.FindName("PillLiveBt");
            txtPillLiveBt         = (TextBlock)window.FindName("TxtPillLiveBt");
            txtDescBt             = (TextBlock)window.FindName("TxtDescBt");
            txtLiveRebar          = (TextBlock)window.FindName("TxtLiveRebar");
            pillLiveRebar         = (Border)window.FindName("PillLiveRebar");
            txtPillLiveRebar      = (TextBlock)window.FindName("TxtPillLiveRebar");
            txtLiveIgpu           = (TextBlock)window.FindName("TxtLiveIgpu");
            pillLiveIgpu          = (Border)window.FindName("PillLiveIgpu");
            txtPillLiveIgpu       = (TextBlock)window.FindName("TxtPillLiveIgpu");
            txtDescIgpu           = (TextBlock)window.FindName("TxtDescIgpu");

            // Hardware-Info Tab
            txtHwCpuName       = (TextBlock)window.FindName("TxtHwCpuName");
            txtHwCpuBadge      = (TextBlock)window.FindName("TxtHwCpuBadge");
            txtHwCpuCores      = (TextBlock)window.FindName("TxtHwCpuCores");
            txtHwCpuClock      = (TextBlock)window.FindName("TxtHwCpuClock");
            txtHwCpuCache      = (TextBlock)window.FindName("TxtHwCpuCache");
            txtHwCpuSocket     = (TextBlock)window.FindName("TxtHwCpuSocket");

            txtHwGpuName       = (TextBlock)window.FindName("TxtHwGpuName");
            txtHwGpuBadge      = (TextBlock)window.FindName("TxtHwGpuBadge");
            txtHwGpuDriver     = (TextBlock)window.FindName("TxtHwGpuDriver");
            txtHwGpuVram       = (TextBlock)window.FindName("TxtHwGpuVram");
            txtHwGpuRes        = (TextBlock)window.FindName("TxtHwGpuRes");

            txtHwGpuSubGpu     = (TextBlock)window.FindName("TxtHwGpuSubGpu");

            txtHwBoardModel    = (TextBlock)window.FindName("TxtHwBoardModel");
            txtHwBoardVendor   = (TextBlock)window.FindName("TxtHwBoardVendor");
            txtHwBoardChipset  = (TextBlock)window.FindName("TxtHwBoardChipset");
            txtHwBiosVersion   = (TextBlock)window.FindName("TxtHwBiosVersion");
            txtHwBiosDate      = (TextBlock)window.FindName("TxtHwBiosDate");
            txtHwBoardSerial   = (TextBlock)window.FindName("TxtHwBoardSerial");

            txtHwRamTotal      = (TextBlock)window.FindName("TxtHwRamTotal");
            txtHwRamSpeed      = (TextBlock)window.FindName("TxtHwRamSpeed");
            txtHwRamSlots      = (TextBlock)window.FindName("TxtHwRamSlots");
            txtHwRamModules    = (TextBlock)window.FindName("TxtHwRamModules");
            txtHwRamClock      = (TextBlock)window.FindName("TxtHwRamClock");
            txtHwRamUsage      = (TextBlock)window.FindName("TxtHwRamUsage");

            txtHwStorageSummary = (TextBlock)window.FindName("TxtHwStorageSummary");
            stackHwDrivesList  = (StackPanel)window.FindName("StackHwDrivesList");

            txtStorageSummary  = (TextBlock)window.FindName("TxtStorageSummary");
            stackDrivesList    = (StackPanel)window.FindName("StackDrivesList");
            btnOpenDiskMgmt    = (Button)window.FindName("BtnOpenDiskMgmt");
            btnOpenCleanMgr    = (Button)window.FindName("BtnOpenCleanMgr");

            txtHwOsCaption     = (TextBlock)window.FindName("TxtHwOsCaption");
            txtHwOsActivation  = (TextBlock)window.FindName("TxtHwOsActivation");
            txtHwOsProductKey  = (TextBlock)window.FindName("TxtHwOsProductKey");
            btnToggleHwOsKey   = (Button)window.FindName("BtnToggleHwOsKey");
            btnCopyHwOsKey     = (Button)window.FindName("BtnCopyHwOsKey");
            txtHwOsBuild       = (TextBlock)window.FindName("TxtHwOsBuild");
            txtHwOsInstallDate = (TextBlock)window.FindName("TxtHwOsInstallDate");
            txtHwOsUptime      = (TextBlock)window.FindName("TxtHwOsUptime");
            txtHwOsSecurity    = (TextBlock)window.FindName("TxtHwOsSecurity");

            txtHwNetAdapter     = (TextBlock)window.FindName("TxtHwNetAdapter");
            txtHwNetSpeed       = (TextBlock)window.FindName("TxtHwNetSpeed");
            txtHwNetMac         = (TextBlock)window.FindName("TxtHwNetMac");
            txtHwNetDriverVer   = (TextBlock)window.FindName("TxtHwNetDriverVer");
            badgeHwNetProvider  = (Border)window.FindName("BadgeHwNetProvider");
            txtHwNetProvider    = (TextBlock)window.FindName("TxtHwNetProvider");
            txtHwNetDriverDate  = (TextBlock)window.FindName("TxtHwNetDriverDate");
            txtHwNetStatus      = (TextBlock)window.FindName("TxtHwNetStatus");
            btnCopyHwNetMac     = (Button)window.FindName("BtnCopyHwNetMac");
            btnOpenDevMgmtLan    = (Button)window.FindName("BtnOpenDevMgmtLan");
            btnOpenSoundSettings = (Button)window.FindName("BtnOpenSoundSettings");

            stackHwAudioList   = (StackPanel)window.FindName("StackHwAudioList");
            gridHwMonitors     = (Grid)window.FindName("GridHwMonitors");

            btnCopyHwCpu        = (Button)window.FindName("BtnCopyHwCpu");
            btnCopyHwGpu        = (Button)window.FindName("BtnCopyHwGpu");
            btnCopyHwBoard      = (Button)window.FindName("BtnCopyHwBoard");
            btnCopyHwRam        = (Button)window.FindName("BtnCopyHwRam");
            btnCopyHwRamModules = (Button)window.FindName("BtnCopyHwRamModules");
            btnCopyHwNetAdapter = (Button)window.FindName("BtnCopyHwNetAdapter");

            InitializeBufferbloatControls();
            ConfigureAdminState();
            SetupEventHandlers();

            // Fast registry-only reads – sofort, kein WMI
            RefreshMouseUI();
            RefreshKernelUI();
            RefreshPriorityUI();
            RefreshBcdUI();
            RefreshPowerUI();
            RefreshCoreIsoUI();
            RefreshLanEnergyUI();
            RefreshPagingExecutiveUI();
            RefreshSystemResponsivenessUI();
            RefreshNetworkThrottlingUI();
            RefreshMmcssGamesUI();
            RefreshGameDvrUI();
            RefreshUsbSelectiveSuspendUI();
            RefreshRealtekRssUI();
            RefreshTimerResUI();
            RefreshGamebarUI();
            RefreshOfficeStatusUI();
            RefreshAppsStatusUI();
            RefreshAllDrivesUI();
            SetTelemetryLoadingState();
            DetectRamExpoProfileAsync();
            SetBreadcrumb("🏠", "Home");

            // Fenster immer exakt im sichtbaren Bereich zentrieren & optimal skalieren
            try {
                double workWidth = SystemParameters.WorkArea.Width;
                double workHeight = SystemParameters.WorkArea.Height;
                window.WindowStartupLocation = WindowStartupLocation.Manual;
                window.Width = Math.Min(1500, Math.Max(800, workWidth * 0.90));
                window.Height = Math.Min(940, Math.Max(500, workHeight * 0.90));
                window.Left = Math.Max(SystemParameters.WorkArea.Left, (workWidth - window.Width) / 2 + SystemParameters.WorkArea.Left);
                window.Top = Math.Max(SystemParameters.WorkArea.Top, (workHeight - window.Height) / 2 + SystemParameters.WorkArea.Top);
            } catch {}

            // Langsame WMI-Abfragen NACH dem Öffnen des Fensters im Hintergrund laden
            window.Loaded += (s, e) => {
                UpdateChromeResizeBorder();
                UpdateResponsiveLayout(window.ActualWidth);
                try {
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    Program.ForceForeground(hwnd);
                    window.Activate();
                    window.Focus();
                } catch {}
                RefreshChipsetUI();
                RefreshBiosUI();
                RefreshHardwareInfoUI();
            };

            window.ContentRendered += (s, e) => {
                try {
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    window.Topmost = false;
                    Program.ForceForeground(hwnd);
                    window.Activate();
                    window.Focus();
                } catch {}
            };

            window.Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(delegate() {
                try {
                    window.Topmost = false;
                    IntPtr hwnd = new System.Windows.Interop.WindowInteropHelper(window).Handle;
                    Program.ForceForeground(hwnd);
                    window.Activate();
                    window.Focus();
                } catch {}
            }));

            return window;
        }




        private bool TestIsAdmin() {
            var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private void ConfigureAdminState() {
            try {
                // Falls das System gerade im Abgesicherten Modus läuft, safeboot-Flag automatisch löschen,
                // damit der nächste reguläre Neustart wieder ganz normal in Windows bootet
                if (GetSystemMetrics(67) != 0) {
                    ProcessRunner.RunAndGetOutput("bcdedit.exe", "/deletevalue {current} safeboot");
                }
            } catch {}

            if (!isAdmin) {
                if (bannerAdminWarning != null) bannerAdminWarning.Visibility = Visibility.Visible;

                if (toggleQueue != null) toggleQueue.IsEnabled = false;
                if (btnAdjustMouseQueue != null) btnAdjustMouseQueue.IsEnabled = false;
                if (toggleKernelTimer != null) toggleKernelTimer.IsEnabled = false;
                if (togglePriority != null) togglePriority.IsEnabled = false;
                if (cmbPriority != null) cmbPriority.IsEnabled = false;
                if (cmbClock != null) cmbClock.IsEnabled = false;
                if (cmbTick != null) cmbTick.IsEnabled = false;
                if (cmbTsc != null) cmbTsc.IsEnabled = false;
                if (cmbDynamic != null) cmbDynamic.IsEnabled = false;
                if (btnResetBcd != null) btnResetBcd.IsEnabled = false;
                if (cmbPowerPlans != null) cmbPowerPlans.IsEnabled = false;
                if (toggleCoreIso != null) toggleCoreIso.IsEnabled = false;
                if (toggleLanEnergy != null) toggleLanEnergy.IsEnabled = false;
                if (togglePagingExecutive != null) togglePagingExecutive.IsEnabled = false;
                if (toggleSystemResponsiveness != null) toggleSystemResponsiveness.IsEnabled = false;
                if (toggleNetworkThrottling != null) toggleNetworkThrottling.IsEnabled = false;
                if (toggleMmcssGames != null) toggleMmcssGames.IsEnabled = false;
                if (toggleGameDvr != null) toggleGameDvr.IsEnabled = false;
                if (toggleUsbSelectiveSuspend != null) toggleUsbSelectiveSuspend.IsEnabled = false;
                if (toggleRealtekRss != null) toggleRealtekRss.IsEnabled = false;
            }
        }

        private void SetBreadcrumb(string icon, string path) {
            if (txtBreadcrumbIcon != null) txtBreadcrumbIcon.Text = icon;
            if (txtBreadcrumbPath != null) txtBreadcrumbPath.Text = path;
        }

        private void SetupCopyButton(Button btn, Func<string> getTextToCopy, string tooltip = "Kopieren") {
            if (btn == null) return;
            btn.ToolTip = tooltip;
            btn.Click += (s, e) => {
                try {
                    string text = getTextToCopy != null ? getTextToCopy() : "";
                    if (!string.IsNullOrEmpty(text) && text != "Ermittle...") {
                        Clipboard.SetText(text);
                        btn.Content = "✓";
                        btn.ToolTip = "Kopiert: " + text;
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(1200) };
                        timer.Tick += (ts, te) => {
                            btn.Content = "📋";
                            btn.ToolTip = tooltip;
                            timer.Stop();
                        };
                        timer.Start();
                    }
                } catch {}
            };
        }

        private void UpdateChromeResizeBorder() {
            try {
                if (window == null) return;
                var chrome = System.Windows.Shell.WindowChrome.GetWindowChrome(window);
                if (chrome != null) {
                    chrome.ResizeBorderThickness = (window.WindowState == WindowState.Maximized)
                        ? new Thickness(0)
                        : new Thickness(6);
                }
                var rootGrid = window.Content as Grid;
                if (rootGrid != null) {
                    rootGrid.Margin = (window.WindowState == WindowState.Maximized)
                        ? new Thickness(7)
                        : new Thickness(0);
                }
            } catch {}
        }

        private void ToggleMaximizeWindow() {
            if (window == null) return;
            if (window.WindowState == WindowState.Maximized) {
                window.WindowState = WindowState.Normal;
                try {
                    double workWidth = SystemParameters.WorkArea.Width;
                    double workHeight = SystemParameters.WorkArea.Height;
                    if (window.Width <= 0 || double.IsNaN(window.Width) || window.Width > workWidth)
                        window.Width = Math.Min(1500, Math.Max(800, workWidth * 0.90));
                    if (window.Height <= 0 || double.IsNaN(window.Height) || window.Height > workHeight)
                        window.Height = Math.Min(940, Math.Max(500, workHeight * 0.90));
                    window.Left = Math.Max(SystemParameters.WorkArea.Left, (workWidth - window.Width) / 2 + SystemParameters.WorkArea.Left);
                    window.Top = Math.Max(SystemParameters.WorkArea.Top, (workHeight - window.Height) / 2 + SystemParameters.WorkArea.Top);
                } catch {}
            } else {
                window.WindowState = WindowState.Maximized;
            }
            UpdateChromeResizeBorder();
        }

        private static bool IsChildOf<T>(DependencyObject node) where T : DependencyObject {
            while (node != null) {
                if (node is T) return true;
                node = (node is Visual) ? VisualTreeHelper.GetParent(node) : LogicalTreeHelper.GetParent(node);
            }
            return false;
        }

        private void UpdateResponsiveLayout(double width) {
            if (width <= 0) return;

            // Tier 0: Wide (>= 1080px)
            // Tier 1: Compact (750px .. 1079px) - cards wrap/stack vertically across 1 column
            // Tier 2: Ultra-Compact (< 750px) - inner sub-grids (like CPU/GPU side-by-side and BB metrics) also stack into 1 column
            int tier = (width >= 1080) ? 0 : (width >= 750 ? 1 : 2);

            if (tier == currentResponsiveTier) return;
            currentResponsiveTier = tier;

            try {
                // 1. Sidebar width
                if (colSidebar != null) {
                    colSidebar.Width = new GridLength(tier == 0 ? 240 : (tier == 1 ? 200 : 185));
                }

                // 2. Home Tab Top Cards
                if (gridHomeTopCards != null && panelCpuGpuBlock != null && cardHomeRam != null && cardHomeGpuLimits != null) {
                    gridHomeTopCards.ColumnDefinitions.Clear();
                    gridHomeTopCards.RowDefinitions.Clear();

                    if (tier == 0) {
                        // Wide: 3 columns (2*, 12, 1*, 12, 1*)
                        gridHomeTopCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(2, GridUnitType.Star) });
                        gridHomeTopCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                        gridHomeTopCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHomeTopCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                        gridHomeTopCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        Grid.SetRow(panelCpuGpuBlock, 0);
                        Grid.SetColumn(panelCpuGpuBlock, 0);
                        panelCpuGpuBlock.Margin = new Thickness(0);

                        Grid.SetRow(cardHomeRam, 0);
                        Grid.SetColumn(cardHomeRam, 2);
                        cardHomeRam.Margin = new Thickness(0);

                        Grid.SetRow(cardHomeGpuLimits, 0);
                        Grid.SetColumn(cardHomeGpuLimits, 4);
                        cardHomeGpuLimits.Margin = new Thickness(0);
                    } else {
                        // Compact / Ultra-Compact: 1 column, 3 rows stacked vertically
                        gridHomeTopCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHomeTopCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHomeTopCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHomeTopCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(panelCpuGpuBlock, 0);
                        Grid.SetColumn(panelCpuGpuBlock, 0);
                        panelCpuGpuBlock.Margin = new Thickness(0, 0, 0, 10);

                        Grid.SetRow(cardHomeRam, 1);
                        Grid.SetColumn(cardHomeRam, 0);
                        cardHomeRam.Margin = new Thickness(0, 0, 0, 10);

                        Grid.SetRow(cardHomeGpuLimits, 2);
                        Grid.SetColumn(cardHomeGpuLimits, 0);
                        cardHomeGpuLimits.Margin = new Thickness(0);
                    }
                }

                // 2b. CPU & GPU row inside panelCpuGpuBlock
                if (gridCpuGpuRow != null && cardHomeCpu != null && cardHomeGpu != null) {
                    gridCpuGpuRow.ColumnDefinitions.Clear();
                    gridCpuGpuRow.RowDefinitions.Clear();

                    if (tier < 2) {
                        // Wide & Compact: CPU and GPU side-by-side
                        gridCpuGpuRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridCpuGpuRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(12) });
                        gridCpuGpuRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        Grid.SetRow(cardHomeCpu, 0);
                        Grid.SetColumn(cardHomeCpu, 0);
                        cardHomeCpu.Margin = new Thickness(0);

                        Grid.SetRow(cardHomeGpu, 0);
                        Grid.SetColumn(cardHomeGpu, 2);
                        cardHomeGpu.Margin = new Thickness(0);
                    } else {
                        // Ultra-Compact: CPU stacked above GPU
                        gridCpuGpuRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridCpuGpuRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridCpuGpuRow.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(cardHomeCpu, 0);
                        Grid.SetColumn(cardHomeCpu, 0);
                        cardHomeCpu.Margin = new Thickness(0, 0, 0, 8);

                        Grid.SetRow(cardHomeGpu, 1);
                        Grid.SetColumn(cardHomeGpu, 0);
                        cardHomeGpu.Margin = new Thickness(0);
                    }
                }

                // 3. Home Tab Monitors (3 Live Monitors)
                if (gridHomeMonitors != null && cardMonitorTemp != null && cardMonitorLoad != null && cardMonitorPower != null) {
                    gridHomeMonitors.ColumnDefinitions.Clear();
                    gridHomeMonitors.RowDefinitions.Clear();

                    if (tier == 0) {
                        // Wide: 3 columns (1*, 14, 1*, 14, 1*)
                        gridHomeMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHomeMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        gridHomeMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHomeMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        gridHomeMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        Grid.SetRow(cardMonitorTemp, 0);
                        Grid.SetColumn(cardMonitorTemp, 0);
                        cardMonitorTemp.Margin = new Thickness(0);

                        Grid.SetRow(cardMonitorLoad, 0);
                        Grid.SetColumn(cardMonitorLoad, 2);
                        cardMonitorLoad.Margin = new Thickness(0);

                        Grid.SetRow(cardMonitorPower, 0);
                        Grid.SetColumn(cardMonitorPower, 4);
                        cardMonitorPower.Margin = new Thickness(0);
                    } else {
                        // Compact / Ultra-Compact: 1 column, 3 rows stacked
                        gridHomeMonitors.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHomeMonitors.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHomeMonitors.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHomeMonitors.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(cardMonitorTemp, 0);
                        Grid.SetColumn(cardMonitorTemp, 0);
                        cardMonitorTemp.Margin = new Thickness(0, 0, 0, 12);

                        Grid.SetRow(cardMonitorLoad, 1);
                        Grid.SetColumn(cardMonitorLoad, 0);
                        cardMonitorLoad.Margin = new Thickness(0, 0, 0, 12);

                        Grid.SetRow(cardMonitorPower, 2);
                        Grid.SetColumn(cardMonitorPower, 0);
                        cardMonitorPower.Margin = new Thickness(0);
                    }
                }

                // 4. Hardware Tab Hero Cards (2x2 -> 4x1)
                if (gridHwHeroCards != null && cardHwCpu != null && cardHwGpu != null && cardHwBoard != null && cardHwRam != null) {
                    gridHwHeroCards.ColumnDefinitions.Clear();
                    gridHwHeroCards.RowDefinitions.Clear();

                    if (tier == 0) {
                        gridHwHeroCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHwHeroCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        gridHwHeroCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        gridHwHeroCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHwHeroCards.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
                        gridHwHeroCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(cardHwCpu, 0);
                        Grid.SetColumn(cardHwCpu, 0);
                        cardHwCpu.Margin = new Thickness(0);

                        Grid.SetRow(cardHwGpu, 0);
                        Grid.SetColumn(cardHwGpu, 2);
                        cardHwGpu.Margin = new Thickness(0);

                        Grid.SetRow(cardHwBoard, 2);
                        Grid.SetColumn(cardHwBoard, 0);
                        cardHwBoard.Margin = new Thickness(0);

                        Grid.SetRow(cardHwRam, 2);
                        Grid.SetColumn(cardHwRam, 2);
                        cardHwRam.Margin = new Thickness(0);
                    } else {
                        gridHwHeroCards.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHwHeroCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHwHeroCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHwHeroCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHwHeroCards.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(cardHwCpu, 0);
                        Grid.SetColumn(cardHwCpu, 0);
                        cardHwCpu.Margin = new Thickness(0, 0, 0, 12);

                        Grid.SetRow(cardHwGpu, 1);
                        Grid.SetColumn(cardHwGpu, 0);
                        cardHwGpu.Margin = new Thickness(0, 0, 0, 12);

                        Grid.SetRow(cardHwBoard, 2);
                        Grid.SetColumn(cardHwBoard, 0);
                        cardHwBoard.Margin = new Thickness(0, 0, 0, 12);

                        Grid.SetRow(cardHwRam, 3);
                        Grid.SetColumn(cardHwRam, 0);
                        cardHwRam.Margin = new Thickness(0);
                    }
                }

                // 5. Hardware Tab Network & Audio (2x1 -> 1x2)
                if (gridHwNetAudio != null && cardHwNet != null && cardHwAudio != null) {
                    gridHwNetAudio.ColumnDefinitions.Clear();
                    gridHwNetAudio.RowDefinitions.Clear();

                    if (tier == 0) {
                        gridHwNetAudio.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHwNetAudio.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        gridHwNetAudio.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        Grid.SetRow(cardHwNet, 0);
                        Grid.SetColumn(cardHwNet, 0);
                        cardHwNet.Margin = new Thickness(0);

                        Grid.SetRow(cardHwAudio, 0);
                        Grid.SetColumn(cardHwAudio, 2);
                        cardHwAudio.Margin = new Thickness(0);
                    } else {
                        gridHwNetAudio.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridHwNetAudio.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridHwNetAudio.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(cardHwNet, 0);
                        Grid.SetColumn(cardHwNet, 0);
                        cardHwNet.Margin = new Thickness(0, 0, 0, 12);

                        Grid.SetRow(cardHwAudio, 1);
                        Grid.SetColumn(cardHwAudio, 0);
                        cardHwAudio.Margin = new Thickness(0);
                    }
                }

                // 6. Bufferbloat Results (Grade Card + 3 Metric Columns)
                if (gridBbMain != null && cardBbGrade != null && gridBbMetrics != null) {
                    gridBbMain.ColumnDefinitions.Clear();
                    gridBbMain.RowDefinitions.Clear();

                    if (tier == 0) {
                        gridBbMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(340) });
                        gridBbMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                        gridBbMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        Grid.SetRow(cardBbGrade, 0);
                        Grid.SetColumn(cardBbGrade, 0);
                        cardBbGrade.Margin = new Thickness(0);

                        Grid.SetRow(gridBbMetrics, 0);
                        Grid.SetColumn(gridBbMetrics, 2);
                        gridBbMetrics.Margin = new Thickness(0);
                    } else {
                        gridBbMain.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridBbMain.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridBbMain.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(cardBbGrade, 0);
                        Grid.SetColumn(cardBbGrade, 0);
                        cardBbGrade.Margin = new Thickness(0, 0, 0, 12);

                        Grid.SetRow(gridBbMetrics, 1);
                        Grid.SetColumn(gridBbMetrics, 0);
                        gridBbMetrics.Margin = new Thickness(0);
                    }
                }

                // 6b. Bufferbloat 3 Metric Columns
                if (gridBbMetrics != null && cardBbIdle != null && cardBbDl != null && cardBbUl != null) {
                    gridBbMetrics.ColumnDefinitions.Clear();
                    gridBbMetrics.RowDefinitions.Clear();

                    if (tier < 2) {
                        gridBbMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridBbMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                        gridBbMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridBbMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                        gridBbMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                        Grid.SetRow(cardBbIdle, 0);
                        Grid.SetColumn(cardBbIdle, 0);
                        cardBbIdle.Margin = new Thickness(0);

                        Grid.SetRow(cardBbDl, 0);
                        Grid.SetColumn(cardBbDl, 2);
                        cardBbDl.Margin = new Thickness(0);

                        Grid.SetRow(cardBbUl, 0);
                        Grid.SetColumn(cardBbUl, 4);
                        cardBbUl.Margin = new Thickness(0);
                    } else {
                        gridBbMetrics.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                        gridBbMetrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridBbMetrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                        gridBbMetrics.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                        Grid.SetRow(cardBbIdle, 0);
                        Grid.SetColumn(cardBbIdle, 0);
                        cardBbIdle.Margin = new Thickness(0, 0, 0, 8);

                        Grid.SetRow(cardBbDl, 1);
                        Grid.SetColumn(cardBbDl, 0);
                        cardBbDl.Margin = new Thickness(0, 0, 0, 8);

                        Grid.SetRow(cardBbUl, 2);
                        Grid.SetColumn(cardBbUl, 0);
                        cardBbUl.Margin = new Thickness(0);
                    }
                }

                // 7. Allgemeine Tweaks (Mouse) Tab
                RelayoutMouseTweaks();
            } catch {}
        }

        private void SetupEventHandlers() {
            // CUSTOM WINDOW CHROME & DRAGMOVE / DOUBLE-CLICK MAXIMIZE / DRAG-TO-RESTORE
            UpdateChromeResizeBorder();
            Point? titleBarDragStart = null;
            bool isTitleBarDragging = false;

            Action<object, System.Windows.Input.MouseButtonEventArgs> handleTitleBarMouseDown = (s, e) => {
                if (e.ChangedButton != System.Windows.Input.MouseButton.Left) return;
                DependencyObject dep = e.OriginalSource as DependencyObject;
                if (dep != null && IsChildOf<Button>(dep)) return;

                if (e.ClickCount == 2) {
                    isTitleBarDragging = false;
                    titleBarDragStart = null;
                    UIElement elem = s as UIElement;
                    if (elem != null) { try { elem.ReleaseMouseCapture(); } catch {} }
                    ToggleMaximizeWindow();
                    return;
                }

                if (window.WindowState == WindowState.Normal) {
                    isTitleBarDragging = false;
                    titleBarDragStart = null;
                    try { window.DragMove(); } catch {}
                } else if (window.WindowState == WindowState.Maximized) {
                    titleBarDragStart = e.GetPosition(window);
                    isTitleBarDragging = true;
                    UIElement elem = s as UIElement;
                    if (elem != null) {
                        try { elem.CaptureMouse(); } catch {}
                    }
                }
            };

            Action<object, System.Windows.Input.MouseEventArgs> handleTitleBarMouseMove = (s, e) => {
                if (!isTitleBarDragging || e.LeftButton != System.Windows.Input.MouseButtonState.Pressed || !titleBarDragStart.HasValue) {
                    return;
                }

                if (window.WindowState == WindowState.Maximized) {
                    Point currentPos = e.GetPosition(window);
                    Vector diff = currentPos - titleBarDragStart.Value;
                    if (Math.Abs(diff.X) > 4 || Math.Abs(diff.Y) > 4) {
                        isTitleBarDragging = false;
                        UIElement elem = s as UIElement;
                        if (elem != null) {
                            try { elem.ReleaseMouseCapture(); } catch {}
                        }

                        double mouseClickX = titleBarDragStart.Value.X;
                        double mouseClickY = titleBarDragStart.Value.Y;
                        double percentX = mouseClickX / Math.Max(1.0, window.ActualWidth);
                        if (percentX < 0.0) percentX = 0.0;
                        if (percentX > 1.0) percentX = 1.0;

                        double targetWidth = 1500;
                        if (window.RestoreBounds.Width > 0 && !double.IsNaN(window.RestoreBounds.Width)) {
                            targetWidth = window.RestoreBounds.Width;
                        } else if (window.Width > 0 && !double.IsNaN(window.Width)) {
                            targetWidth = window.Width;
                        }

                        double targetHeight = 940;
                        if (window.RestoreBounds.Height > 0 && !double.IsNaN(window.RestoreBounds.Height)) {
                            targetHeight = window.RestoreBounds.Height;
                        } else if (window.Height > 0 && !double.IsNaN(window.Height)) {
                            targetHeight = window.Height;
                        }

                        Point screenPos = window.PointToScreen(currentPos);
                        PresentationSource source = PresentationSource.FromVisual(window);
                        Point dipPos = (source != null && source.CompositionTarget != null)
                            ? source.CompositionTarget.TransformFromDevice.Transform(screenPos)
                            : screenPos;

                        window.WindowState = WindowState.Normal;
                        UpdateChromeResizeBorder();
                        window.Width = targetWidth;
                        window.Height = targetHeight;
                        window.Left = dipPos.X - (targetWidth * percentX);
                        double effectiveClickY = Math.Max(12.0, mouseClickY);
                        window.Top = dipPos.Y - effectiveClickY;

                        titleBarDragStart = null;
                        try { window.DragMove(); } catch {}
                    }
                }
            };

            Action<object, System.Windows.Input.MouseButtonEventArgs> handleTitleBarMouseUp = (s, e) => {
                isTitleBarDragging = false;
                titleBarDragStart = null;
                UIElement elem = s as UIElement;
                if (elem != null) {
                    try { elem.ReleaseMouseCapture(); } catch {}
                }
            };

            if (headerDragArea != null) {
                headerDragArea.MouseDown += (s, e) => handleTitleBarMouseDown(s, e);
                headerDragArea.MouseMove += (s, e) => handleTitleBarMouseMove(s, e);
                headerDragArea.MouseUp += (s, e) => handleTitleBarMouseUp(s, e);
            }
            FrameworkElement sidebarDrag = sidebarTopDragArea ?? sidebarBrandHeader;
            if (sidebarDrag != null) {
                sidebarDrag.MouseDown += (s, e) => handleTitleBarMouseDown(s, e);
                sidebarDrag.MouseMove += (s, e) => handleTitleBarMouseMove(s, e);
                sidebarDrag.MouseUp += (s, e) => handleTitleBarMouseUp(s, e);
            }
            window.PreviewMouseLeftButtonUp += (s, e) => {
                isTitleBarDragging = false;
                titleBarDragStart = null;
                if (headerDragArea != null) { try { headerDragArea.ReleaseMouseCapture(); } catch {} }
                if (sidebarTopDragArea != null) { try { sidebarTopDragArea.ReleaseMouseCapture(); } catch {} }
                if (sidebarBrandHeader != null) { try { sidebarBrandHeader.ReleaseMouseCapture(); } catch {} }
            };

            if (btnWinMinimize != null) {
                btnWinMinimize.Click += (s, e) => { window.WindowState = WindowState.Minimized; };
            }

            if (btnWinMaximize != null) {
                btnWinMaximize.Click += (s, e) => ToggleMaximizeWindow();
            }

            window.SizeChanged += (s, e) => {
                UpdateResponsiveLayout(e.NewSize.Width);
            };

            window.StateChanged += (s, e) => {
                UpdateChromeResizeBorder();
                if (btnWinMaximize != null) {
                    btnWinMaximize.Content = (window.WindowState == WindowState.Maximized) ? "❐" : "▢";
                    btnWinMaximize.ToolTip = (window.WindowState == WindowState.Maximized) ? "Verkleinern" : "Maximieren";
                }
            };
            if (btnWinMaximize != null && window.WindowState == WindowState.Maximized) {
                btnWinMaximize.Content = "❐";
                btnWinMaximize.ToolTip = "Verkleinern";
            }

            if (btnWinClose != null) {
                btnWinClose.Click += (s, e) => { window.Close(); };
            }
            window.Closing += (s, e) => { HardwareStressTester.Stop(); };

            // HARDWARE COPY BUTTONS
            SetupCopyButton(btnCopyHwCpu, () => txtHwCpuName != null ? txtHwCpuName.Text : "", "Prozessor-Namen kopieren");
            SetupCopyButton(btnCopyHwGpu, () => txtHwGpuName != null ? txtHwGpuName.Text : "", "Grafikkarten-Namen kopieren");
            SetupCopyButton(btnCopyHwBoard, () => txtHwBoardModel != null ? txtHwBoardModel.Text : "", "Mainboard-Modell kopieren");
            SetupCopyButton(btnCopyHwRam, () => txtHwRamModules != null ? txtHwRamModules.Text : "", "Installierte Module kopieren");
            if (btnCopyHwRamModules != null) SetupCopyButton(btnCopyHwRamModules, () => txtHwRamModules != null ? txtHwRamModules.Text : "", "Installierte Module kopieren");
            SetupCopyButton(btnCopyHwNetAdapter, () => txtHwNetAdapter != null ? txtHwNetAdapter.Text : "", "Netzwerkadapter kopieren");
            if (btnCopyHwNetMac != null) {
                SetupCopyButton(btnCopyHwNetMac, () => txtHwNetMac != null ? txtHwNetMac.Text : "", "MAC-Adresse kopieren");
            }
            if (btnOpenDevMgmtLan != null) {
                btnOpenDevMgmtLan.Click += (s, e) => {
                    if (currentLanAdapter == null || string.IsNullOrEmpty(currentLanAdapter.PnpInstanceId) ||
                        !ProcessRunner.Start("rundll32.exe", "devmgr.dll,DeviceProperties_RunDLL /DeviceID \"" + currentLanAdapter.PnpInstanceId + "\"")) {
                        ProcessRunner.Start("devmgmt.msc");
                    }
                };
            }
            if (btnOpenSoundSettings != null) {
                btnOpenSoundSettings.Click += (s, e) => ProcessRunner.Start("mmsys.cpl");
            }
            if (btnOpenDiskMgmt != null) {
                btnOpenDiskMgmt.Click += (s, e) => ProcessRunner.Start("diskmgmt.msc");
            }
            if (btnOpenCleanMgr != null) {
                btnOpenCleanMgr.Click += (s, e) => ProcessRunner.Start("cleanmgr.exe");
            }
            if (btnCopyHwOsKey != null) {
                SetupCopyButton(btnCopyHwOsKey, () => !string.IsNullOrEmpty(cachedProductKey) ? cachedProductKey : "", "Product Key kopieren");
            }
            if (btnToggleHwOsKey != null) {
                btnToggleHwOsKey.Click += (s, e) => {
                    isProductKeyVisible = !isProductKeyVisible;
                    if (txtHwOsProductKey != null) {
                        txtHwOsProductKey.Text = isProductKeyVisible
                            ? (!string.IsNullOrEmpty(cachedProductKey) ? cachedProductKey : "Kein Key gefunden")
                            : "•••••-•••••-•••••-•••••-•••••";
                    }
                    btnToggleHwOsKey.Content = isProductKeyVisible ? "🙈" : "👁️";
                    btnToggleHwOsKey.ToolTip = isProductKeyVisible ? "Product Key maskieren" : "Product Key anzeigen";
                };
            }

            // GLOBAL PC RESTART (OPTIONS MODAL)
            if (btnGlobalRestart != null) {
                btnGlobalRestart.Click += (s, e) => {
                    if (overlayRestartModal != null) {
                        overlayRestartModal.Visibility = Visibility.Visible;
                    }
                };
            }

            // GLOBAL PC SHUTDOWN
            if (btnGlobalShutdown != null) {
                btnGlobalShutdown.Click += async (s, e) => {
                    bool ok = await ShowConfirmModalAsync(
                        "Herunterfahren",
                        "Möchtest du den Computer jetzt wirklich herunterfahren?\n\nBitte stelle sicher, dass alle geöffneten Dokumente und Arbeiten gespeichert sind!",
                        "Jetzt herunterfahren",
                        "Abbrechen",
                        "⏻",
                        "#EF4444"
                    );

                    if (ok) {
                        if (!ProcessRunner.Start("shutdown.exe", "/s /t 0")) {
                            ShowAlertModal("Fehler beim Herunterfahren", "Der Befehl zum Herunterfahren konnte nicht ausgeführt werden.", "OK", "❌", "#EF4444");
                        }
                    }
                };
            }

            if (btnRelaunchAdmin != null) {
                btnRelaunchAdmin.Click += (s, e) => {
                    string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                    if (ProcessRunner.Start(currentExe, asAdmin: true)) {
                        window.Close();
                    }
                };
            }

            // TAB SWITCHING & MODULAR LIFECYCLE
            InitAioCooler();
            InitializeTabSystem();

            // USB-TOPOLOGIE (CPU DIRECT LATENCY) ACTIONS
            if (btnUsbRun != null) {
                btnUsbRun.Click += (s, e) => {
                    bool isRunning = false;
                    lock (usbProcessLock) {
                        isRunning = (usbDirectProcess != null && !usbDirectProcess.HasExited);
                    }
                    if (isRunning) {
                        CancelUsbDirectAnalysis();
                    } else {
                        StartUsbDirectAnalysis();
                    }
                };
            }
            if (btnUsbCopy != null) {
                btnUsbCopy.Click += (s, e) => {
                    if (txtUsbConsoleOutput != null && txtUsbConsoleOutput.Document != null) {
                        try {
                            TextRange range = new TextRange(txtUsbConsoleOutput.Document.ContentStart, txtUsbConsoleOutput.Document.ContentEnd);
                            if (!string.IsNullOrWhiteSpace(range.Text)) {
                                Clipboard.SetText(range.Text);
                                btnUsbCopy.Content = "✓ Kopiert!";
                                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
                                timer.Tick += (ts, te) => {
                                    btnUsbCopy.Content = "📋 Kopieren";
                                    timer.Stop();
                                };
                                timer.Start();
                            }
                        } catch {}
                    }
                };
            }
            if (btnUsbClear != null) {
                btnUsbClear.Click += (s, e) => {
                    if (paraUsbConsoleOutput != null) {
                        paraUsbConsoleOutput.Inlines.Clear();
                    }
                    RenderUsbHardwareList(new List<UsbDetectedItem>());
                };
            }

            if (btnViewDetailsCpu != null) btnViewDetailsCpu.Click += (s, e) => ShowHardwareDetails("CPU");
            if (btnViewDetailsGpu != null) btnViewDetailsGpu.Click += (s, e) => ShowHardwareDetails("GPU");
            if (btnViewDetailsRam != null) btnViewDetailsRam.Click += (s, e) => ShowHardwareDetails("RAM");

            // TAB EVENT INITIALIZATION (1:1 DEDICATED PER TAB)
            SetupMouseEvents();
            SetupTimerResEvents();
            SetupBcdEvents();
            SetupChipsetEvents();
            SetupGamebarEvents();
            SetupAppsEvents();
            InitializeGpuLimitUI();
            if (btnRunWindowsUpdate != null) {
                btnRunWindowsUpdate.Click += (s, e) => {
                    try {
                        ProcessRunner.Start("ms-settings:windowsupdate-action");
                        ProcessRunner.Start("UsoClient.exe", "StartScan");
                    } catch {
                        ProcessRunner.Start("ms-settings:windowsupdate");
                    }
                };
            }
            if (btnRunStoreUpdate != null) {
                btnRunStoreUpdate.Click += (s, e) => {
                    if (!ProcessRunner.Start("ms-windows-store://downloadsandupdates")) {
                        ProcessRunner.Start("ms-windows-store:");
                    }
                };
            }
            SetupOfficeEvents();
        }

        // =========================================================================
        // MODULAR TAB LIFECYCLE & ACTIVITY SYSTEM
        // =========================================================================
        private void RegisterTab(TabDescriptor tab) {
            if (tab == null) return;
            registeredTabs.Add(tab);
            if (tab.Spinner != null) {
                var grid = tab.Spinner.Parent as Grid;
                if (grid != null) {
                    var dot = new System.Windows.Shapes.Ellipse {
                        Width = 7,
                        Height = 7,
                        Fill = UIHelper.GetBrush("#10B981"),
                        HorizontalAlignment = HorizontalAlignment.Right,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0, 0, 7, 0),
                        Visibility = Visibility.Collapsed,
                        ToolTip = "Vorgang abgeschlossen (Klicken zum Anzeigen)",
                        IsHitTestVisible = false
                    };
                    grid.Children.Add(dot);
                    tab.DoneDot = dot;
                }
            }
            if (tab.Button != null) {
                tab.Button.Checked += (s, e) => SwitchToTab(tab);
                tab.Button.Click += (s, e) => SwitchToTab(tab);
            }
        }

        private TabDescriptor FindTab(string id) {
            for (int i = 0; i < registeredTabs.Count; i++) {
                if (registeredTabs[i].Id == id) return registeredTabs[i];
            }
            return null;
        }

        private void InitializeTabSystem() {
            registeredTabs.Clear();

            // 1. Performance
            RegisterTab(new TabDescriptor {
                Id = "Home",
                Button = tabBtnHome,
                Content = tabContentHome,
                Spinner = spinnerTabHome,
                Icon = "🏠",
                Title = "Performance",
                OnActivate = () => {
                    isHomeActive = true;
                    isInitialTelemetryLoaded = false;
                    SetTelemetryLoadingState();
                    if (telemetryTimer != null) {
                        telemetryTimer.Interval = TimeSpan.FromMilliseconds(40);
                        if (!telemetryTimer.IsEnabled) telemetryTimer.Start();
                    }
                    Task.Run(() => HardwareTelemetry.EnsureRunning());
                    lastTelemetryTimestamp = DateTime.UtcNow;
                    if (smoothGraphTimer != null && !smoothGraphTimer.IsEnabled) smoothGraphTimer.Start();
                    TelemetryTimer_Tick(null, null);
                },
                OnDeactivate = () => {
                    isHomeActive = false;
                    isInitialTelemetryLoaded = false;
                    SetTelemetryLoadingState();
                    if (telemetryTimer != null) {
                        if (telemetryTimer.IsEnabled) telemetryTimer.Stop();
                        telemetryTimer.Interval = TimeSpan.FromMilliseconds(500);
                    }
                    if (!isTimerResTabActive && smoothGraphTimer != null && smoothGraphTimer.IsEnabled) smoothGraphTimer.Stop();
                    if (!HardwareStressTester.IsRunning) {
                        Task.Run(() => HardwareTelemetry.Shutdown());
                    }
                    ClearAllMonitorGraphs();
                },
                CheckActivity = () => HardwareStressTester.IsRunning
                    ? new TabActivityState(true, "#38BDF8", "Hardware-Stresstest läuft...")
                    : default(TabActivityState)
            });

            // 2. Hardware-Info
            RegisterTab(new TabDescriptor {
                Id = "HardwareInfo",
                Button = tabBtnHardwareInfo,
                Content = tabContentHardwareInfo,
                Icon = "🖥️",
                Title = "Hardware-Info",
                OnActivate = () => {
                    RefreshHardwareInfoUI();
                },
                CheckActivity = null // Hardware-Info is static specs, no background task
            });

            // 2.1 Datenträger & Laufwerke
            RegisterTab(new TabDescriptor {
                Id = "Drives",
                Button = tabBtnDrives,
                Content = tabContentDrives,
                Icon = "💾",
                Title = "Laufwerke",
                OnActivate = () => {
                    RefreshAllDrivesUI();
                },
                CheckActivity = null
            });

            // 3. Bufferbloat & Speed
            RegisterTab(new TabDescriptor {
                Id = "Bufferbloat",
                Button = tabBtnBufferbloat,
                Content = tabContentBufferbloat,
                Spinner = spinnerTabBufferbloat,
                Icon = "📶",
                Title = "Bufferbloat & Speed",
                CheckActivity = () => _isBbRunning
                    ? new TabActivityState(true, "#38BDF8", "Bufferbloat- / Speedtest läuft...")
                    : default(TabActivityState)
            });

            // 4. Allgemeine Tweaks
            RegisterTab(new TabDescriptor {
                Id = "Mouse",
                Button = tabBtnMouse,
                Content = tabContentMouse,
                Icon = "⚙️",
                Title = "Allgemeine Tweaks",
                OnActivate = () => {
                    RefreshMouseUI();
                    RefreshPriorityUI();
                    RefreshCoreIsoUI();
                    RefreshPowerUI();
                    RefreshLanEnergyUI();
                    RefreshPagingExecutiveUI();
                    RefreshSystemResponsivenessUI();
                    RefreshNetworkThrottlingUI();
                    RefreshMmcssGamesUI();
                    RefreshGameDvrUI();
                    RefreshUsbSelectiveSuspendUI();
                    RefreshRealtekRssUI();
                }
            });

            // 5. Timer Resolution
            RegisterTab(new TabDescriptor {
                Id = "TimerRes",
                Button = tabBtnTimerRes,
                Content = tabContentTimerRes,
                Icon = "⏱️",
                Title = "Timer Resolution",
                OnActivate = () => {
                    isTimerResTabActive = true;
                    if (smoothGraphTimer != null && !smoothGraphTimer.IsEnabled) smoothGraphTimer.Start();
                    RefreshKernelUI();
                    RefreshTimerResUI();
                    ClearTimerResGraph();
                    isMeasureSleepPaused = false;
                    if (btnToggleMeasureSleep != null) btnToggleMeasureSleep.Content = "⏸️ Pause";
                    UIHelper.SetPill(pillMeasureSleepState, txtMeasureSleepState, true, "🟢 LIVE (1s Intervall)");
                    StartMeasureSleep();
                },
                OnDeactivate = () => {
                    isTimerResTabActive = false;
                    if (!isHomeActive && smoothGraphTimer != null && smoothGraphTimer.IsEnabled) smoothGraphTimer.Stop();
                    StopMeasureSleep();
                    ClearTimerResGraph();
                },
                CheckActivity = null // Static 0.5ms registry settings do not trigger a spinner
            });

            // 6. HPET & Timers
            RegisterTab(new TabDescriptor {
                Id = "Bcd",
                Button = tabBtnBcd,
                Content = tabContentBcd,
                Icon = "⏰",
                Title = "HPET & Timers",
                OnActivate = () => {
                    RefreshKernelUI();
                    RefreshBcdUI();
                }
            });

            // 7. Microsoft Office
            RegisterTab(new TabDescriptor {
                Id = "Office",
                Button = tabBtnOffice,
                Content = tabContentOffice,
                Spinner = spinnerTabOffice,
                Icon = "📄",
                Title = "Microsoft Office",
                OnActivate = () => {
                    if (IsOfficeOperationRunning) return;
                    RefreshOfficeStatusUI();
                    UpdateOfficeActionButtonState();
                },
                CheckActivity = () => {
                    bool active = IsOfficeOperationRunning;
                    return active ? new TabActivityState(true, "#38BDF8", "Office Download / Installation läuft...") : default(TabActivityState);
                }
            });

            // 8. Chipsatz & Mainboard
            RegisterTab(new TabDescriptor {
                Id = "Chipset",
                Button = tabBtnChipset,
                Content = tabContentChipset,
                Spinner = spinnerTabChipset,
                Icon = "⚡",
                Title = "Chipsatz & Mainboard",
                OnActivate = () => RefreshChipsetUI(),
                CheckActivity = () => {
                    bool active = (_chipsetDownloadCts != null && !_chipsetDownloadCts.IsCancellationRequested);
                    return active ? new TabActivityState(true, "#38BDF8", "Chipset-Treiber Download läuft...") : default(TabActivityState);
                }
            });

            // 10. Spielprofile & KGL
            RegisterTab(new TabDescriptor {
                Id = "Gamebar",
                Button = tabBtnGamebar,
                Content = tabContentGamebar,
                Icon = "🎮",
                Title = "Spielprofile & KGL",
                OnActivate = () => RefreshGamebarUI()
            });

            // 11. Software & Tools Downloader
            RegisterTab(new TabDescriptor {
                Id = "Apps",
                Button = tabBtnApps,
                Content = tabContentApps,
                Spinner = spinnerTabApps,
                Icon = "📦",
                Title = "Software & Tools Downloader",
                OnActivate = () => RefreshAppsStatusUI(),
                CheckActivity = () => {
                    bool active = (_downloadQueueCts != null && !_downloadQueueCts.IsCancellationRequested);
                    return active ? new TabActivityState(true, "#38BDF8", "Software Download / Installation läuft...") : default(TabActivityState);
                }
            });

            // 12. Windows Update & Store Wartung
            RegisterTab(new TabDescriptor {
                Id = "Updates",
                Button = tabBtnUpdates,
                Content = tabContentUpdates,
                Icon = "🔄",
                Title = "Windows Update & Store Wartung"
            });

            // 13. BIOS & UEFI Einstellungen
            RegisterTab(new TabDescriptor {
                Id = "Bios",
                Button = tabBtnBios,
                Content = tabContentBios,
                Icon = "💻",
                Title = "BIOS & UEFI Einstellungen",
                OnActivate = () => RefreshBiosUI()
            });

            // 14. USB-Topologie
            RegisterTab(new TabDescriptor {
                Id = "UsbDirect",
                Button = tabBtnUsbDirect,
                Content = tabContentUsbDirect,
                Spinner = spinnerTabUsbDirect,
                Icon = "🔌",
                Title = "USB-Topologie & CPU-Direct Analyzer",
                CheckActivity = () => {
                    bool active = false;
                    try {
                        lock (usbProcessLock) {
                            active = (usbDirectProcess != null && !usbDirectProcess.HasExited);
                        }
                    } catch {}
                    return active ? new TabActivityState(true, "#38BDF8", "USB-Topologie Analyse läuft...") : default(TabActivityState);
                }
            });

            // 15. AiO
            RegisterTab(new TabDescriptor {
                Id = "AioCooler",
                Button = tabBtnAioCooler,
                Content = tabContentAioCooler,
                Spinner = spinnerTabAioCooler,
                Icon = "💧",
                Title = "AiO",
                OnActivate = () => RefreshAioUI(false)
            });

            currentActiveTab = FindTab("Home");
        }

        private void SwitchToTab(TabDescriptor targetTab) {
            if (targetTab == null) return;
            targetTab.HasCompletedNotice = false;
            if (targetTab.DoneDot != null) {
                targetTab.DoneDot.Visibility = Visibility.Collapsed;
            }
            if (isNavigatingTab) return;
            if (currentActiveTab == targetTab && targetTab.Content != null && targetTab.Content.Visibility == Visibility.Visible) return;
            if (window != null && !window.Dispatcher.CheckAccess()) {
                window.Dispatcher.BeginInvoke(new Action(() => SwitchToTab(targetTab)));
                return;
            }

            isNavigatingTab = true;
            try {
                if (currentActiveTab != null && currentActiveTab != targetTab && currentActiveTab.OnDeactivate != null) {
                    try { currentActiveTab.OnDeactivate(); } catch {}
                }

                for (int i = 0; i < registeredTabs.Count; i++) {
                    var t = registeredTabs[i];
                    if (t.Content != null && t != targetTab) {
                        t.Content.Visibility = Visibility.Collapsed;
                    }
                }

                if (targetTab.Button != null && targetTab.Button.IsChecked != true) {
                    targetTab.Button.IsChecked = true;
                }
                if (targetTab.Content != null) {
                    targetTab.Content.Visibility = Visibility.Visible;
                }
                SetBreadcrumb(targetTab.Icon, targetTab.Title);
                currentActiveTab = targetTab;

                if (targetTab.OnActivate != null) {
                    try { targetTab.OnActivate(); } catch {}
                }
                UpdateSidebarActivitySpinners();
            } finally {
                isNavigatingTab = false;
            }
        }

        public void UpdateSidebarActivitySpinners() {
            try {
                if (window == null) return;
                if (!window.Dispatcher.CheckAccess()) {
                    window.Dispatcher.BeginInvoke(new Action(UpdateSidebarActivitySpinners));
                    return;
                }
                for (int i = 0; i < registeredTabs.Count; i++) {
                    var tab = registeredTabs[i];
                    if (tab.Spinner == null) continue;
                    TabActivityState state = default(TabActivityState);
                    if (tab.CheckActivity != null) {
                        try {
                            state = tab.CheckActivity();
                        } catch {}
                    }

                    bool isBusyNow = state.IsActive;

                    // Transition detection: If it was busy and now just finished:
                    if (tab.WasBusy && !isBusyNow) {
                        // The background operation finished!
                        // If user is currently on a different tab, light up the green notification dot!
                        if (currentActiveTab == null || currentActiveTab.Id != tab.Id) {
                            tab.HasCompletedNotice = true;
                        }
                    }

                    // If a new process started, or if user is on this tab, clear notice
                    if (isBusyNow || (currentActiveTab != null && currentActiveTab.Id == tab.Id)) {
                        tab.HasCompletedNotice = false;
                    }

                    tab.WasBusy = isBusyNow;

                    SetSpinnerState(tab.Spinner, isBusyNow, state.ColorHex, state.Tooltip);

                    if (tab.DoneDot != null) {
                        tab.DoneDot.Visibility = (tab.HasCompletedNotice && !isBusyNow)
                            ? Visibility.Visible
                            : Visibility.Collapsed;
                    }
                }
            } catch {}
        }

        private void SetSpinnerState(Viewbox spinner, bool isRunning, string colorHex, string tooltip) {
            if (spinner == null) return;
            if (!spinner.Dispatcher.CheckAccess()) {
                spinner.Dispatcher.BeginInvoke(new Action(() => SetSpinnerState(spinner, isRunning, colorHex, tooltip)));
                return;
            }
            var targetVis = isRunning ? Visibility.Visible : Visibility.Collapsed;
            if (spinner.Visibility != targetVis) {
                spinner.Visibility = targetVis;
            }
            if (isRunning) {
                if (tooltip != null) {
                    spinner.ToolTip = tooltip;
                }
                if (!string.IsNullOrEmpty(colorHex)) {
                    try {
                        var canvas = spinner.Child as Canvas;
                        if (canvas != null && canvas.Children.Count > 0) {
                            var path = canvas.Children[0] as System.Windows.Shapes.Path;
                            if (path != null) {
                                path.Stroke = UIHelper.GetBrush(colorHex);
                            }
                        }
                    } catch {}
                }
            }
        }

        private void StartUsbDirectAnalysis() {
            lock (usbProcessLock) {
                if (usbDirectProcess != null && !usbDirectProcess.HasExited) {
                    try { usbDirectProcess.Kill(); } catch {}
                    try { usbDirectProcess.Dispose(); } catch {}
                    usbDirectProcess = null;
                }
            }

            if (pillUsbStatus != null) {
                pillUsbStatus.Background = UIHelper.GetBrush("#181C26");
                pillUsbStatus.BorderBrush = UIHelper.BrushBlueText;
            }
            if (txtPillUsbStatus != null) {
                txtPillUsbStatus.Text = "ANALYSE LÄUFT...";
                txtPillUsbStatus.Foreground = UIHelper.BrushBlueText;
            }
            if (btnUsbRun != null) {
                var dangerStyle = Application.Current.TryFindResource("DangerBtn") as Style;
                if (dangerStyle != null) btnUsbRun.Style = dangerStyle;
            }
            if (txtUsbRunIcon != null) txtUsbRunIcon.Text = "⏹ ";
            if (txtUsbRunLabel != null) txtUsbRunLabel.Text = "Abbrechen";

            if (paraUsbConsoleOutput != null) {
                paraUsbConsoleOutput.Inlines.Clear();
                paraUsbConsoleOutput.Inlines.Add(new Run("▶ Starte native USB-Topologie & CPU-Direct Analyse...\n") {
                    Foreground = UIHelper.BrushBlueText,
                    FontWeight = FontWeights.Bold
                });
                paraUsbConsoleOutput.Inlines.Add(new Run("Ermittle USB-Host-Controller, Bus-Topologie und Latenz-Ebenen...\n\n") {
                    Foreground = UIHelper.BrushMuted
                });
            }

            Task.Run(() => {
                try {
                    string script = "$cpuDids = @('15b6','15b7','15b8','1587','1588','1589','158b','158d','158e','161a','161b','161c','161d','161e','161f','15d6','15d7','162e','149c','145c','145f','1639','13f7','13f8','8a13','9a13','9a17','461e','464e','a71e','7ec0','a831','5782','5785','5787','57a5','1138','1135','0b27','15e9','15ec','15f0','15b5','15b6','15c1','15d4','15db'); " +
                        "$ctrlMap = @{}; Get-CimInstance Win32_USBControllerDevice -ErrorAction SilentlyContinue | ForEach-Object { $cDid = [regex]::Match($_.Antecedent.DeviceID, 'DEV_([0-9A-Fa-f]{4})').Groups[1].Value.ToLower(); $devId = $_.Dependent.DeviceID; if ($cDid -and $devId) { $ctrlMap[$devId] = $cDid } }; " +
                        "Get-PnpDevice -Status OK -ErrorAction SilentlyContinue | Where-Object { $_.InstanceId -match '^USB\\\\VID_' -and $_.InstanceId -notmatch '&MI_' } | ForEach-Object { $id = $_.InstanceId; $parent = ''; try { $parent = (Get-PnpDeviceProperty -InstanceId $id -KeyName 'DEVPKEY_Device_Parent' -ErrorAction SilentlyContinue).Data } catch {}; $name = $_.FriendlyName; try { $bus = (Get-PnpDeviceProperty -InstanceId $id -KeyName 'DEVPKEY_Device_BusReportedDeviceDesc' -ErrorAction SilentlyContinue).Data; if ($bus) { $name = $bus } } catch {}; if (-not $name -or $name -match 'Hub' -or $_.FriendlyName -match 'Hub') { return }; $cDid = $ctrlMap[$id]; $isCpu = $cpuDids.Contains($cDid); $isHub = ($parent -and -not ($parent -match 'ROOT_HUB')); $tier = 1; if ($isHub) { $tier = 2 } elseif ($isCpu) { $tier = 0 }; $devVid = [regex]::Match($id, 'VID_([0-9A-F]{4})').Groups[1].Value; $devPid = [regex]::Match($id, 'PID_([0-9A-F]{4})').Groups[1].Value; $hubName = if ($isHub) { 'USB-Hub' } else { '' }; Write-Output ($name + '|' + $tier + '|' + $devVid + '|' + $devPid + '|' + $hubName) }";

                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = "powershell.exe",
                        Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"" + script + "\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = Encoding.UTF8
                    };

                    Process proc = new Process { StartInfo = psi };
                    var detectedItems = new List<UsbDetectedItem>();
                    var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

                    proc.OutputDataReceived += (s, e) => {
                        if (!string.IsNullOrEmpty(e.Data)) {
                            string[] parts = e.Data.Split('|');
                            if (parts.Length >= 4) {
                                string devName = parts[0].Trim();
                                int tier = 1;
                                int.TryParse(parts[1].Trim(), out tier);
                                string devVid = parts[2].Trim();
                                string devPid = parts[3].Trim();
                                string devHub = parts.Length > 4 ? parts[4].Trim() : "";

                                if (!string.IsNullOrEmpty(devName) && !seenNames.Contains(devName)) {
                                    seenNames.Add(devName);
                                    var item = new UsbDetectedItem {
                                        Name = devName,
                                        ChipCount = tier,
                                        Vid = devVid,
                                        Pid = devPid,
                                        Note = devHub
                                    };
                                    lock (detectedItems) {
                                        detectedItems.Add(item);
                                    }

                                    window.Dispatcher.InvokeAsync(() => {
                                        if (paraUsbConsoleOutput != null) {
                                            string tierTag = tier == 0 ? "[CHIP 0 - CPU DIRECT] " : (tier == 1 ? "[CHIP 1 - CHIPSET] " : "[CHIP 2+ - HUB] ");
                                            Brush tierBrush = tier == 0 ? UIHelper.BrushGreenText : (tier == 1 ? UIHelper.BrushAmberText : UIHelper.BrushRedText);
                                            paraUsbConsoleOutput.Inlines.Add(new Run("  " + tierTag) {
                                                Foreground = tierBrush,
                                                FontWeight = FontWeights.Bold
                                            });
                                            paraUsbConsoleOutput.Inlines.Add(new Run(devName) {
                                                Foreground = UIHelper.BrushWhite,
                                                FontWeight = FontWeights.SemiBold
                                            });
                                            string meta = " (VID:" + devVid + " PID:" + devPid + (!string.IsNullOrEmpty(devHub) ? " · " + devHub : "") + ")\n";
                                            paraUsbConsoleOutput.Inlines.Add(new Run(meta) {
                                                Foreground = UIHelper.BrushMuted
                                            });
                                            if (txtUsbConsoleOutput != null) txtUsbConsoleOutput.ScrollToEnd();
                                        }
                                    });
                                }
                            }
                        }
                    };

                    lock (usbProcessLock) {
                        usbDirectProcess = proc;
                    }

                    proc.Start();
                    UpdateSidebarActivitySpinners();
                    proc.BeginOutputReadLine();
                    proc.WaitForExit();

                    window.Dispatcher.InvokeAsync(() => {
                        List<UsbDetectedItem> listToRender;
                        lock (detectedItems) {
                            listToRender = new List<UsbDetectedItem>(detectedItems);
                        }
                        RenderUsbHardwareList(listToRender);

                        ResetUsbRunButton();
                        UIHelper.SetPill(pillUsbStatus, txtPillUsbStatus, true, "ABGESCHLOSSEN");

                        if (paraUsbConsoleOutput != null) {
                            paraUsbConsoleOutput.Inlines.Add(new Run("\n✓ [USB-Topologie Analyse abgeschlossen: " + listToRender.Count + " Peripheriegeräte klassifiziert]\n") {
                                Foreground = UIHelper.BrushGreenText,
                                FontWeight = FontWeights.Bold
                            });
                            if (txtUsbConsoleOutput != null) txtUsbConsoleOutput.ScrollToEnd();
                        }
                    });
                } catch (Exception ex) {
                    window.Dispatcher.InvokeAsync(() => {
                        ResetUsbRunButton();
                        UIHelper.SetPillDanger(pillUsbStatus, txtPillUsbStatus, "FEHLER");

                        if (paraUsbConsoleOutput != null) {
                            paraUsbConsoleOutput.Inlines.Add(new Run("\n❌ Fehler bei der Ausführung: " + ex.Message + "\n") {
                                Foreground = UIHelper.BrushRedText,
                                FontWeight = FontWeights.Bold
                            });
                            if (txtUsbConsoleOutput != null) txtUsbConsoleOutput.ScrollToEnd();
                        }
                    });
                }
            });
        }

        private void ResetUsbRunButton() {
            if (btnUsbRun != null) {
                var primaryStyle = Application.Current.TryFindResource("PrimaryBtn") as Style;
                if (primaryStyle != null) btnUsbRun.Style = primaryStyle;
            }
            if (txtUsbRunIcon != null) txtUsbRunIcon.Text = "🔄 ";
            if (txtUsbRunLabel != null) txtUsbRunLabel.Text = "Neu starten";
            UpdateSidebarActivitySpinners();
        }

        private void CancelUsbDirectAnalysis() {
            lock (usbProcessLock) {
                if (usbDirectProcess != null && !usbDirectProcess.HasExited) {
                    try {
                        usbDirectProcess.Kill();
                    } catch {}
                }
            }

            UIHelper.SetPillDanger(pillUsbStatus, txtPillUsbStatus, "ABGEBROCHEN");
            ResetUsbRunButton();
            if (paraUsbConsoleOutput != null) {
                paraUsbConsoleOutput.Inlines.Add(new Run("\n⏹ [Analyse durch Benutzer abgebrochen]\n") {
                    Foreground = UIHelper.BrushRedText,
                    FontWeight = FontWeights.Bold
                });
                if (txtUsbConsoleOutput != null) txtUsbConsoleOutput.ScrollToEnd();
            }
        }

        private void RenderUsbHardwareList(List<UsbDetectedItem> items) {
            if (panelUsbHardwareEmpty == null || panelUsbHardwareContent == null) return;

            if (items == null || items.Count == 0) {
                panelUsbHardwareEmpty.Visibility = Visibility.Visible;
                panelUsbHardwareContent.Visibility = Visibility.Collapsed;
                if (txtUsbDeviceCount != null) txtUsbDeviceCount.Text = "0 Geräte";
                return;
            }

            panelUsbHardwareEmpty.Visibility = Visibility.Collapsed;
            panelUsbHardwareContent.Visibility = Visibility.Visible;
            if (txtUsbDeviceCount != null) txtUsbDeviceCount.Text = items.Count + " Geräte";

            PopulateUsbTierStack(stackUsbDevicesChip0, items.FindAll(it => it.ChipCount == 0), 0);
            PopulateUsbTierStack(stackUsbDevicesChip1, items.FindAll(it => it.ChipCount == 1), 1);
            PopulateUsbTierStack(stackUsbDevicesChip2, items.FindAll(it => it.ChipCount >= 2), 2);
        }

        private void PopulateUsbTierStack(StackPanel stack, List<UsbDetectedItem> items, int tier) {
            if (stack == null) return;
            stack.Children.Clear();

            if (items == null || items.Count == 0) {
                var txt = new TextBlock {
                    Text = "Keine Geräte an diesem Pfad angeschlossen",
                    Foreground = UIHelper.BrushMuted,
                    FontSize = 11,
                    FontStyle = FontStyles.Italic,
                    Margin = new Thickness(0, 2, 0, 2)
                };
                stack.Children.Add(txt);
                return;
            }

            foreach (var item in items) {
                Border card = new Border {
                    Background = UIHelper.GetBrush("#11151E"),
                    BorderBrush = tier == 0 ? UIHelper.BrushGreenBorder : (tier == 1 ? UIHelper.BrushAmberBorder : UIHelper.BrushRedBorder),
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(10, 7, 10, 7),
                    Margin = new Thickness(0, 0, 0, 6)
                };

                Grid g = new Grid();
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                string icon = GetUsbDeviceIcon(item.Name);

                TextBlock txtIcon = new TextBlock {
                    Text = icon,
                    FontSize = 14,
                    Foreground = UIHelper.BrushWhite,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left
                };
                Grid.SetColumn(txtIcon, 0);
                g.Children.Add(txtIcon);

                StackPanel spInfo = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                TextBlock txtName = new TextBlock {
                    Text = item.Name,
                    FontWeight = FontWeights.Bold,
                    FontSize = 11.5,
                    Foreground = UIHelper.BrushWhite
                };
                spInfo.Children.Add(txtName);

                string sub = "";
                if (tier == 0) sub = "Direkte CPU-Lanes · Geringste USB-Latenz";
                else if (tier == 1) sub = "Über Chipsatz (PCH)";
                else sub = string.IsNullOrEmpty(item.Note) ? "Über USB-Hub" : "Über Hub (" + item.Note + ")";

                if (!string.IsNullOrEmpty(item.Vid) && !string.IsNullOrEmpty(item.Pid)) {
                    sub += " · VID:" + item.Vid + " PID:" + item.Pid;
                }

                TextBlock txtSub = new TextBlock {
                    Text = sub,
                    FontSize = 10,
                    Foreground = UIHelper.BrushGrayText,
                    Margin = new Thickness(0, 1, 0, 0)
                };
                spInfo.Children.Add(txtSub);
                Grid.SetColumn(spInfo, 1);
                g.Children.Add(spInfo);

                Border pill = new Border {
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0, 0, 0)
                };
                TextBlock txtPill = new TextBlock { FontSize = 9, FontWeight = FontWeights.Bold };
                if (tier == 0) UIHelper.SetPill(pill, txtPill, true, "OPTIMAL");
                else if (tier == 1) UIHelper.SetPillWarning(pill, txtPill, "GUT");
                else UIHelper.SetPillDanger(pill, txtPill, "LATENZ");
                pill.Child = txtPill;
                Grid.SetColumn(pill, 2);
                g.Children.Add(pill);

                card.Child = g;
                stack.Children.Add(card);
            }
        }

        private static string GetUsbDeviceIcon(string name) {
            string n = (name ?? "").ToLower();
            if (ContainsAny(n, "mouse", "maus", "wlmouse", "viper", "deathadder", "g pro", "superlight")) return "🖱️";
            if (ContainsAny(n, "keyboard", "tastatur", "wooting", "apex", "keychron", "huntsman")) return "⌨️";
            if (ContainsAny(n, "audio", "headset", "headphone", "wave", "mic", "sound", "jack")) return "🎧";
            if (ContainsAny(n, "controller", "gamepad", "xbox", "dualshock", "dualsense")) return "🎮";
            if (n.Contains("deck")) return "🎛️";
            if (ContainsAny(n, "led", "rgb", "aura")) return "💡";
            return "🔌";
        }

        private class SmoothBar {
            public ColumnDefinition Fill;
            public ColumnDefinition Empty;
            public double Current;
            public double Target;
        }
        private readonly List<SmoothBar> _smoothBars = new List<SmoothBar>();
        private readonly Dictionary<ColumnDefinition, SmoothBar> _smoothBarMap = new Dictionary<ColumnDefinition, SmoothBar>();

        private void SetBarFill(ColumnDefinition fill, ColumnDefinition empty, double percent, bool instant = false) {
            if (fill == null || empty == null) return;
            try {
                double target = Math.Min(100.0, Math.Max(0.0, percent));
                SmoothBar bar;
                if (!_smoothBarMap.TryGetValue(fill, out bar)) {
                    bar = new SmoothBar { Fill = fill, Empty = empty, Current = target, Target = target };
                    _smoothBarMap[fill] = bar;
                    _smoothBars.Add(bar);
                    fill.Width = new GridLength(target, GridUnitType.Star);
                    empty.Width = new GridLength(100.0 - target, GridUnitType.Star);
                    return;
                }

                bar.Empty = empty;
                bar.Target = target;
                if (instant) {
                    bar.Current = target;
                    fill.Width = new GridLength(target, GridUnitType.Star);
                    empty.Width = new GridLength(100.0 - target, GridUnitType.Star);
                }
            } catch {}
        }

        private void UpdateSmoothBars() {
            for (int i = 0; i < _smoothBars.Count; i++) {
                var bar = _smoothBars[i];
                if (bar.Fill == null || bar.Empty == null) continue;
                double diff = bar.Target - bar.Current;
                if (Math.Abs(diff) > 0.04) {
                    bar.Current += diff * 0.12;
                    bar.Fill.Width = new GridLength(bar.Current, GridUnitType.Star);
                    bar.Empty.Width = new GridLength(100.0 - bar.Current, GridUnitType.Star);
                } else if (bar.Current != bar.Target) {
                    bar.Current = bar.Target;
                    bar.Fill.Width = new GridLength(bar.Current, GridUnitType.Star);
                    bar.Empty.Width = new GridLength(100.0 - bar.Current, GridUnitType.Star);
                }
            }
        }

        private void UpdateAllMonitorGraphs() {
            if (!isHomeActive) return;
            try {
                UpdateDualSmoothGraph(canvasTempGraph, pathTempCpu, pathTempGpu, cpuTempHistory, gpuTempHistory, 0, 100.0);
                UpdateDualSmoothGraph(canvasLoadGraph, pathLoadCpu, pathLoadGpu, cpuLoadHistory, gpuLoadHistory, 0, 100.0);

                // Auto-scale watt axis based on maximum recent power draw
                double maxP = 150.0;
                for (int i = 0; i < cpuPowerHistory.Count; i++) {
                    if (cpuPowerHistory[i] > maxP) maxP = cpuPowerHistory[i];
                }
                for (int i = 0; i < gpuPowerHistory.Count; i++) {
                    if (gpuPowerHistory[i] > maxP) maxP = gpuPowerHistory[i];
                }
                double roundedMaxP = Math.Ceiling(maxP / 50.0) * 50.0;
                lastMaxWattScale = roundedMaxP;
                if (txtGraphPowerYMax != null) txtGraphPowerYMax.Text = string.Format(CultureInfo.InvariantCulture, "{0:0}W", roundedMaxP);
                if (txtGraphPowerY75 != null) txtGraphPowerY75.Text = string.Format(CultureInfo.InvariantCulture, "{0:0}W", roundedMaxP * 0.75);
                if (txtGraphPowerY50 != null) txtGraphPowerY50.Text = string.Format(CultureInfo.InvariantCulture, "{0:0}W", roundedMaxP * 0.50);
                if (txtGraphPowerY25 != null) txtGraphPowerY25.Text = string.Format(CultureInfo.InvariantCulture, "{0:0}W", roundedMaxP * 0.25);

                UpdateDualSmoothGraph(canvasPowerGraph, pathPowerCpu, pathPowerGpu, cpuPowerHistory, gpuPowerHistory, 0, roundedMaxP);

                // Live hover refresh
                if (isHoverTemp) UpdateTempHover(lastPosTemp);
                if (isHoverLoad) UpdateLoadHover(lastPosLoad);
                if (isHoverPower) UpdatePowerHover(lastPosPower);
            } catch {}
        }

        private static Geometry CreateSmoothSplineGeometry(List<double> hist, int maxSamples, double stepX, double shiftX, double progress, double minY, double rangeY, double h, double w) {
            if (hist == null || hist.Count <= 1) return Geometry.Empty;

            int total = hist.Count;
            int count = Math.Min(total, maxSamples);
            int startIndex = total - count;
            double valPrev = hist[total - 2];
            double valTarget = hist[total - 1];

            // Smoothly interpolate live edge towards the newest sample (eliminates discrete edge hops)
            double pClamped = Math.Min(1.0, Math.Max(0.0, progress));
            double pSmooth = pClamped * pClamped * (3.0 - 2.0 * pClamped);
            double valLive = valPrev + (valTarget - valPrev) * pSmooth;

            List<Point> pts = new List<Point>(count + 2);
            for (int i = 0; i < count - 1; i++) {
                double val = hist[startIndex + i];
                double norm = Math.Min(1.0, Math.Max(0.0, (val - minY) / rangeY));
                double y = h - 4.0 - (norm * (h - 8.0));
                double x = (maxSamples - count + 1 + i) * stepX - shiftX;
                pts.Add(new Point(x, y));
            }

            // Always anchor the final point to the right canvas edge (w) with valLive
            double normLive = Math.Min(1.0, Math.Max(0.0, (valLive - minY) / rangeY));
            double yLive = h - 4.0 - (normLive * (h - 8.0));
            pts.Add(new Point(w, yLive));

            // Only when history has completely filled maxSamples, anchor leftmost point to x = 0
            if (count >= maxSamples && pts.Count > 0 && pts[0].X > 0) {
                pts.Insert(0, new Point(0, pts[0].Y));
            }

            if (pts.Count <= 1) return Geometry.Empty;

            StreamGeometry geom = new StreamGeometry();
            using (StreamGeometryContext ctx = geom.Open()) {
                ctx.BeginFigure(pts[0], false, false);
                if (pts.Count == 2) {
                    ctx.LineTo(pts[1], true, false);
                } else {
                    for (int i = 0; i < pts.Count - 1; i++) {
                        Point p0 = pts[Math.Max(0, i - 1)];
                        Point p1 = pts[i];
                        Point p2 = pts[i + 1];
                        Point p3 = pts[Math.Min(pts.Count - 1, i + 2)];

                        // If two consecutive points coincide, skip to prevent degenerate Bezier
                        if (Math.Abs(p2.X - p1.X) < 0.001 && Math.Abs(p2.Y - p1.Y) < 0.001) {
                            continue;
                        }

                        // Catmull-Rom cubic Bezier tangents (smooth sine-like curves)
                        double c1X = p1.X + (p2.X - p0.X) / 6.0;
                        double c1Y = p1.Y + (p2.Y - p0.Y) / 6.0;
                        double c2X = p2.X - (p3.X - p1.X) / 6.0;
                        double c2Y = p2.Y - (p3.Y - p1.Y) / 6.0;

                        // Clamping to guarantee monotonicity along X and bounds along Y
                        c1X = Math.Max(p1.X, Math.Min(p2.X, c1X));
                        c2X = Math.Max(p1.X, Math.Min(p2.X, c2X));
                        c1Y = Math.Max(4.0, Math.Min(h - 4.0, c1Y));
                        c2Y = Math.Max(4.0, Math.Min(h - 4.0, c2Y));

                        ctx.BezierTo(new Point(c1X, c1Y), new Point(c2X, c2Y), p2, true, false);
                    }
                }
            }
            geom.Freeze();
            return geom;
        }

        private void UpdateDualSmoothGraph(Canvas canvas, System.Windows.Shapes.Path pathCpu, System.Windows.Shapes.Path pathGpu, List<double> cpuHist, List<double> gpuHist, double minY, double maxY) {
            if (canvas == null) return;
            if (!isInitialTelemetryLoaded) {
                if (pathCpu != null) pathCpu.Data = null;
                if (pathGpu != null) pathGpu.Data = null;
                return;
            }
            double w = canvas.ActualWidth > 40 ? canvas.ActualWidth : 350.0;
            double h = canvas.ActualHeight > 40 ? canvas.ActualHeight : 145.0;
            double rangeY = Math.Max(1.0, maxY - minY);
            int maxSamples = 120;
            double stepX = w / (maxSamples - 1);

            double progress = 0.0;
            double shiftX = 0.0;
            if (lastTelemetryTimestamp != DateTime.MinValue) {
                double elapsedMs = (DateTime.UtcNow - lastTelemetryTimestamp).TotalMilliseconds;
                double interval = Math.Max(250.0, _measuredTelemetryIntervalMs);
                progress = Math.Min(1.0, Math.Max(0.0, elapsedMs / interval));
                shiftX = progress * stepX;
            }

            // Filter visibility
            bool showCpu = (activeMonitorFilter != 2);
            bool showGpu = (activeMonitorFilter != 1 && hasGpu);

            if (pathCpu != null) {
                pathCpu.Visibility = showCpu ? Visibility.Visible : Visibility.Collapsed;
                pathCpu.Data = showCpu ? CreateSmoothSplineGeometry(cpuHist, maxSamples, stepX, shiftX, progress, minY, rangeY, h, w) : null;
            }
            if (pathGpu != null) {
                pathGpu.Visibility = showGpu ? Visibility.Visible : Visibility.Collapsed;
                pathGpu.Data = showGpu ? CreateSmoothSplineGeometry(gpuHist, maxSamples, stepX, shiftX, progress, minY, rangeY, h, w) : null;
            }
        }

        private void CycleMonitorFilter() {
            activeMonitorFilter = (activeMonitorFilter + 1) % 3;
            ApplyMonitorFilterUI();
        }

        private void ApplyMonitorFilterUI() {
            try {
                if (btnFilterMonitorCycle != null) {
                    btnFilterMonitorCycle.Content = (activeMonitorFilter == 0) ? "💻 CPU + GPU" : (activeMonitorFilter == 1 ? "⚡ Nur CPU" : "🎮 Nur GPU");
                }

                UpdateAllMonitorGraphs();
                if (isHoverTemp) UpdateTempHover(lastPosTemp);
                if (isHoverLoad) UpdateLoadHover(lastPosLoad);
                if (isHoverPower) UpdatePowerHover(lastPosPower);
            } catch {}
        }

        private void UpdateTempHover(Point p) {
            UpdateGraphHover(canvasTempGraph, p, lineHoverTemp, dotHoverTempCpu, dotHoverTempGpu, badgeHoverTemp, txtHoverTempTime, txtHoverTempCpu, txtHoverTempGpu, panelHoverTempCpu, panelHoverTempGpu, cpuTempHistory, gpuTempHistory, 0, 100.0, "°C", "{0:F0}");
        }

        private void UpdateLoadHover(Point p) {
            UpdateGraphHover(canvasLoadGraph, p, lineHoverLoad, dotHoverLoadCpu, dotHoverLoadGpu, badgeHoverLoad, txtHoverLoadTime, txtHoverLoadCpu, txtHoverLoadGpu, panelHoverLoadCpu, panelHoverLoadGpu, cpuLoadHistory, gpuLoadHistory, 0, 100.0, "%", "{0:F0}");
        }

        private void UpdatePowerHover(Point p) {
            UpdateGraphHover(canvasPowerGraph, p, lineHoverPower, dotHoverPowerCpu, dotHoverPowerGpu, badgeHoverPower, txtHoverPowerTime, txtHoverPowerCpu, txtHoverPowerGpu, panelHoverPowerCpu, panelHoverPowerGpu, cpuPowerHistory, gpuPowerHistory, 0, lastMaxWattScale, "W", "{0:F0}", txtHoverPowerTotal, panelHoverPowerTotal, sepHoverPowerTotal);
        }

        private static double UpdateSingleChannelHover(
            bool show, 
            List<double> hist, 
            int k, 
            double progress, 
            double minY, 
            double rangeY, 
            double h, 
            double exactX, 
            string label, 
            string format, 
            string unit, 
            TextBlock txt, 
            System.Windows.Shapes.Ellipse dot, 
            StackPanel panel) 
        {
            double targetY = h / 2.0;
            if (show && hist != null && hist.Count > 0) {
                int total = hist.Count;
                int idx = total - 1 - k;
                if (idx >= 0 && idx < total) {
                    double val;
                    if (k == 0 && total >= 2) {
                        double pClamped = Math.Min(1.0, Math.Max(0.0, progress));
                        double pSmooth = pClamped * pClamped * (3.0 - 2.0 * pClamped);
                        val = hist[total - 2] + (hist[total - 1] - hist[total - 2]) * pSmooth;
                    } else {
                        val = hist[idx];
                    }

                    double norm = Math.Min(1.0, Math.Max(0.0, (val - minY) / rangeY));
                    targetY = h - 4.0 - (norm * (h - 8.0));
                    if (txt != null) txt.Text = string.Format(label + ": " + format + " " + unit, val);
                    if (dot != null) {
                        Canvas.SetLeft(dot, exactX - 4);
                        Canvas.SetTop(dot, targetY - 4);
                        dot.Visibility = Visibility.Visible;
                    }
                    if (panel != null) panel.Visibility = Visibility.Visible;
                    return targetY;
                }
            }
            if (dot != null) dot.Visibility = Visibility.Collapsed;
            if (panel != null) panel.Visibility = Visibility.Collapsed;
            return targetY;
        }

        private void UpdateGraphHover(
            Canvas canvas, 
            Point mousePos,
            System.Windows.Shapes.Line lineHover, 
            System.Windows.Shapes.Ellipse dotCpu, 
            System.Windows.Shapes.Ellipse dotGpu, 
            Border badgeHover, 
            TextBlock txtTime, 
            TextBlock txtCpu, 
            TextBlock txtGpu, 
            StackPanel panelCpu, 
            StackPanel panelGpu,
            List<double> cpuHist, 
            List<double> gpuHist, 
            double minY, 
            double maxY, 
            string unit, 
            string format,
            TextBlock txtTotal = null,
            StackPanel panelTotal = null,
            Border sepTotal = null) 
        {
            if (canvas == null) return;
            if (!isInitialTelemetryLoaded) {
                HideGraphHover(lineHover, dotCpu, dotGpu, badgeHover);
                return;
            }
            int count = Math.Max(cpuHist != null ? cpuHist.Count : 0, gpuHist != null ? gpuHist.Count : 0);
            if (count <= 1) {
                HideGraphHover(lineHover, dotCpu, dotGpu, badgeHover);
                return;
            }

            double w = canvas.ActualWidth > 40 ? canvas.ActualWidth : 350.0;
            double h = canvas.ActualHeight > 40 ? canvas.ActualHeight : 145.0;
            double rangeY = Math.Max(1.0, maxY - minY);
            int maxSamples = 120;
            double stepX = w / (maxSamples - 1);
            int maxVisible = Math.Min(count, maxSamples);
            double shiftX = 0.0;
            double progress = 0.0;
            if (lastTelemetryTimestamp != DateTime.MinValue) {
                double elapsedMs = (DateTime.UtcNow - lastTelemetryTimestamp).TotalMilliseconds;
                double interval = Math.Max(250.0, _measuredTelemetryIntervalMs);
                progress = Math.Min(1.0, Math.Max(0.0, elapsedMs / interval));
                shiftX = progress * stepX;
            }

            double minCurveX = (count >= maxSamples) ? 0.0 : ((maxSamples - maxVisible + 1) * stepX - shiftX);
            if (mousePos.X < minCurveX - 8.0) {
                HideGraphHover(lineHover, dotCpu, dotGpu, badgeHover);
                return;
            }

            int sampleIdx;
            if (mousePos.X >= w - (shiftX * 0.5)) {
                sampleIdx = maxVisible - 1;
            } else {
                sampleIdx = (int)Math.Round((mousePos.X + shiftX) / stepX - (maxSamples - maxVisible + 1));
                if (sampleIdx < 0) sampleIdx = 0;
                if (sampleIdx >= maxVisible - 1) sampleIdx = maxVisible - 2;
            }

            double exactX = (sampleIdx == maxVisible - 1) ? w : ((maxSamples - maxVisible + 1 + sampleIdx) * stepX - shiftX);
            if (count >= maxSamples && sampleIdx == 0 && exactX < 0) exactX = 0;
            if (exactX > w) exactX = w;

            int k = maxVisible - 1 - sampleIdx;
            int secAgo = (int)Math.Round(k * 0.5);
            string timeStr = secAgo <= 0 ? "jetzt" : string.Format("vor {0}s", secAgo);
            if (txtTime != null) txtTime.Text = timeStr;

            bool showCpu = (activeMonitorFilter == 0 || activeMonitorFilter == 1);
            bool showGpu = (activeMonitorFilter == 0 || activeMonitorFilter == 2) && hasGpu;

            double cpuY = UpdateSingleChannelHover(showCpu, cpuHist, k, progress, minY, rangeY, h, exactX, "CPU", format, unit, txtCpu, dotCpu, panelCpu);
            double gpuY = UpdateSingleChannelHover(showGpu, gpuHist, k, progress, minY, rangeY, h, exactX, "GPU", format, unit, txtGpu, dotGpu, panelGpu);

            bool cpuVis = panelCpu != null && panelCpu.Visibility == Visibility.Visible;
            bool gpuVis = panelGpu != null && panelGpu.Visibility == Visibility.Visible;

            bool showTotal = panelTotal != null && (cpuVis && gpuVis);
            if (panelTotal != null) {
                if (showTotal) {
                    double cpuVal = 0;
                    if (cpuHist != null && cpuHist.Count > 0) {
                        int total = cpuHist.Count;
                        int idx = total - 1 - k;
                        if (idx >= 0 && idx < total) {
                            if (k == 0 && total >= 2) {
                                double pClamped = Math.Min(1.0, Math.Max(0.0, progress));
                                double pSmooth = pClamped * pClamped * (3.0 - 2.0 * pClamped);
                                cpuVal = cpuHist[total - 2] + (cpuHist[total - 1] - cpuHist[total - 2]) * pSmooth;
                            } else {
                                cpuVal = cpuHist[idx];
                            }
                        }
                    }
                    double gpuVal = 0;
                    if (gpuHist != null && gpuHist.Count > 0) {
                        int total = gpuHist.Count;
                        int idx = total - 1 - k;
                        if (idx >= 0 && idx < total) {
                            if (k == 0 && total >= 2) {
                                double pClamped = Math.Min(1.0, Math.Max(0.0, progress));
                                double pSmooth = pClamped * pClamped * (3.0 - 2.0 * pClamped);
                                gpuVal = gpuHist[total - 2] + (gpuHist[total - 1] - gpuHist[total - 2]) * pSmooth;
                            } else {
                                gpuVal = gpuHist[idx];
                            }
                        }
                    }
                    double totalVal = Math.Round(cpuVal + gpuVal, 0);
                    if (txtTotal != null) txtTotal.Text = string.Format("Summe: " + format + " " + unit, totalVal);
                    panelTotal.Visibility = Visibility.Visible;
                    if (sepTotal != null) sepTotal.Visibility = Visibility.Visible;
                } else {
                    panelTotal.Visibility = Visibility.Collapsed;
                    if (sepTotal != null) sepTotal.Visibility = Visibility.Collapsed;
                }
            }

            if (!cpuVis && !gpuVis) {
                HideGraphHover(lineHover, dotCpu, dotGpu, badgeHover);
                return;
            }

            if (lineHover != null) {
                lineHover.X1 = exactX;
                lineHover.X2 = exactX;
                lineHover.Y1 = 0;
                lineHover.Y2 = h;
                lineHover.Visibility = Visibility.Visible;
            }

            if (badgeHover != null) {
                double badgeW = showTotal ? 104.0 : 95.0;
                double badgeH = showTotal ? 64.0 : ((cpuVis && gpuVis) ? 46.0 : 32.0);
                double badgeX = exactX + 8;
                if (badgeX + badgeW > w) badgeX = exactX - badgeW - 8;
                if (badgeX < 2) badgeX = 2;

                double refY = cpuVis ? (gpuVis ? Math.Min(cpuY, gpuY) : cpuY) : gpuY;
                double badgeY = refY - badgeH - 6;
                if (badgeY < 2) badgeY = refY + 10;
                if (badgeY + badgeH > h) badgeY = h - badgeH - 2;

                Canvas.SetLeft(badgeHover, badgeX);
                Canvas.SetTop(badgeHover, badgeY);
                badgeHover.Visibility = Visibility.Visible;
            }
        }

        private void HideGraphHover(System.Windows.Shapes.Line line, System.Windows.Shapes.Ellipse dot1, System.Windows.Shapes.Ellipse dot2, Border badge) {
            if (line != null) line.Visibility = Visibility.Collapsed;
            if (dot1 != null) dot1.Visibility = Visibility.Collapsed;
            if (dot2 != null) dot2.Visibility = Visibility.Collapsed;
            if (badge != null) badge.Visibility = Visibility.Collapsed;
        }

        private void SetTelemetryLoadingState() {
            if (txtCpuTempRow != null) { txtCpuTempRow.Text = "lädt..."; txtCpuTempRow.ToolTip = null; }
            if (txtCpuLoadRow != null) txtCpuLoadRow.Text = "lädt...";
            if (txtCpuPowerRow != null) { txtCpuPowerRow.Text = "lädt..."; txtCpuPowerRow.ToolTip = null; }
            SetBarFill(colCpuTempBarFill, colCpuTempBarEmpty, 0, true);
            SetBarFill(colCpuLoadBarFill, colCpuLoadBarEmpty, 0, true);
            SetBarFill(colCpuPowerBarFill, colCpuPowerBarEmpty, 0, true);

            if (txtGpuTempRow != null) txtGpuTempRow.Text = "lädt...";
            if (txtGpuLoadRow != null) txtGpuLoadRow.Text = "lädt...";
            if (txtGpuPowerRow != null) txtGpuPowerRow.Text = "lädt...";
            SetBarFill(colGpuTempBarFill, colGpuTempBarEmpty, 0, true);
            SetBarFill(colGpuLoadBarFill, colGpuLoadBarEmpty, 0, true);
            SetBarFill(colGpuPowerBarFill, colGpuPowerBarEmpty, 0, true);

            if (txtRamLoadRow != null) txtRamLoadRow.Text = "lädt...";
            if (txtRamUsedRow != null) txtRamUsedRow.Text = "lädt...";
            if (txtRamFreeRow != null) txtRamFreeRow.Text = "lädt...";
            SetBarFill(colRamLoadBarFill, colRamLoadBarEmpty, 0, true);
            SetBarFill(colRamMemBarFill, colRamMemBarEmpty, 0, true);
            SetBarFill(colRamFreeBarFill, colRamFreeBarEmpty, 0, true);

            if (txtTotalPowerRow != null) txtTotalPowerRow.Text = "lädt...";
            if (txtTotalPowerSubtitle != null) txtTotalPowerSubtitle.Text = "Kombinierter Verbrauch aus CPU und GPU";
            SetBarFill(colTotalPowerBarFill, colTotalPowerBarEmpty, 0, true);

            if (txtGpuLimitPowerVal != null) txtGpuLimitPowerVal.Text = "lädt...";
            if (txtGpuLimitTempVal != null) txtGpuLimitTempVal.Text = "lädt...";
            if (txtGpuLimitStatusSummary != null) {
                txtGpuLimitStatusSummary.Text = "LÄDT...";
                txtGpuLimitStatusSummary.Foreground = UIHelper.BrushMuted;
            }
            SetBarFill(colGpuLimitBarFill, colGpuLimitBarEmpty, 0, true);
            SetBarFill(colGpuTempLimitBarFill, colGpuTempLimitBarEmpty, 0, true);

            if (txtGpuPcieVal != null) {
                txtGpuPcieVal.Text = "LÄDT...";
                txtGpuPcieVal.Foreground = UIHelper.BrushGrayText;
            }
            if (txtGpuPcieSub != null) {
                txtGpuPcieSub.Text = "lädt...";
                txtGpuPcieSub.Foreground = UIHelper.BrushMuted;
            }

            ClearAllMonitorGraphs();
        }

        private void ClearAllMonitorGraphs() {
            cpuTempHistory.Clear();
            gpuTempHistory.Clear();
            cpuLoadHistory.Clear();
            gpuLoadHistory.Clear();
            cpuPowerHistory.Clear();
            gpuPowerHistory.Clear();

            if (pathTempCpu != null) pathTempCpu.Data = null;
            if (pathTempGpu != null) pathTempGpu.Data = null;
            if (pathLoadCpu != null) pathLoadCpu.Data = null;
            if (pathLoadGpu != null) pathLoadGpu.Data = null;
            if (pathPowerCpu != null) pathPowerCpu.Data = null;
            if (pathPowerGpu != null) pathPowerGpu.Data = null;

            HideGraphHover(lineHoverTemp, dotHoverTempCpu, dotHoverTempGpu, badgeHoverTemp);
            HideGraphHover(lineHoverLoad, dotHoverLoadCpu, dotHoverLoadGpu, badgeHoverLoad);
            HideGraphHover(lineHoverPower, dotHoverPowerCpu, dotHoverPowerGpu, badgeHoverPower);
        }

        private void UpdateJitterHover(Point p) {
            lastPosJitter = p;
            if (jitterHistory == null || jitterHistory.Count <= 1 || canvasJitterGraph == null) {
                HideJitterHover();
                return;
            }

            int total = jitterHistory.Count;
            double w = canvasJitterGraph.ActualWidth > 50 ? canvasJitterGraph.ActualWidth : 260.0;
            double h = canvasJitterGraph.ActualHeight > 20 ? canvasJitterGraph.ActualHeight : 145.0;
            int maxSamples = 40;
            double stepX = w / (maxSamples - 1);
            int maxVisible = Math.Min(total, maxSamples);

            double progress = 0.0;
            double shiftX = 0.0;
            if (lastJitterTimestamp != DateTime.MinValue) {
                double elapsedMs = (DateTime.UtcNow - lastJitterTimestamp).TotalMilliseconds;
                double interval = Math.Max(400.0, _measuredJitterIntervalMs);
                progress = Math.Min(1.0, Math.Max(0.0, elapsedMs / interval));
                shiftX = progress * stepX;
            }

            double minCurveX = (total >= maxSamples) ? 0.0 : ((maxSamples - maxVisible + 1) * stepX - shiftX);
            if (p.X < minCurveX - 8.0) {
                HideJitterHover();
                return;
            }

            int sampleIdx;
            if (p.X >= w - (shiftX * 0.5)) {
                sampleIdx = maxVisible - 1;
            } else {
                sampleIdx = (int)Math.Round((p.X + shiftX) / stepX - (maxSamples - maxVisible + 1));
                if (sampleIdx < 0) sampleIdx = 0;
                if (sampleIdx >= maxVisible - 1) sampleIdx = maxVisible - 2;
            }

            double exactX = (sampleIdx == maxVisible - 1) ? w : ((maxSamples - maxVisible + 1 + sampleIdx) * stepX - shiftX);
            if (total >= maxSamples && sampleIdx == 0 && exactX < 0) exactX = 0;
            if (exactX > w) exactX = w;

            int k = maxVisible - 1 - sampleIdx;
            int secAgo = k;
            string timeText = secAgo <= 0 ? "jetzt" : string.Format("vor {0}s", secAgo);
            if (txtJitterHoverTime != null) txtJitterHoverTime.Text = timeText;

            int idx = total - 1 - k;
            if (idx >= 0 && idx < total) {
                double val;
                if (k == 0 && total >= 2) {
                    double pClamped = Math.Min(1.0, Math.Max(0.0, progress));
                    double pSmooth = pClamped * pClamped * (3.0 - 2.0 * pClamped);
                    val = jitterHistory[total - 2] + (jitterHistory[total - 1] - jitterHistory[total - 2]) * pSmooth;
                } else {
                    val = jitterHistory[idx];
                }

                double scale = 1.0;
                double norm = Math.Min(1.0, Math.Max(0.0, (val - (-scale)) / (2.0 * scale)));
                double targetY = h - 4.0 - (norm * (h - 8.0));

                if (lineJitterHover != null) {
                    lineJitterHover.X1 = exactX;
                    lineJitterHover.X2 = exactX;
                    lineJitterHover.Y1 = 0;
                    lineJitterHover.Y2 = h;
                    lineJitterHover.Visibility = Visibility.Visible;
                }
                if (dotJitterHover != null) {
                    Canvas.SetLeft(dotJitterHover, exactX - 4);
                    Canvas.SetTop(dotJitterHover, targetY - 4);
                    if (pathJitter != null) dotJitterHover.Stroke = pathJitter.Stroke;
                    dotJitterHover.Visibility = Visibility.Visible;
                }
                if (badgeJitterHover != null && txtJitterHover != null) {
                    string sign = val > 0 ? "+" : "";
                    txtJitterHover.Text = string.Format(CultureInfo.InvariantCulture, "Delta: {0}{1:F4} ms", sign, val);

                    double badgeW = 95.0;
                    double badgeH = 34.0;
                    double badgeX = exactX + 8;
                    if (badgeX + badgeW > w) badgeX = exactX - badgeW - 8;
                    if (badgeX < 2) badgeX = 2;

                    double badgeY = targetY - badgeH - 6;
                    if (badgeY < 2) badgeY = targetY + 10;
                    if (badgeY + badgeH > h) badgeY = h - badgeH - 2;

                    Canvas.SetLeft(badgeJitterHover, badgeX);
                    Canvas.SetTop(badgeJitterHover, badgeY);
                    badgeJitterHover.Visibility = Visibility.Visible;
                }
                return;
            }
            HideJitterHover();
        }

        private void HideJitterHover() {
            isHoverJitter = false;
            if (lineJitterHover != null) lineJitterHover.Visibility = Visibility.Collapsed;
            if (dotJitterHover != null) dotJitterHover.Visibility = Visibility.Collapsed;
            if (badgeJitterHover != null) badgeJitterHover.Visibility = Visibility.Collapsed;
        }

        private void TelemetryTimer_Tick(object sender, EventArgs e) {
            if (isBenchmarkRunning) return;
            UpdateSidebarActivitySpinners();
            if (!isHomeActive) return;

            if (Interlocked.CompareExchange(ref _isTelemetryQueryRunning, 1, 0) != 0) return;

            Task.Run(() => {
                try {
                    if (!isHomeActive) return;

                    // 1. Read pure hardware telemetry in background (zero UI thread blocking!)
                    var snap = HardwareTelemetry.Read();

                    // Query NVML directly in background for real-time NVIDIA GPU telemetry (100% Anti-Cheat & Driver-safe)
                    double nvmlTemp = 0, nvmlPwr = 0, nvmlLoad = 0, nvmlClk = 0, nvmlMemClk = 0, nvmlVram = 0, nvmlFan = 0;
                    double nvmlPwrLimit = 0, nvmlPwrDefault = 0, nvmlPwrMin = 0, nvmlPwrMax = 0, nvmlThrottle = 83.0, nvmlTempLimit = 0;
                    string nvmlName = null;
                    bool nvmlHasGpu = false;

                    if (isNvmlInitialized && nvmlDeviceHandle != IntPtr.Zero) {
                        try {
                            uint tempVal;
                            if (nvmlDeviceGetTemperature(nvmlDeviceHandle, 0, out tempVal) == 0 && tempVal > 0 && tempVal < 130) {
                                nvmlTemp = tempVal;
                                nvmlHasGpu = true;
                            }
                            uint pwrMw;
                            if (nvmlDeviceGetPowerUsage(nvmlDeviceHandle, out pwrMw) == 0 && pwrMw > 0) {
                                nvmlPwr = Math.Round(pwrMw / 1000.0, 0);
                                nvmlHasGpu = true;
                            }
                            NvmlUtilization util;
                            if (nvmlDeviceGetUtilizationRates(nvmlDeviceHandle, out util) == 0) {
                                nvmlLoad = util.gpu;
                                nvmlHasGpu = true;
                            }
                            uint clk;
                            if (nvmlDeviceGetClockInfo(nvmlDeviceHandle, 0, out clk) == 0 && clk > 0) {
                                nvmlClk = clk;
                            }
                            uint mClk;
                            if (nvmlDeviceGetClockInfo(nvmlDeviceHandle, 2, out mClk) == 0 && mClk > 0) {
                                nvmlMemClk = mClk;
                            }
                            NvmlMemory mem;
                            if (nvmlDeviceGetMemoryInfo(nvmlDeviceHandle, out mem) == 0 && mem.total > 0) {
                                nvmlVram = Math.Round(mem.used / (1024.0 * 1024.0), 0);
                            }
                            uint fanSpd;
                            if (nvmlDeviceGetFanSpeed(nvmlDeviceHandle, out fanSpd) == 0) {
                                nvmlFan = fanSpd;
                            }
                            if (string.IsNullOrEmpty(latestGpuName)) {
                                byte[] nameBuf = new byte[64];
                                if (nvmlDeviceGetName(nvmlDeviceHandle, nameBuf, (uint)nameBuf.Length) == 0) {
                                    int nLen = 0;
                                    while (nLen < nameBuf.Length && nameBuf[nLen] != 0) nLen++;
                                    if (nLen > 0) nvmlName = Encoding.ASCII.GetString(nameBuf, 0, nLen).Trim();
                                }
                            }

                            uint enforcedLimitMw;
                            if (nvmlDeviceGetEnforcedPowerLimit(nvmlDeviceHandle, out enforcedLimitMw) == 0 && enforcedLimitMw > 0) {
                                nvmlPwrLimit = Math.Round(enforcedLimitMw / 1000.0, 0);
                            }
                            uint defaultLimitMw;
                            if (nvmlDeviceGetPowerManagementDefaultLimit(nvmlDeviceHandle, out defaultLimitMw) == 0 && defaultLimitMw > 0) {
                                nvmlPwrDefault = Math.Round(defaultLimitMw / 1000.0, 0);
                            }
                            uint minLimitMw, maxLimitMw;
                            if (nvmlDeviceGetPowerManagementLimitConstraints(nvmlDeviceHandle, out minLimitMw, out maxLimitMw) == 0 && maxLimitMw > 0) {
                                if (minLimitMw > 0) nvmlPwrMin = Math.Round(minLimitMw / 1000.0, 0);
                                nvmlPwrMax = Math.Round(maxLimitMw / 1000.0, 0);
                            }
                            uint slowdownTemp;
                            if (nvmlDeviceGetTemperatureThreshold(nvmlDeviceHandle, 1, out slowdownTemp) == 0 && slowdownTemp > 50 && slowdownTemp < 125) {
                                nvmlThrottle = slowdownTemp;
                            }
                            uint targetTemp;
                            if (nvmlDeviceGetTemperatureThreshold(nvmlDeviceHandle, 5, out targetTemp) == 0 && targetTemp > 40 && targetTemp < 125) {
                                nvmlTempLimit = targetTemp;
                            } else if (nvmlDeviceGetTemperatureThreshold(nvmlDeviceHandle, 3, out targetTemp) == 0 && targetTemp > 40 && targetTemp < 125) {
                                nvmlTempLimit = targetTemp;
                            }
                        } catch {}
                    }

                    window.Dispatcher.InvokeAsync(() => {
                        if (!isHomeActive) return;
                        try {
                            bool hasCpuTemp = snap.CpuTemp.HasValue && snap.CpuTemp.Value > 0;
                            bool hasCpuLoad = snap.CpuLoad.HasValue;
                            bool hasCpuPower = snap.CpuPower.HasValue && snap.CpuPower.Value > 0;

                            if (hasCpuTemp) latestCpuTemp = snap.CpuTemp.Value;
                            if (hasCpuPower) latestCpuPower = snap.CpuPower.Value;
                            if (hasCpuLoad) latestCpuLoad = snap.CpuLoad.Value;
                            bool hasCpu = hasCpuTemp || hasCpuLoad || hasCpuPower;

                            hasGpu = nvmlHasGpu;
                            if (nvmlHasGpu) {
                                if (nvmlTemp > 0) latestGpuTemp = nvmlTemp;
                                if (nvmlPwr > 0) latestGpuPower = nvmlPwr;
                                latestGpuLoad = nvmlLoad;
                                if (nvmlClk > 0) latestGpuClock = nvmlClk;
                                if (nvmlMemClk > 0) latestGpuMemoryClock = nvmlMemClk;
                                if (nvmlVram > 0) latestGpuVramUsedMb = nvmlVram;
                                latestGpuFanPercent = nvmlFan;
                                if (!string.IsNullOrEmpty(nvmlName)) latestGpuName = nvmlName;
                                if (nvmlPwrLimit > 0) latestGpuPowerLimit = nvmlPwrLimit;
                                if (nvmlPwrDefault > 0) latestGpuPowerDefault = nvmlPwrDefault;
                                if (nvmlPwrMin > 0) latestGpuPowerMin = nvmlPwrMin;
                                if (nvmlPwrMax > 0) latestGpuPowerMax = nvmlPwrMax;
                                latestGpuThrottleTemp = nvmlThrottle;
                                if (nvmlTempLimit > 0) latestGpuTempLimit = nvmlTempLimit;
                            }

                            // 4. Update RAM Card (3 grouped tiles: Auslastung, Belegt, Freier Speicher - 100% Win32 Native)
                            bool hasRamLoad = snap.RamLoad.HasValue;
                            bool hasRamUsed = snap.RamUsedGb.HasValue;
                            bool hasRamFree = snap.RamFreeGb.HasValue;

                            // Synchronisation Gate: Bleibe im Status "lädt...", bis alle Hardware-Sensoren bereit sind
                            bool isCpuReady = hasCpuTemp && latestCpuTemp > 0;
                            bool isGpuExpected = isNvmlInitialized && nvmlDeviceHandle != IntPtr.Zero;
                            bool isGpuReady = !isGpuExpected || (hasGpu && latestGpuTemp > 0);
                            bool isRamReady = hasRamLoad;

                            if (!isInitialTelemetryLoaded) {
                                if (isCpuReady && isGpuReady && isRamReady) {
                                    isInitialTelemetryLoaded = true;
                                    if (telemetryTimer != null) telemetryTimer.Interval = TimeSpan.FromMilliseconds(500);
                                } else {
                                    SetTelemetryLoadingState();
                                    return;
                                }
                            }

                // Append live metrics to graph histories in lockstep (alle Graphen wachsen absolut synchron)
                if (lastTelemetryTimestamp != DateTime.MinValue) {
                    double dt = (DateTime.UtcNow - lastTelemetryTimestamp).TotalMilliseconds;
                    if (dt >= 200 && dt <= 2500) {
                        _measuredTelemetryIntervalMs = (_measuredTelemetryIntervalMs * 0.7) + (dt * 0.3);
                    }
                }
                lastTelemetryTimestamp = DateTime.UtcNow;
                cpuTempHistory.Add(latestCpuTemp);
                if (cpuTempHistory.Count > 120) cpuTempHistory.RemoveAt(0);
                cpuPowerHistory.Add(latestCpuPower);
                if (cpuPowerHistory.Count > 120) cpuPowerHistory.RemoveAt(0);
                cpuLoadHistory.Add(latestCpuLoad);
                if (cpuLoadHistory.Count > 120) cpuLoadHistory.RemoveAt(0);

                if (hasGpu) {
                    gpuTempHistory.Add(latestGpuTemp);
                    if (gpuTempHistory.Count > 120) gpuTempHistory.RemoveAt(0);
                    gpuPowerHistory.Add(latestGpuPower);
                    if (gpuPowerHistory.Count > 120) gpuPowerHistory.RemoveAt(0);
                    gpuLoadHistory.Add(latestGpuLoad);
                    if (gpuLoadHistory.Count > 120) gpuLoadHistory.RemoveAt(0);
                }

                // 2. Update CPU Card (3 grouped tiles: Temperatur, Auslastung, Energieverbrauch)
                if (txtCpuTempRow != null) {
                    if (hasCpuTemp || latestCpuTemp > 0) {
                        txtCpuTempRow.Text = string.Format("{0:F1} °C", latestCpuTemp);
                        txtCpuTempRow.ToolTip = null;
                        SetBarFill(colCpuTempBarFill, colCpuTempBarEmpty, (latestCpuTemp / 95.0) * 100.0);
                    } else {
                        txtCpuTempRow.Text = "lädt...";
                        SetBarFill(colCpuTempBarFill, colCpuTempBarEmpty, 0);
                    }
                }

                if (txtCpuLoadRow != null) {
                    if (hasCpuLoad || latestCpuLoad > 0) {
                        txtCpuLoadRow.Text = string.Format("{0:F0} %", latestCpuLoad);
                        SetBarFill(colCpuLoadBarFill, colCpuLoadBarEmpty, latestCpuLoad);
                    } else {
                        txtCpuLoadRow.Text = "lädt...";
                        SetBarFill(colCpuLoadBarFill, colCpuLoadBarEmpty, 0);
                    }
                }

                double cpuLimit = latestCpuPower > 150 ? 250.0 : 142.0;
                if (txtCpuPowerRow != null) {
                    if (hasCpuPower || latestCpuPower > 0) {
                        txtCpuPowerRow.Text = string.Format("{0:F0} W", latestCpuPower);
                        txtCpuPowerRow.ToolTip = null;
                        SetBarFill(colCpuPowerBarFill, colCpuPowerBarEmpty, (latestCpuPower / cpuLimit) * 100.0);
                    } else {
                        txtCpuPowerRow.Text = "lädt...";
                        SetBarFill(colCpuPowerBarFill, colCpuPowerBarEmpty, 0);
                    }
                }

                // 3. Update GPU Card (3 grouped tiles: Temperatur, Auslastung, Energieverbrauch)
                if (txtGpuTempRow != null) txtGpuTempRow.Text = string.Format("{0:F0} °C", latestGpuTemp);
                SetBarFill(colGpuTempBarFill, colGpuTempBarEmpty, (latestGpuTemp / 90.0) * 100.0);

                if (txtGpuLoadRow != null) txtGpuLoadRow.Text = string.Format("{0:F0} %", latestGpuLoad);
                SetBarFill(colGpuLoadBarFill, colGpuLoadBarEmpty, latestGpuLoad);

                double defaultPl = latestGpuPowerDefault > 10 ? latestGpuPowerDefault : 450.0;
                double maxPl = latestGpuPowerMax > 10 ? latestGpuPowerMax : (defaultPl > 0 ? defaultPl * 1.15 : 520.0);
                double activeGpuLimit = latestGpuPowerLimit > 10 ? latestGpuPowerLimit : defaultPl;

                if (txtGpuPowerRow != null) txtGpuPowerRow.Text = string.Format("{0:F0} W", latestGpuPower);
                SetBarFill(colGpuPowerBarFill, colGpuPowerBarEmpty, (latestGpuPower / Math.Max(10.0, activeGpuLimit)) * 100.0);

                double memPercent = hasRamLoad ? snap.RamLoad.Value : 0;
                double usedGb = hasRamUsed ? snap.RamUsedGb.Value : 0;
                double totalGb = (hasRamUsed && snap.RamTotalGb.HasValue) ? snap.RamTotalGb.Value : (memPercent > 0 ? (usedGb / (memPercent / 100.0)) : 0);
                double freeGb = hasRamFree ? snap.RamFreeGb.Value : Math.Max(0.0, totalGb - usedGb);
                double freePercent = Math.Max(0.0, 100.0 - memPercent);

                if (txtRamLoadRow != null) txtRamLoadRow.Text = string.Format("{0:F0} %", memPercent);
                SetBarFill(colRamLoadBarFill, colRamLoadBarEmpty, memPercent);

                if (txtRamUsedRow != null) txtRamUsedRow.Text = string.Format("{0:F1} / {1:F0} GB", usedGb, totalGb);
                SetBarFill(colRamMemBarFill, colRamMemBarEmpty, (usedGb / Math.Max(1.0, totalGb)) * 100.0);

                if (txtRamFreeRow != null) txtRamFreeRow.Text = string.Format("{0:F1} GB frei", freeGb);
                SetBarFill(colRamFreeBarFill, colRamFreeBarEmpty, freePercent);

                // Update Kachel 4: EXPO / XMP RAM-Profil
                if (!isRamExpoDetected) {
                    DetectRamExpoProfileAsync();
                } else {
                    if (txtRamExpoTitle != null) txtRamExpoTitle.Text = cachedRamExpoTitle;
                    if (txtRamExpoSub != null) {
                        txtRamExpoSub.Text = cachedRamExpoSub;
                        txtRamExpoSub.Foreground = cachedRamExpoIsActive ? UIHelper.BrushMuted : UIHelper.BrushRedText;
                    }
                    if (pillRamExpoStatus != null && txtRamExpoVal != null) {
                        if (cachedRamExpoIsActive) {
                            UIHelper.SetPill(pillRamExpoStatus, txtRamExpoVal, true, cachedRamExpoVal);
                        } else {
                            UIHelper.SetPillDanger(pillRamExpoStatus, txtRamExpoVal, cachedRamExpoVal);
                        }
                    }
                }

                // Update Elongated Gesamt-Power Card (spans across CPU and GPU)
                if (latestCpuPower > 0 && hasGpu) {
                    double totalPower = Math.Round(latestCpuPower + latestGpuPower, 2);
                    if (txtTotalPowerRow != null) txtTotalPowerRow.Text = string.Format("{0:F0} W", totalPower);
                    if (txtTotalPowerSubtitle != null) {
                        txtTotalPowerSubtitle.Text = string.Format("CPU ({0:F0} W) + GPU ({1:F0} W)", latestCpuPower, latestGpuPower);
                    }
                    double maxTotal = cpuLimit + activeGpuLimit;
                    SetBarFill(colTotalPowerBarFill, colTotalPowerBarEmpty, (totalPower / Math.Max(1.0, maxTotal)) * 100.0);
                } else if (hasGpu) {
                    if (txtTotalPowerRow != null) txtTotalPowerRow.Text = string.Format("{0:F0} W", latestGpuPower);
                    if (txtTotalPowerSubtitle != null) {
                        txtTotalPowerSubtitle.Text = string.Format("GPU ({0:F0} W) • CPU-Sensor nicht aktiv", latestGpuPower);
                    }
                    SetBarFill(colTotalPowerBarFill, colTotalPowerBarEmpty, (latestGpuPower / Math.Max(1.0, activeGpuLimit)) * 100.0);
                } else {
                    if (txtTotalPowerRow != null) txtTotalPowerRow.Text = "lädt...";
                    if (txtTotalPowerSubtitle != null) {
                        txtTotalPowerSubtitle.Text = "Kombinierter Verbrauch aus CPU und GPU";
                    }
                    SetBarFill(colTotalPowerBarFill, colTotalPowerBarEmpty, 0);
                }

                // 5. Update GPU Clock Lock & Status Card (3 grouped tiles: GPU-Takt, Power & Temp, Modus)
                var gpuCfg = GpuLimitManager.LoadConfig();
                bool isGpuTaskActive = GpuLimitManager.IsAutostartTaskRegistered();
                bool isGpuLocked = gpuCfg.Enabled || isGpuTaskActive;
                int lockedMhz = gpuCfg.ClockMhz > 0 ? gpuCfg.ClockMhz : 750;
                double maxGpuClock = GpuLimitManager.QueryMaxClock();

                // Tile 1: GPU Core Clock
                if (txtGpuLimitPowerVal != null) {
                    txtGpuLimitPowerVal.Text = latestGpuClock > 0 
                        ? string.Format("{0:F0} MHz", latestGpuClock)
                        : (isGpuLocked ? string.Format("{0} MHz", lockedMhz) : "Dynamisch");
                }
                if (txtGpuLimitPowerSub != null) {
                    txtGpuLimitPowerSub.Text = isGpuLocked
                        ? string.Format("Arretiert auf {0} MHz", lockedMhz)
                        : string.Format("Max Boost: {0:F0} MHz", maxGpuClock);
                }

                // Tile 2: Live Power & Temp
                if (txtGpuLimitTempVal != null) {
                    txtGpuLimitTempVal.Text = string.Format("{0:F0} W • {1:F0} °C", latestGpuPower, latestGpuTemp);
                }
                if (txtGpuLimitTempSub != null) {
                    txtGpuLimitTempSub.Text = string.Format("Power-Limit: {0:F0} W (Default)", defaultPl > 0 ? defaultPl : 450.0);
                }

                // Tile 3: Status Badge
                Brush pillBg, pillBorder, summaryFg, clockFillBrush, pwrFillBrush;
                string summaryText;
                double clockFillPct, pwrFillPct;

                if (isGpuLocked) {
                    pillBg = UIHelper.GetBrush("#1E1B4B");
                    pillBorder = UIHelper.GetBrush("#6366F1");
                    summaryText = string.Format("LOCKED ({0} MHz)", lockedMhz);
                    summaryFg = UIHelper.GetBrush("#A5B4FC");
                    clockFillPct = Math.Min(100.0, (lockedMhz / Math.Max(500.0, maxGpuClock)) * 100.0);
                    clockFillBrush = UIHelper.GetBrush("#6366F1");
                } else {
                    pillBg = UIHelper.GetBrush("#0D281E");
                    pillBorder = UIHelper.BrushGreenBorder;
                    summaryText = "DYNAMISCH (WERK)";
                    summaryFg = UIHelper.BrushGreenText;
                    clockFillPct = maxGpuClock > 0 ? Math.Min(100.0, (latestGpuClock / maxGpuClock) * 100.0) : 50.0;
                    clockFillBrush = UIHelper.BrushGreenBorder;
                }

                double activeMaxPl = defaultPl > 0 ? defaultPl : 450.0;
                pwrFillPct = Math.Min(100.0, (latestGpuPower / Math.Max(1.0, activeMaxPl)) * 100.0);
                pwrFillBrush = latestGpuTemp > 80 ? UIHelper.GetBrush("#EA580C") : UIHelper.BrushAmberBorder;

                if (pillGpuLimitSummary != null) {
                    pillGpuLimitSummary.Background = pillBg;
                    pillGpuLimitSummary.BorderBrush = pillBorder;
                }
                if (txtGpuLimitStatusSummary != null) {
                    txtGpuLimitStatusSummary.Text = summaryText;
                    txtGpuLimitStatusSummary.Foreground = summaryFg;
                }
                SetBarFill(colGpuLimitBarFill, colGpuLimitBarEmpty, clockFillPct);
                if (barGpuLimitFill != null) barGpuLimitFill.Background = clockFillBrush;
                SetBarFill(colGpuTempLimitBarFill, colGpuTempLimitBarEmpty, pwrFillPct);
                if (barGpuTempLimitFill != null) barGpuTempLimitFill.Background = pwrFillBrush;

                // 4b. Update PCIe Lanes Tile (Kachel 4: Bottom-Left PCIe-Info, Right Badge)
                if (txtGpuPcieVal != null && txtGpuPcieSub != null) {
                    uint curGen = 0, curWidth = 0, maxGen = 0, maxWidth = 0;
                    if (GetRealGpuPcieLinkInfo(out curGen, out curWidth, out maxGen, out maxWidth)) {
                        uint displayGen = maxGen > 0 ? maxGen : curGen;
                        uint displayWidth = curWidth > 0 ? curWidth : maxWidth;
                        string pcieText = displayGen > 0 
                            ? string.Format("PCIe {0}.0 x{1}", displayGen, displayWidth)
                            : string.Format("PCIe x{0}", displayWidth);

                        if (maxWidth > 0) {
                            if (curWidth >= maxWidth) {
                                txtGpuPcieSub.Text = pcieText;
                                txtGpuPcieSub.Foreground = UIHelper.BrushMuted;

                                txtGpuPcieVal.Text = "Volle Anbindung";
                                txtGpuPcieVal.Foreground = UIHelper.BrushGreenText;
                                if (pillGpuPcieStatus != null) {
                                    pillGpuPcieStatus.Background = UIHelper.BrushGreenBg;
                                    pillGpuPcieStatus.BorderBrush = UIHelper.BrushGreenBorder;
                                }
                            } else if (curWidth > 0) {
                                txtGpuPcieSub.Text = string.Format("{0} (Max: x{1})", pcieText, maxWidth);
                                txtGpuPcieSub.Foreground = UIHelper.BrushRedText;

                                txtGpuPcieVal.Text = "Eingeschränkt";
                                txtGpuPcieVal.Foreground = UIHelper.BrushRedText;
                                if (pillGpuPcieStatus != null) {
                                    pillGpuPcieStatus.Background = UIHelper.BrushRedBg;
                                    pillGpuPcieStatus.BorderBrush = UIHelper.BrushRedBorder;
                                }
                            } else {
                                txtGpuPcieSub.Text = pcieText;
                                txtGpuPcieSub.Foreground = UIHelper.BrushMuted;

                                txtGpuPcieVal.Text = "Volle Anbindung";
                                txtGpuPcieVal.Foreground = UIHelper.BrushGreenText;
                                if (pillGpuPcieStatus != null) {
                                    pillGpuPcieStatus.Background = UIHelper.BrushGreenBg;
                                    pillGpuPcieStatus.BorderBrush = UIHelper.BrushGreenBorder;
                                }
                            }
                        } else if (curWidth > 0) {
                            txtGpuPcieSub.Text = pcieText;
                            txtGpuPcieSub.Foreground = UIHelper.BrushMuted;

                            txtGpuPcieVal.Text = string.Format("x{0} Aktiv", curWidth);
                            txtGpuPcieVal.Foreground = UIHelper.BrushGreenText;
                            if (pillGpuPcieStatus != null) {
                                pillGpuPcieStatus.Background = UIHelper.BrushGreenBg;
                                pillGpuPcieStatus.BorderBrush = UIHelper.BrushGreenBorder;
                            }
                        } else {
                            txtGpuPcieSub.Text = "Keine PCIe-Anbindung";
                            txtGpuPcieSub.Foreground = UIHelper.BrushGrayText;

                            txtGpuPcieVal.Text = "N/A";
                            txtGpuPcieVal.Foreground = UIHelper.BrushGrayText;
                            if (pillGpuPcieStatus != null) {
                                pillGpuPcieStatus.Background = UIHelper.GetBrush("#181C26");
                                pillGpuPcieStatus.BorderBrush = UIHelper.GetBrush("#262D3D");
                            }
                        }
                    } else {
                        txtGpuPcieSub.Text = "Keine PCIe-Anbindung";
                        txtGpuPcieSub.Foreground = UIHelper.BrushGrayText;

                        txtGpuPcieVal.Text = "N/A";
                        txtGpuPcieVal.Foreground = UIHelper.BrushGrayText;
                        if (pillGpuPcieStatus != null) {
                            pillGpuPcieStatus.Background = UIHelper.GetBrush("#181C26");
                            pillGpuPcieStatus.BorderBrush = UIHelper.GetBrush("#262D3D");
                        }
                    }
                }

                // 5. Update Live Graphs (All 3 Monitors simultaneously)
                UpdateAllMonitorGraphs();

                // 6. Refresh Hardware Details Modal if active
                if (overlayHardwareDetails != null && overlayHardwareDetails.Visibility == Visibility.Visible) {
                    RefreshHardwareDetailsUI(snap);
                }
            } catch {}
                    });
                } catch {
                } finally {
                    Interlocked.Exchange(ref _isTelemetryQueryRunning, 0);
                }
            });
        }

        private void ShowHardwareDetails(string mode) {
            activeHwDetailMode = mode;
            if (overlayHardwareDetails != null) {
                overlayHardwareDetails.Visibility = Visibility.Visible;
            }
            RefreshHardwareDetailsUI(HardwareTelemetry.Read());
        }

        private void HideHardwareDetails() {
            activeHwDetailMode = null;
            if (overlayHardwareDetails != null) {
                overlayHardwareDetails.Visibility = Visibility.Collapsed;
            }
        }

        // =========================================================================
        // MODERN IN-APP MODAL HELPER (DARK THEME OVERLAY DIALOG)
        // =========================================================================
        public enum ModalChoice {
            Cancel,
            Custom,
            Confirm
        }

        private void CloseModalWithChoice(ModalChoice choice) {
            if (overlayModal != null) overlayModal.Visibility = Visibility.Collapsed;
            if (modalTcs != null && !modalTcs.Task.IsCompleted) {
                modalTcs.TrySetResult(choice == ModalChoice.Confirm);
            }
            if (modalChoiceTcs != null && !modalChoiceTcs.Task.IsCompleted) {
                modalChoiceTcs.TrySetResult(choice);
            }
        }

        private void CloseModal(bool result) {
            CloseModalWithChoice(result ? ModalChoice.Confirm : ModalChoice.Cancel);
        }

        public Task<bool> ShowConfirmModalAsync(string title, string message, string confirmBtnText = "Bestätigen", string cancelBtnText = "Abbrechen", string icon = "⚠️", string iconColor = "#F59E0B", string category = "Znipe Optimization Tool") {
            modalTcs = new TaskCompletionSource<bool>();
            if (window != null && !window.Dispatcher.CheckAccess()) {
                return (Task<bool>)window.Dispatcher.Invoke(new Func<Task<bool>>(() => ShowConfirmModalAsync(title, message, confirmBtnText, cancelBtnText, icon, iconColor, category)));
            }

            if (overlayModal == null) {
                modalTcs.TrySetResult(string.IsNullOrEmpty(cancelBtnText));
                return modalTcs.Task;
            }

            if (txtModalTitle != null) txtModalTitle.Text = title;
            if (txtModalMessage != null) txtModalMessage.Text = message;
            if (txtModalCategory != null) txtModalCategory.Text = category;
            if (txtModalIcon != null) txtModalIcon.Text = icon;
            if (borderModalIcon != null) borderModalIcon.BorderBrush = UIHelper.GetBrush(iconColor);
            if (btnModalCustom != null) btnModalCustom.Visibility = Visibility.Collapsed;
            if (btnModalConfirm != null) {
                btnModalConfirm.Content = confirmBtnText;
                btnModalConfirm.Visibility = Visibility.Visible;
                if (!string.IsNullOrEmpty(iconColor) && window != null) {
                    if (iconColor.Equals("#EF4444", StringComparison.OrdinalIgnoreCase)) {
                        btnModalConfirm.Style = (Style)window.FindResource("DangerBtn");
                    } else if (iconColor.Equals("#10B981", StringComparison.OrdinalIgnoreCase) || iconColor.Equals("#059669", StringComparison.OrdinalIgnoreCase) || iconColor.Equals("#34D399", StringComparison.OrdinalIgnoreCase)) {
                        btnModalConfirm.Style = (Style)window.FindResource("SuccessBtn");
                    } else {
                        btnModalConfirm.Style = (Style)window.FindResource("PrimaryBtn");
                    }
                }
            }
            if (btnModalCancel != null) {
                btnModalCancel.Content = cancelBtnText;
                btnModalCancel.Visibility = string.IsNullOrEmpty(cancelBtnText) ? Visibility.Collapsed : Visibility.Visible;
            }

            overlayModal.Visibility = Visibility.Visible;
            return modalTcs.Task;
        }

        public Task<ModalChoice> ShowChoiceModalAsync(string title, string message, string confirmBtnText, string customBtnText, string cancelBtnText = "Abbrechen", string icon = "🚀", string iconColor = "#10B981", string category = "Znipe Optimization Tool", string confirmBtnStyle = "SuccessBtn", string customBtnStyle = "SecondaryBtn") {
            modalChoiceTcs = new TaskCompletionSource<ModalChoice>();
            if (window != null && !window.Dispatcher.CheckAccess()) {
                return (Task<ModalChoice>)window.Dispatcher.Invoke(new Func<Task<ModalChoice>>(() => ShowChoiceModalAsync(title, message, confirmBtnText, customBtnText, cancelBtnText, icon, iconColor, category, confirmBtnStyle, customBtnStyle)));
            }

            if (overlayModal == null) {
                modalChoiceTcs.TrySetResult(ModalChoice.Cancel);
                return modalChoiceTcs.Task;
            }

            if (txtModalTitle != null) txtModalTitle.Text = title;
            if (txtModalMessage != null) txtModalMessage.Text = message;
            if (txtModalCategory != null) txtModalCategory.Text = category;
            if (txtModalIcon != null) txtModalIcon.Text = icon;
            if (borderModalIcon != null) borderModalIcon.BorderBrush = UIHelper.GetBrush(iconColor);

            if (btnModalConfirm != null) {
                btnModalConfirm.Content = confirmBtnText;
                btnModalConfirm.Visibility = Visibility.Visible;
                if (window != null) btnModalConfirm.Style = (Style)window.FindResource(confirmBtnStyle);
            }
            if (btnModalCustom != null) {
                btnModalCustom.Content = customBtnText;
                btnModalCustom.Visibility = string.IsNullOrEmpty(customBtnText) ? Visibility.Collapsed : Visibility.Visible;
                if (window != null) btnModalCustom.Style = (Style)window.FindResource(customBtnStyle);
            }
            if (btnModalCancel != null) {
                btnModalCancel.Content = cancelBtnText;
                btnModalCancel.Visibility = string.IsNullOrEmpty(cancelBtnText) ? Visibility.Collapsed : Visibility.Visible;
            }

            overlayModal.Visibility = Visibility.Visible;
            return modalChoiceTcs.Task;
        }

        public Task ShowAlertModalAsync(string title, string message, string okBtnText = "Verstanden", string icon = "ℹ️", string iconColor = "#3B82F6", string category = "Znipe Optimization Tool") {
            return ShowConfirmModalAsync(title, message, okBtnText, null, icon, iconColor, category);
        }

        public void ShowAlertModal(string title, string message, string okBtnText = "Verstanden", string icon = "ℹ️", string iconColor = "#3B82F6", string category = "Znipe Optimization Tool") {
            ShowAlertModalAsync(title, message, okBtnText, icon, iconColor, category);
        }

        private void ToggleHardwareStressTest() {
            if (HardwareStressTester.IsRunning) {
                HardwareStressTester.Stop();
                if (stressDurationTimer != null) stressDurationTimer.Stop();
            } else {
                StressTarget target = StressTarget.Both;
                if (radStressCpu != null && radStressCpu.IsChecked == true) {
                    target = StressTarget.CpuOnly;
                } else if (radStressGpu != null && radStressGpu.IsChecked == true) {
                    target = StressTarget.GpuOnly;
                }
                HardwareStressTester.Start(target);
                if (stressDurationTimer != null) stressDurationTimer.Start();
            }
            UpdateHardwareStressUI();
        }

        private void UpdateHardwareStressUI() {
            bool running = HardwareStressTester.IsRunning;

            if (radStressBoth != null) radStressBoth.IsEnabled = !running;
            if (radStressCpu != null) radStressCpu.IsEnabled = !running;
            if (radStressGpu != null) radStressGpu.IsEnabled = !running;

            if (btnStressToggle != null) {
                var style = (btnStressToggle.TryFindResource(running ? "PrimaryBtn" : "SuccessBtn") ?? Application.Current.TryFindResource(running ? "PrimaryBtn" : "SuccessBtn")) as Style;
                if (style != null) btnStressToggle.Style = style;
            }
            if (txtStressBtnIcon != null) txtStressBtnIcon.Text = running ? "⏹ " : "🔥 ";
            if (txtStressBtnLabel != null) txtStressBtnLabel.Text = running ? "Stresstest stoppen" : "Stresstest starten";

            if (running) UIHelper.SetPillDanger(pillStressStatus, txtStressStatus, "VOLLLAST AKTIV");
            else UIHelper.SetPill(pillStressStatus, txtStressStatus, false, "", "BEREIT");

            if (txtStressLiveFeedback != null) {
                txtStressLiveFeedback.Text = HardwareStressTester.LiveFeedback;
                txtStressLiveFeedback.Foreground = running ? UIHelper.GetBrush("#F8FAFC") : UIHelper.BrushGrayText;
            }
            if (txtStressDuration != null && !running && HardwareStressTester.Elapsed.TotalSeconds == 0) {
                txtStressDuration.Text = "⏱ 00:00";
            }
        }

        private void RefreshHardwareDetailsUI(HardwareTelemetry.Snapshot snap) {
            if (panelHwDetailMetrics == null || string.IsNullOrEmpty(activeHwDetailMode)) return;

            panelHwDetailMetrics.Children.Clear();

            if (!isInitialTelemetryLoaded) {
                if (activeHwDetailMode == "CPU") {
                    if (txtHwDetailIcon != null) txtHwDetailIcon.Text = "⚡";
                    if (txtHwDetailTitle != null) txtHwDetailTitle.Text = "Prozessor (CPU) Details";
                    if (txtHwDetailSubtitle != null) txtHwDetailSubtitle.Text = "Live-Sensoren laden...";

                    AddDetailTile("🌡️ CPU Temperatur", "lädt...", "#EF4444");
                    AddDetailTile("🛡️ Throttling bei", "lädt...", "#FB7185");
                    AddDetailTile("📊 Gesamt-Auslastung", "lädt...", "#34D399");
                    AddDetailTile("⚡ Package Power", "lädt...", "#F59E0B");
                } else if (activeHwDetailMode == "GPU") {
                    if (txtHwDetailIcon != null) txtHwDetailIcon.Text = "🎮";
                    if (txtHwDetailTitle != null) txtHwDetailTitle.Text = "Grafikkarte (GPU) Details";
                    if (txtHwDetailSubtitle != null) txtHwDetailSubtitle.Text = "Live-Sensoren laden...";

                    AddDetailTile("🌡️ Kern-Temperatur", "lädt...", "#EF4444");
                    AddDetailTile("🛡️ Throttling bei", "lädt...", "#FB7185");
                    AddDetailTile("⚡ Board Power", "lädt...", "#F59E0B");
                    AddDetailTile("🛡️ Aktives Power-Limit", "lädt...", "#FBBF24");
                    AddDetailTile("⏱️ GPU Kern-Takt", "lädt...", "#60A5FA");
                    AddDetailTile("⏱️ Speicher-Takt", "lädt...", "#818CF8");
                    AddDetailTile("🌀 Lüfter-Geschwindigkeit", "lädt...", "#38BDF8");
                    AddDetailTile("📊 GPU-Auslastung", "lädt...", "#34D399");
                    AddDetailTile("💾 VRAM Belegung", "lädt...", "#A78BFA");
                } else if (activeHwDetailMode == "RAM") {
                    if (txtHwDetailIcon != null) txtHwDetailIcon.Text = "💾";
                    if (txtHwDetailTitle != null) txtHwDetailTitle.Text = "Arbeitsspeicher (RAM) Details";
                    if (txtHwDetailSubtitle != null) txtHwDetailSubtitle.Text = "Live-Sensoren laden...";

                    AddDetailTile("💾 RAM Belegt", "lädt...", "#34D399");
                    AddDetailTile("🟢 RAM Frei", "lädt...", "#10B981");
                    AddDetailTile("📦 RAM Gesamt", "lädt...", "#CBD5E1");
                    AddDetailTile("📊 RAM Auslastung", "lädt...", "#38BDF8");
                }
                return;
            }

            if (activeHwDetailMode == "CPU") {
                if (txtHwDetailIcon != null) txtHwDetailIcon.Text = "⚡";
                if (txtHwDetailTitle != null) txtHwDetailTitle.Text = "Prozessor (CPU) Details";
                string cpuName = !string.IsNullOrEmpty(snap.CpuName) ? snap.CpuName : "Hauptprozessor (CPU)";
                if (txtHwDetailSubtitle != null) txtHwDetailSubtitle.Text = cpuName;

                double temp = snap.CpuTempDie.HasValue ? snap.CpuTempDie.Value : (snap.CpuTemp.HasValue ? snap.CpuTemp.Value : latestCpuTemp);
                if (temp > 0) {
                    AddDetailTile("🌡️ CPU Temperatur", string.Format("{0:F1} °C", temp), "#EF4444");
                }
                if (snap.CpuTempCcd.HasValue && snap.CpuTempCcd.Value > 0) {
                    AddDetailTile("🌡️ CPU Tccd (Kerne)", string.Format("{0:F1} °C", snap.CpuTempCcd.Value), "#F87171");
                }
                string throtTemp;
                if (snap.CpuTjMax.HasValue && snap.CpuTjMax.Value > 0) {
                    throtTemp = string.Format("{0:F0} °C", snap.CpuTjMax.Value);
                } else {
                    string rawCpu = (snap.CpuName ?? "").ToLowerInvariant();
                    if (rawCpu.Contains("7800x3d") || rawCpu.Contains("7950x3d") || rawCpu.Contains("7900x3d") || rawCpu.Contains("5800x3d")) {
                        throtTemp = "89 °C";
                    } else if (rawCpu.Contains("intel")) {
                        throtTemp = "100 °C";
                    } else {
                        throtTemp = "95 °C";
                    }
                }
                AddDetailTile("🛡️ Throttling bei", throtTemp, "#FB7185");
                AddDetailTile("📊 Gesamt-Auslastung", string.Format("{0:F0} %", latestCpuLoad), "#34D399");
                if (latestCpuPower > 0) {
                    AddDetailTile("⚡ Package Power", string.Format("{0:F0} W", latestCpuPower), "#F59E0B");
                }
                if (snap.CpuClock.HasValue && snap.CpuClock.Value > 0) {
                    AddDetailTile("⏱️ Kern-Takt", string.Format("{0:N0} MHz", snap.CpuClock.Value), "#60A5FA");
                }
                if (snap.CpuVoltage.HasValue && snap.CpuVoltage.Value > 0) {
                    AddDetailTile("🔋 Kern-Spannung (VID)", string.Format("{0:F3} V", snap.CpuVoltage.Value), "#A78BFA");
                }
            } else if (activeHwDetailMode == "GPU") {
                if (txtHwDetailIcon != null) txtHwDetailIcon.Text = "🎮";
                if (txtHwDetailTitle != null) txtHwDetailTitle.Text = "Grafikkarte (GPU) Details";
                string gpuName = !string.IsNullOrEmpty(latestGpuName) ? latestGpuName : "Grafikkarte (GPU)";
                if (txtHwDetailSubtitle != null) txtHwDetailSubtitle.Text = gpuName;

                AddDetailTile("🌡️ Kern-Temperatur", string.Format("{0:F0} °C", Math.Round(latestGpuTemp, 0)), "#EF4444");
                if (latestGpuHotspot > 0) {
                    AddDetailTile("🔥 Hot Spot Temperatur", string.Format("{0:F0} °C", Math.Round(latestGpuHotspot, 0)), "#F97316");
                }
                if (latestGpuTempMemory > 0) {
                    AddDetailTile("💾 VRAM-Temperatur", string.Format("{0:F0} °C", Math.Round(latestGpuTempMemory, 0)), "#FB923C");
                }
                double gpuThrot = latestGpuThrottleTemp > 0 ? latestGpuThrottleTemp : 83;
                AddDetailTile("🛡️ Throttling bei", string.Format("{0:F0} °C", gpuThrot), "#FB7185");
                if (latestGpuTempLimit > 0) {
                    AddDetailTile("🎯 Gesetztes Temp-Limit", string.Format("{0:F0} °C", latestGpuTempLimit), "#FB923C");
                }
                AddDetailTile("⚡ Board Power", string.Format("{0:F0} W", latestGpuPower), "#F59E0B");
                double defaultPl = latestGpuPowerDefault > 10 ? latestGpuPowerDefault : 450.0;
                double gPl = latestGpuPowerLimit > 10 ? latestGpuPowerLimit : defaultPl;
                AddDetailTile("🛡️ Aktives Power-Limit", string.Format("{0:F0} W", gPl), "#FBBF24");
                if (latestGpuClock > 0) {
                    AddDetailTile("⏱️ GPU Kern-Takt", string.Format("{0:N0} MHz", latestGpuClock), "#60A5FA");
                }
                if (latestGpuMemoryClock > 0) {
                    AddDetailTile("⏱️ Speicher-Takt", string.Format("{0:N0} MHz", latestGpuMemoryClock), "#818CF8");
                }
                string fan = latestGpuFanRpm > 0 ? (latestGpuFanPercent > 0 ? string.Format("{0:F0} RPM ({1:F0}%)", latestGpuFanRpm, latestGpuFanPercent) : string.Format("{0:F0} RPM", latestGpuFanRpm)) : (latestGpuFanPercent > 0 ? string.Format("{0:F0} %", latestGpuFanPercent) : "0 RPM (Passiv)");
                AddDetailTile("🌀 Lüfter-Geschwindigkeit", fan, "#38BDF8");
                AddDetailTile("📊 GPU-Auslastung", string.Format("{0:F0} %", latestGpuLoad), "#34D399");
                if (latestGpuVramUsedMb > 0) {
                    AddDetailTile("💾 VRAM Belegung", string.Format("{0:F1} GB", latestGpuVramUsedMb / 1024.0), "#A78BFA");
                }
            } else if (activeHwDetailMode == "RAM") {
                if (txtHwDetailIcon != null) txtHwDetailIcon.Text = "💾";
                if (txtHwDetailTitle != null) txtHwDetailTitle.Text = "Arbeitsspeicher (RAM) Details";
                if (txtHwDetailSubtitle != null) txtHwDetailSubtitle.Text = "System Memory Telemetrie (100% Win32 Native)";

                double memPercent = snap.RamLoad.HasValue ? snap.RamLoad.Value : 0;
                double usedGb = snap.RamUsedGb.HasValue ? snap.RamUsedGb.Value : 0;
                double freeGb = snap.RamFreeGb.HasValue ? snap.RamFreeGb.Value : 0;
                double totalGb = snap.RamTotalGb.HasValue ? snap.RamTotalGb.Value : (usedGb + freeGb);

                AddDetailTile("💾 RAM Belegt", string.Format("{0:F1} GB", usedGb), "#34D399");
                AddDetailTile("🟢 RAM Frei", string.Format("{0:F1} GB", freeGb), "#10B981");
                AddDetailTile("📦 RAM Gesamt", string.Format("{0:F0} GB", totalGb), "#CBD5E1");
                AddDetailTile("📊 RAM Auslastung", string.Format("{0:F0} %", memPercent), "#38BDF8");
            }
        }

        private void AddDetailTile(string label, string val, string colorHex) {
            if (string.IsNullOrEmpty(val) || val == "-" || val == "—" || val == "Kein Sensor") return;

            var b = new Border {
                Background = UIHelper.GetBrush("#161A24"),
                BorderBrush = UIHelper.GetBrush("#232B3C"),
                BorderThickness = new Thickness(1.2),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(4, 4, 4, 4),
                Width = 210
            };

            var sp = new StackPanel();
            var txtLabel = new TextBlock {
                Text = label,
                FontSize = 11,
                Foreground = UIHelper.BrushGrayText,
                Margin = new Thickness(0, 0, 0, 4)
            };
            var txtVal = new TextBlock {
                Text = val,
                FontSize = 16,
                FontWeight = FontWeights.Bold,
                Foreground = (val == "lädt..." ? UIHelper.GetBrush("#F8FAFC") : UIHelper.GetBrush(colorHex))
            };

            sp.Children.Add(txtLabel);
            sp.Children.Add(txtVal);
            b.Child = sp;
            panelHwDetailMetrics.Children.Add(b);
        }

        private class DriveInfoSnapshot {
            public string Name;
            public string Label;
            public double TotalGb;
            public double UsedGb;
            public double FreeGb;
            public double Percent;
        }

        private class PhysicalDiskEntry {
            public int Index;
            public string Model;
            public string SizeStr;
            public double SizeGb;
            public string InterfaceType;
            public List<DriveInfoSnapshot> Volumes = new List<DriveInfoSnapshot>();
        }

        private void RefreshAllDrivesUI() {
            if (stackHwDrivesList == null && stackDrivesList == null) return;
            Task.Run(() => {
                try {
                    var physicalDisks = new List<PhysicalDiskEntry>();
                    double totalDiskGb = 0;
                    try {
                        WmiHelper.ForEach("SELECT Index, Model, Size, InterfaceType, MediaType FROM Win32_DiskDrive", obj => {
                            int idx = 0;
                            if (obj["Index"] != null) int.TryParse(obj["Index"].ToString(), out idx);
                            string model = obj["Model"] != null ? obj["Model"].ToString().Trim() : "Laufwerk " + (idx + 1);
                            string iface = obj["InterfaceType"] != null ? obj["InterfaceType"].ToString().Trim() : "NVMe / SSD";
                            iface = iface.Replace("SCSI", "PCIe NVMe / High-Speed");

                            double dGb = 0;
                            string sizeStr = "";
                            long sBytes;
                            if (obj["Size"] != null && long.TryParse(obj["Size"].ToString(), out sBytes)) {
                                dGb = Math.Round(sBytes / (1000.0 * 1000.0 * 1000.0), 0);
                                totalDiskGb += dGb;
                                if (dGb >= 1000) {
                                    sizeStr = string.Format(CultureInfo.InvariantCulture, "{0:F0} TB ({1:F0} GB)", Math.Round(dGb / 1000.0, 0), dGb);
                                } else {
                                    sizeStr = string.Format(CultureInfo.InvariantCulture, "{0:F0} GB", dGb);
                                }
                            }
                            physicalDisks.Add(new PhysicalDiskEntry {
                                Index = idx,
                                Model = model,
                                SizeStr = sizeStr,
                                SizeGb = dGb,
                                InterfaceType = iface
                            });
                        });
                        physicalDisks.Sort((a, b) => a.Index.CompareTo(b.Index));
                    } catch {}

                    // Logical disk to partition mapping
                    var diskToLetters = new Dictionary<int, List<string>>();
                    try {
                        WmiHelper.ForEach("SELECT Antecedent, Dependent FROM Win32_LogicalDiskToPartition", obj => {
                            string ant = obj["Antecedent"] != null ? obj["Antecedent"].ToString() : "";
                            string dep = obj["Dependent"] != null ? obj["Dependent"].ToString() : "";
                            var mDisk = Regex.Match(ant, @"Disk\s*#(\d+)", RegexOptions.IgnoreCase);
                            var mDrive = Regex.Match(dep, @"DeviceID\s*=\s*""([A-Za-z]:)""", RegexOptions.IgnoreCase);
                            if (mDisk.Success && mDrive.Success) {
                                int dNum = int.Parse(mDisk.Groups[1].Value);
                                string letter = mDrive.Groups[1].Value.ToUpperInvariant();
                                if (!diskToLetters.ContainsKey(dNum)) diskToLetters[dNum] = new List<string>();
                                if (!diskToLetters[dNum].Contains(letter)) diskToLetters[dNum].Add(letter);
                            }
                        });
                    } catch {}

                    // Volume data from DriveInfo
                    var volumeMap = new Dictionary<string, DriveInfoSnapshot>(StringComparer.OrdinalIgnoreCase);
                    try {
                        foreach (var d in DriveInfo.GetDrives()) {
                            try {
                                if (d.IsReady && (d.DriveType == DriveType.Fixed || d.DriveType == DriveType.Removable)) {
                                    string name = d.Name.TrimEnd('\\');
                                    string label = !string.IsNullOrEmpty(d.VolumeLabel)
                                        ? d.VolumeLabel
                                        : (name.Equals("C:", StringComparison.OrdinalIgnoreCase) ? "System" : "Laufwerk");

                                    double totalGb = (double)d.TotalSize / (1024.0 * 1024.0 * 1024.0);
                                    double freeGb = (double)d.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
                                    double usedGb = Math.Max(0, totalGb - freeGb);
                                    double percent = totalGb > 0 ? (usedGb / totalGb) * 100.0 : 0.0;

                                    volumeMap[name] = new DriveInfoSnapshot {
                                        Name = name,
                                        Label = label,
                                        TotalGb = totalGb,
                                        UsedGb = usedGb,
                                        FreeGb = freeGb,
                                        Percent = percent
                                    };
                                }
                            } catch {}
                        }
                    } catch {}

                    // Match volumes to physical disks
                    var matchedLetters = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    foreach (var pDisk in physicalDisks) {
                        if (diskToLetters.ContainsKey(pDisk.Index)) {
                            foreach (var letter in diskToLetters[pDisk.Index]) {
                                DriveInfoSnapshot snap;
                                if (volumeMap.TryGetValue(letter, out snap)) {
                                    pDisk.Volumes.Add(snap);
                                    matchedLetters.Add(letter);
                                }
                            }
                        }
                    }

                    // Any unassigned volume (e.g. virtual or unmapped disks)
                    var unmappedVolumes = new List<DriveInfoSnapshot>();
                    foreach (var kvp in volumeMap) {
                        if (!matchedLetters.Contains(kvp.Key)) {
                            if (physicalDisks.Count == 1) {
                                physicalDisks[0].Volumes.Add(kvp.Value);
                            } else {
                                unmappedVolumes.Add(kvp.Value);
                            }
                        }
                    }

                    string summaryText = "";
                    if (totalDiskGb >= 1000) {
                        summaryText = string.Format(CultureInfo.InvariantCulture, "{0:F0} TB Gesamtkapazität ({1} Laufwerke)", Math.Round(totalDiskGb / 1000.0, 0), physicalDisks.Count);
                    } else if (totalDiskGb > 0) {
                        summaryText = string.Format(CultureInfo.InvariantCulture, "{0:F0} GB Gesamtkapazität ({1} Laufwerke)", totalDiskGb, physicalDisks.Count);
                    }

                    if (window != null) {
                        window.Dispatcher.BeginInvoke((Action)(() => {
                            try {
                                if (txtStorageSummary != null && !string.IsNullOrEmpty(summaryText)) {
                                    txtStorageSummary.Text = summaryText;
                                }
                                if (txtHwStorageSummary != null && !string.IsNullOrEmpty(summaryText)) {
                                    txtHwStorageSummary.Text = summaryText;
                                }

                                var targetStack = stackDrivesList ?? stackHwDrivesList;
                                if (targetStack == null) return;
                                targetStack.Children.Clear();

                                int driveNumber = 1;
                                foreach (var pDisk in physicalDisks) {
                                    var driveCard = new Border {
                                        Background = UIHelper.GetBrush("#141824"),
                                        BorderBrush = UIHelper.GetBrush("#232A3B"),
                                        BorderThickness = new Thickness(1),
                                        CornerRadius = new CornerRadius(8),
                                        Padding = new Thickness(13, 11, 13, 11),
                                        Margin = new Thickness(0, 0, 0, 10)
                                    };

                                    var cardStack = new StackPanel();

                                    // Header with Drive title, size, interface, and copy button
                                    var headerGrid = new Grid();
                                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                    headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                                    var leftStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

                                    var iconBorder = new Border {
                                        Width = 26,
                                        Height = 26,
                                        CornerRadius = new CornerRadius(6),
                                        Background = UIHelper.GetBrush("#181D2A"),
                                        BorderBrush = UIHelper.GetBrush("#262F42"),
                                        BorderThickness = new Thickness(1),
                                        Margin = new Thickness(0, 0, 10, 0)
                                    };
                                    iconBorder.Child = new System.Windows.Shapes.Path {
                                        Data = Geometry.Parse("M22 12H2 M5.45 5.11L2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z M6 16h.01 M10 16h.01"),
                                        Stroke = UIHelper.GetBrush("#F8FAFC"),
                                        StrokeThickness = 1.5,
                                        StrokeStartLineCap = PenLineCap.Round,
                                        StrokeEndLineCap = PenLineCap.Round,
                                        Width = 13,
                                        Height = 13,
                                        Stretch = Stretch.Uniform,
                                        HorizontalAlignment = HorizontalAlignment.Center,
                                        VerticalAlignment = VerticalAlignment.Center
                                    };
                                    leftStack.Children.Add(iconBorder);

                                    var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
                                    var titleTb = new TextBlock {
                                        Text = string.Format("Laufwerk {0}: {1}", driveNumber, pDisk.Model),
                                        FontWeight = FontWeights.Bold,
                                        FontSize = 12.5,
                                        Foreground = UIHelper.GetBrush("#F8FAFC")
                                    };
                                    var subTb = new TextBlock {
                                        Text = string.Format("{0} • {1}", pDisk.SizeStr, pDisk.InterfaceType),
                                        FontSize = 11,
                                        Foreground = UIHelper.BrushGrayText,
                                        Margin = new Thickness(0, 2, 0, 0)
                                    };
                                    infoStack.Children.Add(titleTb);
                                    infoStack.Children.Add(subTb);
                                    leftStack.Children.Add(infoStack);
                                    Grid.SetColumn(leftStack, 0);
                                    headerGrid.Children.Add(leftStack);

                                    var copyBtn = new Button {
                                        Content = "📋",
                                        Style = (Style)window.FindResource("CopyIconBtn"),
                                        Margin = new Thickness(8, 0, 0, 0),
                                        VerticalAlignment = VerticalAlignment.Center
                                    };
                                    string sizeVariant = "";
                                    if (pDisk.SizeGb >= 950) {
                                        double tb = Math.Round(pDisk.SizeGb / 1000.0, 0);
                                        sizeVariant = string.Format(CultureInfo.InvariantCulture, "{0:F0}TB", tb);
                                    } else if (pDisk.SizeGb > 0) {
                                        sizeVariant = string.Format(CultureInfo.InvariantCulture, "{0:F0}GB", pDisk.SizeGb);
                                    }

                                    string modelToCopy = pDisk.Model;
                                    if (!string.IsNullOrEmpty(sizeVariant) && !modelToCopy.ToUpperInvariant().Contains(sizeVariant.ToUpperInvariant())) {
                                        modelToCopy = string.Format("{0} {1}", modelToCopy, sizeVariant);
                                    }
                                    SetupCopyButton(copyBtn, () => modelToCopy, "Laufwerks-Modell & Kapazität kopieren");
                                    Grid.SetColumn(copyBtn, 1);
                                    headerGrid.Children.Add(copyBtn);

                                    cardStack.Children.Add(headerGrid);

                                    // Partitions under this drive
                                    if (pDisk.Volumes.Count > 0) {
                                        foreach (var vol in pDisk.Volumes) {
                                            var volBorder = new Border {
                                                Background = UIHelper.GetBrush("#0D1017"),
                                                BorderBrush = UIHelper.GetBrush("#1E2536"),
                                                BorderThickness = new Thickness(1),
                                                CornerRadius = new CornerRadius(6),
                                                Padding = new Thickness(11, 8, 11, 8),
                                                Margin = new Thickness(0, 8, 0, 0)
                                            };

                                            var volStack = new StackPanel();

                                            var volHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                                            volHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                            volHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                                            var volLeftSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                                            var tbLetter = new TextBlock {
                                                Text = vol.Name + " ",
                                                FontWeight = FontWeights.Bold,
                                                FontSize = 12.5,
                                                Foreground = UIHelper.GetBrush("#38BDF8") // Clean modern cyan/sky blue
                                            };
                                            var tbLabel = new TextBlock {
                                                Text = "(" + vol.Label + ")",
                                                FontSize = 11.5,
                                                Foreground = UIHelper.BrushGrayText,
                                                VerticalAlignment = VerticalAlignment.Center
                                            };
                                            volLeftSp.Children.Add(tbLetter);
                                            volLeftSp.Children.Add(tbLabel);
                                            Grid.SetColumn(volLeftSp, 0);

                                            string usageStr = string.Format(CultureInfo.InvariantCulture, "{0:F1} GB von {1:F0} GB belegt ({2:F0}%)", vol.UsedGb, vol.TotalGb, vol.Percent);
                                            var tbUsage = new TextBlock {
                                                Text = usageStr,
                                                FontSize = 11,
                                                FontWeight = FontWeights.SemiBold,
                                                Foreground = UIHelper.GetBrush("#E2E8F0"),
                                                VerticalAlignment = VerticalAlignment.Center
                                            };
                                            Grid.SetColumn(tbUsage, 1);

                                            volHeaderGrid.Children.Add(volLeftSp);
                                            volHeaderGrid.Children.Add(tbUsage);

                                            var barBorder = new Border {
                                                Height = 6,
                                                CornerRadius = new CornerRadius(3),
                                                Background = UIHelper.GetBrush("#161B26"),
                                                ClipToBounds = true
                                            };

                                            string progColor = vol.Percent >= 90.0 ? "#EF4444" : (vol.Percent >= 75.0 ? "#F59E0B" : "#38BDF8");
                                            var pb = new ProgressBar {
                                                Height = 6,
                                                Minimum = 0,
                                                Maximum = 100,
                                                Value = vol.Percent,
                                                Background = Brushes.Transparent,
                                                Foreground = UIHelper.GetBrush(progColor)
                                            };
                                            barBorder.Child = pb;

                                            volStack.Children.Add(volHeaderGrid);
                                            volStack.Children.Add(barBorder);

                                            // Free space note
                                            var volFooterGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                                            var tbFree = new TextBlock {
                                                Text = string.Format(CultureInfo.InvariantCulture, "{0:F1} GB frei", vol.FreeGb),
                                                FontSize = 10.5,
                                                Foreground = UIHelper.GetBrush("#64748B")
                                            };
                                            volFooterGrid.Children.Add(tbFree);
                                            if (vol.Percent >= 90.0) {
                                                var tbWarn = new TextBlock {
                                                    Text = "⚠️ Fast voll",
                                                    FontSize = 10.5,
                                                    FontWeight = FontWeights.Bold,
                                                    Foreground = UIHelper.GetBrush("#EF4444"),
                                                    HorizontalAlignment = HorizontalAlignment.Right
                                                };
                                                volFooterGrid.Children.Add(tbWarn);
                                            }
                                            volStack.Children.Add(volFooterGrid);

                                            volBorder.Child = volStack;
                                            cardStack.Children.Add(volBorder);
                                        }
                                    } else {
                                        var tbNoParts = new TextBlock {
                                            Text = "• Keine zugewiesenen Partitionen / Laufwerksbuchstaben",
                                            FontSize = 11,
                                            Foreground = UIHelper.GetBrush("#64748B"),
                                            Margin = new Thickness(4, 8, 0, 0)
                                        };
                                        cardStack.Children.Add(tbNoParts);
                                    }

                                    driveCard.Child = cardStack;
                                    targetStack.Children.Add(driveCard);
                                    driveNumber++;
                                }

                                // If any unmapped volumes exist (e.g. virtual/network drives)
                                if (unmappedVolumes.Count > 0) {
                                    var unmappedCard = new Border {
                                        Background = UIHelper.GetBrush("#141824"),
                                        BorderBrush = UIHelper.GetBrush("#232A3B"),
                                        BorderThickness = new Thickness(1),
                                        CornerRadius = new CornerRadius(8),
                                        Padding = new Thickness(13, 11, 13, 11),
                                        Margin = new Thickness(0, 0, 0, 10)
                                    };
                                    var unmappedStack = new StackPanel();
                                    unmappedStack.Children.Add(new TextBlock {
                                        Text = "Weitere Volumes & Laufwerke",
                                        FontWeight = FontWeights.Bold,
                                        FontSize = 12.5,
                                        Foreground = UIHelper.GetBrush("#F8FAFC"),
                                        Margin = new Thickness(0, 0, 0, 6)
                                    });
                                    foreach (var vol in unmappedVolumes) {
                                        var volBorder = new Border {
                                            Background = UIHelper.GetBrush("#0D1017"),
                                            BorderBrush = UIHelper.GetBrush("#1E2536"),
                                            BorderThickness = new Thickness(1),
                                            CornerRadius = new CornerRadius(6),
                                            Padding = new Thickness(11, 8, 11, 8),
                                            Margin = new Thickness(0, 8, 0, 0)
                                        };
                                        var volStack = new StackPanel();

                                        var volHeaderGrid = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                                        volHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                        volHeaderGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                                        var volLeftSp = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                                        var tbLetter = new TextBlock {
                                            Text = vol.Name + " ",
                                            FontWeight = FontWeights.Bold,
                                            FontSize = 12.5,
                                            Foreground = UIHelper.GetBrush("#38BDF8")
                                        };
                                        var tbLabel = new TextBlock {
                                            Text = "(" + vol.Label + ")",
                                            FontSize = 11.5,
                                            Foreground = UIHelper.BrushGrayText,
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        volLeftSp.Children.Add(tbLetter);
                                        volLeftSp.Children.Add(tbLabel);
                                        Grid.SetColumn(volLeftSp, 0);

                                        string usageStr = string.Format(CultureInfo.InvariantCulture, "{0:F1} GB von {1:F0} GB belegt ({2:F0}%)", vol.UsedGb, vol.TotalGb, vol.Percent);
                                        var tbUsage = new TextBlock {
                                            Text = usageStr,
                                            FontSize = 11,
                                            FontWeight = FontWeights.SemiBold,
                                            Foreground = UIHelper.GetBrush("#E2E8F0"),
                                            VerticalAlignment = VerticalAlignment.Center
                                        };
                                        Grid.SetColumn(tbUsage, 1);

                                        volHeaderGrid.Children.Add(volLeftSp);
                                        volHeaderGrid.Children.Add(tbUsage);

                                        var barBorder = new Border {
                                            Height = 6,
                                            CornerRadius = new CornerRadius(3),
                                            Background = UIHelper.GetBrush("#161B26"),
                                            ClipToBounds = true
                                        };

                                        string progColor = vol.Percent >= 90.0 ? "#EF4444" : (vol.Percent >= 75.0 ? "#F59E0B" : "#38BDF8");
                                        var pb = new ProgressBar {
                                            Height = 6,
                                            Minimum = 0,
                                            Maximum = 100,
                                            Value = vol.Percent,
                                            Background = Brushes.Transparent,
                                            Foreground = UIHelper.GetBrush(progColor)
                                        };
                                        barBorder.Child = pb;

                                        volStack.Children.Add(volHeaderGrid);
                                        volStack.Children.Add(barBorder);

                                        var volFooterGrid = new Grid { Margin = new Thickness(0, 4, 0, 0) };
                                        var tbFree = new TextBlock {
                                            Text = string.Format(CultureInfo.InvariantCulture, "{0:F1} GB frei", vol.FreeGb),
                                            FontSize = 10.5,
                                            Foreground = UIHelper.GetBrush("#64748B")
                                        };
                                        volFooterGrid.Children.Add(tbFree);
                                        volStack.Children.Add(volFooterGrid);

                                        volBorder.Child = volStack;
                                        unmappedStack.Children.Add(volBorder);
                                    }
                                    unmappedCard.Child = unmappedStack;
                                    targetStack.Children.Add(unmappedCard);
                                }

                                // Fallback if no physical drives were returned by WMI
                                if (physicalDisks.Count == 0 && volumeMap.Count > 0) {
                                    foreach (var vol in volumeMap.Values) {
                                        var fallbackCard = new Border {
                                            Background = UIHelper.GetBrush("#141824"),
                                            BorderBrush = UIHelper.GetBrush("#232A3B"),
                                            BorderThickness = new Thickness(1),
                                            CornerRadius = new CornerRadius(8),
                                            Padding = new Thickness(13, 11, 13, 11),
                                            Margin = new Thickness(0, 0, 0, 10)
                                        };
                                        var fbStack = new StackPanel();
                                        var fbHeader = new Grid { Margin = new Thickness(0, 0, 0, 6) };
                                        fbHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                                        fbHeader.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                                        var fbLeft = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
                                        fbLeft.Children.Add(new TextBlock { Text = vol.Name + " ", FontWeight = FontWeights.Bold, FontSize = 12.5, Foreground = UIHelper.GetBrush("#38BDF8") });
                                        fbLeft.Children.Add(new TextBlock { Text = "(" + vol.Label + ")", FontSize = 11.5, Foreground = UIHelper.BrushGrayText, VerticalAlignment = VerticalAlignment.Center });
                                        Grid.SetColumn(fbLeft, 0);
                                        var fbUsage = new TextBlock { Text = string.Format(CultureInfo.InvariantCulture, "{0:F1} GB von {1:F0} GB belegt ({2:F0}%)", vol.UsedGb, vol.TotalGb, vol.Percent), FontSize = 11, FontWeight = FontWeights.SemiBold, Foreground = UIHelper.GetBrush("#E2E8F0"), VerticalAlignment = VerticalAlignment.Center };
                                        Grid.SetColumn(fbUsage, 1);
                                        fbHeader.Children.Add(fbLeft);
                                        fbHeader.Children.Add(fbUsage);

                                        var fbBarBorder = new Border { Height = 6, CornerRadius = new CornerRadius(3), Background = UIHelper.GetBrush("#161B26"), ClipToBounds = true };
                                        string fbProgColor = vol.Percent >= 90.0 ? "#EF4444" : (vol.Percent >= 75.0 ? "#F59E0B" : "#38BDF8");
                                        fbBarBorder.Child = new ProgressBar { Height = 6, Minimum = 0, Maximum = 100, Value = vol.Percent, Background = Brushes.Transparent, Foreground = UIHelper.GetBrush(fbProgColor) };
                                        fbStack.Children.Add(fbHeader);
                                        fbStack.Children.Add(fbBarBorder);
                                        fallbackCard.Child = fbStack;
                                        targetStack.Children.Add(fallbackCard);
                                    }
                                }
                            } catch {}
                        }));
                    }
                } catch {}
            });
        }

        private void OpenRegeditAtKey(string subKey) {
            try {
                string regTarget = @"Computer\HKEY_LOCAL_MACHINE\" + subKey;
                using (RegistryKey applet = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Applets\Regedit")) {
                    if (applet != null) applet.SetValue("LastKey", regTarget, RegistryValueKind.String);
                }
                foreach (var p in Process.GetProcessesByName("regedit")) {
                    try { p.Kill(); } catch {}
                }
                ProcessRunner.Start("regedit.exe");
            } catch {}
        }

    }

    public static class Program {
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsIconic(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        public static void ForceForeground(IntPtr hWnd) {
            if (hWnd == IntPtr.Zero) return;
            try {
                ShowWindow(hWnd, SW_SHOW);
                SetForegroundWindow(hWnd);
            } catch {}
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowPlacement(IntPtr hWnd, ref WINDOWPLACEMENT lpwndpl);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int x; public int y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct WINDOWPLACEMENT {
            public int length;
            public int flags;
            public int showCmd;
            public POINT ptMinPosition;
            public POINT ptMaxPosition;
            public RECT rcNormalPosition;
        }

        private const int SW_SHOWMAXIMIZED = 3;
        private const int SW_SHOW = 5;
        private const int SW_RESTORE = 9;
        private const int SW_SHOWMINIMIZED = 2;
        private const int WPF_RESTORETOMAXIMIZED = 0x0002;
        private const string AppMutexName = @"Global\ZnipeOptimizationTool_SingleInstanceMutex";

        [STAThread]
        public static void Main(string[] args) {
            bool forceRestart = false;
            if (args != null) {
                for (int i = 0; i < args.Length; i++) {
                    if (string.Equals(args[i], "/restart", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "-restart", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "--restart", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(args[i], "/force", StringComparison.OrdinalIgnoreCase)) {
                        forceRestart = true;
                        break;
                    }
                }
            }

            if (forceRestart) {
                try {
                    using (var exitEvt = EventWaitHandle.OpenExisting(@"Global\ZnipeOptimizationTool_CloseEvent")) {
                        exitEvt.Set();
                    }
                    Thread.Sleep(200);
                } catch {}
            }

            bool createdNew = false;
            Mutex mutex = null;
            try {
                try {
                    mutex = new Mutex(true, AppMutexName, out createdNew);
                } catch (AbandonedMutexException) {
                    createdNew = true;
                } catch (UnauthorizedAccessException) {
                    createdNew = false;
                }

                if (!createdNew) {
                    if (BringExistingInstanceToFront()) {
                        return;
                    }

                    // If no visible window exists, previous instance may be shutting down in background.
                    // Wait up to 1500ms to acquire mutex instead of silently dying.
                    try {
                        if (mutex != null && mutex.WaitOne(1500)) {
                            createdNew = true;
                        }
                    } catch (AbandonedMutexException) {
                        createdNew = true;
                    }

                    if (!createdNew) {
                        return;
                    }
                }

                // Start listener for force-close event (e.g. triggered by build.ps1 or Ctrl+Shift+B)
                Task.Run(() => {
                    try {
                        var sec = new EventWaitHandleSecurity();
                        var sid = new SecurityIdentifier(WellKnownSidType.WorldSid, null);
                        sec.AddAccessRule(new EventWaitHandleAccessRule(
                            sid,
                            EventWaitHandleRights.FullControl,
                            AccessControlType.Allow));
                        bool created;
                        using (var exitEvt = new EventWaitHandle(false, EventResetMode.AutoReset, @"Global\ZnipeOptimizationTool_CloseEvent", out created, sec)) {
                            exitEvt.WaitOne();
                            try {
                                Process.GetCurrentProcess().Kill();
                            } catch {
                                Environment.Exit(0);
                            }
                        }
                    } catch {}
                });

                RunApp();
            } finally {
                if (mutex != null) {
                    if (createdNew) {
                        try { mutex.ReleaseMutex(); } catch {}
                    }
                    mutex.Dispose();
                }
            }
        }

        private static bool BringExistingInstanceToFront() {
            try {
                IntPtr hWnd = FindWindow(null, "Znipe Optimization Tool");
                if (hWnd == IntPtr.Zero) {
                    Process current = Process.GetCurrentProcess();
                    foreach (Process p in Process.GetProcessesByName(current.ProcessName)) {
                        if (p.Id != current.Id && p.MainWindowHandle != IntPtr.Zero) {
                            hWnd = p.MainWindowHandle;
                            break;
                        }
                    }
                }

                if (hWnd != IntPtr.Zero) {
                    WINDOWPLACEMENT wp = new WINDOWPLACEMENT();
                    wp.length = Marshal.SizeOf(typeof(WINDOWPLACEMENT));
                    if (GetWindowPlacement(hWnd, ref wp)) {
                        if (wp.showCmd == SW_SHOWMINIMIZED) {
                            if ((wp.flags & WPF_RESTORETOMAXIMIZED) != 0) {
                                ShowWindow(hWnd, SW_SHOWMAXIMIZED);
                            } else {
                                ShowWindow(hWnd, SW_RESTORE);
                            }
                        } else if (wp.showCmd == SW_SHOWMAXIMIZED) {
                            ShowWindow(hWnd, SW_SHOWMAXIMIZED);
                        } else {
                            ShowWindow(hWnd, SW_SHOW);
                        }
                    } else {
                        if (IsIconic(hWnd)) {
                            ShowWindow(hWnd, SW_RESTORE);
                        } else {
                            ShowWindow(hWnd, SW_SHOW);
                        }
                    }
                    ForceForeground(hWnd);
                    return true;
                }
            } catch {}
            return false;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void RunApp() {
            try {
                Application app = new Application();

                // Global Crash Protection & Unhandled Exception Interceptors
                app.DispatcherUnhandledException += (s, e) => {
                    try { File.AppendAllText("crash.log", "\nDispatcher Exception: " + e.Exception.ToString()); } catch {}
                    e.Handled = true;
                };

                AppDomain.CurrentDomain.UnhandledException += (s, e) => {};

                TaskScheduler.UnobservedTaskException += (s, e) => {
                    e.SetObserved();
                };

                app.Exit += (s, e) => { HardwareTelemetry.Shutdown(); HardwareTelemetry.StopSensorDriver(); };
                AppDomain.CurrentDomain.ProcessExit += (s, e) => { HardwareTelemetry.Shutdown(); HardwareTelemetry.StopSensorDriver(); };

                // Bereinige etwaige verwaiste Run-Einträge aus früheren Testversionen
                try {
                    using (var rk = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true)) {
                        if (rk != null) rk.DeleteValue("*ZnipeOptimizationTool", false);
                    }
                } catch {}

                // Hardware-Telemetrie & Daemon asynchron im Hintergrund starten, damit das Hauptfenster verzögerungsfrei lädt
                Task.Run(() => { HardwareTelemetry.EnsureRunning(); });

                string xaml;
                using (Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream("MainWindow.xaml"))
                using (StreamReader sr = new StreamReader(stream, Encoding.UTF8)) {
                    xaml = sr.ReadToEnd();
                }
                MainWindowLogic logic = new MainWindowLogic();
                Window win = logic.Initialize(xaml);

                win.Topmost = true;

                app.Run(win);
                HardwareTelemetry.Shutdown();
                HardwareTelemetry.StopSensorDriver();
            } catch (Exception ex) {
                try { File.WriteAllText("crash.log", ex.ToString()); } catch {}
                MessageBox.Show("Kritischer Anwendungsfehler:\n" + ex.ToString(), "Znipe Optimization Tool", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    public static class HardwareTelemetry {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        private class MEMORYSTATUSEX {
            public uint dwLength;
            public uint dwMemoryLoad;
            public ulong ullTotalPhys;
            public ulong ullAvailPhys;
            public ulong ullTotalPageFile;
            public ulong ullAvailPageFile;
            public ulong ullTotalVirtual;
            public ulong ullAvailVirtual;
            public ulong ullAvailExtendedVirtual;
            public MEMORYSTATUSEX() {
                this.dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
            }
        }

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

        private static PerformanceCounter _cpuCounter = null;
        private static readonly object _counterLock = new object();

        public class Snapshot {
            public bool IsAvailable = true;

            // CPU Details
            public string CpuName = null;
            public double? CpuTemp = null;
            public double? CpuTempDie = null;
            public double? CpuTempCcd = null;
            public double? CpuPower = null;
            public double? CpuLoad = null;
            public double? CpuClock = null;
            public double? CpuVoltage = null;
            public double? CpuTjMax = null;

            // RAM Details
            public double? RamUsedGb = null;
            public double? RamTotalGb = null;
            public double? RamFreeGb = null;
            public double? RamLoad = null;
            public double? PagefileUsedGb = null;
            public double? PagefileTotalGb = null;
            public double? PagefileLoad = null;
        }

        private static Snapshot _lastSnapshot = new Snapshot();
        private static Process _spawnedSensorProc = null;
        private static readonly object _daemonLock = new object();
        private static bool _isStartingDaemon = false;

        public static void EnsureRunning() {
            lock (_counterLock) {
                if (_cpuCounter == null) {
                    try {
                        _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                        _cpuCounter.NextValue();
                    } catch {}
                }
            }
            TryStartSensorDaemon();
        }

        private static bool IsCoreTempAlreadyRunning() {
            if (_spawnedSensorProc != null && !_spawnedSensorProc.HasExited) return true;
            try {
                if (Process.GetProcessesByName("CoreTemp").Length > 0 || Process.GetProcessesByName("Core Temp").Length > 0) return true;
            } catch {}
            try {
                using (var mmf = MemoryMappedFile.OpenExisting("CoreTempMappingObjectEx", MemoryMappedFileRights.Read)) return true;
            } catch {}
            try {
                using (var mmf = MemoryMappedFile.OpenExisting("CoreTempMappingObject", MemoryMappedFileRights.Read)) return true;
            } catch {}
            return false;
        }

        private static string GetSensorDir() {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZnipeOptimizationTool", "Sensors");
            try {
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            } catch {}
            return dir;
        }

        private static void TryStartSensorDaemon() {
            lock (_daemonLock) {
                if (IsCoreTempAlreadyRunning() || _isStartingDaemon) return;
                _isStartingDaemon = true;

                try {
                    string cacheDir = GetSensorDir();

                    // 0. Bereinige alte Log-Dateien aus früheren Sitzungen
                    try {
                        foreach (var f in Directory.GetFiles(cacheDir, "CT-Log*.csv")) {
                            try { File.Delete(f); } catch {}
                        }
                    } catch {}

                    string exePath = Path.Combine(cacheDir, "CoreTemp.exe");

                    // 1. Extract embedded CoreTempBinary if not present or size mismatch
                    var asm = typeof(HardwareTelemetry).Assembly;
                    using (var resStream = asm.GetManifestResourceStream("CoreTempBinary")) {
                        if (resStream != null) {
                            bool extract = true;
                            if (File.Exists(exePath)) {
                                try {
                                    var fi = new FileInfo(exePath);
                                    if (fi.Length == resStream.Length) extract = false;
                                } catch { }
                            }
                            if (extract) {
                                try {
                                    using (var fs = new FileStream(exePath, FileMode.Create, FileAccess.Write, FileShare.None)) {
                                        resStream.CopyTo(fs);
                                    }
                                } catch { }
                            }
                        }
                    }

                    // Fallback to local candidates during dev
                    if (!File.Exists(exePath)) {
                        string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                        string[] candidates = new string[] {
                            Path.Combine(baseDir, @"tools\sensors\Core Temp.exe"),
                            Path.Combine(baseDir, @"Core Temp.exe")
                        };
                        foreach (var cand in candidates) {
                            if (File.Exists(cand)) {
                                try { File.Copy(cand, exePath, true); break; } catch { }
                            }
                        }
                    }

                    if (!File.Exists(exePath)) return;

                    // 2. Ensure CoreTemp.ini is configured for 500ms polling, 1-second real-time logging, zero update checks, zero plugins & silent systray
                    string iniPath = Path.Combine(cacheDir, "CoreTemp.ini");
                    if (!File.Exists(iniPath) || !File.ReadAllText(iniPath).Contains("SystrayOption=3") || !File.ReadAllText(iniPath).Contains("AutoUpdateCheck=0")) {
                        string iniContent = "[General]\r\nLanguage=English\r\nEnLog=1\r\nLogInt=1\r\nReadInt=500\r\nLogFolder=.\r\nSingleInstance=0\r\nAutoUpdateCheck=0\r\nPlugins=0\r\n\r\n[Advanced]\r\nSnmpSharedMemory=1\r\n\r\n[Display]\r\nMinimized=1\r\nHideTaskbarButton=1\r\nCloseToSystray=1\r\n\r\n[System tray]\r\nSystrayOption=3\r\n";
                        try { File.WriteAllText(iniPath, iniContent, Encoding.ASCII); } catch { }
                    }

                    // 3. Launch Core Temp completely hidden
                    var psi = new ProcessStartInfo(exePath) {
                        WindowStyle = ProcessWindowStyle.Hidden,
                        WorkingDirectory = cacheDir,
                        UseShellExecute = true
                    };
                    _spawnedSensorProc = Process.Start(psi);
                } catch { }
                finally {
                    _isStartingDaemon = false;
                }
            }
        }


        public static void Shutdown() {
            lock (_counterLock) {
                if (_cpuCounter != null) {
                    try { _cpuCounter.Dispose(); } catch {}
                    _cpuCounter = null;
                }
            }
            if (_spawnedSensorProc != null) {
                try {
                    if (!_spawnedSensorProc.HasExited) {
                        _spawnedSensorProc.Kill();
                    }
                } catch {}
                _spawnedSensorProc = null;
            }
            try {
                var procs = new System.Collections.Generic.List<Process>();
                procs.AddRange(Process.GetProcessesByName("CoreTemp"));
                procs.AddRange(Process.GetProcessesByName("Core Temp"));
                foreach (var p in procs) {
                    try {
                        bool shouldKill = false;
                        try {
                            if (p.MainModule != null && (p.MainModule.FileName.IndexOf("Znipe", StringComparison.OrdinalIgnoreCase) >= 0 || p.MainModule.FileName.IndexOf("ZnipeSensors", StringComparison.OrdinalIgnoreCase) >= 0)) shouldKill = true;
                        } catch {
                            shouldKill = true;
                        }
                        if (shouldKill && !p.HasExited) {
                            p.Kill();
                        }
                    } catch {}
                }
            } catch {}
            try {
                string cacheDir = GetSensorDir();
                if (Directory.Exists(cacheDir)) {
                    foreach (var f in Directory.GetFiles(cacheDir, "CT-Log*.csv")) {
                        try { File.Delete(f); } catch {}
                    }
                }
            } catch {}
            _cachedLogFilePath = null;
            _coreTempLogHeader = null;
            _coreTempLogTdieIdx = -1;
            _coreTempLogTccdIdx = -1;
            _coreTempLogPwrIdx = -1;
            _lastLogFileCheck = DateTime.MinValue;
            _isStartingDaemon = false;
            _lastSnapshot = new Snapshot();
        }

        // Entlädt den Core-Temp-Kernel-Treiber (ALSysIO64.sys) explizit.
        // Process.Kill() beendet nur den User-Mode-Prozess; der Treiber bliebe sonst
        // bis zum Reboot geladen und für Anti-Cheat sichtbar. Wird bewusst NUR beim
        // App-Exit aufgerufen (nicht beim Tabwechsel), damit der Treiber innerhalb
        // einer Sitzung resident bleibt und der Re-Start auf dem Home-Tab schnell ist.
        public static void StopSensorDriver() {
            string[] serviceNames = new string[] { "ALSysIO", "ALSysIO64" };
            foreach (string name in serviceNames) {
                try {
                    ProcessRunner.RunAndGetOutput("sc.exe", "stop " + name, 5000);
                } catch {}
            }
        }

        public static Snapshot Read() {
            try {
                var snap = new Snapshot();
                snap.IsAvailable = true;

                // 1. Native Win32 Memory status
                var mem = new MEMORYSTATUSEX();
                if (GlobalMemoryStatusEx(mem)) {
                    snap.RamTotalGb = Math.Round(mem.ullTotalPhys / (1024.0 * 1024.0 * 1024.0), 1);
                    snap.RamFreeGb = Math.Round(mem.ullAvailPhys / (1024.0 * 1024.0 * 1024.0), 1);
                    snap.RamUsedGb = Math.Round((mem.ullTotalPhys - mem.ullAvailPhys) / (1024.0 * 1024.0 * 1024.0), 1);
                    snap.RamLoad = (double)mem.dwMemoryLoad;

                    if (mem.ullTotalPageFile > 0) {
                        snap.PagefileTotalGb = Math.Round(mem.ullTotalPageFile / (1024.0 * 1024.0 * 1024.0), 1);
                        snap.PagefileUsedGb = Math.Round((mem.ullTotalPageFile - mem.ullAvailPageFile) / (1024.0 * 1024.0 * 1024.0), 1);
                        if (snap.PagefileTotalGb > 0) {
                            snap.PagefileLoad = Math.Round((snap.PagefileUsedGb.Value / snap.PagefileTotalGb.Value) * 100.0, 0);
                        }
                    }
                }

                // 2. Native CPU Load
                EnsureRunning();
                if (_cpuCounter != null) {
                    try {
                        float cpuVal = _cpuCounter.NextValue();
                        snap.CpuLoad = Math.Max(0, Math.Min(100, Math.Round((double)cpuVal, 0)));
                    } catch {}
                }

                // 3. Native CPU Name & Info from Registry
                string cpuName = RegistryHelper.GetString(RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString");
                if (!string.IsNullOrEmpty(cpuName)) {
                    snap.CpuName = UIHelper.CleanCpuName(cpuName);
                }

                // 4. Hardware Sensors: Core Temp Log (Tdie, Tccd, Package Power)
                ReadCoreTempLog(snap);

                // 5. Dynamic Metrics: Core Temp Shared Memory (Live Boost Clock, VID, & Instant Temp Fallback)
                ReadCoreTempSharedMemory(snap);

                // 6. Native CPU Clock Fallback from Registry (only if Core Temp is not ready)
                if (snap.CpuClock == null) {
                    int? mhz = RegistryHelper.GetDword(RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "~MHz");
                    if (mhz.HasValue && mhz.Value > 0) {
                        snap.CpuClock = mhz.Value;
                    }
                }

                _lastSnapshot = snap;
                return snap;
            } catch {
                return _lastSnapshot;
            }
        }


        private static string _coreTempLogHeader = null;
        private static int _coreTempLogTdieIdx = -1;
        private static int _coreTempLogTccdIdx = -1;
        private static int _coreTempLogPwrIdx = -1;
        private static string _cachedLogFilePath = null;
        private static DateTime _lastLogFileCheck = DateTime.MinValue;

        private static void ReadCoreTempLog(Snapshot snap) {
            try {
                bool needSearch = _cachedLogFilePath == null || !File.Exists(_cachedLogFilePath) || (DateTime.UtcNow - _lastLogFileCheck).TotalSeconds > 5;
                if (needSearch) {
                    _lastLogFileCheck = DateTime.UtcNow;
                    string cacheDir = GetSensorDir();
                    string bestFile = null;
                    DateTime bestTime = DateTime.MinValue;

                    if (Directory.Exists(cacheDir)) {
                        foreach (var f in Directory.GetFiles(cacheDir, "CT-Log*.csv")) {
                            try {
                                var fi = new FileInfo(f);
                                if (fi.LastWriteTimeUtc > bestTime && fi.Length > 60) {
                                    bestTime = fi.LastWriteTimeUtc;
                                    bestFile = f;
                                }
                            } catch {}
                        }
                    }

                    if (bestFile != null && _cachedLogFilePath != bestFile) {
                        _cachedLogFilePath = bestFile;
                        _coreTempLogHeader = null;
                        _coreTempLogTdieIdx = -1;
                        _coreTempLogTccdIdx = -1;
                        _coreTempLogPwrIdx = -1;
                    }
                }

                if (_cachedLogFilePath == null || !File.Exists(_cachedLogFilePath)) return;

                using (var fs = new FileStream(_cachedLogFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)) {
                    if (fs.Length < 60) return;

                    if (_coreTempLogHeader == null) {
                        using (var r = new StreamReader(fs, Encoding.UTF8, true, 2048, true)) {
                            for (int i = 0; i < 20; i++) {
                                string l = r.ReadLine();
                                if (l == null) break;
                                if (l.StartsWith("Time,Cur.", StringComparison.OrdinalIgnoreCase) || l.StartsWith("Time,", StringComparison.OrdinalIgnoreCase)) {
                                    _coreTempLogHeader = l;
                                    string[] h = l.Split(',');
                                    for (int k = 0; k < h.Length; k++) {
                                        string name = h[k].Trim();
                                        if (name.Equals("Cur. CPU #0 Tdie", StringComparison.OrdinalIgnoreCase)) _coreTempLogTdieIdx = k;
                                        else if (name.Equals("Cur. CPU #0 Tccd #0", StringComparison.OrdinalIgnoreCase)) _coreTempLogTccdIdx = k;
                                        else if (name.Equals("CPU #0 Package", StringComparison.OrdinalIgnoreCase)) _coreTempLogPwrIdx = k;
                                        else if (_coreTempLogTdieIdx < 0 && name.Equals("Cur. CPU #0 Core #0", StringComparison.OrdinalIgnoreCase)) _coreTempLogTdieIdx = k;
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    long seekPos = Math.Max(0, fs.Length - 1024);
                    fs.Seek(seekPos, SeekOrigin.Begin);
                    byte[] tailBuf = new byte[fs.Length - seekPos];
                    int bytesRead = fs.Read(tailBuf, 0, tailBuf.Length);
                    string tail = Encoding.UTF8.GetString(tailBuf, 0, bytesRead);

                    string[] lines = tail.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    if (lines.Length == 0) return;

                    for (int i = lines.Length - 1; i >= 0; i--) {
                        string line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line)) continue;
                        if (line.StartsWith("Time,", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("CPUID", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("Session", StringComparison.OrdinalIgnoreCase) ||
                            line.StartsWith("Core", StringComparison.OrdinalIgnoreCase)) continue;

                        string[] cols = line.Split(',');
                        if (cols.Length < 2) continue;

                        int tdieIdx = _coreTempLogTdieIdx >= 0 ? _coreTempLogTdieIdx : 5;
                        int tccdIdx = _coreTempLogTccdIdx >= 0 ? _coreTempLogTccdIdx : 1;
                        int pwrIdx = _coreTempLogPwrIdx >= 0 ? _coreTempLogPwrIdx : 9;

                        double tdie = 0, tccd = 0, pwr = 0;
                        if (tdieIdx < cols.Length) {
                            double v;
                            if (double.TryParse(cols[tdieIdx].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) tdie = v / 1000.0;
                        }
                        if (tccdIdx < cols.Length) {
                            double v;
                            if (double.TryParse(cols[tccdIdx].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) tccd = v / 1000.0;
                        }
                        if (tdie <= 0 && tccd > 0 && _coreTempLogTdieIdx < 0) {
                            tdie = tccd;
                        }
                        if (pwrIdx < cols.Length) {
                            double v;
                            if (double.TryParse(cols[pwrIdx].Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out v)) pwr = v / 1000.0;
                        }

                        double realTemp = Math.Max(tdie, tccd);
                        if (realTemp > 15 && realTemp < 130) {
                            snap.CpuTemp = Math.Round(realTemp, 1);
                        }
                        if (tdie > 15 && tdie < 130) {
                            snap.CpuTempDie = Math.Round(tdie, 1);
                        }
                        if (tccd > 15 && tccd < 130) {
                            snap.CpuTempCcd = Math.Round(tccd, 1);
                        }
                        if (pwr > 0 && pwr < 1000 && snap.CpuPower == null) {
                            snap.CpuPower = Math.Round(pwr, 0);
                        }

                        if (snap.CpuTemp != null) break;
                    }
                }
            } catch {}
        }

        private static void ReadCoreTempSharedMemory(Snapshot snap) {
            string[] mapNames = new string[] { "CoreTempMappingObjectEx", @"Global\CoreTempMappingObjectEx", "CoreTempMappingObject", @"Global\CoreTempMappingObject" };
            foreach (var mapName in mapNames) {
                try {
                    using (var mmf = MemoryMappedFile.OpenExisting(mapName, MemoryMappedFileRights.Read))
                    using (var accessor = mmf.CreateViewAccessor(0, 0, MemoryMappedFileAccess.Read)) {
                        if (accessor.Capacity < 2600) continue;

                        // 0. TjMax (uiTjMax[128] at offset 1024)
                        if (snap.CpuTjMax == null && accessor.Capacity >= 1536) {
                            uint tj = accessor.ReadUInt32(1024);
                            if (tj >= 60 && tj <= 115) snap.CpuTjMax = (double)tj;
                        }

                        // 1. Power (fPower[128] at offset 3204)
                        if (accessor.Capacity >= 3332 && snap.CpuPower == null) {
                            byte pwrSupp = accessor.ReadByte(2687);
                            uint ver = accessor.ReadUInt32(2688);
                            if (ver >= 2 && pwrSupp != 0) {
                                float pwr = accessor.ReadSingle(3204);
                                if (pwr > 0 && pwr < 1000) {
                                    snap.CpuPower = Math.Round((double)pwr, 0);
                                }
                            }
                        }

                        // 2. Voltage (float fVID at offset 2568)
                        if (snap.CpuVoltage == null) {
                            float vid = accessor.ReadSingle(2568);
                            if (vid > 0.5 && vid < 2.5) snap.CpuVoltage = Math.Round((double)vid, 3);
                        }

                        // 3. Boost Clock (float fCPUSpeed at offset 2572)
                        if (snap.CpuClock == null) {
                            float spd = accessor.ReadSingle(2572);
                            if (spd > 500 && spd < 10000) snap.CpuClock = Math.Round((double)spd, 0);
                        }

                        // 4. CPU Name (sCPUName[100] at offset 2584)
                        if (string.IsNullOrEmpty(snap.CpuName)) {
                            byte[] nameBytes = new byte[100];
                            accessor.ReadArray(2584, nameBytes, 0, 100);
                            string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ');
                            if (!string.IsNullOrEmpty(name)) snap.CpuName = UIHelper.CleanCpuName(name);
                        }
                        return;
                    }
                } catch { }
            }
        }
    }
}
