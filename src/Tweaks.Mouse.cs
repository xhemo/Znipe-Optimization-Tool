using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Management;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Logik für den Maus- & Allgemeine Tweaks Reiter (Queue, Priority, CoreIso, Power) nach dem KISS-Prinzip.
    /// </summary>
    public partial class MainWindowLogic {
        private const string MouseRegSubKey = @"SYSTEM\CurrentControlSet\Services\mouclass\Parameters";
        private const string MouseValueName = "MouseDataQueueSize";

        private const string PriorityRegSubKey = @"SYSTEM\CurrentControlSet\Control\PriorityControl";
        private const string PriorityValueName = "Win32PrioritySeparation";

        private const string CoreIsoKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity";
        private const string DeviceGuardKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard";
        private const string StackProtectionKey = @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\KernelShadowStacks";
        private const string LsaKey = @"SYSTEM\CurrentControlSet\Control\Lsa";
        private const string CiConfigKey = @"SYSTEM\CurrentControlSet\Control\CI\Config";

        private const string PagingRegKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\Memory Management";
        private const string SystemProfileRegKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
        private const string MmcssGamesRegKey = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile\Tasks\Games";
        private const string GameConfigStoreKey = @"System\GameConfigStore";
        private const string GameDvrPolicyKey = @"SOFTWARE\Policies\Microsoft\Windows\GameDVR";
        private const string GameDvrUserKey = @"Software\Microsoft\Windows\CurrentVersion\GameDVR";

        private void RefreshMouseUI() {
            updateUIRefCount++;
            try {
                int? val = RegistryHelper.GetDword(RegistryHive.LocalMachine, MouseRegSubKey, MouseValueName);
                int cur = val.HasValue ? val.Value : 100;
                bool opt = val.HasValue && val.Value >= 16 && val.Value < 100;

                if (toggleQueue != null) toggleQueue.IsChecked = opt;
                if (txtSwitchSubtitle != null) {
                    txtSwitchSubtitle.Text = opt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtSwitchSubtitle.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtMouseQueueLiveVal != null) {
                    if (val.HasValue) {
                        txtMouseQueueLiveVal.Text = val.Value.ToString() + (opt ? " (Optimiert)" : " (Standard)");
                        txtMouseQueueLiveVal.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    } else {
                        txtMouseQueueLiveVal.Text = "Gelöscht (Standard 100)";
                        txtMouseQueueLiveVal.Foreground = UIHelper.GetBrush("#F97316");
                    }
                }
                if (btnAdjustMouseQueue != null) btnAdjustMouseQueue.Content = "⚙️ " + cur;
                if (sliderMouseQueue != null) sliderMouseQueue.Value = cur;
                if (txtMouseQueueSliderVal != null) txtMouseQueueSliderVal.Text = cur.ToString();
            } finally {
                updateUIRefCount--;
            }
        }

        private void PopulatePriorityComboBox(bool isOptimized, int selectedVal) {
            if (cmbPriority == null) return;
            cmbPriority.Items.Clear();

            string[][] items = isOptimized
                ? new[] {
                    new[] { "42", "42 (0x2A) - Kurze Quanta, Variable (Empfohlen)" },
                    new[] { "40", "40 (0x28) - Feste Quanta, Hohe Prio" },
                    new[] { "38", "38 (0x26) - Variable Quanta, Ausbalanciert" },
                    new[] { "36", "36 (0x24) - Maximale Vordergrund-Reaktion" },
                    new[] { "26", "26 (0x1A) - Feste kurze Quanta (Low Latency)" }
                }
                : new[] {
                    new[] { "2", "2 (0x02) - Windows Standard" },
                    new[] { "0", "0 (0x00) - Keine Bevorzugung" },
                    new[] { "18", "18 (0x12) - Hintergrund optimiert" },
                    new[] { "26", "26 (0x1A) - Feste kurze Quanta (Low Latency)" }
                };

            ComboBoxItem matched = null;
            string selStr = selectedVal.ToString();
            foreach (var entry in items) {
                ComboBoxItem item = new ComboBoxItem { Tag = entry[0], Content = entry[1] };
                cmbPriority.Items.Add(item);
                if (entry[0] == selStr) matched = item;
            }
            if (matched == null && selectedVal != 2 && selectedVal != 0) {
                ComboBoxItem custom = new ComboBoxItem { 
                    Tag = selectedVal.ToString(), 
                    Content = string.Format("{1} (0x{0:X2}) - Benutzerdefiniert", selectedVal, selectedVal) 
                };
                cmbPriority.Items.Add(custom);
                matched = custom;
            }
            cmbPriority.SelectedItem = matched ?? (cmbPriority.Items.Count > 0 ? cmbPriority.Items[0] : null);
        }

        private void RefreshPriorityUI() {
            updateUIRefCount++;
            try {
                int? val = RegistryHelper.GetDword(RegistryHive.LocalMachine, PriorityRegSubKey, PriorityValueName);
                int cur = val.HasValue ? val.Value : 2;
                bool opt = val.HasValue && val.Value != 2 && val.Value != 0;

                if (togglePriority != null) togglePriority.IsChecked = opt;
                if (txtPrioritySubtitle != null) {
                    txtPrioritySubtitle.Text = opt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtPrioritySubtitle.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtPriorityLiveVal != null) {
                    if (val.HasValue) {
                        txtPriorityLiveVal.Text = string.Format("{0} (0x{0:X2})", val.Value);
                        txtPriorityLiveVal.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    } else {
                        txtPriorityLiveVal.Text = "2 (0x02) - Standard";
                        txtPriorityLiveVal.Foreground = UIHelper.GetBrush("#F97316");
                    }
                }
                PopulatePriorityComboBox(opt, cur);
            } finally {
                updateUIRefCount--;
            }
        }

        // =========================================================================
        // NATIVE KERNISOLIERUNG (CORE ISOLATION / HVCI / VBS) OPERATIONEN
        // =========================================================================
        private int? GetCoreIsoStatus() {
            int? val = RegistryHelper.GetDword(RegistryHive.LocalMachine, CoreIsoKey, "Enabled");
            if (!val.HasValue) val = RegistryHelper.GetDword(RegistryHive.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity");
            return val;
        }

        private void SetCoreIsoStatus(bool enable) {
            int val = enable ? 1 : 0;
            RegistryHelper.SetDword(RegistryHive.LocalMachine, CoreIsoKey, "Enabled", val);
            RegistryHelper.SetDword(RegistryHive.LocalMachine, DeviceGuardKey, "EnableVirtualizationBasedSecurity", val);
            RegistryHelper.SetDword(RegistryHive.LocalMachine, StackProtectionKey, "Enabled", val);
            RegistryHelper.SetDword(RegistryHive.LocalMachine, LsaKey, "RunAsPPL", val);
            RegistryHelper.SetDword(RegistryHive.LocalMachine, CiConfigKey, "VulnerableDriverBlocklistEnable", val);
            ProcessRunner.RunAndGetOutput("bcdedit.exe", enable ? "/set hypervisorlaunchtype auto" : "/set hypervisorlaunchtype off");
        }

        private void RefreshCoreIsoUI() {
            updateUIRefCount++;
            try {
                int? status = GetCoreIsoStatus();
                bool disabled = status.HasValue && status.Value == 0;

                if (toggleCoreIso != null) toggleCoreIso.IsChecked = disabled;
                if (txtCoreIsoSubtitle != null) {
                    txtCoreIsoSubtitle.Text = disabled ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtCoreIsoSubtitle.Foreground = disabled ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtCoreIsoLiveVal != null) {
                    if (status.HasValue) {
                        txtCoreIsoLiveVal.Text = disabled ? "0 (Deaktiviert / Aus)" : "1 (Aktiviert / Schutz an)";
                        txtCoreIsoLiveVal.Foreground = disabled ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    } else {
                        txtCoreIsoLiveVal.Text = "1 (Standard / Aktiv)";
                        txtCoreIsoLiveVal.Foreground = UIHelper.GetBrush("#F97316");
                    }
                }
                if (txtHypervisorLiveVal != null) {
                    txtHypervisorLiveVal.Text = disabled ? "off" : "auto";
                    txtHypervisorLiveVal.Foreground = disabled ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
            } finally {
                updateUIRefCount--;
            }
        }

        // =========================================================================
        // NATIVE ENERGIESPARPLAN (POWERCFG) OPERATIONEN
        // =========================================================================
        private List<PowerPlanItem> GetPowerPlans() {
            List<PowerPlanItem> list = new List<PowerPlanItem>();
            string output = ProcessRunner.RunAndGetOutput("powercfg.exe", "-l");

            foreach (string line in output.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)) {
                Match m = Regex.Match(line, @"([a-fA-F0-9]{8}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{4}-[a-fA-F0-9]{12})\s*\((.+?)\)(\s*\*?)", RegexOptions.IgnoreCase);
                if (m.Success) {
                    string guid = m.Groups[1].Value.Trim();
                    string name = m.Groups[2].Value.Trim();

                    name = name.Replace("H”chstleistung", "Höchstleistung")
                               .Replace("Hchstleistung", "Höchstleistung")
                               .Replace("H\u201dchstleistung", "Höchstleistung")
                               .Replace("H\u0094chstleistung", "Höchstleistung");

                    bool isActive = m.Groups[3].Value.Contains("*") || line.TrimEnd().EndsWith("*");
                    list.Add(new PowerPlanItem { Guid = guid, Name = name, IsActive = isActive });
                }
            }
            return list;
        }

        private void SetActivePowerPlan(string guid) {
            ProcessRunner.RunAndGetOutput("powercfg.exe", string.Format("-setactive {0}", guid));
        }

        private void RefreshPowerUI() {
            Task.Run(() => {
                try {
                    List<PowerPlanItem> plans = GetPowerPlans();
                    PowerPlanItem activePlan = plans.Find(p => p.IsActive);

                    if (window != null) {
                        window.Dispatcher.BeginInvoke((Action)(() => {
                            updateUIRefCount++;
                            try {
                                if (cmbPowerPlans != null) {
                                    cmbPowerPlans.Items.Clear();
                                    foreach (var plan in plans) {
                                        var item = new ComboBoxItem {
                                            Content = plan.Name,
                                            Tag = plan.Guid,
                                            IsSelected = plan.IsActive
                                        };
                                        cmbPowerPlans.Items.Add(item);
                                    }
                                }

                                if (activePlan != null) {
                                    bool isOptimized = string.Equals(activePlan.Guid, "381b4222-f694-41f0-9685-ff5bb260df2e", StringComparison.OrdinalIgnoreCase) ||
                                                       activePlan.Name.IndexOf("Ausbalanciert", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                       activePlan.Name.IndexOf("Balanced", StringComparison.OrdinalIgnoreCase) >= 0;

                                    if (togglePowerPlan != null) togglePowerPlan.IsChecked = isOptimized;
                                    if (txtPowerSubtitle != null) {
                                        txtPowerSubtitle.Text = isOptimized ? "Status: Optimiert" : "Status: Nicht optimiert";
                                        txtPowerSubtitle.Foreground = isOptimized ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                                    }
                                    if (txtPowerPlanLiveVal != null) {
                                        txtPowerPlanLiveVal.Text = activePlan.Name;
                                        txtPowerPlanLiveVal.Foreground = isOptimized ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                                    }
                                } else {
                                    if (togglePowerPlan != null) togglePowerPlan.IsChecked = false;
                                    if (txtPowerSubtitle != null) {
                                        txtPowerSubtitle.Text = "Status: Nicht optimiert";
                                        txtPowerSubtitle.Foreground = UIHelper.BrushRedText;
                                    }
                                    if (txtPowerPlanLiveVal != null) {
                                        txtPowerPlanLiveVal.Text = "Unbekannt";
                                        txtPowerPlanLiveVal.Foreground = UIHelper.BrushGrayText;
                                    }
                                }
                            } finally {
                                updateUIRefCount--;
                            }
                        }));
                    }
                } catch {}
            });
        }

        // =========================================================================
        // EVENT WIRING: MAUS & ALLGEMEINE TWEAKS
        // =========================================================================
        public void SetupMouseEvents() {
            if (btnOpenRegedit != null) btnOpenRegedit.Click += (s, e) => { OpenRegeditAtKey(MouseRegSubKey); };

            if (btnAdjustMouseQueue != null) {
                btnAdjustMouseQueue.Click += (s, e) => {
                    if (popupMouseQueue != null) popupMouseQueue.IsOpen = true;
                };
            }

            if (sliderMouseQueue != null) {
                sliderMouseQueue.ValueChanged += (s, e) => {
                    if (txtMouseQueueSliderVal != null) {
                        txtMouseQueueSliderVal.Text = ((int)e.NewValue).ToString();
                    }
                };
            }

            if (btnMouseQueuePreset16 != null) {
                btnMouseQueuePreset16.Click += (s, e) => {
                    if (sliderMouseQueue != null) sliderMouseQueue.Value = 16;
                };
            }

            if (btnMouseQueueApply != null) {
                btnMouseQueueApply.Click += (s, e) => {
                    if (!isAdmin) return;
                    int target = (sliderMouseQueue != null) ? (int)sliderMouseQueue.Value : 16;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, MouseRegSubKey, MouseValueName, target);
                    if (popupMouseQueue != null) popupMouseQueue.IsOpen = false;
                    RefreshMouseUI();
                    if (txtMouseQueueReboot != null) txtMouseQueueReboot.Visibility = Visibility.Visible;
                };
            }

            if (toggleQueue != null) {
                toggleQueue.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, MouseRegSubKey, MouseValueName, 16);
                    RefreshMouseUI();
                    if (txtMouseQueueReboot != null) txtMouseQueueReboot.Visibility = Visibility.Visible;
                };

                toggleQueue.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.DeleteValue(RegistryHive.LocalMachine, MouseRegSubKey, MouseValueName);
                    RefreshMouseUI();
                    if (txtMouseQueueReboot != null) txtMouseQueueReboot.Visibility = Visibility.Visible;
                };
            }

            if (btnOpenPriorityRegedit != null) btnOpenPriorityRegedit.Click += (s, e) => { OpenRegeditAtKey(PriorityRegSubKey); };

            if (togglePriority != null) {
                togglePriority.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    int targetVal = 42;
                    ComboBoxItem sel = cmbPriority != null ? cmbPriority.SelectedItem as ComboBoxItem : null;
                    if (sel != null && sel.Tag != null) {
                        int parsed;
                        if (int.TryParse(sel.Tag.ToString(), out parsed) && parsed != 2) {
                            targetVal = parsed;
                        }
                    }
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, PriorityRegSubKey, PriorityValueName, targetVal);
                    RefreshPriorityUI();
                    if (txtPriorityReboot != null) txtPriorityReboot.Visibility = Visibility.Visible;
                };

                togglePriority.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, PriorityRegSubKey, PriorityValueName, 2);
                    RefreshPriorityUI();
                    if (txtPriorityReboot != null) txtPriorityReboot.Visibility = Visibility.Visible;
                };
            }

            if (cmbPriority != null) {
                cmbPriority.SelectionChanged += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    if (togglePriority != null && togglePriority.IsChecked == true) {
                        ComboBoxItem item = cmbPriority.SelectedItem as ComboBoxItem;
                        if (item != null && item.Tag != null) {
                            int parsed;
                            if (int.TryParse(item.Tag.ToString(), out parsed) && parsed != 2) {
                                RegistryHelper.SetDword(RegistryHive.LocalMachine, PriorityRegSubKey, PriorityValueName, parsed);
                                RefreshPriorityUI();
                                if (txtPriorityReboot != null) txtPriorityReboot.Visibility = Visibility.Visible;
                            }
                        }
                    }
                };
            }

            if (togglePowerPlan != null) {
                togglePowerPlan.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetActivePowerPlan("381b4222-f694-41f0-9685-ff5bb260df2e");
                    RefreshPowerUI();
                };

                togglePowerPlan.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetActivePowerPlan("8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c");
                    RefreshPowerUI();
                };
            }

            if (cmbPowerPlans != null) {
                cmbPowerPlans.SelectionChanged += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    ComboBoxItem selectedItem = cmbPowerPlans.SelectedItem as ComboBoxItem;
                    if (selectedItem != null && selectedItem.Tag != null) {
                        string guid = selectedItem.Tag.ToString();
                        SetActivePowerPlan(guid);
                        RefreshPowerUI();
                    }
                };
            }

            if (btnOpenPowerCpl != null) {
                btnOpenPowerCpl.Click += (s, e) => ProcessRunner.Start("control.exe", "powercfg.cpl");
            }

            if (toggleCoreIso != null) {
                toggleCoreIso.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetCoreIsoStatus(false);
                    RefreshCoreIsoUI();
                    if (txtCoreIsoReboot != null) txtCoreIsoReboot.Visibility = Visibility.Visible;
                };

                toggleCoreIso.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetCoreIsoStatus(true);
                    RefreshCoreIsoUI();
                    if (txtCoreIsoReboot != null) txtCoreIsoReboot.Visibility = Visibility.Visible;
                };
            }

            Action openDefenderCoreIso = () => {
                if (!ProcessRunner.Start("windowsdefender://coreisolation")) {
                    ProcessRunner.Start("ms-settings:windowsdefender");
                }
            };

            if (btnOpenDefenderCoreIso != null) {
                btnOpenDefenderCoreIso.Click += (s, e) => openDefenderCoreIso();
            }

            if (toggleLanEnergy != null) {
                toggleLanEnergy.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetLanEnergyOptimized(cachedLanEnergySubPath, true);
                    RefreshLanEnergyUI();
                };

                toggleLanEnergy.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetLanEnergyOptimized(cachedLanEnergySubPath, false);
                    RefreshLanEnergyUI();
                };
            }

            if (btnOpenLanDevMgmt != null) {
                btnOpenLanDevMgmt.Click += (s, e) => {
                    if (string.IsNullOrEmpty(cachedLanEnergyPnpId) ||
                        !ProcessRunner.Start("rundll32.exe", "devmgr.dll,DeviceProperties_RunDLL /DeviceID \"" + cachedLanEnergyPnpId + "\"")) {
                        ProcessRunner.Start("devmgmt.msc");
                    }
                };
            }

            if (txtSearchMouseTweaks != null) {
                txtSearchMouseTweaks.TextChanged += (s, e) => {
                    string q = txtSearchMouseTweaks.Text ?? "";
                    if (txtSearchMousePlaceholder != null) {
                        txtSearchMousePlaceholder.Visibility = string.IsNullOrEmpty(q) ? Visibility.Visible : Visibility.Collapsed;
                    }
                    if (btnClearSearchMouseTweaks != null) {
                        btnClearSearchMouseTweaks.Visibility = string.IsNullOrEmpty(q) ? Visibility.Collapsed : Visibility.Visible;
                    }
                    FilterMouseTweaks(q);
                };

                txtSearchMouseTweaks.KeyDown += (s, e) => {
                    if (e.Key == System.Windows.Input.Key.Escape) {
                        txtSearchMouseTweaks.Text = "";
                    }
                };
            }

            if (btnClearSearchMouseTweaks != null) {
                btnClearSearchMouseTweaks.Click += (s, e) => {
                    if (txtSearchMouseTweaks != null) {
                        txtSearchMouseTweaks.Text = "";
                        txtSearchMouseTweaks.Focus();
                    }
                };
            }

            Action applyFilterMode = () => FilterMouseTweaks();
            if (radFilterMouseAll != null) radFilterMouseAll.Checked += (s, e) => applyFilterMode();
            if (radFilterMouseActive != null) radFilterMouseActive.Checked += (s, e) => applyFilterMode();
            if (radFilterMouseInactive != null) radFilterMouseInactive.Checked += (s, e) => applyFilterMode();

            // 6. DisablePagingExecutive
            if (btnOpenPagingRegedit != null) btnOpenPagingRegedit.Click += (s, e) => OpenRegeditAtKey(PagingRegKey);
            if (togglePagingExecutive != null) {
                togglePagingExecutive.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, PagingRegKey, "DisablePagingExecutive", 1);
                    RefreshPagingExecutiveUI();
                    if (txtPagingReboot != null) txtPagingReboot.Visibility = Visibility.Visible;
                };
                togglePagingExecutive.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, PagingRegKey, "DisablePagingExecutive", 0);
                    RefreshPagingExecutiveUI();
                    if (txtPagingReboot != null) txtPagingReboot.Visibility = Visibility.Visible;
                };
            }

            // 7. SystemResponsiveness
            if (btnOpenSystemResponsivenessRegedit != null) btnOpenSystemResponsivenessRegedit.Click += (s, e) => OpenRegeditAtKey(SystemProfileRegKey);
            if (toggleSystemResponsiveness != null) {
                toggleSystemResponsiveness.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, SystemProfileRegKey, "SystemResponsiveness", 0);
                    RefreshSystemResponsivenessUI();
                    if (txtSystemResponsivenessReboot != null) txtSystemResponsivenessReboot.Visibility = Visibility.Visible;
                };
                toggleSystemResponsiveness.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, SystemProfileRegKey, "SystemResponsiveness", 20);
                    RefreshSystemResponsivenessUI();
                    if (txtSystemResponsivenessReboot != null) txtSystemResponsivenessReboot.Visibility = Visibility.Visible;
                };
            }

            // 8. NetworkThrottlingIndex
            if (btnOpenNetworkThrottlingRegedit != null) btnOpenNetworkThrottlingRegedit.Click += (s, e) => OpenRegeditAtKey(SystemProfileRegKey);
            if (toggleNetworkThrottling != null) {
                toggleNetworkThrottling.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, SystemProfileRegKey, "NetworkThrottlingIndex", -1);
                    RefreshNetworkThrottlingUI();
                    if (txtNetworkThrottlingReboot != null) txtNetworkThrottlingReboot.Visibility = Visibility.Visible;
                };
                toggleNetworkThrottling.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, SystemProfileRegKey, "NetworkThrottlingIndex", 10);
                    RefreshNetworkThrottlingUI();
                    if (txtNetworkThrottlingReboot != null) txtNetworkThrottlingReboot.Visibility = Visibility.Visible;
                };
            }

            // 8. MMCSS Games Priority
            if (btnOpenMmcssGamesRegedit != null) btnOpenMmcssGamesRegedit.Click += (s, e) => OpenRegeditAtKey(MmcssGamesRegKey);
            if (toggleMmcssGames != null) {
                toggleMmcssGames.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, MmcssGamesRegKey, "GPU Priority", 8);
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, MmcssGamesRegKey, "Priority", 6);
                    RegistryHelper.SetString(RegistryHive.LocalMachine, MmcssGamesRegKey, "Scheduling Category", "High");
                    RegistryHelper.SetString(RegistryHive.LocalMachine, MmcssGamesRegKey, "SFIO Priority", "High");
                    RefreshMmcssGamesUI();
                };
                toggleMmcssGames.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, MmcssGamesRegKey, "GPU Priority", 8);
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, MmcssGamesRegKey, "Priority", 2);
                    RegistryHelper.SetString(RegistryHive.LocalMachine, MmcssGamesRegKey, "Scheduling Category", "Medium");
                    RegistryHelper.SetString(RegistryHive.LocalMachine, MmcssGamesRegKey, "SFIO Priority", "Normal");
                    RefreshMmcssGamesUI();
                };
            }

            // 9. GameDVR & FSE
            if (btnOpenGameDvrRegedit != null) btnOpenGameDvrRegedit.Click += (s, e) => OpenRegeditAtKey(GameConfigStoreKey);
            if (toggleGameDvr != null) {
                toggleGameDvr.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_Enabled", 0);
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_FSEBehaviorMode", 2);
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_HonorUserFSEBehaviorMode", 1);
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_DXGIHonorFSEWindowsCompatible", 1);
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameDvrUserKey, "AppCaptureEnabled", 0);
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, GameDvrPolicyKey, "AllowGameDVR", 0);
                    RefreshGameDvrUI();
                };
                toggleGameDvr.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_Enabled", 1);
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_FSEBehaviorMode", 0);
                    RegistryHelper.SetDword(RegistryHive.CurrentUser, GameDvrUserKey, "AppCaptureEnabled", 1);
                    RegistryHelper.DeleteValue(RegistryHive.LocalMachine, GameDvrPolicyKey, "AllowGameDVR");
                    RefreshGameDvrUI();
                };
            }

            // 10. USB Selective Suspend
            if (btnOpenUsbDevMgmt != null) btnOpenUsbDevMgmt.Click += (s, e) => ProcessRunner.Start("devmgmt.msc");
            if (toggleUsbSelectiveSuspend != null) {
                toggleUsbSelectiveSuspend.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetUsbSelectiveSuspend(true);
                    RefreshUsbSelectiveSuspendUI();
                };
                toggleUsbSelectiveSuspend.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    SetUsbSelectiveSuspend(false);
                    RefreshUsbSelectiveSuspendUI();
                };
            }

            // 11. Realtek LAN RSS Optimizer
            if (btnRealtekRssDriver != null) {
                btnRealtekRssDriver.Click += (s, e) => {
                    string desc = !string.IsNullOrEmpty(cachedRealtekDesc) ? cachedRealtekDesc : "Realtek PCIe Family Controller";
                    string query = desc.Replace(" ", "+");
                    string url = "https://www.google.de/search?q=" + Uri.EscapeDataString(query) + "+Software+Windows+11+site:realtek.com";
                    ProcessRunner.Start(url);
                };
            }

            if (toggleRealtekRss != null) {
                toggleRealtekRss.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    if (string.IsNullOrEmpty(cachedRealtekAdapterName)) return;
                    toggleRealtekRss.IsEnabled = false;
                    if (txtRealtekRssSubtitle != null) txtRealtekRssSubtitle.Text = "Status: Wird angewendet...";
                    Task.Run(() => {
                        string args = string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Enable-NetAdapterRss -Name '{0}' -ErrorAction SilentlyContinue; Set-NetAdapterRss -Name '{0}' -BaseProcessorNumber 2 -ErrorAction SilentlyContinue\"", cachedRealtekAdapterName);
                        ProcessRunner.RunAndGetOutput("powershell.exe", args, 10000);
                        if (window != null) {
                            window.Dispatcher.BeginInvoke((Action)(() => {
                                if (txtRealtekRssReboot != null) txtRealtekRssReboot.Visibility = Visibility.Visible;
                                RefreshRealtekRssUI();
                            }));
                        }
                    });
                };

                toggleRealtekRss.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    if (string.IsNullOrEmpty(cachedRealtekAdapterName)) return;
                    toggleRealtekRss.IsEnabled = false;
                    if (txtRealtekRssSubtitle != null) txtRealtekRssSubtitle.Text = "Status: Wird zurückgesetzt...";
                    Task.Run(() => {
                        string args = string.Format("-NoProfile -ExecutionPolicy Bypass -Command \"Set-NetAdapterRss -Name '{0}' -BaseProcessorNumber 0 -ErrorAction SilentlyContinue\"", cachedRealtekAdapterName);
                        ProcessRunner.RunAndGetOutput("powershell.exe", args, 10000);
                        if (window != null) {
                            window.Dispatcher.BeginInvoke((Action)(() => {
                                if (txtRealtekRssReboot != null) txtRealtekRssReboot.Visibility = Visibility.Visible;
                                RefreshRealtekRssUI();
                            }));
                        }
                    });
                };
            }
        }

        private Border[] GetAllMouseCards() {
            return new Border[] {
                cardMouseQueue,
                cardMousePriority,
                cardMouseCoreIso,
                cardMousePowerPlan,
                cardLanEnergy,
                cardPagingExecutive,
                cardSystemResponsiveness,
                cardNetworkThrottling,
                cardMmcssGames,
                cardGameDvr,
                cardUsbSelectiveSuspend,
                cardRealtekRss
            };
        }

        private bool IsMouseCardActive(Border card) {
            if (card == cardMouseQueue) return toggleQueue != null && toggleQueue.IsChecked == true;
            if (card == cardMousePriority) return togglePriority != null && togglePriority.IsChecked == true;
            if (card == cardMouseCoreIso) return toggleCoreIso != null && toggleCoreIso.IsChecked == true;
            if (card == cardMousePowerPlan) return togglePowerPlan != null && togglePowerPlan.IsChecked == true;
            if (card == cardLanEnergy) return toggleLanEnergy != null && toggleLanEnergy.IsChecked == true;
            if (card == cardPagingExecutive) return togglePagingExecutive != null && togglePagingExecutive.IsChecked == true;
            if (card == cardSystemResponsiveness) return toggleSystemResponsiveness != null && toggleSystemResponsiveness.IsChecked == true;
            if (card == cardNetworkThrottling) return toggleNetworkThrottling != null && toggleNetworkThrottling.IsChecked == true;
            if (card == cardMmcssGames) return toggleMmcssGames != null && toggleMmcssGames.IsChecked == true;
            if (card == cardGameDvr) return toggleGameDvr != null && toggleGameDvr.IsChecked == true;
            if (card == cardUsbSelectiveSuspend) return toggleUsbSelectiveSuspend != null && toggleUsbSelectiveSuspend.IsChecked == true;
            if (card == cardRealtekRss) return toggleRealtekRss != null && toggleRealtekRss.IsChecked == true;
            return false;
        }

        // =========================================================================
        // FILTER & DYNAMIC LAYOUT: ALLGEMEINE TWEAKS CARDS
        // =========================================================================
        private void FilterMouseTweaks(string query = null) {
            try {
                if (query == null && txtSearchMouseTweaks != null) {
                    query = txtSearchMouseTweaks.Text;
                }
                string trimmed = (query ?? "").Trim();
                string[] terms = trimmed.ToLowerInvariant().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

                int filterMode = 0; // 0 = Alle, 1 = Aktiv, 2 = Nicht aktiv
                if (radFilterMouseActive != null && radFilterMouseActive.IsChecked == true) filterMode = 1;
                else if (radFilterMouseInactive != null && radFilterMouseInactive.IsChecked == true) filterMode = 2;

                var allCards = GetAllMouseCards();
                for (int i = 0; i < allCards.Length; i++) {
                    Border card = allCards[i];
                    if (card == null) continue;

                    bool isActive = IsMouseCardActive(card);
                    if (filterMode == 1 && !isActive) {
                        card.Visibility = Visibility.Collapsed;
                        continue;
                    }
                    if (filterMode == 2 && isActive) {
                        card.Visibility = Visibility.Collapsed;
                        continue;
                    }

                    if (terms.Length == 0) {
                        card.Visibility = Visibility.Visible;
                        continue;
                    }

                    string cardText = ExtractAllCardText(card).ToLowerInvariant();
                    bool matches = true;
                    for (int t = 0; t < terms.Length; t++) {
                        if (!cardText.Contains(terms[t])) {
                            matches = false;
                            break;
                        }
                    }

                    card.Visibility = matches ? Visibility.Visible : Visibility.Collapsed;
                }

                RelayoutMouseTweaks();
            } catch {}
        }

        private void RelayoutMouseTweaks() {
            if (gridMouseTweaks == null) return;

            var allCards = GetAllMouseCards();
            var visibleCards = new List<Border>();
            for (int i = 0; i < allCards.Length; i++) {
                Border c = allCards[i];
                if (c != null && c.Visibility == Visibility.Visible) visibleCards.Add(c);
            }

            for (int i = 0; i < allCards.Length; i++) {
                Border c = allCards[i];
                if (c != null && c.Visibility != Visibility.Visible) {
                    Grid.SetRow(c, 0);
                    Grid.SetColumn(c, 0);
                }
            }

            if (panelMouseNoResults != null) {
                panelMouseNoResults.Visibility = (visibleCards.Count == 0) ? Visibility.Visible : Visibility.Collapsed;
            }
            gridMouseTweaks.Visibility = (visibleCards.Count == 0) ? Visibility.Collapsed : Visibility.Visible;

            gridMouseTweaks.ColumnDefinitions.Clear();
            gridMouseTweaks.RowDefinitions.Clear();

            int tier = currentResponsiveTier >= 0 ? currentResponsiveTier : 0;
            if (tier == 0) {
                gridMouseTweaks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                gridMouseTweaks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
                gridMouseTweaks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                int rowCount = (visibleCards.Count + 1) / 2;
                for (int r = 0; r < rowCount; r++) {
                    gridMouseTweaks.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    if (r < rowCount - 1) {
                        gridMouseTweaks.RowDefinitions.Add(new RowDefinition { Height = new GridLength(14) });
                    }
                }

                for (int i = 0; i < visibleCards.Count; i++) {
                    var card = visibleCards[i];
                    int gridRow = (i / 2) * 2;
                    int gridCol = (i % 2 == 0) ? 0 : 2;
                    Grid.SetRow(card, gridRow);
                    Grid.SetColumn(card, gridCol);
                    Grid.SetColumnSpan(card, 1);
                    card.Margin = new Thickness(0);
                }
            } else {
                gridMouseTweaks.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                for (int i = 0; i < visibleCards.Count; i++) {
                    gridMouseTweaks.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    var card = visibleCards[i];
                    Grid.SetRow(card, i);
                    Grid.SetColumn(card, 0);
                    Grid.SetColumnSpan(card, 1);
                    card.Margin = (i < visibleCards.Count - 1) ? new Thickness(0, 0, 0, 12) : new Thickness(0);
                }
            }
        }

        private static string ExtractAllCardText(Border card) {
            if (card == null) return "";
            var sb = new System.Text.StringBuilder();

            try {
                if (card.Tag != null) sb.Append(card.Tag.ToString()).Append(' ');

                var queue = new Queue<DependencyObject>();
                queue.Enqueue(card);
                var visited = new HashSet<DependencyObject>();

                while (queue.Count > 0) {
                    var node = queue.Dequeue();
                    if (node == null || !visited.Add(node)) continue;

                    TextBlock tb = node as TextBlock;
                    if (tb != null && !string.IsNullOrEmpty(tb.Text)) {
                        sb.Append(' ').Append(tb.Text);
                    } else {
                        Button btn = node as Button;
                        if (btn != null) {
                            string bs = btn.Content as string;
                            if (!string.IsNullOrEmpty(bs)) sb.Append(' ').Append(bs);
                        } else {
                            ComboBox cb = node as ComboBox;
                            if (cb != null) {
                                foreach (object item in cb.Items) {
                                    ComboBoxItem cbi = item as ComboBoxItem;
                                    if (cbi != null && cbi.Content is string) {
                                        sb.Append(' ').Append((string)cbi.Content);
                                    } else if (item != null) {
                                        sb.Append(' ').Append(item.ToString());
                                    }
                                }
                            }
                        }
                    }

                    try {
                        foreach (object child in LogicalTreeHelper.GetChildren(node)) {
                            DependencyObject dChild = child as DependencyObject;
                            if (dChild != null) queue.Enqueue(dChild);
                        }
                    } catch {}

                    try {
                        Visual v = node as Visual;
                        if (v != null) {
                            int count = VisualTreeHelper.GetChildrenCount(v);
                            for (int i = 0; i < count; i++) {
                                queue.Enqueue(VisualTreeHelper.GetChild(v, i));
                            }
                        }
                    } catch {}
                }
            } catch {}

            return sb.ToString();
        }

        // =========================================================================
        // NATIVE LAN ENERGIESPARMODI (*EEE, Green Ethernet, PowerSavingMode, GigaLite)
        // =========================================================================
        private static readonly string[] LanEeeKeys = new[] { "*EEE", "EnableEEE", "AdvancedEEE" };
        private static readonly string[] LanGreenKeys = new[] { "EnableGreenEthernet", "GreenEthernet", "*GreenEthernet" };
        private static readonly string[] LanPowerKeys = new[] { "PowerSavingMode", "*PowerSavingMode", "AutoPowerSaveModeEnabled", "ReduceSpeedOnPowerDown" };
        private static readonly string[] LanGigaLiteKeys = new[] { "GigaLite", "*GigaLite" };
        private static readonly string[] LanInterruptModerationKeys = new[] { "*InterruptModeration", "InterruptModeration" };

        private string cachedLanEnergySubPath = null;
        private string cachedLanEnergyPnpId = null;
        private string cachedRealtekAdapterName = null;
        private string cachedRealtekDesc = null;

        private string GetActiveLanAdapterSubPath(out string adapterDesc, out string pnpId) {
            adapterDesc = "LAN-Adapter";
            pnpId = null;
            string classPath = @"SYSTEM\CurrentControlSet\Control\Class\{4d36e972-e325-11ce-bfc1-08002be10318}";

            string activeNetCfgId = null;
            try {
                foreach (var ni in System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()) {
                    if (ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                        (ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet ||
                         ni.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.GigabitEthernet)) {
                        string desc = (ni.Description ?? "").ToLowerInvariant();
                        if (!desc.Contains("virtual") && !desc.Contains("hyper-v") && !desc.Contains("wsl") && !desc.Contains("vpn") && !desc.Contains("tap-") && !desc.Contains("vethernet")) {
                            activeNetCfgId = ni.Id;
                            break;
                        }
                    }
                }
            } catch {}

            string fallbackSub = null;
            string fallbackDesc = null;
            string fallbackPnp = null;

            try {
                using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(classPath)) {
                    if (baseKey != null) {
                        foreach (string subName in baseKey.GetSubKeyNames()) {
                            if (subName.Length == 4) {
                                using (RegistryKey adapterKey = baseKey.OpenSubKey(subName)) {
                                    if (adapterKey != null) {
                                        object descObj = adapterKey.GetValue("DriverDesc");
                                        object netCfgObj = adapterKey.GetValue("NetCfgInstanceId");
                                        object matchId = adapterKey.GetValue("MatchingDeviceId");

                                        if (descObj != null) {
                                            string desc = descObj.ToString();
                                            if (!Regex.IsMatch(desc, @"WAN|Miniport|Virtual|Kernel|Bluetooth|Pacer|QoS|Loopback|Teredo|ISATAP|Direct|Wi-Fi|Wireless|802\.11", RegexOptions.IgnoreCase)) {
                                                string pnp = null;
                                                if (netCfgObj != null) {
                                                    string connPath = @"SYSTEM\CurrentControlSet\Control\Network\{4D36E972-E325-11CE-BFC1-08002BE10318}\" + netCfgObj.ToString() + @"\Connection";
                                                    using (RegistryKey connKey = Registry.LocalMachine.OpenSubKey(connPath)) {
                                                        if (connKey != null) {
                                                            object pnpObj = connKey.GetValue("PnPInstanceId") ?? connKey.GetValue("PnPInstanceID");
                                                            if (pnpObj != null) pnp = pnpObj.ToString();
                                                        }
                                                    }
                                                }
                                                if (string.IsNullOrEmpty(pnp) && matchId != null) {
                                                    pnp = matchId.ToString();
                                                }

                                                string subPath = classPath + @"\" + subName;

                                                if (activeNetCfgId != null && netCfgObj != null && string.Equals(netCfgObj.ToString(), activeNetCfgId, StringComparison.OrdinalIgnoreCase)) {
                                                    adapterDesc = desc;
                                                    pnpId = pnp;
                                                    return subPath;
                                                }

                                                if (fallbackSub == null) {
                                                    fallbackSub = subPath;
                                                    fallbackDesc = desc;
                                                    fallbackPnp = pnp;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } catch {}

            if (fallbackSub != null) {
                adapterDesc = fallbackDesc ?? "LAN-Adapter";
                pnpId = fallbackPnp;
                return fallbackSub;
            }

            return null;
        }

        private bool CheckLanEnergyOptimized(string adapterSubPath, out int activeCount, out int totalCount) {
            activeCount = 0;
            totalCount = 0;
            if (string.IsNullOrEmpty(adapterSubPath)) return false;

            try {
                using (RegistryKey adapterKey = Registry.LocalMachine.OpenSubKey(adapterSubPath)) {
                    if (adapterKey == null) return false;

                    using (RegistryKey ndiKey = adapterKey.OpenSubKey(@"Ndi\params")) {
                        var groups = new[] { LanEeeKeys, LanGreenKeys, LanPowerKeys, LanGigaLiteKeys, LanInterruptModerationKeys };
                        foreach (var group in groups) {
                            bool found = false;
                            bool isOff = false;

                            foreach (var keyName in group) {
                                object val = adapterKey.GetValue(keyName);
                                if (val != null) {
                                    found = true;
                                    string s = val.ToString().Trim();
                                    if (s == "0") isOff = true;
                                    break;
                                }
                            }

                            if (!found && ndiKey != null) {
                                foreach (var keyName in group) {
                                    using (var pk = ndiKey.OpenSubKey(keyName)) {
                                        if (pk != null) {
                                            found = true;
                                            object defObj = pk.GetValue("default");
                                            string defStr = defObj != null ? defObj.ToString().Trim() : "1";
                                            if (defStr == "0") isOff = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (found) {
                                totalCount++;
                                if (!isOff) activeCount++;
                            }
                        }
                    }
                }
            } catch {}

            return totalCount > 0 && activeCount == 0;
        }

        private void SetLanEnergyOptimized(string adapterSubPath, bool optimize) {
            if (string.IsNullOrEmpty(adapterSubPath)) {
                string d, p;
                adapterSubPath = GetActiveLanAdapterSubPath(out d, out p);
                if (string.IsNullOrEmpty(adapterSubPath)) return;
            }

            string targetVal = optimize ? "0" : "1";

            try {
                using (RegistryKey adapterKey = Registry.LocalMachine.OpenSubKey(adapterSubPath, true)) {
                    if (adapterKey == null) return;

                    using (RegistryKey ndiKey = adapterKey.OpenSubKey(@"Ndi\params")) {
                        var groups = new[] { LanEeeKeys, LanGreenKeys, LanPowerKeys, LanGigaLiteKeys, LanInterruptModerationKeys };
                        foreach (var group in groups) {
                            bool written = false;

                            foreach (var keyName in group) {
                                if (adapterKey.GetValue(keyName) != null) {
                                    adapterKey.SetValue(keyName, targetVal, RegistryValueKind.String);
                                    written = true;
                                }
                            }

                            if (!written && ndiKey != null) {
                                foreach (var keyName in group) {
                                    using (var pk = ndiKey.OpenSubKey(keyName)) {
                                        if (pk != null) {
                                            adapterKey.SetValue(keyName, targetVal, RegistryValueKind.String);
                                            written = true;
                                            break;
                                        }
                                    }
                                }
                            }

                            if (!written) {
                                adapterKey.SetValue(group[0], targetVal, RegistryValueKind.String);
                            }
                        }
                    }
                }
            } catch {}
        }

        private string GetLanAdapterSetting(string adapterSubPath, string[] keys) {
            if (string.IsNullOrEmpty(adapterSubPath)) return null;
            try {
                using (RegistryKey adapterKey = Registry.LocalMachine.OpenSubKey(adapterSubPath)) {
                    if (adapterKey != null) {
                        foreach (var k in keys) {
                            object v = adapterKey.GetValue(k);
                            if (v != null) return v.ToString().Trim();
                        }
                        using (RegistryKey ndiKey = adapterKey.OpenSubKey(@"Ndi\params")) {
                            if (ndiKey != null) {
                                foreach (var k in keys) {
                                    using (var pk = ndiKey.OpenSubKey(k)) {
                                        if (pk != null) {
                                            object defObj = pk.GetValue("default");
                                            if (defObj != null) return defObj.ToString().Trim();
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            } catch {}
            return null;
        }

        private void RefreshLanEnergyUI() {
            updateUIRefCount++;
            try {
                string desc, pnpId;
                string subPath = GetActiveLanAdapterSubPath(out desc, out pnpId);
                cachedLanEnergySubPath = subPath;
                cachedLanEnergyPnpId = pnpId;

                if (txtLanEnergyAdapter != null) {
                    txtLanEnergyAdapter.Text = !string.IsNullOrEmpty(desc) ? desc : "Kein LAN-Adapter gefunden";
                }

                int activeCount, totalCount;
                bool isOptimized = CheckLanEnergyOptimized(subPath, out activeCount, out totalCount);

                if (toggleLanEnergy != null) {
                    toggleLanEnergy.IsChecked = isOptimized;
                    toggleLanEnergy.IsEnabled = isAdmin && subPath != null;
                }

                if (txtLanEnergySubtitle != null) {
                    if (subPath != null && isOptimized) {
                        txtLanEnergySubtitle.Text = "Status: Optimiert";
                        txtLanEnergySubtitle.Foreground = UIHelper.BrushGreenText;
                    } else {
                        txtLanEnergySubtitle.Text = "Status: Nicht optimiert";
                        txtLanEnergySubtitle.Foreground = UIHelper.BrushRedText;
                    }
                }

                if (subPath != null) {
                    string eeeVal = GetLanAdapterSetting(subPath, LanEeeKeys) ?? GetLanAdapterSetting(subPath, LanGreenKeys);
                    string pwrVal = GetLanAdapterSetting(subPath, LanPowerKeys) ?? GetLanAdapterSetting(subPath, LanGigaLiteKeys);
                    string intVal = GetLanAdapterSetting(subPath, LanInterruptModerationKeys);

                    bool eeeOff = eeeVal == "0";
                    bool pwrOff = pwrVal == "0";
                    bool intOff = intVal == "0";

                    if (txtLanEeeLiveVal != null) {
                        txtLanEeeLiveVal.Text = eeeOff ? "0 (Deaktiviert)" : (eeeVal ?? "1") + " (Aktiv)";
                        txtLanEeeLiveVal.Foreground = eeeOff ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    }
                    if (txtLanPowerLiveVal != null) {
                        txtLanPowerLiveVal.Text = pwrOff ? "0 (Deaktiviert)" : (pwrVal ?? "1") + " (Aktiv)";
                        txtLanPowerLiveVal.Foreground = pwrOff ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    }
                    if (txtLanInterruptLiveVal != null) {
                        txtLanInterruptLiveVal.Text = intOff ? "0 (Deaktiviert)" : (intVal ?? "1") + " (Aktiv)";
                        txtLanInterruptLiveVal.Foreground = intOff ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    }
                } else {
                    if (txtLanEeeLiveVal != null) { txtLanEeeLiveVal.Text = "Nicht ermittelbar"; txtLanEeeLiveVal.Foreground = UIHelper.BrushGrayText; }
                    if (txtLanPowerLiveVal != null) { txtLanPowerLiveVal.Text = "Nicht ermittelbar"; txtLanPowerLiveVal.Foreground = UIHelper.BrushGrayText; }
                    if (txtLanInterruptLiveVal != null) { txtLanInterruptLiveVal.Text = "Nicht ermittelbar"; txtLanInterruptLiveVal.Foreground = UIHelper.BrushGrayText; }
                }
            } finally {
                updateUIRefCount--;
            }
        }

        // =========================================================================
        // CARDS 6 - 10 REFRESH & SET LOGIC
        // =========================================================================

        private void RefreshAllMouseTweaksUI() {
            RefreshMouseUI();
            RefreshPriorityUI();
            RefreshCoreIsoUI();
            RefreshPowerUI();
            RefreshLanEnergyUI();
            RefreshPagingExecutiveUI();
            RefreshSystemResponsivenessUI();
            RefreshNetworkThrottlingUI();
            RefreshMmcssGamesUI();
            RefreshGameDvrUI();
            RefreshUsbSelectiveSuspendUI();
            RefreshRealtekRssUI();
            FilterMouseTweaks();
        }

        // 6. DisablePagingExecutive
        private void RefreshPagingExecutiveUI() {
            updateUIRefCount++;
            try {
                int? val = RegistryHelper.GetDword(RegistryHive.LocalMachine, PagingRegKey, "DisablePagingExecutive");
                bool opt = val.HasValue && val.Value == 1;

                if (togglePagingExecutive != null) togglePagingExecutive.IsChecked = opt;
                if (txtPagingSubtitle != null) {
                    txtPagingSubtitle.Text = opt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtPagingSubtitle.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtPagingLiveVal != null) {
                    if (val.HasValue) {
                        txtPagingLiveVal.Text = opt ? "1 (Im RAM gehalten)" : "0 (Auslagern erlaubt)";
                        txtPagingLiveVal.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    } else {
                        txtPagingLiveVal.Text = "Gelöscht (0 - Auslagern)";
                        txtPagingLiveVal.Foreground = UIHelper.GetBrush("#F97316");
                    }
                }
            } finally {
                updateUIRefCount--;
            }
        }

        // 7. SystemResponsiveness
        private void RefreshSystemResponsivenessUI() {
            updateUIRefCount++;
            try {
                int? sr = RegistryHelper.GetDword(RegistryHive.LocalMachine, SystemProfileRegKey, "SystemResponsiveness");
                bool opt = sr.HasValue && sr.Value == 0;

                if (toggleSystemResponsiveness != null) toggleSystemResponsiveness.IsChecked = opt;
                if (txtSystemResponsivenessSubtitle != null) {
                    txtSystemResponsivenessSubtitle.Text = opt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtSystemResponsivenessSubtitle.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtSystemResponsivenessLiveVal != null) {
                    if (sr.HasValue) {
                        txtSystemResponsivenessLiveVal.Text = opt ? "0 (100% Gaming-Prio)" : sr.Value.ToString() + " (" + sr.Value + "% Reserve)";
                        txtSystemResponsivenessLiveVal.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    } else {
                        txtSystemResponsivenessLiveVal.Text = "Nicht gesetzt (Standard 20)";
                        txtSystemResponsivenessLiveVal.Foreground = UIHelper.GetBrush("#F97316");
                    }
                }
            } finally {
                updateUIRefCount--;
            }
        }

        // 8. NetworkThrottlingIndex
        private void RefreshNetworkThrottlingUI() {
            updateUIRefCount++;
            try {
                int? nti = RegistryHelper.GetDword(RegistryHive.LocalMachine, SystemProfileRegKey, "NetworkThrottlingIndex");
                bool opt = nti.HasValue && (nti.Value == -1 || unchecked((uint)nti.Value) == 0xFFFFFFFF);

                if (toggleNetworkThrottling != null) toggleNetworkThrottling.IsChecked = opt;
                if (txtNetworkThrottlingSubtitle != null) {
                    txtNetworkThrottlingSubtitle.Text = opt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtNetworkThrottlingSubtitle.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtNetworkThrottlingLiveVal != null) {
                    if (nti.HasValue) {
                        if (opt) {
                            txtNetworkThrottlingLiveVal.Text = "0xFFFFFFFF (Drossel aus)";
                            txtNetworkThrottlingLiveVal.Foreground = UIHelper.BrushGreenText;
                        } else {
                            txtNetworkThrottlingLiveVal.Text = nti.Value.ToString() + " (Drossel aktiv)";
                            txtNetworkThrottlingLiveVal.Foreground = UIHelper.GetBrush("#F97316");
                        }
                    } else {
                        txtNetworkThrottlingLiveVal.Text = "Nicht gesetzt (Standard 10)";
                        txtNetworkThrottlingLiveVal.Foreground = UIHelper.GetBrush("#F97316");
                    }
                }
            } finally {
                updateUIRefCount--;
            }
        }

        // 8. MMCSS Gaming Tasks
        private void RefreshMmcssGamesUI() {
            updateUIRefCount++;
            try {
                int? gpu = RegistryHelper.GetDword(RegistryHive.LocalMachine, MmcssGamesRegKey, "GPU Priority");
                int? prio = RegistryHelper.GetDword(RegistryHive.LocalMachine, MmcssGamesRegKey, "Priority");
                string sched = RegistryHelper.GetString(RegistryHive.LocalMachine, MmcssGamesRegKey, "Scheduling Category");
                string sfio = RegistryHelper.GetString(RegistryHive.LocalMachine, MmcssGamesRegKey, "SFIO Priority");

                bool prioOpt = (gpu.HasValue && gpu.Value == 8) && (prio.HasValue && prio.Value == 6);
                bool catOpt = string.Equals(sched, "High", StringComparison.OrdinalIgnoreCase) && string.Equals(sfio, "High", StringComparison.OrdinalIgnoreCase);
                bool opt = prioOpt && catOpt;

                if (toggleMmcssGames != null) toggleMmcssGames.IsChecked = opt;
                if (txtMmcssGamesSubtitle != null) {
                    txtMmcssGamesSubtitle.Text = opt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtMmcssGamesSubtitle.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtMmcssPrioLiveVal != null) {
                    string gStr = gpu.HasValue ? gpu.Value.ToString() : "8";
                    string pStr = prio.HasValue ? prio.Value.ToString() : "2";
                    txtMmcssPrioLiveVal.Text = gStr + " / " + pStr + (prioOpt ? " (Max)" : " (Standard)");
                    txtMmcssPrioLiveVal.Foreground = prioOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
                if (txtMmcssCategoryLiveVal != null) {
                    string cStr = !string.IsNullOrEmpty(sched) ? sched : "Medium";
                    string sStr = !string.IsNullOrEmpty(sfio) ? sfio : "Normal";
                    txtMmcssCategoryLiveVal.Text = cStr + " / " + sStr;
                    txtMmcssCategoryLiveVal.Foreground = catOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
            } finally {
                updateUIRefCount--;
            }
        }

        // 9. GameDVR & FSE
        private void RefreshGameDvrUI() {
            updateUIRefCount++;
            try {
                int? dvr = RegistryHelper.GetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_Enabled");
                int? fse = RegistryHelper.GetDword(RegistryHive.CurrentUser, GameConfigStoreKey, "GameDVR_FSEBehaviorMode");
                int? pol = RegistryHelper.GetDword(RegistryHive.LocalMachine, GameDvrPolicyKey, "AllowGameDVR");

                bool dvrOpt = dvr.HasValue && dvr.Value == 0;
                bool fseOpt = fse.HasValue && fse.Value == 2;
                bool polOpt = pol.HasValue && pol.Value == 0;
                bool opt = dvrOpt && fseOpt && polOpt;

                if (toggleGameDvr != null) toggleGameDvr.IsChecked = opt;
                if (txtGameDvrSubtitle != null) {
                    txtGameDvrSubtitle.Text = opt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtGameDvrSubtitle.Foreground = opt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtGameDvrLiveVal != null) {
                    txtGameDvrLiveVal.Text = dvrOpt ? "0 (Deaktiviert)" : (dvr.HasValue ? dvr.Value.ToString() + " (Aktiv)" : "1 (Aktiv)");
                    txtGameDvrLiveVal.Foreground = dvrOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
                if (txtFseModeLiveVal != null) {
                    txtFseModeLiveVal.Text = fseOpt ? "2 (FSE erzwungen)" : (fse.HasValue ? fse.Value.ToString() + " (Standard)" : "0 (Standard)");
                    txtFseModeLiveVal.Foreground = fseOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
                if (txtAllowGameDvrLiveVal != null) {
                    txtAllowGameDvrLiveVal.Text = polOpt ? "0 (Gesperrt)" : (pol.HasValue ? pol.Value.ToString() : "Gelöscht (Erlaubt)");
                    txtAllowGameDvrLiveVal.Foreground = polOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
            } finally {
                updateUIRefCount--;
            }
        }

        // 10. USB Selective Suspend
        private void RefreshUsbSelectiveSuspendUI() {
            updateUIRefCount++;
            try {
                bool isOpt = CheckUsbSelectiveSuspendOptimized();
                if (toggleUsbSelectiveSuspend != null) toggleUsbSelectiveSuspend.IsChecked = isOpt;
                if (txtUsbSelectiveSuspendSubtitle != null) {
                    txtUsbSelectiveSuspendSubtitle.Text = isOpt ? "Status: Optimiert" : "Status: Nicht optimiert";
                    txtUsbSelectiveSuspendSubtitle.Foreground = isOpt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                if (txtUsbRegLiveVal != null) {
                    txtUsbRegLiveVal.Text = isOpt ? "0 (Deaktiviert)" : "1 (Aktiviert)";
                    txtUsbRegLiveVal.Foreground = isOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
                if (txtUsbPowerLiveVal != null) {
                    txtUsbPowerLiveVal.Text = isOpt ? "Deaktiviert (0)" : "Aktiviert (1)";
                    txtUsbPowerLiveVal.Foreground = isOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                }
            } finally {
                updateUIRefCount--;
            }
        }

        private static bool CheckUsbSelectiveSuspendOptimized() {
            try {
                using (RegistryKey usbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB")) {
                    if (usbKey == null) return false;
                    int total = 0, optimized = 0;
                    CheckUsbKeysRecursive(usbKey, ref total, ref optimized);
                    return total > 0 && optimized == total;
                }
            } catch {
                return false;
            }
        }

        private static void CheckUsbKeysRecursive(RegistryKey key, ref int total, ref int optimized) {
            if (key == null) return;
            try {
                string[] subNames = key.GetSubKeyNames();
                for (int i = 0; i < subNames.Length; i++) {
                    string subName = subNames[i];
                    if (string.Equals(subName, "Device Parameters", StringComparison.OrdinalIgnoreCase)) {
                        using (RegistryKey devParams = key.OpenSubKey(subName)) {
                            if (devParams != null) {
                                object val = devParams.GetValue("SelectiveSuspendEnabled");
                                if (val != null) {
                                    total++;
                                    int intVal = -1;
                                    if (val is int) intVal = (int)val;
                                    byte[] bVal = val as byte[];
                                    if (bVal != null && bVal.Length > 0) intVal = bVal[0];
                                    if (intVal == 0) optimized++;
                                }
                            }
                        }
                    } else {
                        using (RegistryKey sub = key.OpenSubKey(subName)) {
                            CheckUsbKeysRecursive(sub, ref total, ref optimized);
                        }
                    }
                }
            } catch {}
        }

        private static void SetUsbSelectiveSuspend(bool disableSuspend) {
            int target = disableSuspend ? 0 : 1;
            try {
                using (RegistryKey usbKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Enum\USB", true)) {
                    if (usbKey != null) SetUsbKeysRecursive(usbKey, target);
                }
            } catch {}
            try {
                ProcessRunner.RunAndGetOutput("powercfg.exe", "/SETACVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 " + target);
                ProcessRunner.RunAndGetOutput("powercfg.exe", "/SETDCVALUEINDEX SCHEME_CURRENT 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 " + target);
                ProcessRunner.RunAndGetOutput("powercfg.exe", "/SETACTIVE SCHEME_CURRENT");
            } catch {}
        }

        private static void SetUsbKeysRecursive(RegistryKey key, int target) {
            if (key == null) return;
            try {
                string[] subNames = key.GetSubKeyNames();
                for (int i = 0; i < subNames.Length; i++) {
                    string subName = subNames[i];
                    if (string.Equals(subName, "Device Parameters", StringComparison.OrdinalIgnoreCase)) {
                        using (RegistryKey devParams = key.OpenSubKey(subName, true)) {
                            if (devParams != null) {
                                object existing = devParams.GetValue("SelectiveSuspendEnabled");
                                if (existing != null) {
                                    if (existing is byte[]) {
                                        devParams.SetValue("SelectiveSuspendEnabled", new byte[] { (byte)target }, RegistryValueKind.Binary);
                                    } else {
                                        devParams.SetValue("SelectiveSuspendEnabled", target, RegistryValueKind.DWord);
                                    }
                                }
                            }
                        }
                    } else {
                        using (RegistryKey sub = key.OpenSubKey(subName, true)) {
                            SetUsbKeysRecursive(sub, target);
                        }
                    }
                }
            } catch {}
        }

        // 11. Realtek LAN RSS Optimizer
        private void RefreshRealtekRssUI() {
            updateUIRefCount++;
            try {
                string adapterName = null;
                string adapterDesc = null;
                bool? rssEnabled = null;
                int? baseProc = null;

                try {
                    var scope = new ManagementScope(@"\\.\root\StandardCimv2");
                    scope.Connect();
                    var query = new SelectQuery("SELECT Name, InterfaceDescription, Enabled, BaseProcessorNumber FROM MSFT_NetAdapterRssSettingData");
                    using (var searcher = new ManagementObjectSearcher(scope, query)) {
                        foreach (ManagementObject obj in searcher.Get()) {
                            string name = obj["Name"] != null ? obj["Name"].ToString() : "";
                            string desc = obj["InterfaceDescription"] != null ? obj["InterfaceDescription"].ToString() : "";

                            if (adapterName == null || desc.IndexOf("Realtek", StringComparison.OrdinalIgnoreCase) >= 0) {
                                adapterName = name;
                                adapterDesc = desc;
                                if (obj["Enabled"] != null) {
                                    bool en;
                                    if (bool.TryParse(obj["Enabled"].ToString(), out en)) rssEnabled = en;
                                }
                                if (obj["BaseProcessorNumber"] != null) {
                                    int bp;
                                    if (int.TryParse(obj["BaseProcessorNumber"].ToString(), out bp)) baseProc = bp;
                                }
                                if (desc.IndexOf("Realtek", StringComparison.OrdinalIgnoreCase) >= 0) break;
                            }
                        }
                    }
                } catch {}

                if (string.IsNullOrEmpty(adapterDesc)) {
                    string fallbackDesc, pnp;
                    GetActiveLanAdapterSubPath(out fallbackDesc, out pnp);
                    adapterDesc = fallbackDesc;
                }

                cachedRealtekAdapterName = adapterName;
                cachedRealtekDesc = adapterDesc;

                if (txtRealtekRssAdapter != null) {
                    txtRealtekRssAdapter.Text = !string.IsNullOrEmpty(adapterDesc) ? adapterDesc : "Kein LAN-Adapter gefunden";
                }

                if (string.IsNullOrEmpty(adapterName) && !string.IsNullOrEmpty(adapterDesc) && adapterDesc.IndexOf("Realtek", StringComparison.OrdinalIgnoreCase) >= 0) {
                    if (txtRealtekRssSubtitle != null) {
                        txtRealtekRssSubtitle.Text = "Status: Nicht optimiert";
                        txtRealtekRssSubtitle.Foreground = UIHelper.BrushRedText;
                    }
                    if (toggleRealtekRss != null) {
                        toggleRealtekRss.IsChecked = false;
                        toggleRealtekRss.IsEnabled = false;
                    }
                    if (txtRealtekRssEnabledLiveVal != null) {
                        txtRealtekRssEnabledLiveVal.Text = "Inaktiv";
                        txtRealtekRssEnabledLiveVal.Foreground = UIHelper.BrushRedText;
                    }
                    if (txtRealtekRssProcLiveVal != null) {
                        txtRealtekRssProcLiveVal.Text = "0 (Core 0)";
                        txtRealtekRssProcLiveVal.Foreground = UIHelper.BrushRedText;
                    }
                } else if (rssEnabled.HasValue) {
                    bool isOpt = (rssEnabled.Value == true) && (baseProc.HasValue && baseProc.Value == 2);
                    if (toggleRealtekRss != null) {
                        toggleRealtekRss.IsChecked = isOpt;
                        toggleRealtekRss.IsEnabled = isAdmin;
                    }
                    if (txtRealtekRssSubtitle != null) {
                        txtRealtekRssSubtitle.Text = isOpt ? "Status: Optimiert" : "Status: Nicht optimiert";
                        txtRealtekRssSubtitle.Foreground = isOpt ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                    }
                    if (txtRealtekRssEnabledLiveVal != null) {
                        txtRealtekRssEnabledLiveVal.Text = rssEnabled.Value ? "True (Aktiv)" : "False (Inaktiv)";
                        txtRealtekRssEnabledLiveVal.Foreground = rssEnabled.Value ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                    }
                    if (txtRealtekRssProcLiveVal != null) {
                        if (baseProc.HasValue) {
                            bool bpOpt = baseProc.Value == 2;
                            txtRealtekRssProcLiveVal.Text = string.Format("{0} (Core {0})", baseProc.Value);
                            txtRealtekRssProcLiveVal.Foreground = bpOpt ? UIHelper.BrushGreenText : UIHelper.GetBrush("#F97316");
                        } else {
                            txtRealtekRssProcLiveVal.Text = "Nicht gesetzt";
                            txtRealtekRssProcLiveVal.Foreground = UIHelper.BrushGrayText;
                        }
                    }
                } else {
                    if (txtRealtekRssSubtitle != null) {
                        txtRealtekRssSubtitle.Text = "Status: Nicht optimiert";
                        txtRealtekRssSubtitle.Foreground = UIHelper.BrushRedText;
                    }
                    if (toggleRealtekRss != null) {
                        toggleRealtekRss.IsChecked = false;
                        toggleRealtekRss.IsEnabled = false;
                    }
                    if (txtRealtekRssEnabledLiveVal != null) {
                        txtRealtekRssEnabledLiveVal.Text = "Nicht verfügbar";
                        txtRealtekRssEnabledLiveVal.Foreground = UIHelper.BrushGrayText;
                    }
                    if (txtRealtekRssProcLiveVal != null) {
                        txtRealtekRssProcLiveVal.Text = "Nicht verfügbar";
                        txtRealtekRssProcLiveVal.Foreground = UIHelper.BrushGrayText;
                    }
                }
            } finally {
                updateUIRefCount--;
            }
        }
    }
}
