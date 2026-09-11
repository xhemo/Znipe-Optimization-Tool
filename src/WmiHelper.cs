using System;
using System.Management;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Zentraler Helper für WMI-Abfragen nach dem KISS-Prinzip.
    /// Kapselt ManagementObjectSearcher, Ressourcenfreigabe und Exception-Handling ab.
    /// </summary>
    public static class WmiHelper {
        /// <summary>
        /// Führt eine WMI-Abfrage aus und führt für jedes ManagementObject eine Aktion aus.
        /// </summary>
        public static void ForEach(string query, Action<ManagementObject> action) {
            try {
                using (ManagementObjectSearcher searcher = new ManagementObjectSearcher(query)) {
                    foreach (ManagementObject obj in searcher.Get()) {
                        try {
                            action(obj);
                        } catch {}
                    }
                }
            } catch {}
        }
    }
}
