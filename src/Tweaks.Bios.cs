using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Logik für den BIOS / Mainboard Reiter nach dem KISS-Prinzip.
    /// </summary>
    public partial class MainWindowLogic {
        // =========================================================================
        // NATIVE BIOS LIVE-STATUS ABFRAGE (SECURE BOOT, RAM EXPO/XMP, WLAN/BT, iGPU)
        // =========================================================================
        private void RefreshBiosUI() {
            try {
                // 1. Secure Boot Live Status
                try {
                    bool isSecureBoot = (RegistryHelper.GetDword(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\SecureBoot\State", "UEFISecureBootEnabled") ?? 0) == 1;
                    if (txtLiveSecureBoot != null) {
                        txtLiveSecureBoot.Text = isSecureBoot ? "Aktiv" : "Nicht aktiv";
                        txtLiveSecureBoot.Foreground = isSecureBoot ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                    }
                    UIHelper.SetPill(pillLiveSecureBoot, txtPillLiveSecureBoot, isSecureBoot, "AKTIV", "NICHT AKTIV");
                } catch {
                    if (txtLiveSecureBoot != null) txtLiveSecureBoot.Text = "Unbekannt";
                }

                // 2. Resizable BAR (ReBAR / SAM) Live Status
                try {
                    bool isRebar = false;
                    string rebarDetail = "";
                    if (isNvmlInitialized && nvmlDeviceHandle != IntPtr.Zero) {
                        NvmlBAR1Memory bar1;
                        if (nvmlDeviceGetBAR1MemoryInfo(nvmlDeviceHandle, out bar1) == 0 && bar1.total > 0) {
                            ulong totalMb = bar1.total / (1024UL * 1024UL);
                            isRebar = totalMb > 1024;
                            rebarDetail = isRebar ? "Aktiv" : "Nicht aktiv";
                        }
                    }
                    if (string.IsNullOrEmpty(rebarDetail)) {
                        rebarDetail = isRebar ? "Aktiv" : "Nicht aktiv";
                    }
                    if (txtLiveRebar != null) {
                        txtLiveRebar.Text = " " + rebarDetail;
                        txtLiveRebar.Foreground = isRebar ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                    }
                    UIHelper.SetPill(pillLiveRebar, txtPillLiveRebar, isRebar, "AKTIV", "NICHT AKTIV");
                } catch {
                    if (txtLiveRebar != null) txtLiveRebar.Text = " Unbekannt";
                }

                // 3. iGPU Live Status Initial
                if (txtLiveIgpu != null) txtLiveIgpu.Text = " Ermittle Status...";

                // 4. Asynchroner WLAN, LAN, BT & iGPU Scan (non-blocking)
                Task.Run(() => {
                    bool lanActive = false;
                    bool wifiActive = false;
                    bool wifiPresent = false;

                    try {
                        foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
                            if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Loopback ||
                                ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Tunnel) continue;

                            string desc = (ni.Description ?? "").ToLowerInvariant();
                            string name = (ni.Name ?? "").ToLowerInvariant();

                            // Virtuelle Adapter / Tunnel ausfiltern
                            if (desc.Contains("virtual") || desc.Contains("hyper-v") || desc.Contains("tap-") ||
                                desc.Contains("vpn") || desc.Contains("wsl") || desc.Contains("vmware") || desc.Contains("vethernet")) continue;

                            bool isWifi = ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Wireless80211 ||
                                          desc.Contains("wi-fi") || desc.Contains("wireless") || desc.Contains("802.11") || desc.Contains("wlan");

                            if (isWifi) {
                                wifiPresent = true;
                                if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up) {
                                    wifiActive = true;
                                }
                            } else if (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet ||
                                       desc.Contains("ethernet") || desc.Contains("gigabit") || desc.Contains("i226") || desc.Contains("i225") || desc.Contains("realtek")) {
                                if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up) {
                                    lanActive = true;
                                }
                            }
                        }
                    } catch {}

                    // WMI-Fallback für Wi-Fi (falls z.B. in Windows deaktiviert)
                    if (!wifiPresent) {
                        try {
                            WmiHelper.ForEach("SELECT Name, NetEnabled FROM Win32_NetworkAdapter WHERE (Name LIKE '%Wi-Fi%' OR Name LIKE '%Wireless%' OR Name LIKE '%802.11%' OR AdapterType LIKE '%Wireless%') AND PhysicalAdapter = True", obj => {
                                wifiPresent = true;
                                object netEn = obj["NetEnabled"];
                                if (netEn != null && (bool)netEn) {
                                    wifiActive = true;
                                }
                            });
                        } catch {}
                    }

                    // Bluetooth-Erkennung
                    bool btActive = false;
                    try {
                        WmiHelper.ForEach("SELECT Name, Present FROM Win32_PnPEntity WHERE PNPClass = 'Bluetooth'", obj => {
                            bool present = obj["Present"] != null && (bool)obj["Present"];
                            string name = obj["Name"] != null ? obj["Name"].ToString() : "";
                            if (present && (name.IndexOf("Adapter", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("Radio", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                            name.IndexOf("Wireless", StringComparison.OrdinalIgnoreCase) >= 0)) {
                                btActive = true;
                            }
                        });
                    } catch {}

                    // iGPU-Erkennung
                    bool dGpuPresent = false;
                    bool iGpuActive = false;
                    string iGpuName = "";
                    bool iGpuDevMgrDisabled = false;

                    try {
                        WmiHelper.ForEach("SELECT Name, ConfigManagerErrorCode FROM Win32_VideoController", obj => {
                            string name = obj["Name"] != null ? obj["Name"].ToString().Trim() : "";
                            if (string.IsNullOrEmpty(name)) return;

                            uint errCode = 0;
                            if (obj["ConfigManagerErrorCode"] != null) uint.TryParse(obj["ConfigManagerErrorCode"].ToString(), out errCode);

                            if (IsIntegratedGpuName(name)) {
                                if (errCode == 22) {
                                    iGpuDevMgrDisabled = true;
                                    if (string.IsNullOrEmpty(iGpuName)) iGpuName = name;
                                } else {
                                    iGpuActive = true;
                                    iGpuName = name;
                                }
                            } else if (IsDiscreteGpuName(name)) {
                                dGpuPresent = true;
                            }
                        });
                    } catch {}

                    string installedIgpuDesc = "";
                    if (!iGpuActive) {
                        try {
                            for (int i = 0; i < 8; i++) {
                                string subkey = string.Format(@"SYSTEM\CurrentControlSet\Control\Class\{{4d36e968-e325-11ce-bfc1-08002be10318}}\{0:D4}", i);
                                using (var key = Registry.LocalMachine.OpenSubKey(subkey)) {
                                    if (key != null) {
                                        object descObj = key.GetValue("DriverDesc");
                                        if (descObj != null) {
                                            string d = descObj.ToString().Trim();
                                            if (IsIntegratedGpuName(d)) {
                                                installedIgpuDesc = d;
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                        } catch {}
                    }

                    bool cpuHasIgpu = false;
                    if (!iGpuActive && string.IsNullOrEmpty(installedIgpuDesc)) {
                        try {
                            string cpuName = (RegistryHelper.GetString(RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "").Trim();
                            cpuHasIgpu = CpuHasIgpuHardware(cpuName);
                        } catch {}
                    }

                    if (window != null) {
                        window.Dispatcher.Invoke(() => {
                            try {
                                // 1. WLAN
                                if (txtLiveWlan != null) {
                                    if (wifiActive) {
                                        if (lanActive) {
                                            txtLiveWlan.Text = "Aktiv (LAN bevorzugt)";
                                            txtLiveWlan.Foreground = UIHelper.BrushAmberText;
                                            if (txtDescWlan != null) txtDescWlan.Text = "LAN-Kabel ist aktiv verbunden. WLAN sollte im BIOS oder Windows deaktiviert werden, um parallele Funk-Treiberlast zu vermeiden.";
                                            UIHelper.SetPillWarning(pillLiveWlan, txtPillLiveWlan, "AKTIV");
                                        } else {
                                            txtLiveWlan.Text = "Aktiv (In Benutzung)";
                                            txtLiveWlan.Foreground = UIHelper.BrushGreenText;
                                            if (txtDescWlan != null) txtDescWlan.Text = "WLAN wird aktiv als Internetverbindung genutzt (LAN nicht verbunden).";
                                            UIHelper.SetPill(pillLiveWlan, txtPillLiveWlan, true, "IN BENUTZUNG");
                                        }
                                    } else {
                                        txtLiveWlan.Text = "Deaktiviert";
                                        txtLiveWlan.Foreground = UIHelper.BrushGreenText;
                                        if (txtDescWlan != null) {
                                            txtDescWlan.Text = lanActive
                                                ? "LAN-Verbindung ist aktiv (Optimal: Onboard-WLAN im BIOS deaktiviert für minimale DPC-Latenzen)."
                                                : "Onboard-WLAN ist deaktiviert.";
                                        }
                                        UIHelper.SetPill(pillLiveWlan, txtPillLiveWlan, true, "DEAKTIVIERT");
                                    }
                                }

                                // 2. Bluetooth
                                if (txtLiveBt != null) {
                                    if (!btActive) {
                                        txtLiveBt.Text = "Deaktiviert";
                                        txtLiveBt.Foreground = UIHelper.BrushGreenText;
                                        if (txtDescBt != null) txtDescBt.Text = "Bluetooth-Funkmodul ist deaktiviert (Hinweis: Ggfls. aktiv lassen, falls kabellose Controller, Headsets etc. genutzt werden).";
                                        UIHelper.SetPill(pillLiveBt, txtPillLiveBt, true, "DEAKTIVIERT");
                                    } else {
                                        txtLiveBt.Text = "Aktiv";
                                        txtLiveBt.Foreground = UIHelper.BrushGreenText;
                                        if (txtDescBt != null) txtDescBt.Text = "Aktiviert (Empfohlen falls kabellose Controller, Headsets oder BT-Geräte genutzt werden).";
                                        UIHelper.SetPill(pillLiveBt, txtPillLiveBt, true, "AKTIV");
                                    }
                                }

                                // 3. iGPU (Integrierte Grafikeinheit)
                                if (txtLiveIgpu != null) {
                                    if (iGpuActive) {
                                        if (dGpuPresent) {
                                            txtLiveIgpu.Text = "Aktiviert (" + (!string.IsNullOrEmpty(iGpuName) ? iGpuName : "iGPU") + ")";
                                            txtLiveIgpu.Foreground = UIHelper.BrushAmberText;
                                            if (txtDescIgpu != null) {
                                                txtDescIgpu.Text = "iGPU ist parallel zur dGPU aktiv. Für maximale Gaming-FPS, geringere SoC-Hitze & freie RAM-Bandbreite im BIOS deaktivieren.";
                                            }
                                            UIHelper.SetPillWarning(pillLiveIgpu, txtPillLiveIgpu, "AKTIV");
                                        } else {
                                            txtLiveIgpu.Text = "Aktiv (Hauptgrafikkarte)";
                                            txtLiveIgpu.Foreground = UIHelper.BrushGreenText;
                                            if (txtDescIgpu != null) {
                                                txtDescIgpu.Text = "Die integrierte Prozessorgrafik (" + (!string.IsNullOrEmpty(iGpuName) ? iGpuName : "iGPU") + ") wird als primäre Grafikkarte genutzt.";
                                            }
                                            UIHelper.SetPill(pillLiveIgpu, txtPillLiveIgpu, true, "IN BENUTZUNG");
                                        }
                                    } else if (iGpuDevMgrDisabled) {
                                        txtLiveIgpu.Text = "Deaktiviert (Geräte-Manager)";
                                        txtLiveIgpu.Foreground = UIHelper.BrushGreenText;
                                        if (txtDescIgpu != null) {
                                            txtDescIgpu.Text = "Die Onboard-Grafik ist in Windows deaktiviert (Optimaler: Im BIOS komplett abschalten).";
                                        }
                                        UIHelper.SetPill(pillLiveIgpu, txtPillLiveIgpu, true, "DEAKTIVIERT");
                                    } else if (!string.IsNullOrEmpty(installedIgpuDesc) || cpuHasIgpu) {
                                        txtLiveIgpu.Text = "Deaktiviert (Im BIOS)";
                                        txtLiveIgpu.Foreground = UIHelper.BrushGreenText;
                                        if (txtDescIgpu != null) {
                                            string detail = !string.IsNullOrEmpty(installedIgpuDesc) ? " (" + installedIgpuDesc + ")" : "";
                                            txtDescIgpu.Text = "Optimal: Onboard-Grafik" + detail + " ist im BIOS vollständig deaktiviert. Volle SoC-Power für CPU & keine Treiberlast.";
                                        }
                                        UIHelper.SetPill(pillLiveIgpu, txtPillLiveIgpu, true, "DEAKTIVIERT");
                                    } else {
                                        txtLiveIgpu.Text = "Nicht vorhanden (CPU ohne iGPU)";
                                        txtLiveIgpu.Foreground = UIHelper.BrushGrayText;
                                        if (txtDescIgpu != null) {
                                            txtDescIgpu.Text = "Der verbaute Prozessor verfügt hardwareseitig über keine integrierte Grafikeinheit.";
                                        }
                                        UIHelper.SetPill(pillLiveIgpu, txtPillLiveIgpu, false, "NICHT VORHANDEN", "NICHT VORHANDEN");
                                    }
                                }
                            } catch {}
                        });
                    }
                });

            } catch {
                if (txtLiveSecureBoot != null) txtLiveSecureBoot.Text = "Unbekannt";
                if (txtLiveIgpu != null) txtLiveIgpu.Text = "Unbekannt";
            }
        }

        private static bool IsIntegratedGpuName(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.Contains("remote display") || n.Contains("virtual") || n.Contains("basic render") || n.Contains("basic display")) return false;

            if (n.Contains("intel")) {
                bool isIntelDiscrete = n.Contains("arc") && (
                    n.Contains("a310") || n.Contains("a380") || n.Contains("a580") || n.Contains("a750") || n.Contains("a770") ||
                    n.Contains("b570") || n.Contains("b580") || n.Contains("pro a") || n.Contains("pro b")
                );
                if (!isIntelDiscrete) return true;
                return false;
            }

            if (n.Contains("radeon") || n.Contains("amd")) {
                if (n.Contains("radeon(tm) graphics") || n.Contains("radeon graphics")) return true;
                if (n.Contains("vega") || n.Contains("780m") || n.Contains("760m") || n.Contains("740m") || 
                    n.Contains("680m") || n.Contains("660m") || n.Contains("610m") || n.Contains("880m") || n.Contains("890m")) return true;
                if (n.Contains("r7 graphics") || n.Contains("r5 graphics") || n.Contains("r4 graphics") || n.Contains("r3 graphics") || n.Contains("r2 graphics")) return true;
                return false;
            }

            if (n.Contains("adreno")) return true;
            return false;
        }

        private static bool IsDiscreteGpuName(string name) {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.Contains("remote display") || n.Contains("virtual") || n.Contains("basic render") || n.Contains("basic display")) return false;

            if (n.Contains("nvidia") || n.Contains("geforce") || n.Contains("rtx") || n.Contains("gtx")) return true;

            if (n.Contains("radeon") || n.Contains("amd")) {
                if (n.Contains("rx ") || n.Contains("pro ") || n.Contains("xt") || n.Contains("firepro") || n.Contains("instinct")) {
                    if (!n.Contains("radeon graphics") && !n.Contains("radeon(tm) graphics")) return true;
                }
            }

            if (n.Contains("intel") && n.Contains("arc") && (
                n.Contains("a310") || n.Contains("a380") || n.Contains("a580") || n.Contains("a750") || n.Contains("a770") ||
                n.Contains("b570") || n.Contains("b580") || n.Contains("pro a") || n.Contains("pro b"))) return true;

            return false;
        }

        private static bool CpuHasIgpuHardware(string cpuName) {
            if (string.IsNullOrEmpty(cpuName)) return false;
            string c = Regex.Replace(cpuName, @"\([^\)]*\)", "").ToLowerInvariant();

            if (c.Contains("amd") || c.Contains("ryzen")) {
                if (Regex.IsMatch(c, @"ryzen\s+[3579]\s+(7|8|9)\d{3}f\b")) return false;
                if (Regex.IsMatch(c, @"ryzen\s+[3579]\s+(7|8|9)\d{3}")) return true;
                if (c.Contains("ryzen ai")) return true;
                if (Regex.IsMatch(c, @"ryzen\s+[3579]\s+\d{4}ge?\b")) return true;
                if (c.Contains("athlon") && (c.Contains(" g") || c.Contains("ge"))) return true;
                return false;
            }

            if (c.Contains("intel") || c.Contains("core")) {
                if (Regex.IsMatch(c, @"ultra\s+[3579]\s+\d+(kf|f)\b")) return false;
                if (c.Contains("core ultra")) return true;
                if (Regex.IsMatch(c, @"i[3579]-?\d{4,5}(kf|f)\b")) return false;
                if (Regex.IsMatch(c, @"i[3579]-?\d{4,5}")) return true;
                if (c.Contains("celeron") || c.Contains("pentium")) return true;
                return false;
            }

            return false;
        }
    }
}
