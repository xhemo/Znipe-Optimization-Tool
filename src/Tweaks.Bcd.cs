using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Logik für den BCD-Boot & HPET Reiter nach dem KISS-Prinzip.
    /// </summary>
    public partial class MainWindowLogic {
        // =========================================================================
        // NATIVE BCDEDIT OPERATIONEN
        // =========================================================================
        private void UpdateBcdRowHighlight(ComboBox cmb, Border rowBorder, Border pill, TextBlock txtPill, ref int modifiedCount) {
            if (cmb == null) return;
            string tag = GetSelectedTag(cmb);
            bool isModified = !string.IsNullOrEmpty(tag) && !tag.Equals("DELETE", StringComparison.OrdinalIgnoreCase);

            if (isModified) {
                modifiedCount++;
                cmb.BorderBrush = UIHelper.BrushBlueText;
                cmb.BorderThickness = new Thickness(1.5);
                cmb.Background = UIHelper.GetBrush("#0B253A");
                cmb.Foreground = UIHelper.BrushBlueText;

                if (rowBorder != null) {
                    rowBorder.BorderBrush = UIHelper.GetBrush("#0284C7");
                    rowBorder.Background = UIHelper.GetBrush("#091726");
                }

                if (pill != null) {
                    pill.Background = UIHelper.GetBrush("#0C4A6E");
                    pill.BorderBrush = UIHelper.BrushBlueText;
                }
                if (txtPill != null) {
                    txtPill.Text = "⚡ AKTIV: " + tag.ToUpperInvariant();
                    txtPill.Foreground = UIHelper.BrushBlueText;
                }
            } else {
                cmb.BorderBrush = UIHelper.GetBrush("#232938");
                cmb.BorderThickness = new Thickness(1.2);
                cmb.Background = UIHelper.GetBrush("#141722");
                cmb.Foreground = UIHelper.BrushWhite;

                if (rowBorder != null) {
                    rowBorder.BorderBrush = UIHelper.GetBrush("#1B202C");
                    rowBorder.Background = UIHelper.GetBrush("#0A0D14");
                }

                if (pill != null) {
                    pill.Background = UIHelper.GetBrush("#181C26");
                    pill.BorderBrush = UIHelper.GetBrush("#262D3D");
                }
                if (txtPill != null) {
                    txtPill.Text = "STANDARD";
                    txtPill.Foreground = UIHelper.BrushMuted;
                }
            }
        }

        private static string ExtractBcdVal(string output, string paramName) {
            Match m = Regex.Match(output, @"^\s*" + paramName + @"\s+(.+)$", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            return m.Success ? m.Groups[1].Value.Trim() : "Nicht gesetzt (Standard)";
        }

        private void RefreshBcdUI() {
            Task.Run(() => {
                string output = ProcessRunner.RunAndGetOutput("bcdedit.exe", "/enum {current}");
                string clock = ExtractBcdVal(output, "useplatformclock");
                string tick = ExtractBcdVal(output, "useplatformtick");
                string tsc = ExtractBcdVal(output, "tscsyncpolicy");
                string dynamic = ExtractBcdVal(output, "disabledynamictick");

                if (window != null) {
                    window.Dispatcher.BeginInvoke((Action)(() => {
                        updateUIRefCount++;
                        try {
                            SelectComboByTag(cmbClock, clock);
                            SelectComboByTag(cmbTick, tick);
                            SelectComboByTag(cmbTsc, tsc);
                            SelectComboByTag(cmbDynamic, dynamic);

                            int modifiedCount = 0;
                            UpdateBcdRowHighlight(cmbClock, borderRowClock, pillClock, txtPillClock, ref modifiedCount);
                            UpdateBcdRowHighlight(cmbTick, borderRowTick, pillTick, txtPillTick, ref modifiedCount);
                            UpdateBcdRowHighlight(cmbTsc, borderRowTsc, pillTsc, txtPillTsc, ref modifiedCount);
                            UpdateBcdRowHighlight(cmbDynamic, borderRowDynamic, pillDynamic, txtPillDynamic, ref modifiedCount);
                        } finally {
                            updateUIRefCount--;
                        }
                    }));
                }
            });
        }

        private void SetBcdParam(string param, string val) {
            string args = (val == "DELETE")
                ? string.Format("/deletevalue {{current}} {0}", param)
                : string.Format("/set {{current}} {0} {1}", param, val);
            ProcessRunner.RunAndGetOutput("bcdedit.exe", args);
        }

        private string GetSelectedTag(ComboBox cmb) {
            if (cmb == null) return "DELETE";
            ComboBoxItem item = cmb.SelectedItem as ComboBoxItem;
            return (item != null && item.Tag != null) ? item.Tag.ToString() : "DELETE";
        }

        private void SelectComboByTag(ComboBox cmb, string val) {
            if (cmb == null) return;
            if (string.IsNullOrEmpty(val)) val = "DELETE";
            string normVal = val.Trim();
            if (normVal.Equals("ja", StringComparison.OrdinalIgnoreCase) ||
                normVal.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                normVal.Equals("oui", StringComparison.OrdinalIgnoreCase) ||
                normVal.Equals("si", StringComparison.OrdinalIgnoreCase)) {
                normVal = "yes";
            } else if (normVal.Equals("nein", StringComparison.OrdinalIgnoreCase) ||
                       normVal.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                       normVal.Equals("non", StringComparison.OrdinalIgnoreCase)) {
                normVal = "no";
            }

            for (int i = 0; i < cmb.Items.Count; i++) {
                ComboBoxItem item = cmb.Items[i] as ComboBoxItem;
                if (item != null && item.Tag != null) {
                    string tag = item.Tag.ToString();
                    if (tag.Equals(normVal, StringComparison.OrdinalIgnoreCase) || (normVal.IndexOf("Nicht gesetzt", StringComparison.OrdinalIgnoreCase) >= 0 && tag == "DELETE")) {
                        cmb.SelectedIndex = i;
                        return;
                    }
                }
            }
        }

        private void BindBcdCombo(ComboBox cmb, string paramName) {
            if (cmb == null) return;
            cmb.SelectionChanged += (s, e) => {
                if (isUpdatingUI || !isAdmin) return;
                SetBcdParam(paramName, GetSelectedTag(cmb));
                RefreshBcdUI();
            };
        }

        public void SetupBcdEvents() {
            BindBcdCombo(cmbClock, "useplatformclock");
            BindBcdCombo(cmbTick, "useplatformtick");
            BindBcdCombo(cmbTsc, "tscsyncpolicy");
            BindBcdCombo(cmbDynamic, "disabledynamictick");

            if (btnOpenBcdCmd != null) {
                btnOpenBcdCmd.Click += (s, e) => {
                    ProcessRunner.Start("cmd.exe", "/k \"color 0b && echo ================================================================= && echo                  AKTUELLE BCDEDIT-KONFIGURATION                 && echo ================================================================= && echo. && bcdedit /enum {current} && echo. && echo =================================================================\"");
                };
            }

            if (btnResetBcd != null) {
                btnResetBcd.Click += (s, e) => {
                    if (!isAdmin) return;
                    try {
                        SetBcdParam("useplatformclock", "DELETE");
                        SetBcdParam("useplatformtick", "DELETE");
                        SetBcdParam("tscsyncpolicy", "DELETE");
                        SetBcdParam("disabledynamictick", "DELETE");
                        RefreshBcdUI();
                    } catch {}
                };
            }
        }
    }
}
