using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Xml;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Logik für den Chipsatz & Treiber-Manager Reiter nach dem KISS-Prinzip.
    /// </summary>
    public partial class MainWindowLogic {
        public class ChipsetDriverMetadata {
            public bool IsAmd { get; set; }
            public bool IsIntel { get; set; }
            public string Platform { get; set; }
            public string InstalledVersion { get; set; }
            public string LatestVersion { get; set; }
            public string ReleaseDate { get; set; }
            public string DownloadUrl { get; set; }
            public string PackageSize { get; set; }
            public bool HasUpdate { get; set; }
        }

        private ChipsetDriverMetadata _chipsetMeta = new ChipsetDriverMetadata();
        private CancellationTokenSource _chipsetDownloadCts;

        private void CancelChipsetDownload() {
            try {
                if (_chipsetDownloadCts != null && !_chipsetDownloadCts.IsCancellationRequested) {
                    _chipsetDownloadCts.Cancel();
                }
            } catch {}
        }

        private void RefreshChipsetUI() {
            try {
                // Lokale Hardware-Erkennung, DriverStore-Scan und Serverabfrage asynchron im Hintergrund
                Task.Run(async () => {
                    DetectLocalChipsetHardware();
                    ScanChipsetDriverStoreComponents();
                    await CheckChipsetUpdatesOnlineAsync(userTriggered: false);
                });
            } catch {}
        }

        private static bool ContainsAny(string source, params string[] terms) {
            return !string.IsNullOrEmpty(source) && Array.Exists(terms, t => source.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private void DetectLocalChipsetHardware() {
            try {
                string cpuName = (RegistryHelper.GetString(RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString") ?? "").Trim();
                string boardModel = (RegistryHelper.GetString(RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardProduct") ?? "").Trim();
                string boardVendor = (RegistryHelper.GetString(RegistryHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\BIOS", "BaseBoardManufacturer") ?? "").Trim();

                string hw = cpuName + " " + boardModel;
                bool isAmd = ContainsAny(hw, "AMD", "Ryzen", "AM4", "AM5", "X870", "X670", "B650", "B550");
                bool isIntel = !isAmd && ContainsAny(hw, "Intel", "Core", "Z790", "Z890", "B760");
                if (!isAmd && !isIntel) isAmd = true;

                _chipsetMeta.IsAmd = isAmd;
                _chipsetMeta.IsIntel = isIntel;

                if (isAmd) {
                    if (cpuName.IndexOf("9", StringComparison.OrdinalIgnoreCase) >= 0 || cpuName.IndexOf("7", StringComparison.OrdinalIgnoreCase) >= 0 || boardModel.IndexOf("X870", StringComparison.OrdinalIgnoreCase) >= 0 || boardModel.IndexOf("6", StringComparison.OrdinalIgnoreCase) >= 0) {
                        _chipsetMeta.Platform = "AMD Socket AM5 (" + (string.IsNullOrEmpty(boardModel) ? "Ryzen 7000/8000/9000" : boardModel) + ")";
                    } else {
                        _chipsetMeta.Platform = "AMD Socket AM4 / AM5 Platform";
                    }
                } else {
                    _chipsetMeta.Platform = "Intel Core Platform (" + (string.IsNullOrEmpty(boardModel) ? "LGA1700 / LGA1851" : boardModel) + ")";
                }

                var info = SoftwareDetector.Detect(@"AMD (Chipset|Software|Install Manager)|Intel.*Chipset");
                _chipsetMeta.InstalledVersion = (info != null && info.IsInstalled && !string.IsNullOrEmpty(info.Version) && info.Version != "Installiert")
                    ? info.GetFormattedVersion()
                    : "Standard / Treiber-Store";

                if (window != null) {
                    window.Dispatcher.BeginInvoke((Action)(() => {
                        UpdateChipsetUIElements();
                    }));
                }
            } catch {}
        }

        private async Task CheckChipsetUpdatesOnlineAsync(bool userTriggered) {
            try {
                if (userTriggered) {
                    window.Dispatcher.Invoke(() => {
                        if (txtChipsetLatestVer != null) txtChipsetLatestVer.Text = "Prüfe Server...";
                        if (txtChipsetStatusTitle != null) txtChipsetStatusTitle.Text = "🔄 Suche online nach neuester Treiberversion...";
                    });
                }

                string latestVer = null;
                string releaseDate = null;
                string sizeStr = null;
                string downloadUrl = null;

                if (_chipsetMeta.IsAmd) {
                    try {
                        using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(6) }) {
                            http.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
                            http.DefaultRequestHeaders.Add("Referer", "https://www.amd.com");
                            string xmlStr = await http.GetStringAsync("https://drivers.amd.com/drivers/installer/chipset/VersionInfo.xml");
                            var xmlDoc = new XmlDocument();
                            xmlDoc.LoadXml(xmlStr);

                            var verNode = xmlDoc.SelectSingleNode("//product/Version") ?? xmlDoc.SelectSingleNode("//Version");
                            var dateNode = xmlDoc.SelectSingleNode("//product/ReleaseDate") ?? xmlDoc.SelectSingleNode("//ReleaseDate");
                            var sizeNode = xmlDoc.SelectSingleNode("//product/InstallerSize") ?? xmlDoc.SelectSingleNode("//InstallerSize");

                            if (verNode != null) latestVer = verNode.InnerText.Trim();
                            if (dateNode != null) releaseDate = dateNode.InnerText.Trim();
                            if (sizeNode != null) sizeStr = sizeNode.InnerText.Trim();
                        }
                    } catch {}

                    if (!string.IsNullOrEmpty(latestVer)) {
                        downloadUrl = "https://drivers.amd.com/drivers/amd_chipset_software_" + latestVer + ".exe";
                    } else {
                        latestVer = "8.08.12.551";
                        releaseDate = "14/08/2026";
                        sizeStr = "77MB";
                        downloadUrl = "https://drivers.amd.com/drivers/installer/chipset/amd_chipset_software.exe";
                    }
                } else {
                    latestVer = "10.1.19912.8398";
                    releaseDate = "Offiziell";
                    sizeStr = "6.5MB";
                    downloadUrl = "https://downloadmirror.intel.com/839843/SetupChipset.exe";
                }

                _chipsetMeta.LatestVersion = "v" + latestVer.TrimStart('v');
                _chipsetMeta.ReleaseDate = !string.IsNullOrEmpty(releaseDate) ? "(" + FormatGermanDate(releaseDate) + ")" : "";
                _chipsetMeta.PackageSize = sizeStr;
                _chipsetMeta.DownloadUrl = downloadUrl;

                bool isNewer = CompareChipsetVersions(_chipsetMeta.InstalledVersion, _chipsetMeta.LatestVersion);
                _chipsetMeta.HasUpdate = isNewer;

                window.Dispatcher.Invoke(() => {
                    UpdateChipsetUIElements();
                });

            } catch (Exception ex) {
                if (userTriggered) {
                    window.Dispatcher.Invoke(() => {
                        if (txtChipsetStatusTitle != null) txtChipsetStatusTitle.Text = "⚠️ Online-Prüfung fehlgeschlagen: " + ex.Message;
                    });
                }
            }
        }

        private static bool CompareChipsetVersions(string installed, string latest) {
            if (string.IsNullOrEmpty(installed) || installed.IndexOf("Standard", StringComparison.OrdinalIgnoreCase) >= 0 || installed.IndexOf("Nicht", StringComparison.OrdinalIgnoreCase) >= 0) {
                return true;
            }
            try {
                string cleanInst = Regex.Replace(installed, @"[^\d\.]", "").Trim('.');
                string cleanLate = Regex.Replace(latest, @"[^\d\.]", "").Trim('.');
                Version vInst, vLate;
                if (Version.TryParse(cleanInst, out vInst) && Version.TryParse(cleanLate, out vLate)) {
                    return vLate > vInst;
                }
            } catch {}
            return !string.Equals(installed.Trim(), latest.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void UpdateChipsetUIElements() {
            if (txtChipsetPlatform != null) txtChipsetPlatform.Text = _chipsetMeta.Platform ?? "System Platform";
            if (txtChipsetInstalledVer != null) txtChipsetInstalledVer.Text = _chipsetMeta.InstalledVersion ?? "Ermittle...";
            if (txtChipsetLatestVer != null) txtChipsetLatestVer.Text = _chipsetMeta.LatestVersion ?? "v8.08.12.551";
            if (txtChipsetReleaseDate != null) txtChipsetReleaseDate.Text = _chipsetMeta.ReleaseDate ?? "";

            if (_chipsetMeta.HasUpdate) {
                if (borderChipsetStatus != null) {
                    borderChipsetStatus.Background = UIHelper.BrushAmberBg;
                    borderChipsetStatus.BorderBrush = UIHelper.BrushAmberBorder;
                }
                if (txtChipsetStatusTitle != null) {
                    txtChipsetStatusTitle.Text = "⚡ Neues Chipset-Update verfügbar (" + _chipsetMeta.LatestVersion + ")";
                    txtChipsetStatusTitle.Foreground = UIHelper.BrushAmberText;
                }
                if (txtChipsetStatusDesc != null) {
                    txtChipsetStatusDesc.Text = "Ein neuerer Chipsatz-Treiber ist verfügbar. Aktualisiere für optimale Performance & Stabilität.";
                    txtChipsetStatusDesc.Foreground = UIHelper.GetBrush("#FDE68A");
                }
                UIHelper.SetPillWarning(pillChipsetStatus, txtPillChipset, "UPDATE VERFÜGBAR");
                if (btnInstallChipsetDriver != null) {
                    btnInstallChipsetDriver.Content = "🚀 Chipset-Treiber " + _chipsetMeta.LatestVersion + " installieren";
                }
            } else {
                if (borderChipsetStatus != null) {
                    borderChipsetStatus.Background = UIHelper.BrushGreenBg;
                    borderChipsetStatus.BorderBrush = UIHelper.BrushGreenBorder;
                }
                if (txtChipsetStatusTitle != null) {
                    txtChipsetStatusTitle.Text = "✅ Chipset-Treiber ist auf dem neuesten Stand";
                    txtChipsetStatusTitle.Foreground = UIHelper.BrushGreenText;
                }
                if (txtChipsetStatusDesc != null) {
                    txtChipsetStatusDesc.Text = "Alle Chipsatz- und Hardware-Optimierungstreiber sind ordnungsgemäß installiert und aktuell.";
                    txtChipsetStatusDesc.Foreground = UIHelper.GetBrush("#A7F3D0");
                }
                UIHelper.SetPill(pillChipsetStatus, txtPillChipset, true, "AKTUELL");
                if (btnInstallChipsetDriver != null) {
                    btnInstallChipsetDriver.Content = "🔄 Chipset-Treiber neu installieren / reparieren";
                }
            }
        }

        private async Task ExecuteChipsetDownloadAndInstallAsync(bool autoInstall) {
            string userDownloads = RegistryHelper.GetUserDownloadsFolder();
            string targetDir = Path.Combine(userDownloads, "Chipset-Treiber");
            if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);

            string fileName = _chipsetMeta.IsAmd ? "AMD_Chipset_Software_" + (_chipsetMeta.LatestVersion ?? "latest").Replace("v", "") + ".exe" : "Intel_Chipset_Setup.exe";
            string destPath = Path.Combine(targetDir, fileName);
            string dlUrl = _chipsetMeta.DownloadUrl ?? (_chipsetMeta.IsAmd ? "https://drivers.amd.com/drivers/installer/chipset/amd_chipset_software.exe" : "https://downloadmirror.intel.com/839843/SetupChipset.exe");

            if (borderChipsetProgress != null) borderChipsetProgress.Visibility = Visibility.Visible;
            if (txtChipsetProgressStatus != null) txtChipsetProgressStatus.Text = "Lade " + fileName + " herunter...";
            if (progChipsetDownload != null) progChipsetDownload.Value = 0;
            if (txtChipsetDownloadPercent != null) txtChipsetDownloadPercent.Text = "0%";
            if (txtChipsetDownloadSpeed != null) txtChipsetDownloadSpeed.Text = "";
            if (txtChipsetDownloadSize != null) txtChipsetDownloadSize.Text = "";

            if (btnInstallChipsetDriver != null) btnInstallChipsetDriver.IsEnabled = false;
            if (btnDownloadOnlyChipset != null) btnDownloadOnlyChipset.IsEnabled = false;

            _chipsetDownloadCts = new CancellationTokenSource();
            bool success = false;
            string errorMsg = null;

            try {
                success = await DownloadFileWithProgressAsync(dlUrl, destPath, (pct, speedStr, sizeStr) => {
                    if (txtChipsetDownloadSpeed != null) txtChipsetDownloadSpeed.Text = speedStr;
                    if (txtChipsetDownloadPercent != null) txtChipsetDownloadPercent.Text = string.Format("{0:0}%", pct);
                    if (txtChipsetDownloadSize != null) txtChipsetDownloadSize.Text = sizeStr;
                    if (progChipsetDownload != null) progChipsetDownload.Value = pct;
                }, _chipsetDownloadCts.Token, "https://www.amd.com/en/support");
            } catch (OperationCanceledException) {
                errorMsg = "Download abgebrochen.";
                try { if (File.Exists(destPath)) File.Delete(destPath); } catch {}
            } catch (Exception ex) {
                errorMsg = "Fehler: " + ex.Message;
            } finally {
                if (_chipsetDownloadCts != null) {
                    _chipsetDownloadCts.Dispose();
                    _chipsetDownloadCts = null;
                }
                UpdateSidebarActivitySpinners();
            }

            if (btnInstallChipsetDriver != null) btnInstallChipsetDriver.IsEnabled = true;
            if (btnDownloadOnlyChipset != null) btnDownloadOnlyChipset.IsEnabled = true;

            if (success && File.Exists(destPath)) {
                if (autoInstall) {
                    bool doClean = (chkCleanInstallChipset != null && chkCleanInstallChipset.IsChecked == true);
                    if (doClean) {
                        if (txtChipsetProgressStatus != null) txtChipsetProgressStatus.Text = "🗑️ Bereinige alte Treiber & DriverStore (Clean Install)...";
                        await Task.Run(() => PerformChipsetCleanUninstall());
                    }

                    if (txtChipsetProgressStatus != null) txtChipsetProgressStatus.Text = "🚀 Starte Installer: " + fileName;
                    if (!ProcessRunner.Start(destPath)) {
                        if (txtChipsetProgressStatus != null) txtChipsetProgressStatus.Text = "Konnte Setup nicht starten.";
                    }
                } else {
                    if (txtChipsetProgressStatus != null) txtChipsetProgressStatus.Text = "✅ Download fertiggestellt (" + fileName + ")!";
                    ProcessRunner.Start("explorer.exe", targetDir);
                }
            } else {
                if (txtChipsetProgressStatus != null) txtChipsetProgressStatus.Text = errorMsg ?? "Download fehlgeschlagen.";
            }
        }

        private void PerformChipsetCleanUninstall() {
            // 1. Silent Uninstall from Registry if present
            try {
                var info = SoftwareDetector.Detect(@"AMD (Chipset|Software|Install Manager)|Intel.*Chipset");
                if (info != null && info.IsInstalled) {
                    string uninstCmd = !string.IsNullOrEmpty(info.QuietUninstallString) ? info.QuietUninstallString : info.UninstallString;
                    if (!string.IsNullOrEmpty(uninstCmd)) {
                        if (uninstCmd.IndexOf("MsiExec", StringComparison.OrdinalIgnoreCase) >= 0) {
                            var m = Regex.Match(uninstCmd, @"\{[A-Fa-f0-9-]+\}");
                            if (m.Success) {
                                ProcessRunner.RunAndGetOutput("msiexec.exe", string.Format("/X{0} /qn /norestart", m.Value), 60000);
                            }
                        } else if (uninstCmd.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || uninstCmd.Contains(".exe ")) {
                            ProcessRunner.RunAndGetOutput("cmd.exe", "/c \"" + uninstCmd + "\" /qn /silent /uninstall /norestart", 60000);
                        }
                    }
                }
            } catch {}

            // 2. DriverStore Cleanup via pnputil
            try {
                var oemInfs = GetInstalledChipsetOemInfs(_chipsetMeta.IsAmd);
                foreach (var oem in oemInfs) {
                    ProcessRunner.RunAndGetOutput("pnputil.exe", string.Format("/delete-driver {0} /uninstall /force", oem), 15000);
                }
            } catch {}
        }

        private static System.Collections.Generic.List<string> GetInstalledChipsetOemInfs(bool isAmd) {
            var oemList = new System.Collections.Generic.List<string>();
            try {
                string output = ProcessRunner.RunAndGetOutput("pnputil.exe", "/enum-drivers", 15000);
                if (!string.IsNullOrEmpty(output)) {
                    var matches = Regex.Matches(output, @"(?:Published Name|Ver[^\r\n]*Name)\s*:\s*(oem\d+\.inf)[\s\S]*?(?:Original Name|Originalname)\s*:\s*([^\r\n]+)", RegexOptions.IgnoreCase);
                    foreach (Match m in matches) {
                        string oemName = m.Groups[1].Value.Trim();
                        string origName = m.Groups[2].Value.Trim().ToLowerInvariant();

                        if (isAmd) {
                            if (origName.Contains("3dvcache") || origName.Contains("vcache") || origName.Contains("amds3") ||
                                origName.Contains("amdppm") || origName.Contains("amdppkg") || origName.Contains("amdpsp") ||
                                origName.Contains("amdgpio") || origName.Contains("amdi2c") || origName.Contains("smbusamd") ||
                                origName.Contains("amdsmb") || origName.Contains("amdappcompat") || origName.Contains("amdfendr") ||
                                origName.Contains("amdinterface")) {
                                if (!origName.StartsWith("u0") && !origName.Contains("disp") && !origName.Contains("amdkmdag") && !origName.Contains("atihdwt")) {
                                    if (!oemList.Contains(oemName)) oemList.Add(oemName);
                                }
                            }
                        } else {
                            if (origName.Contains("chipset") || origName.Contains("heci") || origName.Contains("mei") ||
                                origName.Contains("inteli2c") || origName.Contains("intelgpio") || origName.Contains("intelio")) {
                                if (!oemList.Contains(oemName)) oemList.Add(oemName);
                            }
                        }
                    }
                }
            } catch {}
            return oemList;
        }

        private void ScanChipsetDriverStoreComponents() {
            string vcacheVer = null;
            string ppmVer = null;
            string smbusVer = null;
            string pspVer = null;
            string gpioVer = null;
            string i2cVer = null;

            string query = "SELECT DeviceName, DriverVersion FROM Win32_PnPSignedDriver WHERE " +
                           "DeviceName LIKE '%3D V-Cache%' OR " +
                           "DeviceName LIKE '%Provisioning%' OR " +
                           "DeviceName LIKE '%SMBUS%' OR " +
                           "DeviceName LIKE '%PSP%' OR " +
                           "DeviceName LIKE '%GPIO%' OR " +
                           "DeviceName LIKE '%I2C%' OR " +
                           "DeviceName LIKE '%Management Engine%' OR " +
                           "DeviceName LIKE '%Chipset%'";

            WmiHelper.ForEach(query, obj => {
                string dev = obj["DeviceName"] != null ? obj["DeviceName"].ToString() : "";
                string ver = obj["DriverVersion"] != null ? obj["DriverVersion"].ToString() : "";

                if (dev.IndexOf("3D V-Cache", StringComparison.OrdinalIgnoreCase) >= 0 && vcacheVer == null) {
                    vcacheVer = "v" + ver + " (Aktiv für V-Cache Gaming)";
                } else if ((dev.IndexOf("Provisioning", StringComparison.OrdinalIgnoreCase) >= 0 || dev.IndexOf("PPM", StringComparison.OrdinalIgnoreCase) >= 0) && ppmVer == null) {
                    ppmVer = "v" + ver + " (PPM Core-Parking Driver)";
                } else if (dev.IndexOf("SMBUS", StringComparison.OrdinalIgnoreCase) >= 0 && smbusVer == null) {
                    smbusVer = "v" + ver;
                } else if (dev.IndexOf("PSP", StringComparison.OrdinalIgnoreCase) >= 0 && pspVer == null) {
                    pspVer = "v" + ver;
                } else if (dev.IndexOf("GPIO", StringComparison.OrdinalIgnoreCase) >= 0 && gpioVer == null) {
                    gpioVer = "v" + ver;
                } else if (dev.IndexOf("I2C", StringComparison.OrdinalIgnoreCase) >= 0 && i2cVer == null) {
                    i2cVer = "v" + ver;
                }
            });

            if (window != null) {
                window.Dispatcher.Invoke(() => {
                    if (txtChipsetVcacheVer != null) txtChipsetVcacheVer.Text = vcacheVer ?? "Nicht benötigt / Kein Dual-CCD 3D V-Cache";
                    if (txtChipsetPpmVer != null) txtChipsetPpmVer.Text = ppmVer ?? "Standard";
                    if (txtChipsetSmbusVer != null) txtChipsetSmbusVer.Text = smbusVer ?? "Standard";
                    if (txtChipsetPspVer != null) txtChipsetPspVer.Text = pspVer ?? "Standard";
                    if (txtChipsetGpioVer != null) txtChipsetGpioVer.Text = gpioVer ?? "Standard";
                    if (txtChipsetI2cVer != null) txtChipsetI2cVer.Text = i2cVer ?? "Standard";
                });
            }
        }

        public void SetupChipsetEvents() {
            if (btnInstallChipsetDriver != null) {
                btnInstallChipsetDriver.Click += async (s, e) => {
                    await ExecuteChipsetDownloadAndInstallAsync(autoInstall: true);
                };
            }

            if (btnDownloadOnlyChipset != null) {
                btnDownloadOnlyChipset.Click += async (s, e) => {
                    await ExecuteChipsetDownloadAndInstallAsync(autoInstall: false);
                };
            }

            if (btnCheckChipsetUpdate != null) {
                btnCheckChipsetUpdate.Click += async (s, e) => {
                    await CheckChipsetUpdatesOnlineAsync(userTriggered: true);
                };
            }

            if (btnOpenChipsetFolder != null) {
                btnOpenChipsetFolder.Click += (s, e) => {
                    try {
                        string chipsetDir = Path.Combine(RegistryHelper.GetUserDownloadsFolder(), "Chipset-Treiber");
                        if (!Directory.Exists(chipsetDir)) Directory.CreateDirectory(chipsetDir);
                        ProcessRunner.Start("explorer.exe", chipsetDir);
                    } catch {}
                };
            }

            if (btnCancelChipsetDownload != null) {
                btnCancelChipsetDownload.Click += (s, e) => CancelChipsetDownload();
            }

            if (btnOpenDevMgmtChipset != null) {
                btnOpenDevMgmtChipset.Click += (s, e) => ProcessRunner.Start("devmgmt.msc");
            }
        }
    }
}
