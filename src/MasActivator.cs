using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Management;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Integration des Open-Source-Aktivators "Microsoft Activation Scripts" (MAS).
    /// Fuehrt die Ohook-Methode (Office-Aktivierung) automatisch nach der Installation
    /// bzw. beim ersten Start des Tools im Hintergrund aus und kann Ohook auch wieder
    /// entfernen. Quelle: https://github.com/massgravel/Microsoft-Activation-Scripts
    /// </summary>
    public static class MasActivator {
        public enum OfficeActivationState {
            NotInstalled,
            ActivatedByOhook,
            ActivatedOther,
            NotActivated
        }

        // Offizielles MAS-All-In-One-Skript (wird zur Laufzeit heruntergeladen und versteckt ausgefuehrt).
        private const string MasScriptUrl =
            "https://raw.githubusercontent.com/massgravel/Microsoft-Activation-Scripts/master/MAS/All-In-One-Version-KL/MAS_AIO.cmd";

        // Office ApplicationId in Windows SoftwareLicensingProduct (fuer Lizenzstatus-Abfrage).
        private const string OfficeAppId = "0ff1ce15-a989-479d-af46-f275c6370663";

        private const string AutoRunSubKey    = @"Software\ZnipeOptimizationTool";
        private const string AutoRunValueName = "MasOhookAutoRan";

        /// <summary>
        /// Wird einmalig beim App-Start aufgerufen. Aktiviert Office via Ohook, sofern
        /// (1) die automatische Aktivierung noch nicht erfolgt ist und (2) Office installiert ist.
        /// Laeuft non-blocking und versteckt im Hintergrund; Fehler blockieren den Start nie.
        /// </summary>
        public static void RunOhookOnFirstLaunch() {
            try {
                if (HasAutoRunCompleted()) { Log("Auto-Run: bereits ausgefuehrt, uebersprungen."); return; }
                if (!IsOfficeInstalled()) { Log("Auto-Run: kein Office installiert, uebersprungen."); return; }
                if (IsOhookInstalled()) { MarkAutoRunCompleted(); Log("Auto-Run: Ohook bereits aktiv, uebersprungen."); return; }

                Log("Auto-Run: starte Ohook-Aktivierung...");
                if (RunOhook()) {
                    MarkAutoRunCompleted();
                    Log("Auto-Run: Ohook-Aktivierung erfolgreich abgeschlossen.");
                } else {
                    Log("Auto-Run: Ohook-Aktivierung fehlgeschlagen (wird beim naechsten Start erneut versucht).");
                }
            } catch (Exception ex) {
                Log("Auto-Run-Fehler: " + ex.Message);
            }
        }

        /// <summary>
        /// Aktiviert Office permanent ueber Ohook (versteckt im Hintergrund).
        /// </summary>
        public static bool RunOhook() {
            return RunMasScript("/Ohook");
        }

        /// <summary>
        /// Entfernt die Ohook-Aktivierung wieder (versteckt im Hintergrund).
        /// </summary>
        public static bool RunOhookUninstall() {
            return RunMasScript("/Ohook-Uninstall");
        }

        /// <summary>
        /// Ermittelt den aktuellen Office-Aktivierungsstatus (Ohook / nicht aktiviert / nicht installiert).
        /// </summary>
        public static OfficeActivationState GetOfficeActivationState() {
            try {
                if (!IsOfficeInstalled()) return OfficeActivationState.NotInstalled;
                if (IsOhookInstalled()) return OfficeActivationState.ActivatedByOhook;
                return OfficeActivationState.NotActivated;
            } catch {
                return OfficeActivationState.NotActivated;
            }
        }

        /// <summary>
        /// Prueft, ob ein Office-Produkt installiert ist (Click-to-Run oder klassisches MSI).
        /// </summary>
        public static bool IsOfficeInstalled() {
            try {
                if (SoftwareDetector.CheckAppPaths("winword.exe") != null ||
                    SoftwareDetector.CheckAppPaths("excel.exe") != null ||
                    SoftwareDetector.CheckAppPaths("powerpnt.exe") != null ||
                    SoftwareDetector.CheckAppPaths("outlook.exe") != null) return true;

                string pf = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
                string pf86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                if (File.Exists(Path.Combine(pf, @"Microsoft Office\root\Office16\WINWORD.EXE")) ||
                    File.Exists(Path.Combine(pf86, @"Microsoft Office\root\Office16\WINWORD.EXE")) ||
                    File.Exists(Path.Combine(pf, @"Microsoft Office\Office16\WINWORD.EXE")) ||
                    File.Exists(Path.Combine(pf86, @"Microsoft Office\Office16\WINWORD.EXE"))) return true;

                return false;
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Prueft, ob Ohook aktuell installiert ist (Vorhandensein von sppc*.dll in Office-Ordnern).
        /// </summary>
        public static bool IsOhookInstalled() {
            return FindOfficeSppcDlls().Count > 0;
        }

        /// <summary>
        /// Prueft ueber WMI, ob Office-Produkte derzeit lizenziert (aktiviert) sind.
        /// </summary>
        public static bool IsOfficeLicensed() {
            try {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE ApplicationID='" + OfficeAppId + "' AND PartialProductKey IS NOT NULL")) {
                    foreach (ManagementObject mo in searcher.Get()) {
                        try {
                            if (mo["LicenseStatus"] != null && Convert.ToInt32(mo["LicenseStatus"]) == 1) return true;
                        } catch { }
                    }
                }
            } catch { }
            return false;
        }

        // =========================================================================
        // Interne Ausfuehrung
        // =========================================================================

        /// <summary>
        /// Laedt das MAS-All-In-One-Skript herunter und fuehrt es vollstaendig versteckt
        /// (ohne sichtbares Konsolenfenster) mit dem gewuenschten Schalter aus.
        /// Wartet ohne fixes Timeout, bis sich der Prozess selbststaendig beendet hat.
        /// </summary>
        private static bool RunMasScript(string switchArg) {
            string scriptPath = null;
            try {
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;

                // MAS selbst speichert sein Skript in %SystemRoot%\Temp (nicht im Benutzer-Temp),
                // da das Skript andernfalls mit "launched from temp folder" abbricht.
                string winTemp = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "Temp");
                Directory.CreateDirectory(winTemp);
                scriptPath = Path.Combine(winTemp, "MAS_" + Guid.NewGuid().ToString("N") + ".cmd");

                using (WebClient wc = new WebClient()) {
                    wc.DownloadFile(MasScriptUrl, scriptPath);
                }
                Log("MAS-Skript heruntergeladen: " + scriptPath + " (" + new FileInfo(scriptPath).Length + " Bytes)");

                string cmdExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                string args = "/c \"" + scriptPath + "\" -el " + switchArg;

                var psi = new ProcessStartInfo {
                    FileName = cmdExe,
                    Arguments = args,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = Encoding.Default
                };

                using (Process proc = Process.Start(psi)) {
                    if (proc == null) { Log("Prozessstart fehlgeschlagen."); return false; }

                    Task<string> stdoutTask = proc.StandardOutput.ReadToEndAsync();
                    Task<string> stderrTask = proc.StandardError.ReadToEndAsync();

                    // Kein fixes Timeout: Der Prozess beendet sich selbststaendig,
                    // sobald MAS die (De-)Aktivierung abgeschlossen hat.
                    proc.WaitForExit();

                    try { Task.WaitAll(stdoutTask, stderrTask); } catch { }
                    int code = proc.ExitCode;
                    Log("MAS beendet, ExitCode=" + code + " (Schalter: " + switchArg + ")");
                    return code == 0;
                }
            } catch (Exception ex) {
                Log("MAS-Fehler: " + ex.Message);
                return false;
            } finally {
                if (!string.IsNullOrEmpty(scriptPath)) {
                    try { File.Delete(scriptPath); } catch { }
                }
            }
        }

        private static List<string> FindOfficeSppcDlls() {
            var result = new List<string>();
            string[] roots = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Microsoft Office"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Microsoft Office")
            };
            foreach (string root in roots) {
                if (!Directory.Exists(root)) continue;
                try {
                    foreach (string f in Directory.GetFiles(root, "sppc*.dll", SearchOption.AllDirectories)) {
                        result.Add(f);
                    }
                } catch { }
            }
            return result;
        }


        private static bool KeyExists(RegistryKey hive, string path) {
            try {
                using (var key = hive.OpenSubKey(path)) {
                    return key != null;
                }
            } catch {
                return false;
            }
        }

        private static bool HasAutoRunCompleted() {
            try {
                using (var key = Registry.CurrentUser.OpenSubKey(AutoRunSubKey)) {
                    return key != null && key.GetValue(AutoRunValueName) != null;
                }
            } catch { }
            return false;
        }

        private static void MarkAutoRunCompleted() {
            try {
                using (var key = Registry.CurrentUser.CreateSubKey(AutoRunSubKey)) {
                    if (key != null) {
                        key.SetValue(AutoRunValueName, DateTime.UtcNow.ToString("o"), RegistryValueKind.String);
                    }
                }
            } catch { }
        }

        private static void Log(string message) {
            try {
                string logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "mas.log");
                File.AppendAllText(logPath, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine);
            } catch { }
        }
    }
}
