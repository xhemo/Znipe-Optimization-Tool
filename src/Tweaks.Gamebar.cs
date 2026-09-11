using System;
using System.Text.RegularExpressions;
using System.Windows.Media;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Logik für den Xbox Game Bar Reiter (KGL & V-Cache Sync) nach dem KISS-Prinzip.
    /// </summary>
    public partial class MainWindowLogic {
        // =========================================================================
        // NATIVE GAMEBAR & KGL LIVE-ERKENNUNG
        // =========================================================================
        private void RefreshGamebarUI() {
            try {
                string kglRevStr = RegistryHelper.GetString(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "KGLRevision");
                string kglGcsStr = RegistryHelper.GetString(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "KGLToGCSUpdatedRevision");

                if (txtKglLoaded != null) txtKglLoaded.Text = !string.IsNullOrEmpty(kglRevStr) ? kglRevStr : "Nicht initialisiert";
                if (txtKglService != null) txtKglService.Text = !string.IsNullOrEmpty(kglGcsStr) ? kglGcsStr : "Nicht initialisiert";

                if (!string.IsNullOrEmpty(kglRevStr) && !string.IsNullOrEmpty(kglGcsStr)) {
                    if (kglRevStr == kglGcsStr) {
                        if (borderKglSync != null) { borderKglSync.Background = UIHelper.BrushGreenBg; borderKglSync.BorderBrush = UIHelper.BrushGreenBorder; }
                        if (txtKglSyncTitle != null) { txtKglSyncTitle.Text = "✅ KGL-Versionen sind identisch (Revision " + kglRevStr + ")"; txtKglSyncTitle.Foreground = UIHelper.BrushGreenText; }
                        if (txtKglSyncDesc != null) { txtKglSyncDesc.Text = "Game Bar und der Background-Service laufen synchron auf der gleichen KGL-Revision. Spiele werden optimal für Core-Parking & V-Cache erkannt."; txtKglSyncDesc.Foreground = UIHelper.GetBrush("#A7F3D0"); }
                        UIHelper.SetPill(pillKglStatus, txtKglPill, true, "OPTIMAL");
                    } else {
                        if (borderKglSync != null) { borderKglSync.Background = UIHelper.BrushAmberBg; borderKglSync.BorderBrush = UIHelper.BrushAmberBorder; }
                        if (txtKglSyncTitle != null) { txtKglSyncTitle.Text = "⚠️ KGL-Versionen asynchron (Loaded: " + kglRevStr + " ≠ Service: " + kglGcsStr + ")"; txtKglSyncTitle.Foreground = UIHelper.BrushAmberText; }
                        if (txtKglSyncDesc != null) { txtKglSyncDesc.Text = "Die KGL-Revisionen stimmen nicht überein. Öffne die Game Bar mit [Win + G], um den Sync zu erzwingen."; txtKglSyncDesc.Foreground = UIHelper.GetBrush("#FDE68A"); }
                        UIHelper.SetPillWarning(pillKglStatus, txtKglPill, "ASYNCHRON");
                    }
                } else {
                    if (borderKglSync != null) { borderKglSync.Background = UIHelper.BrushGrayBg; borderKglSync.BorderBrush = UIHelper.BrushGrayBorder; }
                    if (txtKglSyncTitle != null) { txtKglSyncTitle.Text = "ℹ️ Noch keine KGL-Revision registriert"; txtKglSyncTitle.Foreground = UIHelper.BrushGrayText; }
                    if (txtKglSyncDesc != null) { txtKglSyncDesc.Text = "Starte die Game Bar einmal über [Win + G], damit Windows die Known Game List (KGL) initialisiert."; txtKglSyncDesc.Foreground = UIHelper.GetBrush("#D1D5DB"); }
                    UIHelper.SetPill(pillKglStatus, txtKglPill, false, "", "UNBEKANNT");
                }

                // Appx Version
                string baseKey = @"SOFTWARE\Classes\Local Settings\Software\Microsoft\Windows\CurrentVersion\AppModel\PackageRepository\Packages";
                string version = "Installiert (Aktiv)";

                foreach (var hive in new[] { Registry.CurrentUser, Registry.LocalMachine }) {
                    using (RegistryKey key = hive.OpenSubKey(baseKey)) {
                        if (key != null) {
                            foreach (string sub in key.GetSubKeyNames()) {
                                if (sub.StartsWith("Microsoft.XboxGamingOverlay_", StringComparison.OrdinalIgnoreCase)) {
                                    Match m = Regex.Match(sub, @"Microsoft\.XboxGamingOverlay_([0-9\.]+)");
                                    if (m.Success) {
                                        version = "v" + m.Groups[1].Value;
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    if (version != "Installiert (Aktiv)") break;
                }

                if (txtGamebarVersion != null) txtGamebarVersion.Text = version;

                // AMD 3D V-Cache Service Check
                if (txtAmd3dStatus != null) {
                    txtAmd3dStatus.Text = RegistryHelper.KeyExists(RegistryHive.LocalMachine, @"SYSTEM\CurrentControlSet\Services\amd3dvcachesvc")
                        ? "Installiert & Aktiv (amd3dvcacheSvc)"
                        : "Nicht vorhanden (Intel CPU oder Standard)";
                }
            } catch {
                if (txtGamebarVersion != null) txtGamebarVersion.Text = "Installiert (Aktiv)";
            }
        }

        private void LaunchGamebar() {
            if (!ProcessRunner.Start("ms-gamebar:")) {
                ProcessRunner.Start("explorer.exe", "ms-gamebar:");
            }
        }

        public void SetupGamebarEvents() {
            if (btnOpenGamebar != null) btnOpenGamebar.Click += (s, e) => LaunchGamebar();
        }
    }
}
