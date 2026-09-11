using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZnipeOptimizationTool {
    public partial class MainWindowLogic {
        // =========================================================================
        // LIVE APPS & DOWNLOADS (OFFICE-ANALOG DESIGN & LOGIC)
        // =========================================================================

        private static readonly string[] AllAppKeys = new[] { "Steam", "Discord", "StreamDeck", "BleachBit", "Antigravity", "EVGA", "Hasleo" };
        private readonly HashSet<string> _appsWithAvailableUpdates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _latestKnownVersions = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "EVGA", "1.3.7.0" },
            { "Hasleo", "6.3" },
            { "BleachBit", "4.6.2" },
            { "StreamDeck", "7.5.1" },
            { "Antigravity", "1.1.22" }
        };
        private readonly Dictionary<string, string> _latestKnownDates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) {
            { "EVGA", "21.10.2022" },
            { "Hasleo", "12.01.2025" },
            { "BleachBit", "27.03.2024" },
            { "StreamDeck", "04.02.2025" },
            { "Antigravity", "10.02.2025" }
        };

        private static bool IsNewerVersion(string installedVer, string latestVer) {
            if (string.IsNullOrEmpty(installedVer) || string.IsNullOrEmpty(latestVer)) return false;
            string cInst = installedVer.Trim().TrimStart('v', 'V').TrimEnd('.');
            string cLatest = latestVer.Trim().TrimStart('v', 'V').TrimEnd('.');
            if (cInst.Equals(cLatest, StringComparison.OrdinalIgnoreCase)) return false;

            string normInst = Regex.Replace(cInst, @"[^\d\.]", "");
            string normLatest = Regex.Replace(cLatest, @"[^\d\.]", "");
            if (normInst.Equals(normLatest, StringComparison.OrdinalIgnoreCase)) return false;

            try {
                string[] p1 = normInst.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                string[] p2 = normLatest.Split(new[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                int max = Math.Max(p1.Length, p2.Length);
                long n1, n2;
                for (int i = 0; i < max; i++) {
                    long v1 = (i < p1.Length && long.TryParse(p1[i], out n1)) ? n1 : 0;
                    long v2 = (i < p2.Length && long.TryParse(p2[i], out n2)) ? n2 : 0;
                    if (v2 > v1) return true;
                    if (v2 < v1) return false;
                }
                return false;
            } catch {}
            return !cInst.Equals(cLatest, StringComparison.OrdinalIgnoreCase);
        }

        private class AppItem {
            public string Key { get; set; }
            public string Name { get; set; }
            public string BaseDesc { get; set; }
            public string Pattern { get; set; }
            public string[] Exes { get; set; }
            public string[] Dirs { get; set; }
            public string WingetId { get; set; }
            public Func<Task<string>> ResolveUrl { get; set; }
            public string FallbackFileName { get; set; }
            public CheckBox CheckBox { get; set; }
            public Border Pill { get; set; }
            public TextBlock TxtPill { get; set; }
            public TextBlock TxtDesc { get; set; }
        }

        private List<AppItem> GetApps() {
            return new List<AppItem> {
                new AppItem {
                    Key = "Steam", Name = "Steam", BaseDesc = "Offizieller Gaming Client & Spielebibliothek", Pattern = @"^Steam$",
                    Exes = new[] { "Steam.exe" }, Dirs = new[] { "Steam", @"Valve\Steam" }, WingetId = "Valve.Steam",
                    ResolveUrl = () => Task.FromResult("https://cdn.akamai.steamstatic.com/client/installer/SteamSetup.exe"),
                    FallbackFileName = "SteamSetup.exe",
                    CheckBox = chkSteam, Pill = pillAppSteam, TxtPill = txtPillAppSteam, TxtDesc = txtDescAppSteam
                },
                new AppItem {
                    Key = "Discord", Name = "Discord", BaseDesc = "Voice, Video & Chat Plattform für Gamer", Pattern = @"Discord",
                    Exes = new[] { "Discord.exe", "Update.exe" }, Dirs = new[] { "Discord" }, WingetId = "Discord.Discord",
                    ResolveUrl = () => Task.FromResult("https://discord.com/api/download?platform=win"),
                    FallbackFileName = "DiscordSetup.exe",
                    CheckBox = chkDiscord, Pill = pillAppDiscord, TxtPill = txtPillAppDiscord, TxtDesc = txtDescAppDiscord
                },
                new AppItem {
                    Key = "StreamDeck", Name = "Elgato Stream Deck", BaseDesc = "Hardware-Steuerung & Plugin-Software", Pattern = @"Stream Deck|Elgato",
                    Exes = new[] { "StreamDeck.exe" }, Dirs = new[] { @"Elgato\StreamDeck", "StreamDeck" }, WingetId = "Elgato.StreamDeck",
                    ResolveUrl = () => ResolveLatestStreamDeckUrlAsync(),
                    FallbackFileName = "Stream_Deck_Setup.msi",
                    CheckBox = chkStreamDeck, Pill = pillAppStreamDeck, TxtPill = txtPillAppStreamDeck, TxtDesc = txtDescAppStreamDeck
                },
                new AppItem {
                    Key = "BleachBit", Name = "BleachBit Cleaner", BaseDesc = "Open-Source Cache & Temp-Dateien Bereinigung", Pattern = @"BleachBit",
                    Exes = new[] { "bleachbit.exe" }, Dirs = new[] { "BleachBit" }, WingetId = "BleachBit.BleachBit",
                    ResolveUrl = () => ResolveLatestBleachBitUrlAsync(),
                    FallbackFileName = "BleachBit_Setup.exe",
                    CheckBox = chkBleachBit, Pill = pillAppBleachBit, TxtPill = txtPillAppBleachBit, TxtDesc = txtDescAppBleachBit
                },
                new AppItem {
                    Key = "Antigravity", Name = "Google Antigravity", BaseDesc = "Offizielle Google Next-Gen Coding Suite & Agent Engine", Pattern = @"Antigravity",
                    Exes = new[] { "Antigravity.exe" }, Dirs = new[] { @"Programs\Antigravity", @"Google\Antigravity", "Antigravity" }, WingetId = "Google.Antigravity",
                    ResolveUrl = () => ResolveLatestAntigravityUrlAsync(),
                    FallbackFileName = "Antigravity_Setup.exe",
                    CheckBox = chkAntigravity, Pill = pillAppAntigravity, TxtPill = txtPillAppAntigravity, TxtDesc = txtDescAppAntigravity
                },
                new AppItem {
                    Key = "EVGA", Name = "EVGA Precision X1", BaseDesc = "GPU Tuning, Übertaktung & Sensor-Monitor", Pattern = @"EVGA Precision|Precision X1",
                    Exes = new[] { "PrecisionX_x64.exe", "PrecisionX1.exe", "PrecisionX.exe" }, Dirs = new[] { @"EVGA\Precision X1", "Precision X1" }, WingetId = "",
                    ResolveUrl = () => Task.FromResult("https://www.evga.com/filescdn/software/EVGA_Precision_X1_1.3.7.0.exe"),
                    FallbackFileName = "EVGA_Precision_X1_1.3.7.0.exe",
                    CheckBox = chkEvga, Pill = pillAppEvga, TxtPill = txtPillAppEvga, TxtDesc = txtDescAppEvga
                },
                new AppItem {
                    Key = "Hasleo", Name = "Hasleo Backup Suite Free", BaseDesc = "Windows Backup, System-Image & Festplatten-Klonen", Pattern = @"Hasleo Backup Suite|Hasleo",
                    Exes = new[] { "BackupMainUI.exe", "AppLoader.exe", "HasleoBackupSuite.exe" }, Dirs = new[] { @"Hasleo\Hasleo Backup Suite", "Hasleo Backup Suite" }, WingetId = "Hasleo.BackupSuiteFree",
                    ResolveUrl = () => Task.FromResult("https://www.easyuefi.com/backup-software/downloads/Hasleo_Backup_Suite_Free.exe"),
                    FallbackFileName = "Hasleo_Backup_Suite_Free.exe",
                    CheckBox = chkHasleo, Pill = pillAppHasleo, TxtPill = txtPillAppHasleo, TxtDesc = txtDescAppHasleo
                }
            };
        }

        private CheckBox GetAppCheckbox(string appKey) {
            var a = GetApps().Find(x => x.Key.Equals(appKey, StringComparison.OrdinalIgnoreCase));
            return a != null ? a.CheckBox : null;
        }

        private Border GetAppRowBorder(string appKey) {
            switch (appKey.ToLowerInvariant()) {
                case "steam": return rowAppSteam;
                case "discord": return rowAppDiscord;
                case "streamdeck": return rowAppStreamDeck;
                case "bleachbit": return rowAppBleachBit;
                case "antigravity": return rowAppAntigravity;
                case "evga": return rowAppEvga;
                case "hasleo": return rowAppHasleo;
                default: return null;
            }
        }

        private bool IsAppCheckboxChecked(string appKey) {
            var chk = GetAppCheckbox(appKey);
            return chk != null && chk.IsChecked == true;
        }

        private bool IsAppInstalled(string appKey) {
            var app = GetApps().Find(a => a.Key.Equals(appKey, StringComparison.OrdinalIgnoreCase));
            if (app == null) return false;
            var info = SoftwareDetector.Detect(app.Pattern, app.Exes, app.Dirs);
            return info != null && info.IsInstalled;
        }

        private static string GetAppSearchText(string appKey) {
            switch (appKey.ToLowerInvariant()) {
                case "steam": return "steam offizieller gaming client & spielebibliothek valve games";
                case "discord": return "discord voice, video & chat plattform für gamer community";
                case "streamdeck": return "elgato stream deck hardware-steuerung & plugin-software streaming";
                case "bleachbit": return "bleachbit cleaner open-source cache & temp-dateien bereinigung system";
                case "antigravity": return "google antigravity offizielle google next-gen coding suite & agent engine ide ai";
                case "evga": return "evga precision x1 gpu tuning, übertaktung & sensor-monitor grafikkarte oc";
                case "hasleo": return "hasleo backup suite free windows backup, system-image & festplatten-klonen sicherung";
                default: return appKey.ToLowerInvariant();
            }
        }

        private bool MatchesAppFilter(string appKey, string query = null) {
            bool showInstalled = radFilterAppsInstalled != null && radFilterAppsInstalled.IsChecked == true;
            bool showUpdates = radFilterAppsUpdates != null && radFilterAppsUpdates.IsChecked == true;
            bool showNotInstalled = radFilterAppsNotInstalled != null && radFilterAppsNotInstalled.IsChecked == true;

            bool installed = IsAppInstalled(appKey);
            bool hasUpdate = _appsWithAvailableUpdates.Contains(appKey);

            if (showUpdates && !hasUpdate) return false;
            if (showInstalled && !installed) return false;
            if (showNotInstalled && installed) return false;

            if (query == null && txtSearchApps != null) {
                query = txtSearchApps.Text;
            }
            string trimmed = (query ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(trimmed)) return true;

            string[] terms = trimmed.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            string appSearchText = GetAppSearchText(appKey);
            foreach (var t in terms) {
                if (!appSearchText.Contains(t)) return false;
            }
            return true;
        }

        private bool IsAppVisible(string appKey) {
            var row = GetAppRowBorder(appKey);
            if (row != null) return row.Visibility == Visibility.Visible;
            return MatchesAppFilter(appKey);
        }

        private void SetAllAppCheckboxes(bool isChecked, bool onlyVisible = true) {
            updateUIRefCount++;
            try {
                if (!isChecked && chkAppsSelectAll != null) chkAppsSelectAll.IsChecked = false;
                foreach (var key in AllAppKeys) {
                    if (!onlyVisible || IsAppVisible(key)) {
                        var chk = GetAppCheckbox(key);
                        if (chk != null) chk.IsChecked = isChecked;
                    }
                }
            } finally {
                updateUIRefCount--;
            }
        }

        private void FilterApps(string query = null) {
            if (window == null) return;
            window.Dispatcher.Invoke(() => {
                if (query == null && txtSearchApps != null) {
                    query = txtSearchApps.Text;
                }

                int installedCount = 0;
                int notInstalledCount = 0;
                int updatesCount = 0;
                int visibleCount = 0;

                foreach (var key in AllAppKeys) {
                    bool installed = IsAppInstalled(key);
                    bool hasUpdate = _appsWithAvailableUpdates.Contains(key);
                    if (hasUpdate) updatesCount++;
                    if (installed) installedCount++; else notInstalledCount++;

                    bool visible = MatchesAppFilter(key, query);
                    var row = GetAppRowBorder(key);
                    if (row != null) {
                        row.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
                    }
                    if (visible) visibleCount++;
                }

                if (panelAppsNoResults != null) {
                    panelAppsNoResults.Visibility = (visibleCount == 0) ? Visibility.Visible : Visibility.Collapsed;
                }

                if (radFilterAppsAll != null) radFilterAppsAll.Content = string.Format("Alle ({0})", AllAppKeys.Length);
                if (radFilterAppsInstalled != null) radFilterAppsInstalled.Content = string.Format("Installiert ({0})", installedCount);
                if (radFilterAppsUpdates != null) radFilterAppsUpdates.Content = string.Format("Updates ({0})", updatesCount);
                if (radFilterAppsNotInstalled != null) radFilterAppsNotInstalled.Content = string.Format("Nicht installiert ({0})", notInstalledCount);

                UpdateAppsActionButtonState();
            });
        }

        private void UpdateAppsActionButtonState() {
            if (window == null) return;
            window.Dispatcher.Invoke(() => {
                var visibleKeys = AllAppKeys.Where(k => IsAppVisible(k)).ToArray();
                bool allChecked = visibleKeys.Length > 0 && visibleKeys.All(k => IsAppCheckboxChecked(k));
                if (chkAppsSelectAll != null && chkAppsSelectAll.IsChecked != allChecked) {
                    updateUIRefCount++;
                    try {
                        chkAppsSelectAll.IsChecked = allChecked;
                    } finally {
                        updateUIRefCount--;
                    }
                }

                // Linker Update-Button (btnRefreshApps) passt sich dynamisch an
                if (btnRefreshApps != null && _appsWithAvailableUpdates.Count > 0) {
                    int checkedUpdates = AllAppKeys.Count(k => IsAppCheckboxChecked(k) && _appsWithAvailableUpdates.Contains(k));
                    btnRefreshApps.Style = (Style)window.FindResource("WarningBtn");
                    if (checkedUpdates > 0) {
                        btnRefreshApps.Content = string.Format("⚡ Ausgewählte aktualisieren ({0})", checkedUpdates);
                    } else {
                        btnRefreshApps.Content = string.Format("⚡ Alle Apps aktualisieren ({0})", _appsWithAvailableUpdates.Count);
                    }
                }

                if (btnActionSelectedApps == null) return;

                int selectedCount = 0;
                int toInstall = 0;
                int toUninstall = 0;

                foreach (var key in AllAppKeys) {
                    if (IsAppCheckboxChecked(key)) {
                        selectedCount++;
                        if (IsAppInstalled(key)) {
                            toUninstall++;
                        } else {
                            toInstall++;
                        }
                    }
                }

                if (selectedCount == 0) {
                    btnActionSelectedApps.Visibility = Visibility.Collapsed;
                    return;
                }

                btnActionSelectedApps.Visibility = Visibility.Visible;

                if (toInstall > 0) {
                    btnActionSelectedApps.Style = (Style)window.FindResource("SuccessBtn");
                    btnActionSelectedApps.Content = string.Format("🚀 Ausgewählte installieren ({0})", toInstall);
                } else {
                    btnActionSelectedApps.Style = (Style)window.FindResource("DangerBtn");
                    btnActionSelectedApps.Content = string.Format("🗑️ Ausgewählte deinstallieren ({0})", toUninstall);
                }
            });
        }

        private void RefreshAppsStatusUI() {
            Task.Run(() => {
                var apps = GetApps();
                var detections = new List<Tuple<AppItem, InstalledSoftwareInfo>>();
                foreach (var a in apps) {
                    detections.Add(Tuple.Create(a, SoftwareDetector.Detect(a.Pattern, a.Exes, a.Dirs)));
                }

                if (window != null) {
                    window.Dispatcher.BeginInvoke((Action)(() => {
                        foreach (var d in detections) {
                            var item = d.Item1;
                            var info = d.Item2;
                            bool installed = info != null && info.IsInstalled;
                            bool hasUpdate = _appsWithAvailableUpdates.Contains(item.Key);

                            if (hasUpdate) {
                                UIHelper.SetPillWarning(item.Pill, item.TxtPill, "UPDATE VERFÜGBAR");
                            } else if (installed) {
                                UIHelper.SetPill(item.Pill, item.TxtPill, true, "INSTALLIERT", "");
                            } else {
                                UIHelper.SetPill(item.Pill, item.TxtPill, false, "", "NICHT INSTALLIERT");
                            }

                            if (item.TxtDesc != null) {
                                item.TxtDesc.Inlines.Clear();
                                string installedVer = (installed && !string.IsNullOrEmpty(info.Version) && !info.Version.Equals("Installiert", StringComparison.OrdinalIgnoreCase))
                                    ? info.GetFormattedVersion()
                                    : null;

                                string latestVer = null;
                                if (_latestKnownVersions.TryGetValue(item.Key, out latestVer) && !string.IsNullOrEmpty(latestVer)) {
                                    if (!latestVer.StartsWith("v", StringComparison.OrdinalIgnoreCase)) latestVer = "v" + latestVer;
                                }

                                string relDate = null;
                                _latestKnownDates.TryGetValue(item.Key, out relDate);

                                if (installed) {
                                    string baseWithVer = !string.IsNullOrEmpty(installedVer)
                                        ? string.Format("{0}  •  {1}", item.BaseDesc, installedVer)
                                        : item.BaseDesc;
                                    item.TxtDesc.Inlines.Add(new Run(baseWithVer));

                                    if (hasUpdate && !string.IsNullOrEmpty(latestVer)) {
                                        string updateText = !string.IsNullOrEmpty(relDate)
                                            ? string.Format("  ➔  {0} ({1})", latestVer, relDate)
                                            : string.Format("  ➔  {0}", latestVer);

                                        item.TxtDesc.Inlines.Add(new Run(updateText) {
                                            Foreground = UIHelper.GetBrush("#F97316"),
                                            FontWeight = FontWeights.Bold
                                        });
                                    }
                                } else {
                                    item.TxtDesc.Inlines.Add(new Run(item.BaseDesc));
                                }
                            }
                        }
                        FilterApps();
                    }));
                }
            });
        }

        private async Task CheckAppUpdatesAsync() {
            if (btnRefreshApps != null) {
                btnRefreshApps.IsEnabled = false;
                btnRefreshApps.Style = (Style)window.FindResource("SecondaryBtn");
                btnRefreshApps.Content = "⏳ Prüfe Updates...";
            }
            int updatesFound = 0;

            try {
                RefreshAppsStatusUI();

                // 1. BleachBit
                try {
                    string bbUrl = await ResolveLatestBleachBitUrlAsync();
                    Match m = Regex.Match(bbUrl, @"BleachBit-([0-9]+(?:\.[0-9]+)+)-setup\.exe", RegexOptions.IgnoreCase);
                    if (m.Success) {
                        string latestBB = m.Groups[1].Value.TrimEnd('.');
                        _latestKnownVersions["BleachBit"] = latestBB;
                        var bb = SoftwareDetector.Detect(@"BleachBit", new[] { "bleachbit.exe" }, new[] { "BleachBit" });
                        if (bb != null && bb.IsInstalled && IsNewerVersion(bb.Version, latestBB)) {
                            _appsWithAvailableUpdates.Add("BleachBit");
                            updatesFound++;
                        } else {
                            _appsWithAvailableUpdates.Remove("BleachBit");
                        }
                    }
                } catch {}

                // 2. Stream Deck
                try {
                    string sdUrl = await ResolveLatestStreamDeckUrlAsync();
                    Match mSd = Regex.Match(sdUrl, @"Stream[_\-]Deck[_\-]([0-9]+(?:\.[0-9]+)+)", RegexOptions.IgnoreCase);
                    if (mSd.Success) {
                        string latestSd = mSd.Groups[1].Value.TrimEnd('.');
                        _latestKnownVersions["StreamDeck"] = latestSd;
                        var sd = SoftwareDetector.Detect(@"Stream Deck|Elgato", new[] { "StreamDeck.exe" }, new[] { @"Elgato\StreamDeck", "StreamDeck" });
                        if (sd != null && sd.IsInstalled && IsNewerVersion(sd.Version, latestSd)) {
                            _appsWithAvailableUpdates.Add("StreamDeck");
                            updatesFound++;
                        } else {
                            _appsWithAvailableUpdates.Remove("StreamDeck");
                        }
                    }
                } catch {}

                // 3. Antigravity
                try {
                    string agUrl = await ResolveLatestAntigravityUrlAsync();
                    Match mAg = Regex.Match(agUrl, @"/([0-9\.]+-[0-9]+)/", RegexOptions.IgnoreCase);
                    if (mAg.Success) {
                        string latestAg = mAg.Groups[1].Value;
                        _latestKnownVersions["Antigravity"] = latestAg;
                        var ag = SoftwareDetector.Detect(@"Antigravity", new[] { "Antigravity.exe" }, new[] { @"Programs\Antigravity", @"Google\Antigravity", "Antigravity" });
                        if (ag != null && ag.IsInstalled && IsNewerVersion(ag.Version, latestAg)) {
                            _appsWithAvailableUpdates.Add("Antigravity");
                            updatesFound++;
                        } else {
                            _appsWithAvailableUpdates.Remove("Antigravity");
                        }
                    }
                } catch {}

                // 4. EVGA Precision X1
                try {
                    var evga = SoftwareDetector.Detect(@"EVGA Precision|Precision X1", new[] { "PrecisionX_x64.exe", "PrecisionX1.exe", "PrecisionX.exe" }, new[] { @"EVGA\Precision X1", "Precision X1" });
                    string latestEvga = _latestKnownVersions["EVGA"];
                    if (evga != null && evga.IsInstalled && IsNewerVersion(evga.Version, latestEvga)) {
                        _appsWithAvailableUpdates.Add("EVGA");
                        updatesFound++;
                    } else {
                        _appsWithAvailableUpdates.Remove("EVGA");
                    }
                } catch {}

                // 5. Hasleo Backup Suite
                try {
                    var hasleo = SoftwareDetector.Detect(@"Hasleo Backup Suite|Hasleo", new[] { "BackupMainUI.exe", "AppLoader.exe", "HasleoBackupSuite.exe" }, new[] { @"Hasleo\Hasleo Backup Suite", "Hasleo Backup Suite" });
                    string latestHasleo = _latestKnownVersions["Hasleo"];
                    if (hasleo != null && hasleo.IsInstalled && IsNewerVersion(hasleo.Version, latestHasleo)) {
                        _appsWithAvailableUpdates.Add("Hasleo");
                        updatesFound++;
                    } else {
                        _appsWithAvailableUpdates.Remove("Hasleo");
                    }
                } catch {}

                RefreshAppsStatusUI();

                if (btnRefreshApps != null) {
                    if (updatesFound > 0) {
                        btnRefreshApps.Style = (Style)window.FindResource("WarningBtn");
                        btnRefreshApps.Content = string.Format("⚡ Alle Apps aktualisieren ({0})", updatesFound);
                    } else {
                        btnRefreshApps.Style = (Style)window.FindResource("SecondaryBtn");
                        btnRefreshApps.Content = "✅ Alles aktuell";
                        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                        timer.Tick += (ts, te) => {
                            if (btnRefreshApps != null && _appsWithAvailableUpdates.Count == 0) {
                                btnRefreshApps.Content = "🔄 Auf Updates prüfen";
                            }
                            timer.Stop();
                        };
                        timer.Start();
                    }
                }

            } finally {
                if (btnRefreshApps != null) {
                    btnRefreshApps.IsEnabled = true;
                }
            }
        }

        // =========================================================================
        // DOWNLOAD & INSTALLATION LOGIC
        // =========================================================================
        private CancellationTokenSource _downloadQueueCts;
        private CancellationTokenSource _downloadItemCts;
        private Process _currentProcess = null;

        private void SkipCurrentAppDownload() {
            try {
                if (_downloadItemCts != null && !_downloadItemCts.IsCancellationRequested) {
                    _downloadItemCts.Cancel();
                }
                if (_currentProcess != null && !_currentProcess.HasExited) {
                    try { _currentProcess.Kill(); } catch {}
                }
            } catch {}
        }

        private void ResetAppProgressUI() {
            if (window != null && !window.Dispatcher.CheckAccess()) {
                window.Dispatcher.Invoke(() => ResetAppProgressUI());
                return;
            }
            if (btnSkipAppDownload != null) btnSkipAppDownload.IsEnabled = false;
            if (btnCancelAppDownloads != null) btnCancelAppDownloads.IsEnabled = false;
            if (progAppDownload != null) {
                progAppDownload.IsIndeterminate = false;
                progAppDownload.Value = 0;
            }
            if (txtAppDownloadSize != null) txtAppDownloadSize.Text = "";
            if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "";
            if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "";
            if (txtAppStatus != null) txtAppStatus.Text = "";
            if (borderAppProgress != null) borderAppProgress.Visibility = Visibility.Collapsed;
        }

        private CancellationTokenSource _installationWatcherCts;

        private void StartAppInstallationWatcher(List<string> appKeysToWatch = null, List<Process> processesToWatch = null) {
            if (_installationWatcherCts != null) {
                try { _installationWatcherCts.Cancel(); _installationWatcherCts.Dispose(); } catch {}
            }
            _installationWatcherCts = new CancellationTokenSource();
            var token = _installationWatcherCts.Token;

            Task.Run(async () => {
                try {
                    for (int i = 0; i < 150; i++) {
                        if (token.IsCancellationRequested) break;
                        await Task.Delay(2000, token);

                        bool allInstalled = true;
                        if (window != null) {
                            window.Dispatcher.Invoke(() => {
                                RefreshAppsStatusUI();

                                if (appKeysToWatch != null && appKeysToWatch.Count > 0) {
                                    var targets = GetApps();
                                    foreach (var key in appKeysToWatch) {
                                        var chk = GetAppCheckbox(key);
                                        var target = targets.Find(t => t.Key.Equals(key, StringComparison.OrdinalIgnoreCase) || t.Name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0);
                                        if (target != null) {
                                            var info = SoftwareDetector.Detect(target.Pattern, target.Exes, target.Dirs);
                                            if (info.IsInstalled) {
                                                if (chk != null && chk.IsChecked == true) {
                                                    chk.IsChecked = false;
                                                }
                                            } else {
                                                allInstalled = false;
                                            }
                                        }
                                    }
                                }
                            });
                        }

                        bool anyProcessRunning = false;
                        if (processesToWatch != null && processesToWatch.Count > 0) {
                            foreach (var p in processesToWatch) {
                                try {
                                    if (p != null && !p.HasExited) {
                                        anyProcessRunning = true;
                                        break;
                                    }
                                } catch {}
                            }
                        }

                        if (allInstalled && !anyProcessRunning && appKeysToWatch != null && appKeysToWatch.Count > 0) {
                            break;
                        }
                    }
                } catch (OperationCanceledException) {}
                catch {}
            });
        }

        private void CancelAllAppDownloads() {
            try {
                if (btnCancelAppDownloads != null) btnCancelAppDownloads.IsEnabled = false;
                if (btnSkipAppDownload != null) btnSkipAppDownload.IsEnabled = false;
                if (_downloadQueueCts != null && !_downloadQueueCts.IsCancellationRequested) {
                    _downloadQueueCts.Cancel();
                }
                if (_downloadItemCts != null && !_downloadItemCts.IsCancellationRequested) {
                    _downloadItemCts.Cancel();
                }
                if (_currentProcess != null && !_currentProcess.HasExited) {
                    try { _currentProcess.Kill(); } catch {}
                }
                if (window != null) {
                    window.Dispatcher.Invoke(() => {
                        if (txtAppStatus != null) txtAppStatus.Text = "⏹ Download-Vorgang abgebrochen.";
                        if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "Abgebrochen";
                        if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "";
                        if (txtAppDownloadSize != null) txtAppDownloadSize.Text = "";
                        if (progAppDownload != null) {
                            progAppDownload.IsIndeterminate = false;
                            progAppDownload.Value = 0;
                        }
                    });
                }
            } catch {}
        }

        private async Task<bool> DownloadFileWithProgressAsync(string url, string destinationPath, Action<double, string, string> onProgress, CancellationToken ct, string referer = null) {
            var request = (HttpWebRequest)WebRequest.Create(url);
            request.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";
            if (!string.IsNullOrEmpty(referer)) request.Referer = referer;
            request.Timeout = 180000;

            using (ct.Register(() => { try { request.Abort(); } catch {} }))
            using (var response = (HttpWebResponse)await request.GetResponseAsync())
            using (var responseStream = response.GetResponseStream())
            using (var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536, true)) {
                long totalBytes = response.ContentLength;
                byte[] buffer = new byte[65536];
                long totalRead = 0, lastCheckBytes = 0, lastCheckTime = 0;
                int bytesRead;
                var stopwatch = Stopwatch.StartNew();

                while ((bytesRead = await responseStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0) {
                    ct.ThrowIfCancellationRequested();
                    await fileStream.WriteAsync(buffer, 0, bytesRead, ct);
                    totalRead += bytesRead;

                    long elapsedMs = stopwatch.ElapsedMilliseconds;
                    if (elapsedMs - lastCheckTime >= 150) {
                        long bytesDiff = totalRead - lastCheckBytes;
                        double seconds = (elapsedMs - lastCheckTime) / 1000.0;
                        string speedStr = "0 MB/s";
                        if (seconds > 0) {
                            double spd = bytesDiff / seconds;
                            speedStr = spd >= 1048576 ? string.Format("{0:0.0} MB/s", spd / 1048576) : string.Format("{0:0} KB/s", spd / 1024);
                        }
                        lastCheckBytes = totalRead;
                        lastCheckTime = elapsedMs;

                        double pct = totalBytes > 0 ? (totalRead * 100.0) / totalBytes : 0;
                        string sizeStr = totalBytes > 0
                            ? string.Format("({0:0.0} MB / {1:0.0} MB)", totalRead / 1048576.0, totalBytes / 1048576.0)
                            : string.Format("({0:0.0} MB)", totalRead / 1048576.0);

                        if (onProgress != null && window != null) {
                            window.Dispatcher.Invoke(() => onProgress(pct, speedStr, sizeStr));
                        }
                    }
                }
            }
            return true;
        }

        private async Task<bool> DownloadFileWithLiveProgressAsync(string url, string destinationPath, string appName, int appIndex, int totalApps, CancellationToken ct) {
            bool ok = await DownloadFileWithProgressAsync(url, destinationPath, (pct, speedStr, sizeStr) => {
                if (txtAppStatus != null) txtAppStatus.Text = string.Format("Lade {0} ({1}/{2})...", appName, appIndex, totalApps);
                if (txtAppDownloadSize != null) txtAppDownloadSize.Text = sizeStr;
                if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = speedStr;
                if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = string.Format("{0:0}%", pct);
                if (progAppDownload != null) {
                    progAppDownload.IsIndeterminate = false;
                    progAppDownload.Value = pct;
                }
            }, ct);

            if (window != null) {
                window.Dispatcher.Invoke(() => {
                    if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "100%";
                    if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "Fertig";
                    if (progAppDownload != null) {
                        progAppDownload.IsIndeterminate = false;
                        progAppDownload.Value = 100;
                    }
                });
            }
            return ok;
        }

        private Process LaunchInstaller(string filePath) {
            try {
                if (!File.Exists(filePath)) return null;
                ProcessStartInfo psi;
                if (filePath.EndsWith(".msi", StringComparison.OrdinalIgnoreCase)) {
                    psi = new ProcessStartInfo("msiexec.exe", string.Format("/i \"{0}\"", filePath)) { UseShellExecute = true };
                } else {
                    psi = new ProcessStartInfo(filePath) { UseShellExecute = true };
                }
                return Process.Start(psi);
            } catch (Exception ex) {
                ShowAlertModal("Installer starten", "Fehler beim Starten des Installers:\r\n" + ex.Message, "OK", "❌", "#EF4444");
                return null;
            }
        }

        private async Task ExecuteAppDownloadsAsync(string targetDir, bool autoRunInstallers) {
            if (btnActionSelectedApps != null) btnActionSelectedApps.IsEnabled = false;
            if (btnSkipAppDownload != null) btnSkipAppDownload.IsEnabled = true;
            if (btnCancelAppDownloads != null) btnCancelAppDownloads.IsEnabled = true;
            if (borderAppProgress != null) borderAppProgress.Visibility = Visibility.Visible;
            if (progAppDownload != null) {
                progAppDownload.IsIndeterminate = true;
                progAppDownload.Value = 0;
            }
            if (txtAppDownloadSize != null) txtAppDownloadSize.Text = "";
            if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "";
            if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "0%";
            if (txtAppStatus != null) txtAppStatus.Text = "Ermittle neueste Download-Links...";

            _downloadQueueCts = new CancellationTokenSource();
            UpdateSidebarActivitySpinners();
            bool wasCancelled = false;

            try {
                if (Directory.Exists(targetDir)) Directory.Delete(targetDir, true);
                Directory.CreateDirectory(targetDir);

                var downloadQueue = new List<AppItem>();
                foreach (var a in GetApps()) {
                    if (a.CheckBox != null && a.CheckBox.IsChecked == true) {
                        downloadQueue.Add(a);
                    }
                }

                if (downloadQueue.Count == 0) {
                    if (txtAppStatus != null) txtAppStatus.Text = "Keine Anwendungen ausgewählt.";
                    return;
                }

                int totalTasks = downloadQueue.Count;
                int currentTask = 0;
                var downloadedFiles = new List<Tuple<string, string, string>>();

                foreach (var item in downloadQueue) {
                    if (_downloadQueueCts != null && _downloadQueueCts.IsCancellationRequested) {
                        wasCancelled = true;
                        break;
                    }

                    currentTask++;
                    string appName = item.Name;
                    string destPath = Path.Combine(targetDir, item.FallbackFileName);

                    _downloadItemCts = CancellationTokenSource.CreateLinkedTokenSource(_downloadQueueCts.Token);
                    bool downloadSucceeded = false;
                    bool wasSkipped = false;
                    bool hadError = false;
                    string errorMsg = null;

                    try {
                        if (window != null) {
                            window.Dispatcher.Invoke(() => {
                                if (txtAppStatus != null) txtAppStatus.Text = string.Format("Lade {0} ({1}/{2})...", appName, currentTask, totalTasks);
                                if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "";
                                if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "0%";
                                if (txtAppDownloadSize != null) txtAppDownloadSize.Text = "";
                                if (progAppDownload != null) {
                                    progAppDownload.IsIndeterminate = false;
                                    progAppDownload.Value = 0;
                                }
                            });
                        }

                        string url = await item.ResolveUrl();
                        destPath = Path.Combine(targetDir, item.FallbackFileName);
                        downloadSucceeded = await DownloadFileWithLiveProgressAsync(url, destPath, appName, currentTask, totalTasks, _downloadItemCts.Token);
                    } catch (OperationCanceledException) {
                        if (_downloadQueueCts != null && _downloadQueueCts.IsCancellationRequested) {
                            wasCancelled = true;
                            if (File.Exists(destPath)) try { File.Delete(destPath); } catch {}
                            break;
                        } else {
                            wasSkipped = true;
                        }
                    } catch (Exception ex) {
                        if (_downloadQueueCts != null && _downloadQueueCts.IsCancellationRequested) {
                            wasCancelled = true;
                            break;
                        }
                        hadError = true;
                        errorMsg = ex.Message;
                    }

                    if (wasSkipped) {
                        if (txtAppStatus != null) txtAppStatus.Text = string.Format("⏭ {0} übersprungen. Nächster Download...", appName);
                        if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "Übersprungen";
                        if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "";
                        if (txtAppDownloadSize != null) txtAppDownloadSize.Text = "";
                        if (File.Exists(destPath)) try { File.Delete(destPath); } catch {}
                        await Task.Delay(600);
                        continue;
                    }

                    if (hadError) {
                        if (txtAppStatus != null) txtAppStatus.Text = string.Format("Fehler bei {0}: {1}", appName, errorMsg);
                        if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "Fehler";
                        if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "";
                        if (txtAppDownloadSize != null) txtAppDownloadSize.Text = "";
                        await Task.Delay(1000);
                        continue;
                    }

                    if (downloadSucceeded && File.Exists(destPath)) {
                        downloadedFiles.Add(new Tuple<string, string, string>(item.Key, appName, destPath));
                    }
                }

                if (btnCancelAppDownloads != null) btnCancelAppDownloads.IsEnabled = false;
                if (btnSkipAppDownload != null) btnSkipAppDownload.IsEnabled = false;

                if (wasCancelled) {
                    if (txtAppStatus != null) txtAppStatus.Text = "⏹ Download-Vorgang abgebrochen.";
                    if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "Abgebrochen";
                    if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "";
                    if (txtAppDownloadSize != null) txtAppDownloadSize.Text = "";
                    if (progAppDownload != null) {
                        progAppDownload.IsIndeterminate = false;
                        progAppDownload.Value = 0;
                    }
                    await Task.Delay(2500);
                    ResetAppProgressUI();
                } else if (autoRunInstallers) {
                    if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "100%";
                    if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "Starte...";
                    var launchedKeys = new List<string>();
                    var launchedProcesses = new List<Process>();
                    foreach (var file in downloadedFiles) {
                        if (_downloadQueueCts != null && _downloadQueueCts.IsCancellationRequested) break;
                        if (txtAppStatus != null) txtAppStatus.Text = string.Format("🚀 Starte Installation von {0}...", file.Item2);
                        var proc = LaunchInstaller(file.Item3);
                        if (proc != null) launchedProcesses.Add(proc);
                        launchedKeys.Add(file.Item1);
                        await Task.Delay(500);
                    }
                    if (txtAppStatus != null) txtAppStatus.Text = "✅ Setups gestartet! Überwache Installation...";
                    if (txtAppDownloadSpeed != null) txtAppDownloadSpeed.Text = "Läuft...";
                    if (txtAppDownloadPercent != null) txtAppDownloadPercent.Text = "100%";

                    StartAppInstallationWatcher(launchedKeys, launchedProcesses);

                    await Task.Delay(2500);
                    ResetAppProgressUI();
                }

                SetAllAppCheckboxes(false, false);
                await Task.Delay(500);
                RefreshAppsStatusUI();

            } catch (Exception ex) {
                if (txtAppStatus != null) txtAppStatus.Text = "Fehler: " + ex.Message;
                ShowAlertModal("Download-Fehler", "Fehler beim Herunterladen:\r\n" + ex.Message, "OK", "❌", "#EF4444");
            } finally {
                if (_downloadQueueCts != null) {
                    _downloadQueueCts.Dispose();
                    _downloadQueueCts = null;
                    UpdateSidebarActivitySpinners();
                }
                if (_downloadItemCts != null) {
                    _downloadItemCts.Dispose();
                    _downloadItemCts = null;
                }
                if (btnCancelAppDownloads != null) btnCancelAppDownloads.IsEnabled = false;
                if (btnSkipAppDownload != null) btnSkipAppDownload.IsEnabled = false;
                if (btnActionSelectedApps != null) btnActionSelectedApps.IsEnabled = true;
            }
        }

        private async Task<string> ResolveLatestBleachBitUrlAsync() {
            return await GetLatestGitHubReleaseAssetUrlAsync("BleachBit", "bleachbit/bleachbit", @"BleachBit-[0-9\.]+-setup\.exe", "https://download.bleachbit.org/BleachBit-4.6.2-setup.exe");
        }

        private async Task<string> ResolveLatestStreamDeckUrlAsync() {
            return await Task.Run(() => {
                try {
                    string stdout = ProcessRunner.RunAndGetOutput("winget.exe", "show Elgato.StreamDeck --accept-source-agreements", 6000);
                    var mDate = Regex.Match(stdout, @"(?:Release\s*Date|Veröffentlichungsdatum):\s*([0-9]{4})-([0-9]{2})-([0-9]{2})", RegexOptions.IgnoreCase);
                    if (mDate.Success) {
                        _latestKnownDates["StreamDeck"] = string.Format("{0}.{1}.{2}", mDate.Groups[3].Value, mDate.Groups[2].Value, mDate.Groups[1].Value);
                    }
                    var m = Regex.Match(stdout, @"Installer[\s\-_]*Url:\s*(https://[^\r\n\s]+)", RegexOptions.IgnoreCase);
                    if (m.Success) {
                        return m.Groups[1].Value.Trim();
                    }
                    var mDirect = Regex.Match(stdout, @"(https://[^\r\n\s""']+\.msi)", RegexOptions.IgnoreCase);
                    if (mDirect.Success) {
                        return mDirect.Groups[1].Value.Trim();
                    }
                } catch {}
                return "https://edge.elgato.com/egc/windows/sd/Stream_Deck_7.5.1.22901.msi";
            });
        }

        private async Task<string> ResolveLatestAntigravityUrlAsync() {
            return await Task.Run(() => {
                try {
                    using (WebClient client = new WebClient()) {
                        string json = client.DownloadString("https://storage.googleapis.com/antigravity-public/antigravity-cli/manifest.json");
                        Match m = Regex.Match(json, "\"url\":\\s*\"(https://[^\"]+)\"", RegexOptions.IgnoreCase);
                        if (m.Success) {
                            return m.Groups[1].Value;
                        }
                    }
                } catch {}
                return "https://storage.googleapis.com/antigravity-public/antigravity-cli/1.1.22-5711547746615296/windows-x64/cli_windows_x64.exe";
            });
        }

        private async Task<string> GetLatestGitHubReleaseAssetUrlAsync(string appKey, string repo, string assetPattern, string fallbackUrl) {
            return await Task.Run(() => {
                try {
                    using (WebClient client = new WebClient()) {
                        client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                        string json = client.DownloadString(string.Format("https://api.github.com/repos/{0}/releases/latest", repo));

                        Match mDate = Regex.Match(json, "\"published_at\":\\s*\"([0-9]{4})-([0-9]{2})-([0-9]{2})", RegexOptions.IgnoreCase);
                        if (mDate.Success && !string.IsNullOrEmpty(appKey)) {
                            _latestKnownDates[appKey] = string.Format("{0}.{1}.{2}", mDate.Groups[3].Value, mDate.Groups[2].Value, mDate.Groups[1].Value);
                        }

                        Match m = Regex.Match(json, "\"browser_download_url\":\\s*\"(https://[^\"]+" + assetPattern + ")\"", RegexOptions.IgnoreCase);
                        if (m.Success) {
                            return m.Groups[1].Value;
                        }
                    }
                } catch {}
                return fallbackUrl;
            });
        }

        // =========================================================================
        // SOFTWARE DEINSTALLATION (BATCH)
        // =========================================================================
        private async Task RunAppUninstallerAsync(AppItem target) {
            await Task.Run(() => {
                try {
                    if (target.Exes != null) {
                        foreach (var exe in target.Exes) {
                            string pName = Path.GetFileNameWithoutExtension(exe);
                            try {
                                foreach (var p in Process.GetProcessesByName(pName)) {
                                    try {
                                        p.CloseMainWindow();
                                        if (!p.WaitForExit(1500)) p.Kill();
                                    } catch {}
                                }
                            } catch {}
                        }
                    }

                    var info = SoftwareDetector.Detect(target.Pattern, target.Exes, target.Dirs);
                    string uninst = info != null ? (!string.IsNullOrEmpty(info.QuietUninstallString) ? info.QuietUninstallString : info.UninstallString) : null;

                    if (!string.IsNullOrEmpty(uninst)) {
                        uninst = uninst.Trim();
                        string exePath = "";
                        string args = "";

                        if (uninst.StartsWith("\"")) {
                            int nextQuote = uninst.IndexOf('"', 1);
                            if (nextQuote > 1) {
                                exePath = uninst.Substring(1, nextQuote - 1);
                                args = uninst.Substring(nextQuote + 1).Trim();
                            } else {
                                exePath = uninst.Replace("\"", "").Trim();
                            }
                        } else {
                            int spaceIdx = uninst.IndexOf(' ');
                            if (spaceIdx > 0 && !File.Exists(uninst)) {
                                exePath = uninst.Substring(0, spaceIdx);
                                args = uninst.Substring(spaceIdx + 1).Trim();
                            } else {
                                exePath = uninst;
                            }
                        }

                        if (exePath.IndexOf("msiexec", StringComparison.OrdinalIgnoreCase) >= 0) {
                            if (!args.Contains("/x") && !args.Contains("/X")) {
                                args = args.Replace("/i", "/x").Replace("/I", "/x");
                            }
                        }

                        var psi = new ProcessStartInfo {
                            FileName = exePath,
                            Arguments = args,
                            UseShellExecute = true
                        };
                        using (var proc = Process.Start(psi)) {
                            if (proc != null) {
                                proc.WaitForExit(300000);
                            }
                        }
                    } else if (!string.IsNullOrEmpty(target.WingetId)) {
                        ProcessRunner.RunAndGetOutput("winget.exe", string.Format("uninstall --id {0} --exact --accept-source-agreements", target.WingetId), 300000);
                    }
                } catch (Exception ex) {
                    Debug.WriteLine("Fehler bei Deinstallation von " + target.Name + ": " + ex.Message);
                }
            });
        }

        private async Task UninstallSelectedAppsAsync() {
            var targets = GetApps();
            var selectedInstalled = new List<AppItem>();

            foreach (var t in targets) {
                if (t.CheckBox != null && t.CheckBox.IsChecked == true) {
                    var info = SoftwareDetector.Detect(t.Pattern, t.Exes, t.Dirs);
                    if (info != null && info.IsInstalled) {
                        selectedInstalled.Add(t);
                    }
                }
            }

            if (selectedInstalled.Count == 0) {
                await ShowAlertModalAsync(
                    "Keine Auswahl",
                    "Bitte markiere über die Checkboxen mindestens eine bereits installierte Software, die du deinstallieren möchtest.",
                    "Verstanden",
                    "ℹ️",
                    "#38BDF8"
                );
                return;
            }

            string listNames = string.Join("\r\n• ", selectedInstalled.ConvertAll(x => x.Name));
            bool ok = await ShowConfirmModalAsync(
                "Ausgewählte Software deinstallieren",
                string.Format("Möchtest du die folgenden {0} Programme wirklich vollständig deinstallieren?\r\n\r\n• {1}", selectedInstalled.Count, listNames),
                "Alle deinstallieren",
                "Abbrechen",
                "🗑️",
                "#EF4444"
            );

            if (!ok) return;

            if (borderAppProgress != null) borderAppProgress.Visibility = Visibility.Visible;
            if (progAppDownload != null) progAppDownload.IsIndeterminate = true;

            try {
                int count = 0;
                foreach (var t in selectedInstalled) {
                    count++;
                    if (txtAppStatus != null) {
                        txtAppStatus.Text = string.Format("({0}/{1}) Deinstalliere '{2}'...", count, selectedInstalled.Count, t.Name);
                    }
                    await RunAppUninstallerAsync(t);
                    RefreshAppsStatusUI();
                }

                if (txtAppStatus != null) {
                    txtAppStatus.Text = string.Format("Alle {0} ausgewählten Programme wurden deinstalliert.", selectedInstalled.Count);
                }
                await ShowAlertModalAsync(
                    "Deinstallation abgeschlossen",
                    string.Format("Es wurden {0} Programme erfolgreich deinstalliert.", selectedInstalled.Count),
                    "OK",
                    "✅",
                    "#10B981"
                );
            } finally {
                if (progAppDownload != null) progAppDownload.IsIndeterminate = false;
                SetAllAppCheckboxes(false, false);
                RefreshAppsStatusUI();
                ResetAppProgressUI();
            }
        }

        // =========================================================================
        // SETUP APPS EVENTS
        // =========================================================================
        public void SetupAppsEvents() {
            string userDownloads = RegistryHelper.GetUserDownloadsFolder();
            string targetDir = Path.Combine(userDownloads, "Installationen");

            if (radFilterAppsAll != null) radFilterAppsAll.Checked += (s, e) => FilterApps();
            if (radFilterAppsInstalled != null) radFilterAppsInstalled.Checked += (s, e) => FilterApps();
            if (radFilterAppsUpdates != null) radFilterAppsUpdates.Checked += (s, e) => FilterApps();
            if (radFilterAppsNotInstalled != null) radFilterAppsNotInstalled.Checked += (s, e) => FilterApps();

            if (txtSearchApps != null) {
                txtSearchApps.TextChanged += (s, e) => {
                    string q = txtSearchApps.Text ?? "";
                    if (txtSearchAppsPlaceholder != null) {
                        txtSearchAppsPlaceholder.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
                    }
                    if (btnClearSearchApps != null) {
                        btnClearSearchApps.Visibility = string.IsNullOrEmpty(q) ? Visibility.Collapsed : Visibility.Visible;
                    }
                    FilterApps(q);
                };

                txtSearchApps.KeyDown += (s, e) => {
                    if (e.Key == System.Windows.Input.Key.Escape) {
                        txtSearchApps.Text = "";
                    }
                };
            }

            if (btnClearSearchApps != null) {
                btnClearSearchApps.Click += (s, e) => {
                    if (txtSearchApps != null) {
                        txtSearchApps.Text = "";
                        txtSearchApps.Focus();
                    }
                };
            }

            foreach (var chk in GetApps().ConvertAll(a => a.CheckBox)) {
                if (chk != null) {
                    chk.Checked += (s, e) => { if (!isUpdatingUI) UpdateAppsActionButtonState(); };
                    chk.Unchecked += (s, e) => { if (!isUpdatingUI) UpdateAppsActionButtonState(); };
                }
            }

            if (chkAppsSelectAll != null) {
                chkAppsSelectAll.Checked += (s, e) => {
                    if (isUpdatingUI) return;
                    SetAllAppCheckboxes(true, true);
                    UpdateAppsActionButtonState();
                };
                chkAppsSelectAll.Unchecked += (s, e) => {
                    if (isUpdatingUI) return;
                    SetAllAppCheckboxes(false, true);
                    UpdateAppsActionButtonState();
                };
            }

            if (btnOpenDownloadFolder != null) {
                btnOpenDownloadFolder.Click += (s, e) => {
                    try {
                        if (!Directory.Exists(targetDir)) Directory.CreateDirectory(targetDir);
                        ProcessRunner.Start("explorer.exe", targetDir);
                    } catch {}
                };
            }

            if (btnRefreshApps != null) {
                btnRefreshApps.Click += async (s, e) => {
                    if (_appsWithAvailableUpdates.Count > 0) {
                        var checkedUpdateKeys = AllAppKeys.Where(k => IsAppCheckboxChecked(k) && _appsWithAvailableUpdates.Contains(k)).ToList();
                        var targetUpdateKeys = checkedUpdateKeys.Count > 0 ? checkedUpdateKeys : _appsWithAvailableUpdates.ToList();

                        foreach (var key in AllAppKeys) {
                            var chk = GetAppCheckbox(key);
                            if (chk != null) chk.IsChecked = targetUpdateKeys.Contains(key, StringComparer.OrdinalIgnoreCase);
                        }
                        UpdateAppsActionButtonState();
                        await ExecuteAppDownloadsAsync(targetDir, autoRunInstallers: true);
                        await CheckAppUpdatesAsync();
                    } else {
                        await CheckAppUpdatesAsync();
                    }
                };
            }

            if (btnActionSelectedApps != null) {
                btnActionSelectedApps.Click += async (s, e) => {
                    int toInstall = 0;
                    int toUninstall = 0;

                    foreach (var key in AllAppKeys) {
                        if (IsAppCheckboxChecked(key)) {
                            if (IsAppInstalled(key)) toUninstall++;
                            else toInstall++;
                        }
                    }

                    if (toInstall > 0) {
                        await ExecuteAppDownloadsAsync(targetDir, autoRunInstallers: true);
                    } else if (toUninstall > 0) {
                        await UninstallSelectedAppsAsync();
                    }
                };
            }

            if (btnSkipAppDownload != null) {
                btnSkipAppDownload.Click += (s, e) => SkipCurrentAppDownload();
            }

            if (btnCancelAppDownloads != null) {
                btnCancelAppDownloads.Click += (s, e) => CancelAllAppDownloads();
            }
        }
    }
}
