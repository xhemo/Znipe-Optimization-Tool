using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    public class InstalledSoftwareInfo {
        public bool IsInstalled { get; set; }
        public string DisplayName { get; set; }
        public string Version { get; set; }
        public string InstallLocation { get; set; }
        public string ExePath { get; set; }
        public string UninstallString { get; set; }
        public string QuietUninstallString { get; set; }

        public string GetFormattedVersion() {
            if (string.IsNullOrWhiteSpace(Version) || Version.Equals("Installiert", StringComparison.OrdinalIgnoreCase))
                return "Installiert";
            string v = Version.Trim();
            return v.StartsWith("v", StringComparison.OrdinalIgnoreCase) ? v : "v" + v;
        }
    }

    public static class SoftwareDetector {
        private const string UninstallSubKeyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall";
        private static readonly string[] DefaultRoots = new[] {
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData)
        };

        /// <summary>
        /// Erkennt dynamisch jede installierte Software über die Windows Uninstall-Registry (HKLM 64/32, HKCU),
        /// App Paths sowie bekannte Standardpfade.
        /// </summary>
        public static InstalledSoftwareInfo Detect(string displayNamePattern, string[] fallbackExeNames = null, string[] fallbackFolders = null) {
            var info = new InstalledSoftwareInfo { IsInstalled = false };

            // 1. DYNAMISCHER SCAN DER WINDOWS UNINSTALL REGISTRY (HKLM & HKCU)
            try {
                var roots = new[] {
                    RegistryKey.OpenBaseKey(RegistryHive.CurrentUser, RegistryView.Default),
                    RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64),
                    RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry32)
                };

                foreach (var root in roots) {
                    if (root == null) continue;
                    using (root) {
                        try {
                            using (var key = root.OpenSubKey(UninstallSubKeyPath)) {
                                if (key == null) continue;
                                    foreach (var subKeyName in key.GetSubKeyNames()) {
                                        try {
                                            using (var appKey = key.OpenSubKey(subKeyName)) {
                                                if (appKey == null) continue;
                                                string dispName = appKey.GetValue("DisplayName") as string;
                                                if (string.IsNullOrEmpty(dispName)) continue;

                                                if (Regex.IsMatch(dispName, displayNamePattern, RegexOptions.IgnoreCase)) {
                                                    string ver = appKey.GetValue("DisplayVersion") as string;
                                                    string loc = appKey.GetValue("InstallLocation") as string;
                                                    string icon = appKey.GetValue("DisplayIcon") as string;
                                                    string uninst = appKey.GetValue("UninstallString") as string;
                                                    string quietUninst = appKey.GetValue("QuietUninstallString") as string;

                                                    string resolvedExe = ResolveExecutable(loc, icon, uninst, fallbackExeNames);
                                                    string cleanVer = CleanVersion(ver);

                                                    // Falls Registry keine aussagekräftige Version hat, direkt von der EXE ermitteln
                                                    if (IsInvalidOrGenericVersion(cleanVer) && !string.IsNullOrEmpty(resolvedExe) && File.Exists(resolvedExe)) {
                                                        string fileVer = ExtractFileVersion(resolvedExe);
                                                        if (!string.IsNullOrEmpty(fileVer) && !IsInvalidOrGenericVersion(fileVer)) {
                                                            cleanVer = fileVer;
                                                        }
                                                    }

                                                    info.IsInstalled = true;
                                                    info.DisplayName = dispName;
                                                    info.Version = !string.IsNullOrEmpty(cleanVer) ? cleanVer : "Installiert";
                                                    info.InstallLocation = loc;
                                                    info.ExePath = resolvedExe;
                                                    info.UninstallString = uninst;
                                                    info.QuietUninstallString = quietUninst;
                                                    return info;
                                                }
                                            }
                                        } catch {}
                                    }
                                }
                            } catch {}
                    }
                }
            } catch {}

            // 2. SCAN DER WINDOWS APP PATHS REGISTRY
            if (fallbackExeNames != null) {
                foreach (var exeName in fallbackExeNames) {
                    string appPathExe = CheckAppPaths(exeName);
                    if (!string.IsNullOrEmpty(appPathExe) && File.Exists(appPathExe)) {
                        info.IsInstalled = true;
                        info.DisplayName = Path.GetFileNameWithoutExtension(appPathExe);
                        info.ExePath = appPathExe;
                        info.InstallLocation = Path.GetDirectoryName(appPathExe);
                        info.Version = ExtractFileVersion(appPathExe);
                        return info;
                    }
                }
            }

            // 3. DATEISYSTEM-FALLBACK (Program Files, LocalAppData, AppData, etc.)
            if (fallbackFolders != null && fallbackExeNames != null) {
                foreach (var baseRoot in DefaultRoots) {
                    if (string.IsNullOrEmpty(baseRoot) || !Directory.Exists(baseRoot)) continue;
                    foreach (var folder in fallbackFolders) {
                        try {
                            string targetDir = Path.Combine(baseRoot, folder);
                            if (!Directory.Exists(targetDir)) continue;

                            foreach (var exe in fallbackExeNames) {
                                string fullExe = Path.Combine(targetDir, exe);
                                if (File.Exists(fullExe)) {
                                    info.IsInstalled = true;
                                    info.DisplayName = folder;
                                    info.ExePath = fullExe;
                                    info.InstallLocation = targetDir;
                                    info.Version = ExtractFileVersion(fullExe);
                                    return info;
                                }

                                string binExe = Path.Combine(targetDir, "bin", exe);
                                if (File.Exists(binExe)) {
                                    info.IsInstalled = true;
                                    info.DisplayName = folder;
                                    info.ExePath = binExe;
                                    info.InstallLocation = targetDir;
                                    info.Version = ExtractFileVersion(binExe);
                                    return info;
                                }
                            }
                        } catch {}
                    }
                }
            }

            return info;
        }

        public static string CheckAppPaths(string exeName) {
            foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser }) {
                try {
                    using (var key = hive.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\App Paths\" + exeName)) {
                        if (key != null) {
                            string path = key.GetValue("") as string;
                            if (!string.IsNullOrEmpty(path)) return path.Trim('"', ' ');
                        }
                    }
                } catch {}
            }
            return null;
        }

        private static string ResolveExecutable(string installLoc, string displayIcon, string uninstallString, string[] candidateExeNames) {
            try {
                // Aus DisplayIcon extrahieren
                if (!string.IsNullOrEmpty(displayIcon)) {
                    string cleanIcon = displayIcon.Split(',')[0].Trim('"', ' ');
                    if (cleanIcon.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(cleanIcon)) {
                        return cleanIcon;
                    }
                }

                // Aus UninstallString extrahieren
                if (!string.IsNullOrEmpty(uninstallString)) {
                    string cleanUninst = uninstallString.Trim('"', ' ');
                    if (cleanUninst.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(cleanUninst)) {
                        return cleanUninst;
                    }
                }

                // Aus InstallLocation Verzeichnis prüfen
                if (!string.IsNullOrEmpty(installLoc) && Directory.Exists(installLoc) && candidateExeNames != null) {
                    foreach (var exe in candidateExeNames) {
                        string full = Path.Combine(installLoc, exe);
                        if (File.Exists(full)) return full;
                        string binFull = Path.Combine(installLoc, "bin", exe);
                        if (File.Exists(binFull)) return binFull;
                    }
                }
            } catch {}
            return null;
        }

        private static string ExtractFileVersion(string exePath) {
            try {
                if (File.Exists(exePath)) {
                    var vi = FileVersionInfo.GetVersionInfo(exePath);
                    string prodVer = !string.IsNullOrEmpty(vi.ProductVersion) ? vi.ProductVersion.Trim() : null;
                    string fileVer = !string.IsNullOrEmpty(vi.FileVersion) ? vi.FileVersion.Trim() : null;

                    string candidate = (!string.IsNullOrEmpty(prodVer) && !IsInvalidOrGenericVersion(prodVer)) 
                        ? prodVer 
                        : fileVer;

                    return CleanVersion(candidate);
                }
            } catch {}
            return "Installiert";
        }

        private static bool IsInvalidOrGenericVersion(string ver) {
            string v = CleanVersion(ver);
            return string.IsNullOrEmpty(v) || v == "1.0.0.1" || v == "1.0.0.0" || v == "0.0.0.0" || v == "0.0";
        }

        private static string CleanVersion(string raw) {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            return raw.Replace(',', '.').Replace(" ", "").Trim();
        }
    }
}
