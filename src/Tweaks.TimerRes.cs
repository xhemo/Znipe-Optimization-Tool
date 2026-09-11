using System;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Collections.Generic;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Logik für den Timer Resolution & MeasureSleep Reiter nach dem KISS-Prinzip.
    /// </summary>
    public partial class MainWindowLogic {
        private const string KernelRegSubKey = @"SYSTEM\CurrentControlSet\Control\Session Manager\kernel";
        private const string KernelValueName = "GlobalTimerResolutionRequests";

        // =========================================================================
        // NATIVE KERNEL TIMER REGISTRY (GlobalTimerResolutionRequests)
        // =========================================================================
        private void RefreshKernelUI() {
            updateUIRefCount++;
            try {
                bool isOptimized = (RegistryHelper.GetDword(RegistryHive.LocalMachine, KernelRegSubKey, KernelValueName) ?? 0) == 1;
                if (toggleKernelTimer != null) toggleKernelTimer.IsChecked = isOptimized;
                if (txtKernelSubtitle != null) {
                    txtKernelSubtitle.Text = isOptimized ? "Status: Optimiert (1 - 0.5ms Global)" : "Status: Standard (0 - Inaktiv)";
                    txtKernelSubtitle.Foreground = isOptimized ? UIHelper.BrushGreenText : UIHelper.BrushRedText;
                }
                UIHelper.SetPill(pillKernel, txtPillKernel, isOptimized, "AKTIV (1)", "STANDARD (0)");
            } finally {
                updateUIRefCount--;
                UpdateSidebarActivitySpinners();
            }
        }

        // =========================================================================
        // NATIVE HIGH-PRECISION TIMER RESOLUTION (NTDLL) — Messkern in TimerPrecisionProbe
        // =========================================================================
        public void RefreshTimerResUI() {
            if (window != null && !window.Dispatcher.CheckAccess()) {
                window.Dispatcher.BeginInvoke(new Action(RefreshTimerResUI));
                return;
            }
            double cur, min, max;
            TimerPrecisionProbe.GetResolutions(out cur, out min, out max);

            if (txtTimerResCurrent != null) {
                txtTimerResCurrent.Text = cur.ToString("0.000") + " ms";
                if (cur < 1.0000) {
                    txtTimerResCurrent.Foreground = UIHelper.BrushGreenText;
                } else if (cur <= 1.10) {
                    txtTimerResCurrent.Foreground = UIHelper.BrushAmberText;
                } else {
                    txtTimerResCurrent.Foreground = UIHelper.BrushRedText;
                }
            }
        }

        // =========================================================================
        // MEASURESLEEP LIVE ANALYSIS ENGINE (1s INTERVAL, DEDICATED NATIVE THREAD)
        // =========================================================================
        private Thread measureSleepThread = null;
        private volatile bool isMeasureSleepThreadRunning = false;

        private void StartMeasureSleep() {
            StopMeasureSleep();
            if (!isTimerResTabActive || isMeasureSleepPaused) return;

            isMeasureSleepThreadRunning = true;
            UpdateSidebarActivitySpinners();

            measureSleepThread = new Thread(() => {
                while (isMeasureSleepThreadRunning && isTimerResTabActive && !isMeasureSleepPaused) {
                    double resMs, sleptMs, delta;
                    TimerPrecisionProbe.MeasureSingleSleep(out resMs, out sleptMs, out delta);

                    if (!isMeasureSleepThreadRunning || !isTimerResTabActive || isMeasureSleepPaused) break;

                    window.Dispatcher.BeginInvoke(new Action(() => {
                        UpdateMeasureSleepUI(resMs, sleptMs, delta);
                    }));

                    // 1000ms Native Sleep wie MeasureSleep.exe:
                    // Schlaeft ueber kernel32!Sleep und wacht exakt auf dem Timer-Interrupt-Tick auf.
                    // 10x 100ms Abschnitte, damit Stop/Pause ohne Verzoegerung reagieren.
                    for (int i = 0; i < 10 && isMeasureSleepThreadRunning && isTimerResTabActive && !isMeasureSleepPaused; i++) {
                        TimerPrecisionProbe.NativeSleep(100);
                    }
                }
            }) {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "MeasureSleepNativeThread"
            };
            measureSleepThread.Start();
        }

        private void StopMeasureSleep() {
            try {
                isMeasureSleepThreadRunning = false;
                if (measureSleepThread != null) {
                    var th = measureSleepThread;
                    measureSleepThread = null;
                    try { if (th.IsAlive) th.Join(250); } catch {}
                    UpdateSidebarActivitySpinners();
                }
            } catch {}
        }

        private void UpdateMeasureSleepUI(double resMs, double sleptMs, double delta) {
            try {
                if (!isTimerResTabActive || isMeasureSleepPaused) return;

                string line = string.Format(CultureInfo.InvariantCulture, "Resolution: {0:F4}ms, Sleep(1) slept {1:F4}ms (delta: {2:F4})\r\n", resMs, sleptMs, delta);

                if (txtMeasureSleepLog != null) {
                    if (txtMeasureSleepLog.Text.Length > 25000) {
                        int firstBreak = txtMeasureSleepLog.Text.IndexOf('\n', 5000);
                        if (firstBreak > 0) txtMeasureSleepLog.Text = txtMeasureSleepLog.Text.Substring(firstBreak + 1);
                    }
                    txtMeasureSleepLog.AppendText(line);
                    if (scrollMeasureSleep != null) scrollMeasureSleep.ScrollToEnd();
                }

                if (lastJitterTimestamp != DateTime.MinValue) {
                    double dt = (DateTime.UtcNow - lastJitterTimestamp).TotalMilliseconds;
                    if (dt >= 400 && dt <= 3000) {
                        _measuredJitterIntervalMs = (_measuredJitterIntervalMs * 0.7) + (dt * 0.3);
                    }
                }
                double rawDelta = delta;
                jitterHistory.Add(rawDelta);
                if (jitterHistory.Count > 40) jitterHistory.RemoveAt(0);
                lastJitterTimestamp = DateTime.UtcNow;
                UpdateJitterGraph(rawDelta);

                bool isSubMsOptimal = (resMs < 1.0000 && sleptMs <= 2.5 && delta < 2.0);
                bool isStandardOneMs = (!isSubMsOptimal && resMs <= 1.0500 && sleptMs <= 2.5);

                if (txtTimerResCurrent != null) {
                    txtTimerResCurrent.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4} ms", resMs);
                    txtTimerResCurrent.Foreground = isSubMsOptimal ? UIHelper.BrushGreenText : (isStandardOneMs ? UIHelper.BrushAmberText : UIHelper.BrushRedText);
                }

                if (txtMeasureSleepLastSlept != null) {
                    txtMeasureSleepLastSlept.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4} ms", sleptMs);
                    txtMeasureSleepLastSlept.Foreground = isSubMsOptimal ? UIHelper.BrushGreenText : (isStandardOneMs ? UIHelper.BrushAmberText : UIHelper.BrushRedText);
                }

                if (txtMeasureSleepDelta != null) {
                    txtMeasureSleepDelta.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4} ms", delta);
                    txtMeasureSleepDelta.Foreground = isSubMsOptimal ? UIHelper.BrushGreenText : (isStandardOneMs ? UIHelper.BrushAmberText : UIHelper.BrushRedText);
                }

                Brush sleepBoxBg = (isSubMsOptimal || isStandardOneMs) ? UIHelper.GetBrush("#0E1118") : UIHelper.GetBrush("#2A141A");
                Brush sleepBoxBorder = (isSubMsOptimal || isStandardOneMs) ? UIHelper.GetBrush("#1B202C") : UIHelper.BrushRedBorder;
                if (borderMeasureSleepLastSlept != null) {
                    borderMeasureSleepLastSlept.Background = sleepBoxBg;
                    borderMeasureSleepLastSlept.BorderBrush = sleepBoxBorder;
                }
                if (borderMeasureSleepDelta != null) {
                    borderMeasureSleepDelta.Background = sleepBoxBg;
                    borderMeasureSleepDelta.BorderBrush = sleepBoxBorder;
                }

                if (isSubMsOptimal) {
                    if (borderMeasureSleepVerdict != null) {
                        borderMeasureSleepVerdict.Background = UIHelper.BrushGreenBg;
                        borderMeasureSleepVerdict.BorderBrush = UIHelper.BrushGreenBorder;
                    }
                    if (txtMeasureSleepVerdictIcon != null) txtMeasureSleepVerdictIcon.Text = "✅";
                    if (txtMeasureSleepVerdictTitle != null) {
                        txtMeasureSleepVerdictTitle.Text = string.Format(CultureInfo.InvariantCulture,
                            "Optimale Sub-Millisekunde aktiv – Takt {0:F4} ms, Sleep(1) in {1:F4} ms (Jitter: {2:F4} ms)", resMs, sleptMs, delta);
                        txtMeasureSleepVerdictTitle.Foreground = UIHelper.BrushGreenText;
                    }
                    if (pillMeasureSleepVerdict != null) pillMeasureSleepVerdict.Background = UIHelper.GetBrush("#064E3B");
                    if (txtPillMeasureSleepVerdict != null) {
                        txtPillMeasureSleepVerdict.Text = string.Format(CultureInfo.InvariantCulture, "OPTIMAL ({0:F4} ms)", resMs);
                        txtPillMeasureSleepVerdict.Foreground = UIHelper.GetBrush("#6EE7B7");
                    }
                } else if (isStandardOneMs) {
                    if (borderMeasureSleepVerdict != null) {
                        borderMeasureSleepVerdict.Background = UIHelper.BrushAmberBg;
                        borderMeasureSleepVerdict.BorderBrush = UIHelper.BrushAmberBorder;
                    }
                    if (txtMeasureSleepVerdictIcon != null) txtMeasureSleepVerdictIcon.Text = "⚠️";
                    if (txtMeasureSleepVerdictTitle != null) {
                        txtMeasureSleepVerdictTitle.Text = string.Format(CultureInfo.InvariantCulture,
                            "Standard-Takt aktiv: {0:F4} ms – SetTimerResolution nicht aktiv (erst unter 1 ms optimal)", resMs);
                        txtMeasureSleepVerdictTitle.Foreground = UIHelper.BrushAmberText;
                    }
                    if (pillMeasureSleepVerdict != null) pillMeasureSleepVerdict.Background = UIHelper.GetBrush("#451A03");
                    if (txtPillMeasureSleepVerdict != null) {
                        txtPillMeasureSleepVerdict.Text = string.Format(CultureInfo.InvariantCulture, "STANDARD ({0:F1} ms)", resMs);
                        txtPillMeasureSleepVerdict.Foreground = UIHelper.GetBrush("#FDE68A");
                    }
                } else {
                    if (borderMeasureSleepVerdict != null) {
                        borderMeasureSleepVerdict.Background = UIHelper.GetBrush("#2A141A");
                        borderMeasureSleepVerdict.BorderBrush = UIHelper.BrushRedBorder;
                    }
                    if (txtMeasureSleepVerdictIcon != null) txtMeasureSleepVerdictIcon.Text = "❌";
                    if (txtMeasureSleepVerdictTitle != null) {
                        txtMeasureSleepVerdictTitle.Text = string.Format(CultureInfo.InvariantCulture,
                            "Windows-Drosselung aktiv: Sleep(1) dauert {0:F2} ms statt < 1 ms (15.6ms Drosselung)", sleptMs);
                        txtMeasureSleepVerdictTitle.Foreground = UIHelper.BrushRedText;
                    }
                    if (pillMeasureSleepVerdict != null) pillMeasureSleepVerdict.Background = UIHelper.GetBrush("#450A0A");
                    if (txtPillMeasureSleepVerdict != null) {
                        txtPillMeasureSleepVerdict.Text = string.Format(CultureInfo.InvariantCulture, "GEDROSSELT ({0:F1} ms)", sleptMs);
                        txtPillMeasureSleepVerdict.Foreground = UIHelper.GetBrush("#FCA5A5");
                    }
                }
            } catch {}
        }

        private void UpdateJitterGraph(double currentDelta) {
            if (canvasJitterGraph == null || jitterHistory == null || jitterHistory.Count == 0) return;
            try {
                double minVal = double.MaxValue;
                double maxVal = double.MinValue;
                double maxAbs = 0.05;
                double sum = 0.0;
                for (int i = 0; i < jitterHistory.Count; i++) {
                    double v = jitterHistory[i];
                    if (v < minVal) minVal = v;
                    if (v > maxVal) maxVal = v;
                    double abs = Math.Abs(v);
                    if (abs > maxAbs) maxAbs = abs;
                    sum += v;
                }
                double avgVal = sum / jitterHistory.Count;

                double absDelta = Math.Abs(currentDelta);
                Brush jitterBrush = absDelta <= 0.10 ? UIHelper.BrushGreenText : (absDelta <= 0.25 ? UIHelper.BrushBlueText : UIHelper.BrushAmberText);

                if (txtJitterLiveVal != null) {
                    string sign = currentDelta > 0 ? "+" : "";
                    txtJitterLiveVal.Text = string.Format(CultureInfo.InvariantCulture, "{0}{1:F4} ms", sign, currentDelta);
                    txtJitterLiveVal.Foreground = jitterBrush;
                }

                if (txtJitterMin != null) txtJitterMin.Text = string.Format(CultureInfo.InvariantCulture, "{0:F3} ms", minVal);
                if (txtJitterAvg != null) txtJitterAvg.Text = string.Format(CultureInfo.InvariantCulture, "{0:F3} ms", avgVal);
                if (txtJitterMax != null) txtJitterMax.Text = string.Format(CultureInfo.InvariantCulture, "{0:F3} ms", maxVal);

                if (pathJitter != null) {
                    pathJitter.Stroke = UIHelper.BrushWhite;
                }

                UpdateSmoothJitterGraph();
            } catch {}
        }

        private void UpdateSmoothJitterGraph() {
            if (canvasJitterGraph == null || pathJitter == null) return;
            try {
                if (txtJitterYMax != null) txtJitterYMax.Text = "+1.000 ms";
                if (txtJitterYMid != null) txtJitterYMid.Text = "0.000 ms";
                if (txtJitterYMin != null) txtJitterYMin.Text = "-1.000 ms";

                double w = canvasJitterGraph.ActualWidth > 50 ? canvasJitterGraph.ActualWidth : 350.0;
                double h = canvasJitterGraph.ActualHeight > 20 ? canvasJitterGraph.ActualHeight : 145.0;

                double targetVal = 0.100;
                double scale = 1.0;
                double targetNorm = (targetVal - (-scale)) / (2.0 * scale);
                double targetY = Math.Round(h - 4.0 - (targetNorm * (h - 8.0)));

                if (lineJitterTarget != null) {
                    lineJitterTarget.X1 = 0;
                    lineJitterTarget.X2 = w;
                    lineJitterTarget.Y1 = targetY;
                    lineJitterTarget.Y2 = targetY;
                    lineJitterTarget.Visibility = Visibility.Visible;
                }
                if (txtJitterTargetLabel != null) {
                    Canvas.SetLeft(txtJitterTargetLabel, 6);
                    Canvas.SetTop(txtJitterTargetLabel, targetY - 13);
                    txtJitterTargetLabel.Text = "── < 0.100 ms Ziel";
                    txtJitterTargetLabel.Visibility = Visibility.Visible;
                }

                if (jitterHistory == null || jitterHistory.Count == 0) {
                    pathJitter.Data = null;
                    return;
                }

                int maxSamples = 40;
                double stepX = w / (maxSamples - 1);

                double progress = 0.0;
                double shiftX = 0.0;
                if (lastJitterTimestamp != DateTime.MinValue) {
                    double elapsedMs = (DateTime.UtcNow - lastJitterTimestamp).TotalMilliseconds;
                    double interval = Math.Max(400.0, _measuredJitterIntervalMs);
                    progress = Math.Min(1.0, Math.Max(0.0, elapsedMs / interval));
                    shiftX = progress * stepX;
                }

                pathJitter.Data = CreateSmoothSplineGeometry(jitterHistory, maxSamples, stepX, shiftX, progress, -scale, 2.0 * scale, h, w);

                if (isHoverJitter) {
                    UpdateJitterHover(lastPosJitter);
                }
            } catch {}
        }

        public void ClearTimerResGraph() {
            if (window != null && !window.Dispatcher.CheckAccess()) {
                window.Dispatcher.BeginInvoke(new Action(ClearTimerResGraph));
                return;
            }
            if (txtMeasureSleepLog != null) txtMeasureSleepLog.Text = "";
            jitterHistory.Clear();
            lastJitterTimestamp = DateTime.MinValue;
            _measuredJitterIntervalMs = 1000.0;
            if (txtJitterYMax != null) txtJitterYMax.Text = "+1.000 ms";
            if (txtJitterYMid != null) txtJitterYMid.Text = "0.000 ms";
            if (txtJitterYMin != null) txtJitterYMin.Text = "-1.000 ms";
            if (pathJitter != null) pathJitter.Data = null;
            if (txtJitterLiveVal != null) {
                txtJitterLiveVal.Text = "0.000 ms";
                txtJitterLiveVal.Foreground = UIHelper.BrushGreenText;
            }
            if (txtJitterMin != null) txtJitterMin.Text = "0.000 ms";
            if (txtJitterAvg != null) txtJitterAvg.Text = "0.000 ms";
            if (txtJitterMax != null) txtJitterMax.Text = "0.000 ms";
            HideJitterHover();
        }

        public void SetupTimerResEvents() {
            if (btnOpenKernelRegedit != null) btnOpenKernelRegedit.Click += (s, e) => { OpenRegeditAtKey(KernelRegSubKey); };

            if (toggleKernelTimer != null) {
                toggleKernelTimer.Checked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.SetDword(RegistryHive.LocalMachine, KernelRegSubKey, KernelValueName, 1);
                    RefreshKernelUI();
                };

                toggleKernelTimer.Unchecked += (s, e) => {
                    if (isUpdatingUI || !isAdmin) return;
                    RegistryHelper.DeleteValue(RegistryHive.LocalMachine, KernelRegSubKey, KernelValueName);
                    RefreshKernelUI();
                };
            }

            if (btnToggleMeasureSleep != null) {
                btnToggleMeasureSleep.Click += (s, e) => {
                    isMeasureSleepPaused = !isMeasureSleepPaused;
                    if (isMeasureSleepPaused) {
                        StopMeasureSleep();
                        btnToggleMeasureSleep.Content = "▶️ Fortsetzen";
                        UIHelper.SetPillWarning(pillMeasureSleepState, txtMeasureSleepState, "⏸️ PAUSIERT");
                    } else {
                        btnToggleMeasureSleep.Content = "⏸️ Pause";
                        UIHelper.SetPill(pillMeasureSleepState, txtMeasureSleepState, true, "🟢 LIVE (1s Intervall)");
                        StartMeasureSleep();
                    }
                };
            }

            if (btnClearMeasureSleep != null) {
                btnClearMeasureSleep.Click += (s, e) => {
                    ClearTimerResGraph();
                };
            }

            SetupTimerBenchmarkControls();
        }

        // =========================================================================
        // TIMER RESOLUTION SWEET-SPOT BENCHMARK (SWIFTYPOP ALGORITHMUS)
        // =========================================================================
        private Button btnStartTimerBenchmark;
        private string lastBenchEstimatedTimeStr = "";
        private TextBox txtBenchStart;
        private TextBox txtBenchStep;
        private TextBox txtBenchEnd;
        private TextBox txtBenchSamples;
        private TextBox txtBenchPasses;
        private TextBlock txtBenchCalculatedIterations;
        private TextBlock txtBenchEstimatedTime;
        private TextBlock txtBenchStatus;
        private TextBlock txtBenchProgressPercent;
        private ProgressBar progressBench;
        private Border borderBenchResult;
        private TextBlock txtBenchOptimalRes;
        private TextBlock txtBenchOptimalStats;
        private Border borderBenchTop1;
        private Border borderBenchTop2;
        private Border borderBenchTop3;
        private TextBlock txtBenchTop1Res;
        private TextBlock txtBenchTop2Res;
        private TextBlock txtBenchTop3Res;
        private Button btnApplyTop1;
        private Button btnApplyTop2;
        private Button btnApplyTop3;
        private double top1ResMs = 0.5000;
        private double top2ResMs = 0.5000;
        private double top3ResMs = 0.5000;
        private ScrollViewer scrollBenchResults;
        private TextBox txtBenchResultsLog;
        private TextBox txtManualTimerRes;
        private Button btnApplyManualTimer;

        private class TimerStepAccumulator {
            public int HwTicks;
            public double HardwareMs;
            public double FirstRequestedMs;
            public int TotalSamples;
            public int TotalSpikes;
            public double SumStdev;
            public double SumDelta;
            public double SumP99;
            public double MaxPeakDelta;
            public int MeasurementCount;
            public double HarmonieScore;
        }

        private Thread benchmarkThread = null;
        private volatile bool isBenchmarkRunning = false;
        private volatile bool isBenchmarkCancelled = false;
        private double lastOptimalResMs = 0.5000;
        private static int benchmarkSessionRunCount = 0;

        private void SetupTimerBenchmarkControls() {
            btnStartTimerBenchmark        = (Button)window.FindName("BtnStartTimerBenchmark");
            txtBenchStart                 = (TextBox)window.FindName("TxtBenchStart");
            txtBenchStep                  = (TextBox)window.FindName("TxtBenchStep");
            txtBenchEnd                   = (TextBox)window.FindName("TxtBenchEnd");
            txtBenchSamples               = (TextBox)window.FindName("TxtBenchSamples");
            txtBenchPasses                = (TextBox)window.FindName("TxtBenchPasses");
            txtBenchCalculatedIterations  = (TextBlock)window.FindName("TxtBenchCalculatedIterations");
            txtBenchEstimatedTime         = (TextBlock)window.FindName("TxtBenchEstimatedTime");
            txtBenchStatus                = (TextBlock)window.FindName("TxtBenchStatus");
            txtBenchProgressPercent       = (TextBlock)window.FindName("TxtBenchProgressPercent");
            progressBench                 = (ProgressBar)window.FindName("ProgressBench");
            borderBenchResult             = (Border)window.FindName("BorderBenchResult");
            txtBenchOptimalRes            = (TextBlock)window.FindName("TxtBenchOptimalRes");
            txtBenchOptimalStats          = (TextBlock)window.FindName("TxtBenchOptimalStats");
            borderBenchTop1               = (Border)window.FindName("BorderBenchTop1");
            borderBenchTop2               = (Border)window.FindName("BorderBenchTop2");
            borderBenchTop3               = (Border)window.FindName("BorderBenchTop3");
            txtBenchTop1Res               = (TextBlock)window.FindName("TxtBenchTop1Res");
            txtBenchTop2Res               = (TextBlock)window.FindName("TxtBenchTop2Res");
            txtBenchTop3Res               = (TextBlock)window.FindName("TxtBenchTop3Res");
            btnApplyTop1                  = (Button)window.FindName("BtnApplyTop1");
            btnApplyTop2                  = (Button)window.FindName("BtnApplyTop2");
            btnApplyTop3                  = (Button)window.FindName("BtnApplyTop3");

            if (btnApplyTop1 != null) btnApplyTop1.Click += (s, e) => ApplyTopRank(btnApplyTop1, top1ResMs, "⭐ Top 1");
            if (btnApplyTop2 != null) btnApplyTop2.Click += (s, e) => ApplyTopRank(btnApplyTop2, top2ResMs, "Top 2");
            if (btnApplyTop3 != null) btnApplyTop3.Click += (s, e) => ApplyTopRank(btnApplyTop3, top3ResMs, "Top 3");
            scrollBenchResults            = (ScrollViewer)window.FindName("ScrollBenchResults");
            txtBenchResultsLog            = (TextBox)window.FindName("TxtBenchResultsLog");
            txtManualTimerRes             = (TextBox)window.FindName("TxtManualTimerRes");
            btnApplyManualTimer           = (Button)window.FindName("BtnApplyManualTimer");

            Action recalculate = () => UpdateBenchCalculatedEstimates();

            if (txtBenchStart != null) txtBenchStart.TextChanged += (s, e) => recalculate();
            if (txtBenchStep != null) txtBenchStep.TextChanged += (s, e) => recalculate();
            if (txtBenchEnd != null) txtBenchEnd.TextChanged += (s, e) => recalculate();
            if (txtBenchSamples != null) txtBenchSamples.TextChanged += (s, e) => recalculate();
            if (txtBenchPasses != null) txtBenchPasses.TextChanged += (s, e) => recalculate();

            if (btnApplyManualTimer != null) {
                btnApplyManualTimer.Click += (s, e) => {
                    double val = ParseDouble(txtManualTimerRes != null ? txtManualTimerRes.Text : "0.5100", 0.5100);
                    if (val < 0.5000) val = 0.5000;
                    if (val > 15.625) val = 15.625;
                    TimerPrecisionProbe.ResetResolution();
                    bool daemonApplied = ApplyTimerResolutionToSystem(val, updateShortcut: true);
                    if (btnApplyManualTimer != null) {
                        btnApplyManualTimer.Content = "✓ Gesetzt";
                        var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                        t.Tick += (ts, te) => {
                            btnApplyManualTimer.Content = "⚡ Setzen";
                            t.Stop();
                        };
                        t.Start();
                    }
                    if (txtBenchStatus != null) {
                        int tVal = (int)Math.Round(val * 10000.0);
                        txtBenchStatus.Text = daemonApplied
                            ? string.Format(CultureInfo.InvariantCulture, "Manuell: SetTimerResolution mit --resolution {0} neu gestartet & Autostart aktualisiert!", tVal)
                            : string.Format(CultureInfo.InvariantCulture, "Manuell: Takt {0:F4} ms aktiv!", val);
                    }
                };
            }

            recalculate();

            if (btnStartTimerBenchmark != null) {
                btnStartTimerBenchmark.Click += (s, e) => {
                    if (isBenchmarkRunning) {
                        CancelTimerBenchmark();
                    } else {
                        StartTimerBenchmark();
                    }
                };
            }

        }

        private void ApplyTopRank(Button btn, double resMs, string topLabel) {
            bool daemonApplied = ApplyTimerResolutionToSystem(resMs, updateShortcut: true);
            if (btn != null) {
                btn.Content = "✓ Aktiv";
                var t = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
                t.Tick += (ts, te) => {
                    btn.Content = "Setzen";
                    t.Stop();
                };
                t.Start();
            }
            if (txtBenchStatus != null) {
                int tVal = (int)Math.Round(resMs * 10000.0);
                txtBenchStatus.Text = daemonApplied
                    ? string.Format(CultureInfo.InvariantCulture, "{0} ({1:F4} ms): SetTimerResolution mit --resolution {2} neu gestartet & Autostart aktualisiert!", topLabel, resMs, tVal)
                    : string.Format(CultureInfo.InvariantCulture, "{0} ({1:F4} ms): System-Takt aktiv!", topLabel, resMs);
            }
        }

        private void UpdateBenchCalculatedEstimates() {
            try {
                double start = ParseDouble(txtBenchStart != null ? txtBenchStart.Text : "0.5000", 0.5000);
                double end = ParseDouble(txtBenchEnd != null ? txtBenchEnd.Text : "0.5300", 0.5300);
                double step = ParseDouble(txtBenchStep != null ? txtBenchStep.Text : "0.0050", 0.0050);
                int samples = ParseInt(txtBenchSamples != null ? txtBenchSamples.Text : "450", 450);
                int passes = ParseInt(txtBenchPasses != null ? txtBenchPasses.Text : "3", 3);

                if (step <= 0.00001) step = 0.0050;
                if (end < start) end = start;
                if (samples < 2) samples = 2;
                if (passes < 1) passes = 1;

                int iterations = (int)Math.Round((end - start) / step) + 1;
                if (iterations < 1) iterations = 1;

                int totalRuns = iterations * passes;
                // ~101ms pro Sample (1ms Sleep + 100ms Pause wie MeasureSleep.cpp) + ~25ms Settling pro Schritt
                double totalSec = totalRuns * ((samples * 0.101) + 0.025);

                string estStr = FormatCompactDuration(totalSec, prefixTilde: true);
                lastBenchEstimatedTimeStr = estStr;

                if (txtBenchCalculatedIterations != null) {
                    txtBenchCalculatedIterations.Text = passes > 1
                        ? string.Format(CultureInfo.InvariantCulture, "{0} Schritte × {1} Durchläufe ({2} Runs)", iterations, passes, totalRuns)
                        : string.Format(CultureInfo.InvariantCulture, "{0} Schritte", iterations);
                }

                if (!isBenchmarkRunning && btnStartTimerBenchmark != null) {
                    btnStartTimerBenchmark.Content = string.Format(CultureInfo.InvariantCulture, "🚀 Benchmark starten ({0})", estStr);
                    var successStyle = (Application.Current.TryFindResource("SuccessBtn") ?? window.TryFindResource("SuccessBtn")) as Style;
                    if (successStyle != null) btnStartTimerBenchmark.Style = successStyle;
                }
            } catch {}
        }

        private static string FormatCompactDuration(double totalSeconds, bool prefixTilde = false) {
            if (totalSeconds < 0) totalSeconds = 0;
            int s = (int)Math.Round(totalSeconds);
            int h = s / 3600;
            int m = (s % 3600) / 60;
            int sec = s % 60;

            string prefix = prefixTilde ? "~" : "";
            if (h > 0) {
                return string.Format(CultureInfo.InvariantCulture, "{0}{1}h {2:D2}m {3:D2}s", prefix, h, m, sec);
            }
            if (m > 0) {
                return string.Format(CultureInfo.InvariantCulture, "{0}{1}m {2:D2}s", prefix, m, sec);
            }
            return string.Format(CultureInfo.InvariantCulture, "{0}{1}s", prefix, sec);
        }

        private static double ParseDouble(string text, double fallback) {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            double val;
            text = text.Trim().Replace(',', '.');
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out val)) {
                return val;
            }
            return fallback;
        }

        private static int ParseInt(string text, int fallback) {
            if (string.IsNullOrWhiteSpace(text)) return fallback;
            int val;
            if (int.TryParse(text.Trim(), out val)) {
                return val;
            }
            return fallback;
        }

        private void StartTimerBenchmark() {
            if (isBenchmarkRunning) return;

            double startMs = ParseDouble(txtBenchStart != null ? txtBenchStart.Text : "0.5000", 0.5000);
            double endMs = ParseDouble(txtBenchEnd != null ? txtBenchEnd.Text : "0.5300", 0.5300);
            double stepMs = ParseDouble(txtBenchStep != null ? txtBenchStep.Text : "0.0050", 0.0050);
            int samples = ParseInt(txtBenchSamples != null ? txtBenchSamples.Text : "450", 450);
            int passes = ParseInt(txtBenchPasses != null ? txtBenchPasses.Text : "3", 3);

            if (stepMs <= 0.00001) stepMs = 0.0050;
            if (endMs < startMs) endMs = startMs;
            if (samples < 2) samples = 2;
            if (passes < 1) passes = 1;

            int stepsPerPass = (int)Math.Round((endMs - startMs) / stepMs) + 1;
            if (stepsPerPass < 1) stepsPerPass = 1;
            int totalRuns = stepsPerPass * passes;

            isBenchmarkRunning = true;
            isBenchmarkCancelled = false;

            if (btnStartTimerBenchmark != null) {
                btnStartTimerBenchmark.Content = "⏹️ Abbrechen";
                var dangerStyle = (Application.Current.TryFindResource("PrimaryBtn") ?? window.TryFindResource("PrimaryBtn")) as Style;
                if (dangerStyle != null) btnStartTimerBenchmark.Style = dangerStyle;
            }
            if (borderBenchResult != null) borderBenchResult.Visibility = Visibility.Collapsed;
            if (progressBench != null) progressBench.Value = 0;
            if (txtBenchProgressPercent != null) txtBenchProgressPercent.Text = "0%";
            if (txtBenchStatus != null) txtBenchStatus.Text = "Benchmark läuft...";

            benchmarkSessionRunCount++;
            string logPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TimerBenchmark.log");
            if (benchmarkSessionRunCount == 1) {
                try {
                    if (System.IO.File.Exists(logPath)) {
                        System.IO.File.Delete(logPath);
                    }
                } catch {}
            }

            string div = new string('=', 136);
            string subDiv = new string('-', 136);
            string runHeader = string.Format(CultureInfo.InvariantCulture,
                (benchmarkSessionRunCount > 1 ? "\r\n\r\n" : "") +
                "{0}\r\n" +
                "🚀 TIMER RESOLUTION SWEET-SPOT BENCHMARK — RUN #{1} (SESSION-LOG)\r\n" +
                "Bereich: {2:F4} ms bis {3:F4} ms (Schritt: {4:F4} ms, {5} Samples/Schritt, {6} Durchläufe, {7} Runs)\r\n" +
                "Startzeit: {8}\r\n" +
                "Kriterium: Multi-Dimensionale Harmonie (50% Stutter/Spikes, 25% Jitter, 15% Latenz, 10% P99 Tail)\r\n" +
                "{9}\r\n" +
                "Zeit        Soll-Wert       Hardware-Takt         Mittelwert Δ    STDEV (Jitter)  P99 (Tail)      Max Peak        Status\r\n" +
                "{9}\r\n",
                div, benchmarkSessionRunCount, startMs, endMs, stepMs, samples, passes, totalRuns,
                DateTime.Now.ToString("HH:mm:ss", CultureInfo.InvariantCulture), subDiv);

            if (txtBenchResultsLog != null) {
                if (benchmarkSessionRunCount == 1) {
                    txtBenchResultsLog.Text = runHeader;
                } else {
                    txtBenchResultsLog.AppendText(runHeader);
                    if (scrollBenchResults != null) scrollBenchResults.ScrollToEnd();
                }
            }

            try {
                if (benchmarkSessionRunCount == 1) {
                    System.IO.File.WriteAllText(logPath, runHeader);
                } else {
                    System.IO.File.AppendAllText(logPath, runHeader);
                }
            } catch {}

            // 0. Vorherigen Zustand sichern, um ihn nach Benchmark-Ende oder Abbruch exakt wiederherzustellen
            double preBenchCur, preBenchMin, preBenchMax;
            TimerPrecisionProbe.GetResolutions(out preBenchCur, out preBenchMin, out preBenchMax);
            double preBenchmarkResMs = preBenchCur > 0 ? preBenchCur : 0.5100;
            bool hadSetTimerDaemon = Process.GetProcessesByName("SetTimerResolution").Length > 0;

            // 1. Live-Monitor temporär anhalten und alle UI-Timer stoppen für 100% Entkopplung
            StopMeasureSleep();
            if (telemetryTimer != null) telemetryTimer.Stop();
            if (smoothGraphTimer != null) smoothGraphTimer.Stop();
            if (sidebarActivityTimer != null) sidebarActivityTimer.Stop();

            // 2. Laufende SetTimerResolution-Prozesse rigoros via taskkill und Process.Kill beenden
            try {
                var psi = new ProcessStartInfo("taskkill", "/F /IM SetTimerResolution.exe") {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process pKill = Process.Start(psi);
                if (pKill != null) pKill.WaitForExit(1000);
            } catch {}
            try {
                foreach (var proc in Process.GetProcessesByName("SetTimerResolution")) {
                    try {
                        proc.Kill();
                        proc.WaitForExit(1000);
                    } catch {}
                }
            } catch {}

            TimerPrecisionProbe.ResetResolution();
            TimerPrecisionProbe.NativeSleep(50);

            // 3. Prüfen, ob eine externe App oder ein nicht-beendbarer Dienst den Timer festhält
            double baselineCur, baselineMin, baselineMax;
            TimerPrecisionProbe.GetResolutions(out baselineCur, out baselineMin, out baselineMax);
            if (baselineCur < 1.0000 && baselineCur < (startMs - 0.0001)) {
                if (txtBenchResultsLog != null) {
                    txtBenchResultsLog.AppendText(string.Format(CultureInfo.InvariantCulture,
                        "⚠️ WARNUNG: Hintergrund-Timer ist noch auf {0:F4} ms gesperrt (z. B. durch externen Dienst oder Browser)!\r\n" +
                        "Werte über {0:F4} ms können von Windows nicht sauber geschaltet werden.\r\n\r\n", baselineCur));
                }
            }

            // 4. Garbage Collection forciert durchführen, damit während der Messung keine GC-Pausen auftreten
            try {
                GC.Collect(2, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();
            } catch {}

            benchmarkThread = new Thread(() => {
                var hwClusterMap = new SortedDictionary<int, TimerStepAccumulator>();
                var sw = Stopwatch.StartNew();

                ProcessPriorityClass prevClass = ProcessPriorityClass.Normal;
                try {
                    prevClass = Process.GetCurrentProcess().PriorityClass;
                    Process.GetCurrentProcess().PriorityClass = ProcessPriorityClass.RealTime;
                } catch {}

                string logBase = "";
                window.Dispatcher.Invoke(new Action(() => {
                    logBase = txtBenchResultsLog != null ? txtBenchResultsLog.Text : "";
                }));

                try {
                    int currentRun = 0;
                    int totalBenchmarkSamples = totalRuns * samples;

                    for (int p = 1; p <= passes; p++) {
                        if (isBenchmarkCancelled) break;
                        int passNum = p;

                        if (passes > 1) {
                            string passHeader = string.Format(CultureInfo.InvariantCulture,
                                "\r\n--- DURCHLAUF {0} VON {1} ---\r\n", passNum, passes);
                            logBase += passHeader;
                            string snapshot = logBase;
                            window.Dispatcher.BeginInvoke(new Action(() => {
                                if (txtBenchResultsLog != null) {
                                    txtBenchResultsLog.Text = snapshot;
                                    if (scrollBenchResults != null) scrollBenchResults.ScrollToEnd();
                                }
                            }));
                            try {
                                System.IO.File.AppendAllText(logPath, passHeader);
                            } catch {}
                        }

                        for (double cur = startMs; cur <= endMs + (stepMs * 0.1); cur += stepMs) {
                            if (isBenchmarkCancelled) break;
                            currentRun++;
                            int run = currentRun;
                            double currentRes = cur;

                            // 1. Takt in-place setzen (ohne Reset-Churn) und echte Hardware-Auflösung abfragen
                            double grantedMs;
                            TimerPrecisionProbe.SetResolution(cur, out grantedMs);

                            // 2. Kernel-Takt stabilisieren
                            TimerPrecisionProbe.NativeSleep(20);

                            double curHw, minHw, maxHw;
                            TimerPrecisionProbe.GetResolutions(out curHw, out minHw, out maxHw);
                            double stepHwMs = (curHw > 0 && curHw <= (endMs + 0.0060)) ? curHw : (grantedMs > 0 ? grantedMs : cur);
                            int stepHwTicks = (int)Math.Round(stepHwMs * 10000.0);

                            TimeSpan stepStartElapsed = sw.Elapsed;
                            string timeStamp = string.Format(CultureInfo.InvariantCulture, "[{0:D2}:{1:D2}:{2:D2}]", stepStartElapsed.Hours, stepStartElapsed.Minutes, stepStartElapsed.Seconds);

                            Action<int, int, int> onProgress = (curSample, totSamples, curSpikes) => {
                                int completedTotalSamples = (run - 1) * totSamples + curSample;
                                double overallPct = Math.Min(100.0, (double)completedTotalSamples / totalBenchmarkSamples * 100.0);

                                long elMs = sw.ElapsedMilliseconds;
                                double msPerSample = completedTotalSamples > 0 ? (double)elMs / completedTotalSamples : 101.0;
                                int remSamples = Math.Max(0, totalBenchmarkSamples - completedTotalSamples);
                                double remSec = (remSamples * msPerSample) / 1000.0;
                                string remStr = FormatCompactDuration(remSec);

                                string spikeStr = curSpikes > 0 ? string.Format(CultureInfo.InvariantCulture, " | ⚠️ {0} Spike{1}", curSpikes, curSpikes > 1 ? "s" : "") : "";
                                string inProgressLine = string.Format(CultureInfo.InvariantCulture,
                                    "{0}  {1,6:F4} ms  -->  {2,6:F4} ms ({3,5})     ⏳ Sampling: {4}/{5} ({6:F0}%){7}\r\n",
                                    timeStamp, currentRes, stepHwMs, stepHwTicks, curSample, totSamples, (double)curSample / totSamples * 100.0, spikeStr);

                                string passTag = passes > 1 ? string.Format(CultureInfo.InvariantCulture, "Durchlauf {0}/{1} | ", passNum, passes) : "";
                                string statusStr = string.Format(CultureInfo.InvariantCulture,
                                    "{0}{1} {2}/{3} (noch ~{4}) — Soll: {5:F4} ms | HW: {6:F4} ms | Sampling: {7}/{8} ({9:F0}%){10}",
                                    passTag, timeStamp, run, totalRuns, remStr, currentRes, stepHwMs, curSample, totSamples, (double)curSample / totSamples * 100.0,
                                    curSpikes == 0 ? " | 0 Spikes" : string.Format(CultureInfo.InvariantCulture, " | {0} Spikes!", curSpikes));

                                string logSnapshot = logBase + inProgressLine;
                                window.Dispatcher.BeginInvoke(new Action(() => {
                                    if (progressBench != null) progressBench.Value = overallPct;
                                    if (txtBenchProgressPercent != null) txtBenchProgressPercent.Text = string.Format(CultureInfo.InvariantCulture, "{0:F0}% (~{1})", overallPct, remStr);
                                    if (txtBenchStatus != null) txtBenchStatus.Text = statusStr;
                                    if (txtBenchResultsLog != null) {
                                        txtBenchResultsLog.Text = logSnapshot;
                                        if (scrollBenchResults != null) scrollBenchResults.ScrollToEnd();
                                    }
                                }));
                            };

                            // Sofort erste Zeile anzeigen (Sample 1/N), bevor die Messung schläft!
                            onProgress(1, samples, 0);

                            // 3. Präzisions-Messung (Samples, Mid-Tick Discard, Bessel N-1, Spike-Erkennung, P99, CPU Core Pinning)
                            double resMs, avgSleep, avgDelta, stdev, maxDelta, p99;
                            int spikes;
                            TimerPrecisionProbe.MeasureSleepPrecision(out resMs, out avgSleep, out avgDelta, out stdev, out maxDelta, out p99, out spikes, samples, () => isBenchmarkCancelled, onProgress);
                            if (isBenchmarkCancelled) break;

                            double actualHwMs = (resMs > 0 && resMs <= (endMs + 0.0060)) ? resMs : (grantedMs > 0 ? grantedMs : cur);
                            int hwTicks = (int)Math.Round(actualHwMs * 10000.0);

                            TimerStepAccumulator acc;
                            if (!hwClusterMap.TryGetValue(hwTicks, out acc)) {
                                acc = new TimerStepAccumulator {
                                    HwTicks = hwTicks,
                                    HardwareMs = actualHwMs,
                                    FirstRequestedMs = cur
                                };
                                hwClusterMap[hwTicks] = acc;
                            }
                            acc.TotalSamples += samples;
                            acc.TotalSpikes += spikes;
                            acc.SumStdev += stdev;
                            acc.SumDelta += avgDelta;
                            acc.SumP99 += p99;
                            if (maxDelta > acc.MaxPeakDelta) acc.MaxPeakDelta = maxDelta;
                            acc.MeasurementCount++;

                            double stepDelta = avgDelta;
                            double stepStdev = stdev;
                            double stepP99 = p99;
                            double stepMaxDelta = maxDelta;
                            int stepSpikes = spikes;
                            double pct = Math.Min(100.0, (double)run / totalRuns * 100.0);

                            long elapsedMs = sw.ElapsedMilliseconds;
                            double avgMsPerStep = run > 0 ? (double)elapsedMs / run : 300.0;
                            int remSteps = totalRuns - run;
                            double stepRemSec = (remSteps * avgMsPerStep) / 1000.0;
                            string finalRemStr = FormatCompactDuration(stepRemSec);

                            string statusText = stepSpikes == 0 ? "✅ 0 Spikes" : string.Format(CultureInfo.InvariantCulture, "⚠️ {0} Spikes (>1.25ms)", stepSpikes);
                            string line = string.Format(CultureInfo.InvariantCulture,
                                "{0}  {1,6:F4} ms  -->  {2,6:F4} ms ({3,5})     +{4,6:F4} ms       {5,6:F4} ms      +{6,6:F4} ms      +{7,6:F4} ms      {8}\r\n",
                                timeStamp, currentRes, actualHwMs, hwTicks, stepDelta, stepStdev, stepP99, stepMaxDelta, statusText);

                            try {
                                System.IO.File.AppendAllText(logPath, line);
                            } catch {}

                            logBase += line;
                            string finalizedSnapshot = logBase;

                            window.Dispatcher.BeginInvoke(new Action(() => {
                                if (progressBench != null) progressBench.Value = pct;
                                if (txtBenchProgressPercent != null) txtBenchProgressPercent.Text = string.Format(CultureInfo.InvariantCulture, "{0:F0}% (~{1})", pct, finalRemStr);
                                if (txtBenchStatus != null) {
                                    string spikeTag = stepSpikes == 0 ? "0 Spikes" : string.Format(CultureInfo.InvariantCulture, "{0} Spikes!", stepSpikes);
                                    string passTag = passes > 1 ? string.Format(CultureInfo.InvariantCulture, "Durchlauf {0}/{1} | ", passNum, passes) : "";
                                    txtBenchStatus.Text = string.Format(CultureInfo.InvariantCulture,
                                        "{0}{1} {2}/{3} (noch ~{4}) — Soll: {5:F4} ms | HW: {6:F4} ms | STDEV: {7:F4} ms | P99: +{8:F4} ms | Peak: +{9:F4} ms | {10}",
                                        passTag, timeStamp, run, totalRuns, finalRemStr, currentRes, actualHwMs, stepStdev, stepP99, stepMaxDelta, spikeTag);
                                }

                                if (txtBenchResultsLog != null) {
                                    txtBenchResultsLog.Text = finalizedSnapshot;
                                    if (scrollBenchResults != null) scrollBenchResults.ScrollToEnd();
                                }
                            }));
                        }
                    }
                } finally {
                    try {
                        Process.GetCurrentProcess().PriorityClass = prevClass;
                    } catch {}

                    // Vorherigen Zustand wiederherstellen (Daemon mit altem Takt neu starten bzw. Takt setzen)
                    try {
                        TimerPrecisionProbe.ResetResolution();
                        if (hadSetTimerDaemon) {
                            ApplyTimerResolutionToSystem(preBenchmarkResMs, updateShortcut: false);
                        } else if (preBenchmarkResMs < 1.0000) {
                            double actual;
                            TimerPrecisionProbe.SetResolution(preBenchmarkResMs, out actual);
                        }
                    } catch {}

                    window.Dispatcher.BeginInvoke(new Action(() => {
                        isBenchmarkRunning = false;
                        if (telemetryTimer != null) telemetryTimer.Start();
                        if (smoothGraphTimer != null) smoothGraphTimer.Start();
                        if (sidebarActivityTimer != null) sidebarActivityTimer.Start();
                        if (btnStartTimerBenchmark != null) {
                            btnStartTimerBenchmark.Content = string.IsNullOrEmpty(lastBenchEstimatedTimeStr)
                                ? "🚀 Benchmark starten"
                                : string.Format(CultureInfo.InvariantCulture, "🚀 Benchmark starten ({0})", lastBenchEstimatedTimeStr);
                            var successStyle = (Application.Current.TryFindResource("SuccessBtn") ?? window.TryFindResource("SuccessBtn")) as Style;
                            if (successStyle != null) btnStartTimerBenchmark.Style = successStyle;
                        }
                        RefreshTimerResUI();

                        if (!isBenchmarkCancelled && hwClusterMap.Count > 0) {
                            int minExpectedRuns = passes;

                            // 1. Harmonie-Score für jeden Cluster berechnen
                            foreach (var kvp in hwClusterMap) {
                                var a = kvp.Value;
                                if (a.MeasurementCount == 0 || a.TotalSamples == 0) continue;

                                double sPct = (double)a.TotalSpikes / a.TotalSamples * 100.0;
                                double mStdev = a.SumStdev / a.MeasurementCount;
                                double mDelta = a.SumDelta / a.MeasurementCount;
                                double mP99 = a.SumP99 / a.MeasurementCount;

                                double pStutter = sPct * 2.0;
                                double pJitter = (mStdev * 1000.0) * 0.10;
                                double pLatency = (mDelta * 1000.0) * 0.08;
                                double pTail = Math.Max(0.0, (mP99 * 1000.0) - 150.0) * 0.08;

                                double rawScore = 100.0 - pStutter - pJitter - pLatency - pTail;
                                a.HarmonieScore = Math.Max(0.0, Math.Min(100.0, rawScore));
                            }

                            // 2. Besten Cluster nach Harmonie-Score mit Latenz-Tie-Breaker auf dem Plateau wählen
                            double maxScore = -1.0;
                            foreach (var kvp in hwClusterMap) {
                                var a = kvp.Value;
                                if (a.MeasurementCount == 0 || a.TotalSamples == 0) continue;
                                // Glitch-Schutz: Externe 1.0ms-Glitches oder unvollständige Durchläufe ausschließen
                                if (a.HardwareMs > endMs + 0.0060 || a.HardwareMs < startMs - 0.0060) continue;
                                if (a.MeasurementCount < minExpectedRuns && hwClusterMap.Count > 1) continue;

                                if (a.HarmonieScore > maxScore) {
                                    maxScore = a.HarmonieScore;
                                }
                            }

                            TimerStepAccumulator bestAcc = null;
                            foreach (var kvp in hwClusterMap) {
                                var a = kvp.Value;
                                if (a.MeasurementCount == 0 || a.TotalSamples == 0) continue;
                                if (a.HardwareMs > endMs + 0.0060 || a.HardwareMs < startMs - 0.0060) continue;
                                if (a.MeasurementCount < minExpectedRuns && hwClusterMap.Count > 1) continue;

                                // Plateau-Tie-Breaker: Bei statistischem Gleichstand (<= 3.0 Pkt) gewinnt der niedrigste Hardware-Takt
                                if (a.HarmonieScore >= maxScore - 3.0) {
                                    if (bestAcc == null || a.HardwareMs < bestAcc.HardwareMs) {
                                        bestAcc = a;
                                    }
                                }
                            }

                            if (bestAcc != null) {
                                double optimalHwRes = bestAcc.HardwareMs;
                                int bestHwTicks = bestAcc.HwTicks;
                                lastOptimalResMs = optimalHwRes;
                                TimeSpan totalElapsed = sw.Elapsed;
                                string durStr = FormatCompactDuration(totalElapsed.TotalSeconds);
                                double bestAvgStdev = bestAcc.MeasurementCount > 0 ? bestAcc.SumStdev / bestAcc.MeasurementCount : 0;
                                double bestAvgDelta = bestAcc.MeasurementCount > 0 ? bestAcc.SumDelta / bestAcc.MeasurementCount : 0;
                                double bestAvgP99 = bestAcc.MeasurementCount > 0 ? bestAcc.SumP99 / bestAcc.MeasurementCount : 0;
                                double bestSpikePct = bestAcc.TotalSamples > 0 ? ((double)bestAcc.TotalSpikes / bestAcc.TotalSamples * 100.0) : 0;

                                if (txtBenchStatus != null) {
                                    txtBenchStatus.Text = string.Format(CultureInfo.InvariantCulture,
                                        "Abgeschlossen in {0}! Harmonie-Sieger: {1:F4} ms ({2:F1}/100)", durStr, optimalHwRes, bestAcc.HarmonieScore);
                                }

                                if (borderBenchResult != null) borderBenchResult.Visibility = Visibility.Visible;
                                if (txtBenchOptimalRes != null) txtBenchOptimalRes.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4} ms ({1} Ticks)", optimalHwRes, bestHwTicks);
                                if (txtBenchOptimalStats != null) {
                                    txtBenchOptimalStats.Text = string.Format(CultureInfo.InvariantCulture,
                                        "Score: {0:F1}/100 | Spikes: {1} ({2:F1}%) | P99: +{3:F4} ms | Peak: +{4:F4} ms | STDEV: {5:F4} ms | Ø Δ: +{6:F4} ms",
                                        bestAcc.HarmonieScore, bestAcc.TotalSpikes, bestSpikePct, bestAvgP99, bestAcc.MaxPeakDelta, bestAvgStdev, bestAvgDelta);
                                }

                                // Top 1, Top 2, Top 3 ermitteln & anzeigen
                                var ranked = new List<TimerStepAccumulator>();
                                foreach (var kvp in hwClusterMap) {
                                    var a = kvp.Value;
                                    if (a.MeasurementCount == 0 || a.TotalSamples == 0) continue;
                                    if (a.HardwareMs > endMs + 0.0060 || a.HardwareMs < startMs - 0.0060) continue;
                                    if (a.MeasurementCount < minExpectedRuns && hwClusterMap.Count > 1) continue;
                                    ranked.Add(a);
                                }
                                ranked.Sort((a, b) => {
                                    if (Math.Abs(a.HarmonieScore - b.HarmonieScore) <= 3.0) {
                                        return a.HardwareMs.CompareTo(b.HardwareMs);
                                    }
                                    return b.HarmonieScore.CompareTo(a.HarmonieScore);
                                });

                                if (ranked.Count > 0) {
                                    top1ResMs = ranked[0].HardwareMs;
                                    if (txtBenchTop1Res != null) txtBenchTop1Res.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4} ms", top1ResMs);
                                    if (borderBenchTop1 != null) borderBenchTop1.Visibility = Visibility.Visible;
                                    if (btnApplyTop1 != null) btnApplyTop1.Content = "Setzen";
                                } else {
                                    if (borderBenchTop1 != null) borderBenchTop1.Visibility = Visibility.Collapsed;
                                }

                                if (ranked.Count > 1) {
                                    top2ResMs = ranked[1].HardwareMs;
                                    if (txtBenchTop2Res != null) txtBenchTop2Res.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4} ms", top2ResMs);
                                    if (borderBenchTop2 != null) borderBenchTop2.Visibility = Visibility.Visible;
                                    if (btnApplyTop2 != null) btnApplyTop2.Content = "Setzen";
                                } else {
                                    if (borderBenchTop2 != null) borderBenchTop2.Visibility = Visibility.Collapsed;
                                }

                                if (ranked.Count > 2) {
                                    top3ResMs = ranked[2].HardwareMs;
                                    if (txtBenchTop3Res != null) txtBenchTop3Res.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4} ms", top3ResMs);
                                    if (borderBenchTop3 != null) borderBenchTop3.Visibility = Visibility.Visible;
                                    if (btnApplyTop3 != null) btnApplyTop3.Content = "Setzen";
                                } else {
                                    if (borderBenchTop3 != null) borderBenchTop3.Visibility = Visibility.Collapsed;
                                }

                                if (txtBenchResultsLog != null) {
                                    string consensusTable = div + "\r\n" +
                                        "📊 MULTI-DIMENSIONALE HARMONIE-AUSWERTUNG (P99 + PEAK + STDEV + LATENZ)\r\n" +
                                        subDiv + "\r\n" +
                                        "Hardware-Takt   Ticks  Runs  Samples  Spikes (%)       P99        Max Peak    Ø STDEV      Ø Delta      Harmonie-Score\r\n" +
                                        subDiv + "\r\n";

                                    foreach (var kvp in hwClusterMap) {
                                        var a = kvp.Value;
                                        double aStdev = a.MeasurementCount > 0 ? a.SumStdev / a.MeasurementCount : 0;
                                        double aDelta = a.MeasurementCount > 0 ? a.SumDelta / a.MeasurementCount : 0;
                                        double aP99 = a.MeasurementCount > 0 ? a.SumP99 / a.MeasurementCount : 0;
                                        double aSpikePct = a.TotalSamples > 0 ? ((double)a.TotalSpikes / a.TotalSamples * 100.0) : 0;
                                        string winMark = (a.HwTicks == bestHwTicks) ? " ⭐ SIEGER" : "";
                                        consensusTable += string.Format(CultureInfo.InvariantCulture,
                                            "{0,7:F4} ms     {1,5}  {2,4}   {3,5}    {4,3} ({5,4:F1}%)   +{6,6:F4} ms  +{7,6:F4} ms  {8,7:F4} ms  +{9,7:F4} ms    {10,5:F1} / 100{11}\r\n",
                                            a.HardwareMs, a.HwTicks, a.MeasurementCount, a.TotalSamples, a.TotalSpikes, aSpikePct,
                                            aP99, a.MaxPeakDelta, aStdev, aDelta, a.HarmonieScore, winMark);
                                    }

                                    string sum = consensusTable +
                                        subDiv + "\r\n" +
                                        string.Format(CultureInfo.InvariantCulture,
                                            "🏆 HARMONIE-SWEET-SPOT: {0:F4} ms ({1} Ticks) — Note: {2:F1} / 100\r\n" +
                                            "📊 Samples: {3} | Spikes: {4} ({5:F1}%) | P99: +{6:F4} ms | Peak: +{7:F4} ms | STDEV: {8:F4} ms | Ø Δ: +{9:F4} ms\r\n" +
                                            "⏱️ Gesamtdauer: {10}\r\n" +
                                            "📌 SetTimerResolution Autostart-Befehl:\r\n" +
                                            "   C:\\SetTimerResolution.exe --no-console --resolution {1}\r\n" +
                                            div + "\r\n",
                                            optimalHwRes, bestHwTicks, bestAcc.HarmonieScore, bestAcc.TotalSamples, bestAcc.TotalSpikes,
                                            bestSpikePct, bestAvgP99, bestAcc.MaxPeakDelta, bestAvgStdev, bestAvgDelta, durStr);

                                    txtBenchResultsLog.AppendText(sum);
                                    if (scrollBenchResults != null) scrollBenchResults.ScrollToEnd();

                                    try {
                                        System.IO.File.AppendAllText(logPath, sum);
                                    } catch {}
                                }
                            }
                        } else {
                            if (txtBenchResultsLog != null) {
                                txtBenchResultsLog.Text = logBase + "\r\n⚠️ Benchmark durch Benutzer abgebrochen.\r\n";
                                if (scrollBenchResults != null) scrollBenchResults.ScrollToEnd();
                            }
                            if (txtBenchStatus != null) {
                                txtBenchStatus.Text = string.Format(CultureInfo.InvariantCulture,
                                    "Benchmark abgebrochen – Vorheriger Takt ({0:F4} ms) wiederhergestellt.", preBenchmarkResMs);
                            }
                        }

                        // Live-Monitor fortsetzen
                        if (isTimerResTabActive && !isMeasureSleepPaused) {
                            StartMeasureSleep();
                        }
                    }));
                }
            }) {
                IsBackground = true,
                Priority = ThreadPriority.Highest,
                Name = "TimerResolutionBenchmarkThread"
            };
            benchmarkThread.Start();
        }

        private void CancelTimerBenchmark() {
            if (!isBenchmarkRunning) return;
            isBenchmarkCancelled = true;
            if (btnStartTimerBenchmark != null) {
                btnStartTimerBenchmark.Content = "Wird abgebrochen...";
                var dangerStyle = (Application.Current.TryFindResource("PrimaryBtn") ?? window.TryFindResource("PrimaryBtn")) as Style;
                if (dangerStyle != null) btnStartTimerBenchmark.Style = dangerStyle;
            }
        }

        private bool ApplyTimerResolutionToSystem(double resMs, bool updateShortcut) {
            int ticks = (int)Math.Round(resMs * 10000.0);
            bool appliedViaDaemon = false;

            TimerPrecisionProbe.ResetResolution();
            ClearTimerResGraph();

            // 1. SetTimerResolution-Verknüpfung im Autostart aktualisieren, alten Prozess killen und mit neuem Wert starten
            try {
                string startupDir = Environment.GetFolderPath(Environment.SpecialFolder.Startup);
                if (System.IO.Directory.Exists(startupDir)) {
                    Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                    if (shellType != null) {
                        dynamic shell = Activator.CreateInstance(shellType);
                        string[] lnkFiles = System.IO.Directory.GetFiles(startupDir, "*.lnk");
                        string matchedLnk = null;
                        string currentTarget = null;

                        foreach (string file in lnkFiles) {
                            try {
                                dynamic sc = shell.CreateShortcut(file);
                                string target = sc.TargetPath as string;
                                if (!string.IsNullOrEmpty(target) && target.IndexOf("SetTimerResolution.exe", StringComparison.OrdinalIgnoreCase) >= 0) {
                                    matchedLnk = file;
                                    currentTarget = target;
                                    break;
                                }
                            } catch {}
                        }

                        if (matchedLnk != null && !string.IsNullOrEmpty(currentTarget)) {
                            string newArgs = string.Format(CultureInfo.InvariantCulture, "--no-console --resolution {0}", ticks);
                            if (updateShortcut) {
                                try {
                                    dynamic shortcut = shell.CreateShortcut(matchedLnk);
                                    shortcut.Arguments = newArgs;
                                    shortcut.Save();
                                } catch {}
                            }

                            string procName = System.IO.Path.GetFileNameWithoutExtension(currentTarget);
                            foreach (var proc in Process.GetProcessesByName(procName)) {
                                try {
                                    proc.Kill();
                                    proc.WaitForExit(1000);
                                } catch {}
                            }

                            if (System.IO.File.Exists(currentTarget)) {
                                ProcessStartInfo psi = new ProcessStartInfo {
                                    FileName = currentTarget,
                                    Arguments = newArgs,
                                    UseShellExecute = true,
                                    WindowStyle = ProcessWindowStyle.Hidden
                                };
                                Process.Start(psi);
                                appliedViaDaemon = true;
                            }
                        }
                    }
                }
            } catch {}

            // 2. Nur wenn kein externer SetTimerResolution-Daemon existiert: In-Process Fallback via NtSetTimerResolution
            if (!appliedViaDaemon) {
                double actualMs;
                TimerPrecisionProbe.SetResolution(resMs, out actualMs);
            }

            TimerPrecisionProbe.NativeSleep(50);
            RefreshTimerResUI();
            return appliedViaDaemon;
        }

        private void ApplyOptimalTimer() {
            try {
                int ticks = (int)Math.Round(lastOptimalResMs * 10000.0);
                bool appliedViaDaemon = ApplyTimerResolutionToSystem(lastOptimalResMs, updateShortcut: true);

                if (txtManualTimerRes != null) {
                    txtManualTimerRes.Text = string.Format(CultureInfo.InvariantCulture, "{0:F4}", lastOptimalResMs);
                }

                if (btnApplyTop1 != null) {
                    btnApplyTop1.Content = "✓ Aktiv";
                }
                if (txtBenchStatus != null) {
                    txtBenchStatus.Text = appliedViaDaemon
                        ? string.Format(CultureInfo.InvariantCulture, "SetTimerResolution mit --resolution {0} neu gestartet & Autostart aktualisiert!", ticks)
                        : string.Format(CultureInfo.InvariantCulture, "Optimaler Takt {0:F4} ms aktiv!", lastOptimalResMs);
                }
            } catch {}
        }

        private void ResetTimerResolution() {
            try {
                TimerPrecisionProbe.ResetResolution();
                ClearTimerResGraph();
                try {
                    foreach (var proc in Process.GetProcessesByName("SetTimerResolution")) {
                        try { proc.Kill(); } catch {}
                    }
                } catch {}
                if (btnApplyTop1 != null) btnApplyTop1.Content = "Setzen";
                RefreshTimerResUI();
            } catch {}
        }
    }
}
