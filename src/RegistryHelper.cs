using System;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Zentraler, schlanker Helper für sichere Registry-Operationen nach dem KISS-Prinzip.
    /// Kapselt OpenBaseKey, View (64/32-Bit), SubKey-Erstellung und Null-Prüfungen.
    /// </summary>
    public static class RegistryHelper {
        public static int? GetDword(RegistryHive hive, string subKey, string valueName, int? defaultValue = null, RegistryView view = RegistryView.Default) {
            try {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false)) {
                    if (key != null) {
                        object val = key.GetValue(valueName);
                        if (val is int) return (int)val;
                        if (val != null) {
                            int parsed;
                            if (int.TryParse(val.ToString(), out parsed)) return parsed;
                        }
                    }
                }
            } catch {}
            return defaultValue;
        }

        public static bool SetDword(RegistryHive hive, string subKey, string valueName, int value, RegistryView view = RegistryView.Default) {
            try {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.CreateSubKey(subKey)) {
                    if (key != null) {
                        key.SetValue(valueName, value, RegistryValueKind.DWord);
                        return true;
                    }
                }
            } catch {}
            return false;
        }

        public static string GetString(RegistryHive hive, string subKey, string valueName, string defaultValue = null, RegistryView view = RegistryView.Default) {
            try {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false)) {
                    if (key != null) {
                        object val = key.GetValue(valueName);
                        if (val != null) return val.ToString();
                    }
                }
            } catch {}
            return defaultValue;
        }

        public static bool SetString(RegistryHive hive, string subKey, string valueName, string value, RegistryView view = RegistryView.Default) {
            try {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.CreateSubKey(subKey)) {
                    if (key != null) {
                        key.SetValue(valueName, value, RegistryValueKind.String);
                        return true;
                    }
                }
            } catch {}
            return false;
        }

        public static bool DeleteValue(RegistryHive hive, string subKey, string valueName, RegistryView view = RegistryView.Default) {
            try {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, true)) {
                    if (key != null) {
                        key.DeleteValue(valueName, false);
                        return true;
                    }
                }
            } catch {}
            return false;
        }

        public static bool KeyExists(RegistryHive hive, string subKey, RegistryView view = RegistryView.Default) {
            try {
                using (RegistryKey baseKey = RegistryKey.OpenBaseKey(hive, view))
                using (RegistryKey key = baseKey.OpenSubKey(subKey, false)) {
                    return key != null;
                }
            } catch {
                return false;
            }
        }

        /// <summary>
        /// Ermittelt den tatsächlichen Downloads-Ordner des aktuellen Benutzers dynamisch.
        /// Berücksichtigt benutzerdefinierte/verschobene Pfade (z. B. D:\Downloads oder OneDrive) via User Shell Folders.
        /// </summary>
        public static string GetUserDownloadsFolder() {
            try {
                string raw = GetString(RegistryHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\Explorer\User Shell Folders", "{374DE290-123F-4565-9164-39C4925E467B}");
                if (!string.IsNullOrEmpty(raw)) {
                    string expanded = Environment.ExpandEnvironmentVariables(raw);
                    if (!string.IsNullOrEmpty(expanded)) return expanded;
                }
            } catch {}
            return System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        }
    }
}
