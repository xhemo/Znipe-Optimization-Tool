using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    public partial class MainWindowLogic {
        // =========================================================================
        // LIVE OFFICE SUITE MANAGEMENT & ACTIVATION
        // =========================================================================
        private int _officeProgressGeneration = 0;

        private static void SetOptionalText(TextBlock tb, string val) {
            if (tb == null) return;
            tb.Text = val ?? string.Empty;
            tb.Visibility = string.IsNullOrEmpty(val) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void SetOfficeProgress(double percent, string mainStatus, string details = null, string eta = null, string subStatus = null, bool isIndeterminate = false) {
            if (window == null) return;
            window.Dispatcher.Invoke(() => {
                if (borderOfficeProgress != null && borderOfficeProgress.Visibility != Visibility.Visible)
                    borderOfficeProgress.Visibility = Visibility.Visible;

                int currentGen = ++_officeProgressGeneration;

                if (btnCancelOfficeAction != null) {
                    if (percent >= 100.0 || (percent <= 0 && !isIndeterminate)) {
                        btnCancelOfficeAction.Visibility = Visibility.Collapsed;
                    } else {
                        btnCancelOfficeAction.Visibility = Visibility.Visible;
                    }
                }

                if (progOfficeDownload != null) {
                    progOfficeDownload.IsIndeterminate = isIndeterminate;
                    if (!isIndeterminate) {
                        progOfficeDownload.Value = Math.Max(0, Math.Min(100, percent));
                    }
                }

                if (txtOfficePercent != null) {
                    if (isIndeterminate) {
                        txtOfficePercent.Text = "⚡ In Arbeit...";
                    } else {
                        txtOfficePercent.Text = string.Format("{0:0.0}%", percent);
                    }
                }

                if (txtOfficeStatus != null)
                    txtOfficeStatus.Text = mainStatus;

                SetOptionalText(txtOfficeDownloadDetails, details);
                SetOptionalText(txtOfficeEta, eta);
                SetOptionalText(txtOfficeSubStatus, subStatus);

                // Automatisch nach 4 Sekunden ausblenden, wenn fertig (100%) oder abgebrochen (0%)
                if (percent >= 100.0 || (percent <= 0 && !isIndeterminate)) {
                    Task.Delay(4000).ContinueWith(_ => {
                        window.Dispatcher.Invoke(() => {
                            if (_officeProgressGeneration == currentGen && borderOfficeProgress != null) {
                                borderOfficeProgress.Visibility = Visibility.Collapsed;
                            }
                        });
                    });
                }
            });
        }

        private long GetDirectorySize(string folder) {
            if (!Directory.Exists(folder)) return 0;
            try {
                return new DirectoryInfo(folder).EnumerateFiles("*", SearchOption.AllDirectories).Sum(fi => fi.Length);
            } catch { return 0; }
        }

        private async Task<bool> EnsureOdtAsync(string officeDir) {
            string setupExe = Path.Combine(officeDir, "setup.exe");
            if (File.Exists(setupExe)) return true;
            string odtPath = Path.Combine(officeDir, "odt.exe");
            using (WebClient client = new WebClient()) {
                client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64)");
                await client.DownloadFileTaskAsync(new Uri("https://download.microsoft.com/download/2/7/A/27AF1BE6-DD20-4CB4-B154-EBAB8A7D4A7E/officedeploymenttool_17328-20162.exe"), odtPath);
            }
            if (_officeCts != null && _officeCts.IsCancellationRequested) return false;
            await Task.Run(() => {
                var psi = new ProcessStartInfo {
                    FileName = odtPath,
                    Arguments = "/quiet /extract:\"" + officeDir + "\"",
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using (Process p = Process.Start(psi)) {
                    _currentOfficeProcess = p;
                    if (p != null) p.WaitForExit(30000);
                }
            });
            return File.Exists(setupExe);
        }

        private CheckBox[] GetOfficeCheckboxes() {
            return new[] { chkOfficeWord, chkOfficeExcel, chkOfficePowerPoint, chkOfficeOutlook, chkOfficeOneNote, chkOfficeAccess, chkOfficePublisher, chkOfficeTeams };
        }

        private bool IsOfficeAppVisible(string appKey) {
            var row = GetOfficeRowBorder(appKey);
            if (row != null) return row.Visibility == Visibility.Visible;
            bool showInstalled = radFilterOfficeInstalled != null && radFilterOfficeInstalled.IsChecked == true;
            bool showNotInstalled = radFilterOfficeNotInstalled != null && radFilterOfficeNotInstalled.IsChecked == true;
            bool installed = IsOfficeComponentInstalled(appKey);
            if (showInstalled) return installed;
            if (showNotInstalled) return !installed;
            return true;
        }

        private void SetAllOfficeCheckboxes(bool isChecked, bool onlyVisible = true) {
            updateUIRefCount++;
            try {
                if (!isChecked && chkOfficeSelectAll != null) chkOfficeSelectAll.IsChecked = false;
                foreach (var key in AllOfficeAppKeys) {
                    if (!onlyVisible || IsOfficeAppVisible(key)) {
                        var chk = GetOfficeCheckbox(key);
                        if (chk != null) chk.IsChecked = isChecked;
                    }
                }
            } finally {
                updateUIRefCount--;
            }
        }

        private void SetOfficeCheckboxesEnabled(bool enabled) {
            foreach (var chk in GetOfficeCheckboxes()) {
                if (chk != null) chk.IsEnabled = enabled;
            }
            if (chkOfficeSelectAll != null) chkOfficeSelectAll.IsEnabled = enabled;
        }

        private bool IsOfficeOperationRunning {
            get {
                try {
                    return (_currentOfficeProcess != null && !_currentOfficeProcess.HasExited) ||
                           (_officeCts != null && !_officeCts.IsCancellationRequested);
                } catch {
                    return false;
                }
            }
        }

        private CancellationTokenSource _officeCts;
        private Process _currentOfficeProcess;

        private void CancelOfficeOperation() {
            try {
                if (_officeCts != null && !_officeCts.IsCancellationRequested) {
                    _officeCts.Cancel();
                }
                if (_currentOfficeProcess != null) {
                    try {
                        if (!_currentOfficeProcess.HasExited) _currentOfficeProcess.Kill();
                    } catch {}
                }
                foreach (var p in Process.GetProcessesByName("setup")) {
                    try {
                        if (p.MainModule != null && p.MainModule.FileName.IndexOf("Installationen", StringComparison.OrdinalIgnoreCase) >= 0) {
                            p.Kill();
                        }
                    } catch {}
                }
                SetOfficeProgress(0, "⚠️ Vorgang durch Benutzer abgebrochen!", "", "", "Bereits ausgeführte Schritte zurückgesetzt.");
            } catch {}
        }

        private static readonly string[] AllOfficeAppKeys = new[] { "Word", "Excel", "PowerPoint", "Outlook", "OneNote", "Access", "Publisher", "Teams" };

        private CheckBox GetOfficeCheckbox(string appKey) {
            switch (appKey.ToLowerInvariant()) {
                case "word": return chkOfficeWord;
                case "excel": return chkOfficeExcel;
                case "powerpoint": return chkOfficePowerPoint;
                case "outlook": return chkOfficeOutlook;
                case "onenote": return chkOfficeOneNote;
                case "access": return chkOfficeAccess;
                case "publisher": return chkOfficePublisher;
                case "teams": return chkOfficeTeams;
                default: return null;
            }
        }

        private bool IsOfficeAppCheckboxChecked(string appKey) {
            var chk = GetOfficeCheckbox(appKey);
            return chk != null && chk.IsChecked == true;
        }

        private static readonly string[] OfficeSearchRoots = new string[] {
            @"Microsoft Office\root\Office16",
            @"Microsoft Office\Office16",
            @"Microsoft Office\Office15"
        };

        private bool CheckStandardOfficeApp(string exeName, string secondaryExe = null, string appxName = null) {
            string progFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string progFiles86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            List<string> paths = new List<string>();
            foreach (string root in OfficeSearchRoots) {
                paths.Add(Path.Combine(progFiles, root, exeName));
                paths.Add(Path.Combine(progFiles86, root, exeName));
            }
            return CheckAppInstalled(paths.ToArray(), secondaryExe ?? exeName, appxName);
        }

        private bool IsOfficeComponentInstalled(string appKey) {
            switch (appKey.ToLowerInvariant()) {
                case "word": return CheckStandardOfficeApp("WINWORD.EXE");
                case "excel": return CheckStandardOfficeApp("EXCEL.EXE");
                case "powerpoint": return CheckStandardOfficeApp("POWERPNT.EXE");
                case "outlook": return CheckStandardOfficeApp("OUTLOOK.EXE");
                case "onenote": return CheckStandardOfficeApp("ONENOTE.EXE", "onenote.exe", "Microsoft.Office.OneNote");
                case "access": return CheckStandardOfficeApp("MSACCESS.EXE");
                case "publisher": return CheckStandardOfficeApp("MSPUB.EXE");
                case "teams":
                    return CheckAppInstalled(new[] {
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Microsoft Office\root\Office16\teams.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"Microsoft Office\root\Office16\teams.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft", "Teams", "current", "Teams.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft", "Teams", "current", "Teams.exe"),
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft", "Teams", "current", "Teams.exe")
                    }, "msteams.exe", "MSTeams") || CheckAppxInstalled("MicrosoftTeams");
                default:
                    return false;
            }
        }

        private void UpdateOfficeActionButtonState() {
            if (window == null) return;
            window.Dispatcher.Invoke(() => {
                var visibleKeys = AllOfficeAppKeys.Where(k => IsOfficeAppVisible(k)).ToArray();
                bool allChecked = visibleKeys.Length > 0 && visibleKeys.All(k => IsOfficeAppCheckboxChecked(k));
                if (chkOfficeSelectAll != null && chkOfficeSelectAll.IsChecked != allChecked) {
                    updateUIRefCount++;
                    try {
                        chkOfficeSelectAll.IsChecked = allChecked;
                    } finally {
                        updateUIRefCount--;
                    }
                }

                if (btnInstallSelectedApps == null) return;

                int toInstall = 0;
                int toUninstall = 0;

                foreach (var key in AllOfficeAppKeys) {
                    if (IsOfficeAppCheckboxChecked(key)) {
                        if (IsOfficeComponentInstalled(key)) {
                            toUninstall++;
                        } else {
                            toInstall++;
                        }
                    }
                }

                if (toInstall > 0) {
                    btnInstallSelectedApps.Visibility = Visibility.Visible;
                    btnInstallSelectedApps.Style = (Style)window.FindResource("SuccessBtn");
                    btnInstallSelectedApps.Content = string.Format("🚀 Ausgewählte installieren ({0})", toInstall);
                } else if (toUninstall > 0) {
                    btnInstallSelectedApps.Visibility = Visibility.Visible;
                    btnInstallSelectedApps.Style = (Style)window.FindResource("DangerBtn");
                    btnInstallSelectedApps.Content = string.Format("🗑️ Ausgewählte deinstallieren ({0})", toUninstall);
                } else {
                    btnInstallSelectedApps.Visibility = Visibility.Collapsed;
                }
            });
        }

        private void SetOfficeActionButtons(bool enabled) {
            if (btnInstallSelectedApps != null) btnInstallSelectedApps.IsEnabled = enabled;
            if (btnOhookActivate != null) btnOhookActivate.IsEnabled = enabled;
        }

        private async Task ExecuteOfficeInstallSelectedAppsAsync(string officeDir) {
            var appsToInstall = new List<string>();
            foreach (var app in AllOfficeAppKeys) {
                if (IsOfficeAppCheckboxChecked(app) && !IsOfficeComponentInstalled(app)) {
                    appsToInstall.Add(app);
                }
            }

            if (appsToInstall.Count == 0) {
                SetOfficeProgress(0, "Bitte wähle in der Tabelle mindestens eine Komponente zum Installieren / Hinzufügen aus.", "💡 Hake die gewünschten Programme (z. B. Word, Excel) an.", "", "");
                return;
            }

            string message = "Folgende Komponenten werden direkt von Microsoft bezogen und lokal installiert:\n" +
                             "• " + string.Join(", ", appsToInstall) + "\n\n" +
                             "Wie möchtest du mit der Lizenzierung verfahren?\n\n" +
                             "🔑 Eigener Key (Empfohlen – 100% legal):\n" +
                             "• Installiert ausschließlich die unmodifizierten Original-Dateien von Microsoft\n" +
                             "• Offizielle Aktivierung über dein Microsoft 365 Konto oder gekauften Produktschlüssel\n\n" +
                             "⚡ Mit Ohook aktivieren (Nicht empfohlen / Grauzone):\n" +
                             "• Open-Source-Bypass (MAS sppc.dll Hook) zur lokalen Lizenzemulation\n" +
                             "• Rechtlich nicht offiziell lizenziert & Verstoß gegen Microsoft-Lizenzbedingungen\n" +
                             "• Nur für erfahrene Nutzer – Ausführung erfolgt auf eigene Gefahr und Verantwortung!\n\n" +
                             "Bitte wähle deine gewünschte Option:";

            var choice = await ShowChoiceModalAsync(
                "Office-Installation & Lizenz",
                message,
                "⚡ Mit Ohook (Eigene Gefahr)",
                "🔑 Eigener Key (Empfohlen)",
                "Abbrechen",
                "⚠️",
                "#EF4444",
                "Znipe Optimization Tool",
                "DangerBtn",
                "SuccessBtn"
            );

            if (choice == ModalChoice.Cancel) return;
            bool autoActivateOhook = (choice == ModalChoice.Confirm);

            _officeCts = new CancellationTokenSource();
            SetOfficeActionButtons(false);
            SetOfficeCheckboxesEnabled(false);

            string officeDataDir = Path.Combine(officeDir, "Office");
            try {
                if (!Directory.Exists(officeDir)) Directory.CreateDirectory(officeDir);

                // Benutzerwunsch: Immer frischer Download von Microsoft
                try {
                    if (Directory.Exists(officeDataDir)) {
                        Directory.Delete(officeDataDir, true);
                    }
                } catch {}

                if (_officeCts.IsCancellationRequested) return;

                SetOfficeProgress(1.0, "Bereite Office Deployment Tool vor...", "", "", "odt.exe");
                string setupExe = Path.Combine(officeDir, "setup.exe");
                if (!await EnsureOdtAsync(officeDir) || _officeCts.IsCancellationRequested) return;

                SetOfficeProgress(5.0, "Erstelle XML-Konfiguration...", "", "", "Berechne ausgewählte Office-Komponenten...");
                string xmlContent = GenerateConfigurationXml(officeDir);
                string xmlPath = Path.Combine(officeDir, "configuration.xml");
                File.WriteAllText(xmlPath, xmlContent);

                // PHASE 1: FRISCHER ECHTER LIVE-DOWNLOAD
                SetOfficeProgress(5.0, "Starte Live-Download von Microsoft CDN...", "📦 Verbinde...", "", "Initialisiere Office CDN Stream...");
                await Task.Run(async () => {
                    ProcessStartInfo psiDownload = new ProcessStartInfo {
                        FileName = setupExe,
                        Arguments = "/download \"" + xmlPath + "\"",
                        WorkingDirectory = officeDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using (Process proc = Process.Start(psiDownload)) {
                        _currentOfficeProcess = proc;
                        UpdateSidebarActivitySpinners();
                        if (proc != null) {
                            while (!proc.HasExited) {
                                if (_officeCts.IsCancellationRequested) {
                                    try { proc.Kill(); } catch {}
                                    break;
                                }
                                await Task.Delay(500);
                                long curBytes = GetDirectorySize(officeDataDir);
                                double mb = curBytes / 1048576.0;
                                double progressPercent = Math.Min(74.0, 5.0 + (curBytes / 2400000000.0 * 69.0));
                                SetOfficeProgress(progressPercent, "Lade Microsoft Office Suite herunter...", string.Format("📦 {0:0.0} MB heruntergeladen", mb), "", "Paketdaten werden gestreamt...");
                            }
                            if (!_officeCts.IsCancellationRequested) proc.WaitForExit();
                        }
                    }
                });

                if (_officeCts.IsCancellationRequested) {
                    try { if (Directory.Exists(officeDataDir)) Directory.Delete(officeDataDir, true); } catch {}
                    SetOfficeProgress(0, "⚠️ Download durch Benutzer abgebrochen!", "", "", "");
                    return;
                }

                // PHASE 2: LOKALE INSTALLATION (SSD) DIREKT AUS DEN FRISCHEN DATEN
                SetOfficeProgress(75.0, "Installiere ausgewählte Office-Apps lokal auf SSD...", "⚡ Lokale ClickToRun-Installation läuft", "⏱️ Bitte warten...", "Bereite OfficeClickToRun Engine vor...", true);
                await Task.Run(async () => {
                    ProcessStartInfo psiInstall = new ProcessStartInfo {
                        FileName = setupExe,
                        Arguments = "/configure \"" + xmlPath + "\"",
                        WorkingDirectory = officeDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (Process proc = Process.Start(psiInstall)) {
                        _currentOfficeProcess = proc;
                        if (proc != null) {
                            Stopwatch swInstall = Stopwatch.StartNew();
                            while (!proc.HasExited) {
                                if (_officeCts.IsCancellationRequested) {
                                    try { proc.Kill(); } catch {}
                                    break;
                                }
                                await Task.Delay(500);
                                double sec = swInstall.Elapsed.TotalSeconds;
                                string elapsedStr = string.Format("⏱️ Laufzeit: {0:D2}:{1:D2}", (int)sec / 60, (int)sec % 60);
                                SetOfficeProgress(75.0, "Installiere ausgewählte Office-Apps auf SSD...", "⚡ Lokale ClickToRun-Installation läuft", elapsedStr, "Schreibe Word, Excel & Office-Binärdateien auf Datenträger...", true);
                            }
                            if (!_officeCts.IsCancellationRequested) proc.WaitForExit();
                        }
                    }
                });

                if (_officeCts.IsCancellationRequested) {
                    SetOfficeProgress(0, "⚠️ Installation durch Benutzer abgebrochen!", "", "", "");
                    return;
                }

                // PHASE 3: AUTOMATISCHE BEREINIGUNG DER TEMPORÄREN DOWNLOAD-DATEIEN
                SetOfficeProgress(98.0, "Bereinige temporäre Installationsdateien...", "🧹 Speicherplatz wird freigegeben", "", "Entferne Office-Cache...");
                await Task.Run(() => {
                    try {
                        if (Directory.Exists(officeDataDir)) {
                            Directory.Delete(officeDataDir, true);
                        }
                    } catch {}
                });

                SetAllOfficeCheckboxes(false, false);
                RefreshOfficeStatusUI();
                UpdateOfficeActionButtonState();

                if (autoActivateOhook) {
                    // Fortschrittsleiste durchgehend sichtbar halten: Nach der Installation ohne
                    // Unterbrechung direkt in die Lizenz-Aktivierung übergehen.
                    SetOfficeProgress(90.0, "✅ Installation abgeschlossen – aktiviere Lizenz...", "🎉 Alle ausgewählten Apps sind einsatzbereit.", "", "Ohook-Aktivierung wird ausgeführt...", true);

                    bool ohookHandled = await AutoActivateOhookAfterInstallAsync();

                    if (!ohookHandled) {
                        SetOfficeProgress(100.0, "✅ Installation & Lizenzierung abgeschlossen!", "🎉 Alle ausgewählten Apps sind einsatzbereit.", "", "Fertig!");
                    }
                } else {
                    SetOfficeProgress(100.0, "✅ Installation abgeschlossen!", "🎉 Alle ausgewählten Apps sind einsatzbereit.", "", "Fertig!");
                }
            } catch (Exception ex) {
                SetOfficeProgress(0, "Fehler: " + ex.Message, "", "", "");
                ShowAlertModal("Office-Installation", "Fehler bei der Installation:\n" + ex.Message, "OK", "❌", "#EF4444");
            } finally {
                _currentOfficeProcess = null;
                _officeCts = null;
                UpdateSidebarActivitySpinners();
                SetOfficeCheckboxesEnabled(true);
                SetOfficeActionButtons(true);
            }
        }

        private static string BuildOfficeXml(IEnumerable<string> excludedApps) {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("<Configuration>");
            sb.AppendLine("  <Add OfficeClientEdition=\"64\" Channel=\"Current\">");
            sb.AppendLine("    <Product ID=\"O365ProPlusRetail\">");
            sb.AppendLine("      <Language ID=\"de-de\" />");
            foreach (string app in excludedApps) {
                sb.AppendFormat("      <ExcludeApp ID=\"{0}\" />\r\n", app);
            }
            sb.AppendLine("    </Product>");
            sb.AppendLine("  </Add>");
            sb.AppendLine("  <Display Level=\"None\" AcceptEULA=\"TRUE\" />");
            sb.AppendLine("  <Property Name=\"FORCEAPPSHUTDOWN\" Value=\"TRUE\" />");
            sb.AppendLine("  <Property Name=\"AUTOACTIVATE\" Value=\"1\" />");
            sb.Append("</Configuration>");
            return sb.ToString();
        }

        private string GenerateConfigXmlToKeepRemainingApps(HashSet<string> appsToRemove) {
            var excluded = new List<string>();

            foreach (var app in AllOfficeAppKeys) {
                if (!IsOfficeComponentInstalled(app) || appsToRemove.Contains(app)) {
                    excluded.Add(app);
                }
            }

            if (excluded.Count >= AllOfficeAppKeys.Length) {
                return "<Configuration>\r\n" +
                       "  <Remove All=\"TRUE\" />\r\n" +
                       "  <Display Level=\"None\" AcceptEULA=\"TRUE\" />\r\n" +
                       "  <Property Name=\"FORCEAPPSHUTDOWN\" Value=\"TRUE\" />\r\n" +
                       "</Configuration>";
            }

            return BuildOfficeXml(excluded);
        }

        private async Task ExecuteOfficeUninstallSelectedAppsAsync(string officeDir) {
            var checkedApps = new List<string>();
            foreach (var app in AllOfficeAppKeys) {
                if (IsOfficeAppCheckboxChecked(app)) checkedApps.Add(app);
            }

            if (checkedApps.Count == 0) {
                SetOfficeProgress(0, "Bitte wähle in der Tabelle mindestens eine Komponente zum Entfernen aus.", "💡 Tipp: Hake die zu entfernenden Programme an.", "", "");
                return;
            }

            bool ok = await ShowConfirmModalAsync(
                "Office-Komponenten entfernen",
                "Möchtest du folgende ausgewählte Komponenten wirklich deinstallieren?\n\n• " + string.Join("\n• ", checkedApps) + "\n\n(Alle anderen installierten Programme bleiben erhalten)",
                "Jetzt deinstallieren",
                "Abbrechen",
                "🗑️",
                "#EF4444"
            );
            if (!ok) return;

            _officeCts = new CancellationTokenSource();
            SetOfficeActionButtons(false);
            SetOfficeCheckboxesEnabled(false);

            try {
                if (!Directory.Exists(officeDir)) Directory.CreateDirectory(officeDir);
                string setupExe = Path.Combine(officeDir, "setup.exe");

                if (!File.Exists(setupExe)) {
                    SetOfficeProgress(15, "Bereite Office Deployment Tool für Deinstallation vor...", "", "", "Prüfe ODT Engine...");
                    if (!await EnsureOdtAsync(officeDir) || _officeCts.IsCancellationRequested) return;
                }

                if (_officeCts.IsCancellationRequested) return;

                SetOfficeProgress(30, "Berechne Deinstallations-Plan...", "⚡ Bereite ODT vor", "", "Generiere geräuschlose Deinstallations-Konfiguration...");
                var appsToRemove = new HashSet<string>(checkedApps, StringComparer.OrdinalIgnoreCase);
                string xmlContent = GenerateConfigXmlToKeepRemainingApps(appsToRemove);
                string xmlPath = Path.Combine(officeDir, "uninstall_custom.xml");
                File.WriteAllText(xmlPath, xmlContent);

                SetOfficeProgress(50, "Entferne ausgewählte Komponenten im Hintergrund...", "⚡ Geräuschlose Deinstallation läuft", "⏱️ Bitte warten...", "Führe Office Deployment Tool aus...", true);
                await Task.Run(async () => {
                    ProcessStartInfo psi = new ProcessStartInfo {
                        FileName = setupExe,
                        Arguments = "/configure \"" + xmlPath + "\"",
                        WorkingDirectory = officeDir,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };
                    using (Process proc = Process.Start(psi)) {
                        _currentOfficeProcess = proc;
                        if (proc != null) {
                            Stopwatch sw = Stopwatch.StartNew();
                            while (!proc.HasExited) {
                                if (_officeCts.IsCancellationRequested) {
                                    try { proc.Kill(); } catch {}
                                    break;
                                }
                                await Task.Delay(500);
                                double sec = sw.Elapsed.TotalSeconds;
                                string elapsedStr = string.Format("⏱️ Laufzeit: {0:D2}:{1:D2}", (int)sec / 60, (int)sec % 60);
                                SetOfficeProgress(50, "Entferne ausgewählte Komponenten im Hintergrund...", "⚡ Bereinige Office-Verzeichnisse", elapsedStr, "Geräuschlose Deinstallation via ClickToRun...", true);
                            }
                            if (!_officeCts.IsCancellationRequested) proc.WaitForExit(300000);
                        }
                    }
                });

                if (_officeCts.IsCancellationRequested) {
                    SetOfficeProgress(0, "⚠️ Deinstallation durch Benutzer abgebrochen!", "", "", "");
                    return;
                }

                try { if (File.Exists(xmlPath)) File.Delete(xmlPath); } catch {}

                SetOfficeProgress(100, "✅ Ausgewählte Komponenten erfolgreich entfernt!", "🎉 System aktualisiert", "", "Fertig!", false);
                SetAllOfficeCheckboxes(false, false);
                RefreshOfficeStatusUI();
                UpdateOfficeActionButtonState();
            } catch (Exception ex) {
                SetOfficeProgress(0, "Fehler: " + ex.Message, "", "", "");
            } finally {
                _currentOfficeProcess = null;
                _officeCts = null;
                UpdateSidebarActivitySpinners();
                SetOfficeCheckboxesEnabled(true);
                SetOfficeActionButtons(true);
            }
        }


        private string GenerateConfigurationXml(string officeDir) {
            var excluded = new List<string>();

            foreach (var app in AllOfficeAppKeys) {
                bool isChecked = IsOfficeAppCheckboxChecked(app);
                bool isInstalled = IsOfficeComponentInstalled(app);

                // Ein Programm wird NUR DANN in <ExcludeApp> aufgenommen,
                // wenn es weder aktuell installiert ist NOCH vom Benutzer zum Installieren markiert wurde!
                // Dadurch wird sichergestellt, dass bereits installierte Office-Apps beim Hinzufügen weiterer Komponenten NIEMALS gelöscht werden.
                if (!isChecked && !isInstalled) {
                    excluded.Add(app);
                }
            }

            return BuildOfficeXml(excluded);
        }

        // =========================================================================
        // LIVE OFFICE PER-APP STANDALONE STATUS
        // =========================================================================
        private bool CheckAppxInstalled(string packagePrefix) {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\Repository\Packages")) {
                    if (key != null) {
                        return key.GetSubKeyNames().Any(sub => sub.StartsWith(packagePrefix, StringComparison.OrdinalIgnoreCase));
                    }
                }
            } catch {}
            return false;
        }

        private bool CheckAppInstalled(string[] candidatePaths, string regAppPath = null, string appxPrefix = null) {
            try {
                if (candidatePaths != null && candidatePaths.Any(p => !string.IsNullOrEmpty(p) && File.Exists(p))) return true;
                if (!string.IsNullOrEmpty(regAppPath)) {
                    string path = SoftwareDetector.CheckAppPaths(regAppPath);
                    if (!string.IsNullOrEmpty(path) && File.Exists(path)) return true;
                }
                if (!string.IsNullOrEmpty(appxPrefix) && CheckAppxInstalled(appxPrefix)) return true;
            } catch {}
            return false;
        }

        private string GetOfficeInstalledVersion() {
            try {
                // 1. ClickToRun Configuration in Registry
                string ver = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Office\ClickToRun\Configuration", "VersionToReport", null) as string;
                if (!string.IsNullOrEmpty(ver)) return ver.Trim();
                ver = Registry.GetValue(@"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Office\ClickToRun\Configuration", "ClientVersionToReport", null) as string;
                if (!string.IsNullOrEmpty(ver)) return ver.Trim();

                // 2. Scan via SoftwareDetector
                var info = SoftwareDetector.Detect(@"Microsoft 365|Microsoft Office.*(ProPlus|Standard|Home|Business|Professional)");
                if (info != null && info.IsInstalled && !string.IsNullOrEmpty(info.Version) && !info.Version.Equals("Installiert", StringComparison.OrdinalIgnoreCase)) {
                    return info.Version.Trim();
                }

                // 3. Fallback: Word file version
                string wordPath = SoftwareDetector.CheckAppPaths("winword.exe");
                if (!string.IsNullOrEmpty(wordPath) && File.Exists(wordPath)) {
                    var vi = FileVersionInfo.GetVersionInfo(wordPath);
                    if (!string.IsNullOrEmpty(vi.FileVersion)) return vi.FileVersion.Trim();
                }
            } catch {}
            return null;
        }

        private static string GetOfficeBaseDescription(string key) {
            switch (key.ToLowerInvariant()) {
                case "word": return "Textverarbeitung";
                case "excel": return "Tabellenkalkulation";
                case "powerpoint": return "Präsentationen";
                case "outlook": return "E-Mail & Kalender";
                case "onenote": return "Digitales Notizbuch";
                case "access": return "Datenbankverwaltung";
                case "publisher": return "Layouts & Druckpublikationen";
                case "teams": return "Zusammenarbeit & Chats";
                default: return "Office-Anwendung";
            }
        }

        private static string GetOfficeAppEstimatedSize(string key) {
            switch (key.ToLowerInvariant()) {
                case "word": return "~1.2 GB";
                case "excel": return "~950 MB";
                case "powerpoint": return "~850 MB";
                case "outlook": return "~1.1 GB";
                case "onenote": return "~450 MB";
                case "access": return "~600 MB";
                case "publisher": return "~500 MB";
                case "teams": return "~400 MB";
                default: return "~500 MB";
            }
        }

        private void UpdateOfficeAppRow(string key, bool isInstalled, bool isActivated, Border pill, TextBlock txtPill, TextBlock descTb, string officeVer) {
            if (pill == null || txtPill == null) return;
            if (isInstalled) {
                if (isActivated) {
                    UIHelper.SetPill(pill, txtPill, true, "INSTALLIERT • AKTIVIERT", "");
                } else {
                    pill.Background = UIHelper.BrushAmberBg;
                    pill.BorderBrush = UIHelper.BrushAmberBorder;
                    txtPill.Text = "INSTALLIERT (NICHT AKTIVIERT)";
                    txtPill.Foreground = UIHelper.BrushAmberText;
                }
            } else {
                UIHelper.SetPill(pill, txtPill, false, "", "NICHT INSTALLIERT");
            }

            if (descTb != null) {
                string baseDesc = GetOfficeBaseDescription(key);
                string size = GetOfficeAppEstimatedSize(key);
                if (isInstalled && !string.IsNullOrEmpty(officeVer)) {
                    string vStr = officeVer.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? officeVer : "v" + officeVer;
                    descTb.Text = string.Format("{0}  •  {1}  •  {2}", baseDesc, vStr, size);
                } else {
                    descTb.Text = string.Format("{0}  •  {1}", baseDesc, size);
                }
            }
        }

        private Border GetOfficeRowBorder(string appKey) {
            switch (appKey.ToLowerInvariant()) {
                case "word": return rowOfficeWord;
                case "excel": return rowOfficeExcel;
                case "powerpoint": return rowOfficePowerPoint;
                case "outlook": return rowOfficeOutlook;
                case "onenote": return rowOfficeOneNote;
                case "access": return rowOfficeAccess;
                case "publisher": return rowOfficePublisher;
                case "teams": return rowOfficeTeams;
                default: return null;
            }
        }

        private void FilterOfficeApps() {
            if (window == null) return;
            window.Dispatcher.Invoke(() => {
                bool showAll = radFilterOfficeAll == null || radFilterOfficeAll.IsChecked == true;
                bool showInstalled = radFilterOfficeInstalled != null && radFilterOfficeInstalled.IsChecked == true;
                bool showNotInstalled = radFilterOfficeNotInstalled != null && radFilterOfficeNotInstalled.IsChecked == true;

                int installedCount = 0;
                int notInstalledCount = 0;

                foreach (var key in AllOfficeAppKeys) {
                    bool installed = IsOfficeComponentInstalled(key);
                    if (installed) installedCount++; else notInstalledCount++;

                    var row = GetOfficeRowBorder(key);
                    if (row != null) {
                        if (showAll) {
                            row.Visibility = Visibility.Visible;
                        } else if (showInstalled) {
                            row.Visibility = installed ? Visibility.Visible : Visibility.Collapsed;
                        } else if (showNotInstalled) {
                            row.Visibility = !installed ? Visibility.Visible : Visibility.Collapsed;
                        }
                    }
                }

                if (radFilterOfficeAll != null) radFilterOfficeAll.Content = string.Format("Alle ({0})", AllOfficeAppKeys.Length);
                if (radFilterOfficeInstalled != null) radFilterOfficeInstalled.Content = string.Format("Installiert ({0})", installedCount);
                if (radFilterOfficeNotInstalled != null) radFilterOfficeNotInstalled.Content = string.Format("Nicht installiert ({0})", notInstalledCount);

                UpdateOfficeActionButtonState();
            });
        }

        private void RefreshOfficeStatusUI() {
            Task.Run(() => {
                try {
                    var actState = MasActivator.GetOfficeActivationState();
                    bool isActivated = (actState == MasActivator.OfficeActivationState.ActivatedByOhook || actState == MasActivator.OfficeActivationState.ActivatedOther);
                    string officeVer = GetOfficeInstalledVersion();

                    var apps = new[] {
                        new { Key = "Word", Pill = pillOfficeWord, TxtPill = txtPillOfficeWord, Desc = txtDescOfficeWord },
                        new { Key = "Excel", Pill = pillOfficeExcel, TxtPill = txtPillOfficeExcel, Desc = txtDescOfficeExcel },
                        new { Key = "PowerPoint", Pill = pillOfficePowerPoint, TxtPill = txtPillOfficePowerPoint, Desc = txtDescOfficePowerPoint },
                        new { Key = "Outlook", Pill = pillOfficeOutlook, TxtPill = txtPillOfficeOutlook, Desc = txtDescOfficeOutlook },
                        new { Key = "OneNote", Pill = pillOfficeOneNote, TxtPill = txtPillOfficeOneNote, Desc = txtDescOfficeOneNote },
                        new { Key = "Access", Pill = pillOfficeAccess, TxtPill = txtPillOfficeAccess, Desc = txtDescOfficeAccess },
                        new { Key = "Publisher", Pill = pillOfficePublisher, TxtPill = txtPillOfficePublisher, Desc = txtDescOfficePublisher },
                        new { Key = "Teams", Pill = pillOfficeTeams, TxtPill = txtPillOfficeTeams, Desc = txtDescOfficeTeams }
                    };
                    var results = new List<Tuple<string, Border, TextBlock, TextBlock, bool>>();
                    foreach (var a in apps) {
                        results.Add(Tuple.Create(a.Key, a.Pill, a.TxtPill, a.Desc, IsOfficeComponentInstalled(a.Key)));
                    }
                    if (window != null) {
                        window.Dispatcher.BeginInvoke((Action)(() => {
                            foreach (var res in results) {
                                UpdateOfficeAppRow(res.Item1, res.Item5, isActivated, res.Item2, res.Item3, res.Item4, officeVer);
                            }
                            FilterOfficeApps();
                        }));
                    }
                } catch {}
            });
            RefreshOfficeActivationStatusUI();
        }

        public void SetupOfficeEvents() {
            string userDownloads = RegistryHelper.GetUserDownloadsFolder();
            string targetDir = Path.Combine(userDownloads, "Installationen");
            string officeDir = Path.Combine(targetDir, "Office");

            if (radFilterOfficeAll != null) radFilterOfficeAll.Checked += (s, e) => FilterOfficeApps();
            if (radFilterOfficeInstalled != null) radFilterOfficeInstalled.Checked += (s, e) => FilterOfficeApps();
            if (radFilterOfficeNotInstalled != null) radFilterOfficeNotInstalled.Checked += (s, e) => FilterOfficeApps();

            foreach (var chk in GetOfficeCheckboxes()) {
                if (chk != null) {
                    chk.Checked += (s, e) => { if (!isUpdatingUI) UpdateOfficeActionButtonState(); };
                    chk.Unchecked += (s, e) => { if (!isUpdatingUI) UpdateOfficeActionButtonState(); };
                }
            }

            if (chkOfficeSelectAll != null) {
                chkOfficeSelectAll.Checked += (s, e) => {
                    if (isUpdatingUI) return;
                    SetAllOfficeCheckboxes(true);
                    UpdateOfficeActionButtonState();
                };
                chkOfficeSelectAll.Unchecked += (s, e) => {
                    if (isUpdatingUI) return;
                    SetAllOfficeCheckboxes(false);
                    UpdateOfficeActionButtonState();
                };
            }

            if (btnInstallSelectedApps != null) {
                btnInstallSelectedApps.Click += async (s, e) => {
                    int toInstall = 0;
                    int toUninstall = 0;
                    foreach (var key in AllOfficeAppKeys) {
                        if (IsOfficeAppCheckboxChecked(key)) {
                            if (IsOfficeComponentInstalled(key)) toUninstall++;
                            else toInstall++;
                        }
                    }

                    if (toInstall > 0) {
                        await ExecuteOfficeInstallSelectedAppsAsync(officeDir);
                    } else if (toUninstall > 0) {
                        await ExecuteOfficeUninstallSelectedAppsAsync(officeDir);
                    }
                };
            }

            if (btnOhookActivate != null) {
                btnOhookActivate.Click += async (s, e) => {
                    bool uninstall = await Task.Run(() => MasActivator.IsOhookInstalled());
                    if (!uninstall) {
                        string msg = "Möchtest du Microsoft Office über die Ohook-Methode aktivieren?\n\n" +
                                     "⚠️ Wichtiger rechtlicher Hinweis (Nicht empfohlen):\n" +
                                     "• Ohook ist ein Open-Source-Bypass (sppc.dll Hook) zur lokalen Lizenzemulation\n" +
                                     "• Rechtlich nicht offiziell lizenziert & verstößt gegen Microsoft-Lizenzbestimmungen\n" +
                                     "• Empfohlene & legale Methode: Offizieller Microsoft Produktschlüssel / MS 365 Abo\n" +
                                     "• Fortfahren nur, wenn du weißt was du tust – Ausführung auf eigene Gefahr!\n\n" +
                                     "Technische Details:\n" +
                                     "• Emuliert die Lizenzprüfung lokal in den Office-Binärdateien (sppc.dll Hook)\n" +
                                     "• Kein Hintergrunddienst / KMS-Server nötig\n" +
                                     "• Click-to-Run Updates von Microsoft bleiben erhalten";

                        bool ok = await ShowConfirmModalAsync(
                            "Office-Lizenzierung (Ohook)",
                            msg,
                            "Auf eigene Gefahr aktivieren",
                            "Abbrechen",
                            "⚠️",
                            "#EF4444"
                        );
                        if (!ok) return;
                    } else {
                        string msg = "Möchtest du die Ohook-Lizenzierung von Microsoft Office vollständig entfernen?\n\n" +
                                     "Was passiert bei der Deaktivierung?\n" +
                                     "• Der sppc.dll Hook wird restlos aus allen Office-Verzeichnissen gelöscht\n" +
                                     "• Stellt den sauberen, unmodifizierten Originalzustand von Microsoft wieder her\n" +
                                     "• Empfohlen, um Office legal mit eigenem Lizenzschlüssel / MS-Konto zu nutzen\n" +
                                     "• Installierte Programme (Word, Excel etc.) bleiben erhalten, fordern danach aber eine offizielle Aktivierung an.";

                        bool ok = await ShowConfirmModalAsync(
                            "Office-Lizenzierung aufheben",
                            msg,
                            "Jetzt sauber entfernen",
                            "Abbrechen",
                            "🧹",
                            "#10B981"
                        );
                        if (!ok) return;
                    }
                    await RunOhookActionAsync(uninstall);
                };
            }

            if (btnCancelOfficeAction != null) {
                btnCancelOfficeAction.Click += (s, e) => {
                    CancelOfficeOperation();
                };
            }

            UpdateOfficeActionButtonState();
        }

        private void RefreshOfficeActivationStatusUI() {
            Task.Run(() => {
                try {
                    var state = MasActivator.GetOfficeActivationState();
                    if (window != null) {
                        window.Dispatcher.BeginInvoke((Action)(() => ApplyOfficeActivationState(state)));
                    }
                } catch { }
            });
        }

        private void ApplyOfficeActivationState(MasActivator.OfficeActivationState state) {
            switch (state) {
                case MasActivator.OfficeActivationState.NotInstalled:
                    SetOfficeActivationPill("OFFICE NICHT INSTALLIERT", UIHelper.BrushGrayText, UIHelper.BrushGrayBg, UIHelper.BrushGrayBorder);
                    SetOfficeActivationInfo("Es wurde keine Office-Installation erkannt. Installiere Office zuerst über die Liste.");
                    SetOhookButtonText(false);
                    if (btnOhookActivate != null) btnOhookActivate.IsEnabled = false;
                    break;
                case MasActivator.OfficeActivationState.ActivatedByOhook:
                case MasActivator.OfficeActivationState.ActivatedOther:
                    SetOfficeActivationPill("AKTIVIERT", UIHelper.BrushGreenText, UIHelper.BrushGreenBg, UIHelper.BrushGreenBorder);
                    SetOfficeActivationInfo("Office ist dauerhaft aktiviert.");
                    SetOhookButtonText(true);
                    if (btnOhookActivate != null) btnOhookActivate.IsEnabled = true;
                    break;
                case MasActivator.OfficeActivationState.NotActivated:
                    SetOfficeActivationPill("NICHT AKTIVIERT", UIHelper.BrushRedText, UIHelper.BrushRedBg, UIHelper.BrushRedBorder);
                    SetOfficeActivationInfo("Office ist installiert, aber nicht aktiviert.");
                    SetOhookButtonText(false);
                    if (btnOhookActivate != null) btnOhookActivate.IsEnabled = true;
                    break;
            }
        }

        private void SetOfficeActivationPill(string text, Brush fg, Brush bg, Brush border) {
            if (pillOfficeActivation != null) {
                pillOfficeActivation.Background = bg;
                pillOfficeActivation.BorderBrush = border;
            }
            if (txtPillOfficeActivation != null) {
                txtPillOfficeActivation.Text = text;
                txtPillOfficeActivation.Foreground = fg;
            }
        }

        private void SetOfficeActivationInfo(string text) {
            if (txtOfficeActivationInfo != null) txtOfficeActivationInfo.Text = text;
        }

        private void SetOhookButtonText(bool activated) {
            if (btnOhookActivate != null) {
                btnOhookActivate.Content = activated ? "🔓 Lizenz deaktivieren" : "🔑 Lizenz aktivieren";
                btnOhookActivate.ToolTip = activated ? "Office-Lizenzierung aufheben" : "Permanente Office-Lizenzierung aktivieren";
            }
        }

        /// <summary>
        /// Fuehrt die Ohook-Aktivierung bzw. -Deaktivierung aus und spiegelt den Fortschritt in
        /// der Fortschrittsleiste wider. Wird sowohl vom Button als auch automatisch verwendet.
        /// </summary>
        private async Task RunOhookActionAsync(bool uninstall) {
            if (btnOhookActivate != null) btnOhookActivate.IsEnabled = false;

            string verb = uninstall ? "deaktiviert" : "aktiviert";
            SetOfficeProgress(50, "Office-Lizenz wird " + verb + "...", "", "", "Der Vorgang läuft unsichtbar im Hintergrund.", true);

            try {
                bool ok = await Task.Run(() => uninstall ? MasActivator.RunOhookUninstall() : MasActivator.RunOhook());
                if (ok) {
                    SetOfficeProgress(100, "Office-Lizenz wurde erfolgreich " + verb + ".", "", "", "Vorgang abgeschlossen.");
                } else {
                    SetOfficeProgress(0, "Lizenz-Vorgang fehlgeschlagen.", "", "", "Bitte Internetverbindung prüfen und erneut versuchen.");
                    MessageBox.Show("Der Lizenz-Vorgang konnte nicht abgeschlossen werden. Bitte Internetverbindung prüfen und erneut versuchen.", "Znipe Optimization Tool", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            } catch (Exception ex) {
                SetOfficeProgress(0, "Lizenz-Vorgang fehlgeschlagen.", "", "", ex.Message);
                MessageBox.Show("Der Lizenz-Vorgang ist fehlgeschlagen: " + ex.Message, "Znipe Optimization Tool", MessageBoxButton.OK, MessageBoxImage.Error);
            } finally {
                if (btnOhookActivate != null) btnOhookActivate.IsEnabled = true;
                RefreshOfficeStatusUI();
            }
        }

        /// <summary>
        /// Wird nach einer erfolgreichen Office-Installation aufgerufen und aktiviert Ohook
        /// automatisch, sofern Office installiert ist und noch keine Ohook-Aktivierung vorliegt.
        /// Gibt true zurueck, wenn die Ohook-Aktivierung ausgefuehrt wurde (die Fortschrittsleiste
        /// wurde dann bereits durch RunOhookActionAsync auf "abgeschlossen" gesetzt, sodass sie
        /// durchgehend sichtbar bleibt).
        /// </summary>
        private async Task<bool> AutoActivateOhookAfterInstallAsync() {
            try {
                bool shouldActivate = await Task.Run(() => MasActivator.IsOfficeInstalled() && !MasActivator.IsOhookInstalled());
                if (!shouldActivate) return false;

                // Kurze Pause, damit die Leiste ruhig bleibt (kein Sprung).
                await Task.Delay(1200);
                await RunOhookActionAsync(false);
                return true;
            } catch { }
            return false;
        }
    }
}
