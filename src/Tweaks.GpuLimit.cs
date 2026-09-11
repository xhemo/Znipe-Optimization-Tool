using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Globalization;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    public class GpuLimitConfig {
        public bool Enabled { get; set; }
        public int ClockMhz { get; set; }
        public int PowerWatts { get; set; }
        public int PowerPercent { get; set; }
        public int TempTarget { get; set; }
        public bool Linked { get; set; }

        public GpuLimitConfig() {
            Enabled = false;
            ClockMhz = 750;
            PowerWatts = 150;
            PowerPercent = 33;
            TempTarget = 65;
            Linked = true;
        }
    }

    public static class GpuLimitManager {
        public const string TaskName = "ZnipeGpuLimitDaemon";
        private const string RegKeyPath = @"Software\ZnipeOptimizationTool\GpuLimits";
        private static int cachedMaxClock = 0;

        public static string FindNvidiaSmi() {
            string sys32 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "nvidia-smi.exe");
            if (File.Exists(sys32)) return sys32;

            string sysnative = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), @"Sysnative\nvidia-smi.exe");
            if (File.Exists(sysnative)) return sysnative;

            string nvsmi = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"NVIDIA Corporation\NVSMI\nvidia-smi.exe");
            if (File.Exists(nvsmi)) return nvsmi;

            return "nvidia-smi.exe";
        }

        public static string RunNvidiaSmi(string arguments) {
            string exe = FindNvidiaSmi();
            return ProcessRunner.RunAndGetOutput(exe, arguments, 4000);
        }

        public static int QueryMaxClock() {
            if (cachedMaxClock > 500) return cachedMaxClock;
            try {
                string output = RunNvidiaSmi("--query-gpu=clocks.max.graphics --format=csv,noheader,nounits");
                if (!string.IsNullOrEmpty(output)) {
                    int val;
                    if (int.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out val) && val > 500) {
                        cachedMaxClock = val;
                        return val;
                    }
                }
            } catch {}
            return 3165;
        }

        public static void QueryHardwareLimits(out double defPl, out double minPl, out double maxPl, out double defTemp) {
            defPl = 450.0;
            minPl = 150.0;
            maxPl = 520.0;
            defTemp = 84.0;
            try {
                string output = RunNvidiaSmi("--query-gpu=power.default_limit,power.min_limit,power.max_limit --format=csv,noheader,nounits");
                if (!string.IsNullOrEmpty(output)) {
                    string[] parts = output.Trim().Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 1) {
                        double d;
                        if (double.TryParse(parts[0].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out d) && d > 10) {
                            defPl = Math.Round(d, 0);
                        }
                    }
                    if (parts.Length >= 2) {
                        double mn;
                        if (double.TryParse(parts[1].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out mn) && mn > 10) {
                            minPl = Math.Round(mn, 0);
                        } else {
                            minPl = Math.Max(30.0, Math.Round(defPl * 0.30));
                        }
                    }
                    if (parts.Length >= 3) {
                        double mx;
                        if (double.TryParse(parts[2].Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out mx) && mx > defPl) {
                            maxPl = Math.Round(mx, 0);
                        } else {
                            maxPl = Math.Round(defPl * 1.15);
                        }
                    }
                }
            } catch {}
            if (minPl <= 10.0 || minPl >= defPl) minPl = Math.Max(30.0, Math.Round(defPl * 0.30));
            if (maxPl <= defPl) maxPl = Math.Round(defPl * 1.15);
        }

        public static bool ApplyGpuLock(int clockMhz) {
            try {
                if (clockMhz < 210) clockMhz = 210;
                int maxClk = QueryMaxClock();
                if (clockMhz > maxClk && maxClk > 500) clockMhz = maxClk;

                RegisterAutostartTask(clockMhz);
                RunNvidiaSmi(string.Format("-i 0 -lgc {0}", clockMhz));
                ProcessRunner.RunAndGetOutput("schtasks.exe", string.Format("/run /tn \"{0}\"", TaskName), 3000);
                return true;
            } catch {
                return false;
            }
        }

        public static bool ResetGpuLock() {
            try {
                string nvsmi = FindNvidiaSmi();
                string scriptPath = GetScriptPath();
                string scriptDir = Path.GetDirectoryName(scriptPath);
                if (!Directory.Exists(scriptDir)) Directory.CreateDirectory(scriptDir);
                File.WriteAllText(scriptPath, string.Format("@echo off\r\n\"{0}\" -i 0 -rgc\r\n", nvsmi));

                RunNvidiaSmi("-i 0 -rgc");
                if (IsAutostartTaskRegistered()) {
                    ProcessRunner.RunAndGetOutput("schtasks.exe", string.Format("/run /tn \"{0}\"", TaskName), 3000);
                }

                UnregisterAutostartTask();
                return true;
            } catch {
                return false;
            }
        }

        public static string GetScriptPath() {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZnipeOptimizationTool");
            return Path.Combine(dir, "apply_gpu_limits.cmd");
        }

        public static bool RegisterAutostartTask(int clockMhz) {
            try {
                if (clockMhz < 210) clockMhz = 750;
                string nvsmi = FindNvidiaSmi();
                string scriptPath = GetScriptPath();
                string scriptDir = Path.GetDirectoryName(scriptPath);
                if (!Directory.Exists(scriptDir)) {
                    Directory.CreateDirectory(scriptDir);
                }

                string scriptContent = string.Format(
                    "@echo off\r\n\"{0}\" -i 0 -lgc {1}\r\n",
                    nvsmi, clockMhz
                );
                File.WriteAllText(scriptPath, scriptContent);

                string args = string.Format("/create /tn \"{0}\" /tr \"\\\"{1}\\\"\" /sc onlogon /rl highest /f", TaskName, scriptPath);
                ProcessRunner.RunAndGetOutput("schtasks.exe", args, 3000);
                return IsAutostartTaskRegistered();
            } catch {}
            return false;
        }

        public static bool UnregisterAutostartTask() {
            try {
                string scriptPath = GetScriptPath();
                if (File.Exists(scriptPath)) {
                    try { File.Delete(scriptPath); } catch {}
                }
                ProcessRunner.RunAndGetOutput("schtasks.exe", string.Format("/delete /tn \"{0}\" /f", TaskName), 3000);
                return !IsAutostartTaskRegistered();
            } catch {}
            return false;
        }

        public static bool IsAutostartTaskRegistered() {
            try {
                string outStr = ProcessRunner.RunAndGetOutput("schtasks.exe", string.Format("/query /tn \"{0}\"", TaskName), 3000);
                return !string.IsNullOrEmpty(outStr) && outStr.IndexOf(TaskName, StringComparison.OrdinalIgnoreCase) >= 0;
            } catch {}
            return false;
        }

        public static void SaveConfig(GpuLimitConfig cfg) {
            if (cfg == null) return;
            RegistryHelper.SetDword(RegistryHive.CurrentUser, RegKeyPath, "Enabled", cfg.Enabled ? 1 : 0);
            RegistryHelper.SetDword(RegistryHive.CurrentUser, RegKeyPath, "ClockMhz", cfg.ClockMhz);
        }

        public static GpuLimitConfig LoadConfig() {
            var cfg = new GpuLimitConfig();
            cfg.Enabled = (RegistryHelper.GetDword(RegistryHive.CurrentUser, RegKeyPath, "Enabled") ?? 0) == 1;
            cfg.ClockMhz = RegistryHelper.GetDword(RegistryHive.CurrentUser, RegKeyPath, "ClockMhz") ?? 750;
            if (cfg.ClockMhz <= 0) cfg.ClockMhz = 750;
            return cfg;
        }
    }

    public partial class MainWindowLogic {
        private void InitializeGpuLimitUI() {
            var cfg = GpuLimitManager.LoadConfig();
            bool isTaskActive = GpuLimitManager.IsAutostartTaskRegistered();
            bool isEnabled = cfg.Enabled || isTaskActive;
            SetGpuLimitSwitchUI(isEnabled, cfg.ClockMhz);

            if (pillToggleGpuLimit != null) {
                pillToggleGpuLimit.MouseLeftButtonUp += (s, e) => {
                    OpenGpuLimitsModal();
                };
            }

            if (btnCloseGpuLimitsModal != null) {
                btnCloseGpuLimitsModal.Click += (s, e) => CloseGpuLimitsModal();
            }
            if (btnCancelGpuLimits != null) {
                btnCancelGpuLimits.Click += (s, e) => CloseGpuLimitsModal();
            }

            if (sliderGpuClock != null) {
                sliderGpuClock.ValueChanged += (s, e) => {
                    int val = (int)Math.Round(e.NewValue);
                    if (txtGpuClockTargetVal != null) {
                        txtGpuClockTargetVal.Text = string.Format("{0} MHz", val);
                    }
                };
            }

            if (btnResetGpuLimits != null) {
                btnResetGpuLimits.Click += (s, e) => {
                    GpuLimitManager.ResetGpuLock();
                    GpuLimitManager.UnregisterAutostartTask();

                    var c = GpuLimitManager.LoadConfig();
                    c.Enabled = false;
                    GpuLimitManager.SaveConfig(c);

                    SetGpuLimitSwitchUI(false, c.ClockMhz);
                    CloseGpuLimitsModal();
                };
            }

            if (btnApplyGpuLimits != null) {
                btnApplyGpuLimits.Click += (s, e) => {
                    int targetClock = sliderGpuClock != null ? (int)Math.Round(sliderGpuClock.Value) : 750;
                    if (targetClock < 210) targetClock = 210;

                    GpuLimitManager.ApplyGpuLock(targetClock);
                    GpuLimitManager.RegisterAutostartTask(targetClock);

                    var newCfg = new GpuLimitConfig {
                        Enabled = true,
                        ClockMhz = targetClock
                    };
                    GpuLimitManager.SaveConfig(newCfg);

                    SetGpuLimitSwitchUI(true, targetClock);
                    CloseGpuLimitsModal();
                };
            }
        }

        private void SetGpuLimitSwitchUI(bool isLocked, int clockMhz = 750) {
            if (txtToggleGpuLimitStatus != null) {
                txtToggleGpuLimitStatus.Text = isLocked ? string.Format("{0} MHz", clockMhz) : "Dynamisch";
            }

            if (!isLocked) {
                if (pillToggleGpuLimit != null) {
                    pillToggleGpuLimit.Background = UIHelper.GetBrush("#181C26");
                    pillToggleGpuLimit.BorderBrush = UIHelper.GetBrush("#262D3D");
                }
                if (txtToggleGpuLimitDot != null) {
                    txtToggleGpuLimitDot.Text = "⚙";
                    txtToggleGpuLimitDot.Foreground = UIHelper.GetBrush("#64748B");
                }
                if (txtToggleGpuLimitStatus != null) {
                    txtToggleGpuLimitStatus.Foreground = UIHelper.GetBrush("#64748B");
                }
            } else {
                if (pillToggleGpuLimit != null) {
                    pillToggleGpuLimit.Background = UIHelper.GetBrush("#1E1B4B");
                    pillToggleGpuLimit.BorderBrush = UIHelper.GetBrush("#6366F1");
                }
                if (txtToggleGpuLimitDot != null) {
                    txtToggleGpuLimitDot.Text = "⚡";
                    txtToggleGpuLimitDot.Foreground = UIHelper.GetBrush("#A5B4FC");
                }
                if (txtToggleGpuLimitStatus != null) {
                    txtToggleGpuLimitStatus.Foreground = UIHelper.GetBrush("#A5B4FC");
                }
            }
            UpdateSidebarActivitySpinners();
        }

        private void OpenGpuLimitsModal() {
            var cfg = GpuLimitManager.LoadConfig();
            int maxClock = GpuLimitManager.QueryMaxClock();

            if (sliderGpuClock != null) {
                sliderGpuClock.Minimum = 500;
                sliderGpuClock.Maximum = Math.Max(1000, maxClock);

                int current = cfg.ClockMhz > 0 ? cfg.ClockMhz : 750;
                if (current < 500) current = 500;
                if (current > maxClock) current = maxClock;

                sliderGpuClock.Value = current;

                if (txtGpuClockTargetVal != null) {
                    txtGpuClockTargetVal.Text = string.Format("{0} MHz", current);
                }
                if (txtGpuClockTargetRange != null) {
                    txtGpuClockTargetRange.Text = string.Format("Bereich: Min 500 MHz – Max {0} MHz (Sicherer VBIOS-Frequenzbereich)", maxClock);
                }
                if (txtGpuClockCurrentInfo != null) {
                    txtGpuClockCurrentInfo.Text = string.Format("Live-Takt: {0:F0} MHz", latestGpuClock > 0 ? latestGpuClock : current);
                }
            }

            if (overlayGpuLimitsModal != null) {
                overlayGpuLimitsModal.Visibility = Visibility.Visible;
            }
        }

        private void CloseGpuLimitsModal() {
            if (overlayGpuLimitsModal != null) {
                overlayGpuLimitsModal.Visibility = Visibility.Collapsed;
            }
        }
    }
}
