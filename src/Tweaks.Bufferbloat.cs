using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace ZnipeOptimizationTool {
    public partial class MainWindowLogic {
        // =========================================================================
        // BUFFERBLOAT & NETWORK PERFORMANCE ENGINE (WAVEFORM STANDARD — 100% REAL)
        // =========================================================================

        public class BufferbloatReport {
            public string TargetHost = "1.1.1.1";
            public string TargetHostName = "Cloudflare Anycast (1.1.1.1)";
            public DateTime TestDate = DateTime.Now;

            // Phase 1: Unloaded Baseline (Idle)
            public double UnloadedMin = 0;
            public double UnloadedAvg = 0;
            public double UnloadedMedian = 0;
            public double UnloadedMax = 0;
            public double UnloadedJitter = 0;
            public double UnloadedStdDev = 0;
            public int UnloadedPingsSent = 0;
            public int UnloadedPingsRecv = 0;

            // Phase 2: Loaded Download
            public double DownloadMbps = 0;
            public double DownloadPingAvg = 0;
            public double DownloadPingMedian = 0;
            public double DownloadMaxSpike = 0;
            public double DownloadDelta = 0;
            public int DownloadPingsSent = 0;
            public int DownloadPingsRecv = 0;

            // Phase 4: Loaded Upload
            public double UploadMbps = 0;
            public double UploadPingAvg = 0;
            public double UploadPingMedian = 0;
            public double UploadMaxSpike = 0;
            public double UploadDelta = 0;
            public int UploadPingsSent = 0;
            public int UploadPingsRecv = 0;

            // Overall Waveform Evaluation
            public double OverallPacketLoss = 0;
            public double OverallMaxSpike = 0;
            public double MaxDelta = 0;
            public string Grade = "—";
            public string Verdict = "";
            public string Description = "";
            public string GradeColor = "#10B981";
        }

        // Chunked HttpContent for physical upload saturation without memory overhead
        public class ChunkedUploadContent : HttpContent {
            private readonly byte[] _chunk;
            private readonly int _totalBytes;
            private readonly Action<int> _onChunkSent;
            private readonly CancellationToken _ct;

            public ChunkedUploadContent(int totalBytes, Action<int> onChunkSent, CancellationToken ct) {
                _totalBytes = totalBytes;
                _onChunkSent = onChunkSent;
                _ct = ct;
                _chunk = new byte[32768]; // 32 KB Socket chunk size
                new Random(1337).NextBytes(_chunk);
            }

            protected override async Task SerializeToStreamAsync(Stream stream, TransportContext context) {
                int sent = 0;
                while (sent < _totalBytes && !_ct.IsCancellationRequested) {
                    int toSend = Math.Min(_chunk.Length, _totalBytes - sent);
                    await stream.WriteAsync(_chunk, 0, toSend, _ct);
                    await stream.FlushAsync(_ct);
                    if (_onChunkSent != null) _onChunkSent(toSend);
                    sent += toSend;
                }
            }

            protected override bool TryComputeLength(out long length) {
                length = _totalBytes;
                return true;
            }
        }

        private CancellationTokenSource _bbCts;
        private bool _isBbRunning = false;

        // Top Controls
        private ComboBox cmbBbTargetServer;
        private ComboBox cmbBbDuration;
        private Button btnBbStartTest;

        // Phasen-Chips & Progress Dock
        private Border[] phaseChips;
        private TextBlock[] txtPhaseChips;

        private TextBlock txtBbLiveIcon;
        private TextBlock txtBbLivePhase;
        private TextBlock txtBbLivePercent;
        private ProgressBar progressBbTest;

        // Grade Card
        private Border borderBbGradeCard;
        private TextBlock txtBbGradeBadge;
        private Border borderBbMaxDelta;
        private TextBlock txtBbGradeDelta;
        private TextBlock txtBbGradeVerdict;
        private TextBlock txtBbGradeDesc;

        // Säule 1: Idle (Unloaded)
        private TextBlock txtBbUnloadedAvg;
        private TextBlock txtBbUnloadedJitter;
        private TextBlock txtBbUnloadedMinMax;
        private TextBlock txtBbUnloadedLoss;

        // Säule 2: Download
        private TextBlock txtBbDlSpeed;
        private TextBlock txtBbDlPing;
        private TextBlock txtBbDlDelta;
        private Border borderBbDlDelta;
        private TextBlock txtBbDlMaxSpike;

        // Säule 3: Upload
        private TextBlock txtBbUlSpeed;
        private TextBlock txtBbUlPing;
        private TextBlock txtBbUlDelta;
        private Border borderBbUlDelta;
        private TextBlock txtBbUlMaxSpike;

        // Technische Telemetrie
        private TextBlock txtBbPacketsStats;
        private TextBlock txtBbMaxSpike;
        private TextBlock txtBbMedianStats;
        private TextBlock txtBbStdDevStats;
        private TextBlock txtBbTargetHostDisplay;

        private void InitializeBufferbloatControls() {
            if (window == null) return;

            // Find Elements
            cmbBbTargetServer      = (ComboBox)window.FindName("CmbBbTargetServer");
            cmbBbDuration          = (ComboBox)window.FindName("CmbBbDuration");
            btnBbStartTest         = (Button)window.FindName("BtnBbStartTest");

            phaseChips = new Border[] {
                (Border)window.FindName("ChipPhase1"),
                (Border)window.FindName("ChipPhase2"),
                (Border)window.FindName("ChipPhase3"),
                (Border)window.FindName("ChipPhase4")
            };
            txtPhaseChips = new TextBlock[] {
                (TextBlock)window.FindName("TxtChipPhase1"),
                (TextBlock)window.FindName("TxtChipPhase2"),
                (TextBlock)window.FindName("TxtChipPhase3"),
                (TextBlock)window.FindName("TxtChipPhase4")
            };

            txtBbLiveIcon          = (TextBlock)window.FindName("TxtBbLiveIcon");
            txtBbLivePhase         = (TextBlock)window.FindName("TxtBbLivePhase");
            txtBbLivePercent       = (TextBlock)window.FindName("TxtBbLivePercent");
            progressBbTest         = (ProgressBar)window.FindName("ProgressBbTest");

            borderBbGradeCard      = (Border)window.FindName("BorderBbGradeCard");
            txtBbGradeBadge        = (TextBlock)window.FindName("TxtBbGradeBadge");
            borderBbMaxDelta       = (Border)window.FindName("BorderBbMaxDelta");
            txtBbGradeDelta        = (TextBlock)window.FindName("TxtBbGradeDelta");
            txtBbGradeVerdict      = (TextBlock)window.FindName("TxtBbGradeVerdict");
            txtBbGradeDesc         = (TextBlock)window.FindName("TxtBbGradeDesc");

            txtBbUnloadedAvg       = (TextBlock)window.FindName("TxtBbUnloadedAvg");
            txtBbUnloadedJitter    = (TextBlock)window.FindName("TxtBbUnloadedJitter");
            txtBbUnloadedMinMax    = (TextBlock)window.FindName("TxtBbUnloadedMinMax");
            txtBbUnloadedLoss      = (TextBlock)window.FindName("TxtBbUnloadedLoss");

            txtBbDlSpeed           = (TextBlock)window.FindName("TxtBbDlSpeed");
            txtBbDlPing            = (TextBlock)window.FindName("TxtBbDlPing");
            txtBbDlDelta           = (TextBlock)window.FindName("TxtBbDlDelta");
            borderBbDlDelta        = (Border)window.FindName("BorderBbDlDelta");
            txtBbDlMaxSpike        = (TextBlock)window.FindName("TxtBbDlMaxSpike");

            txtBbUlSpeed           = (TextBlock)window.FindName("TxtBbUlSpeed");
            txtBbUlPing            = (TextBlock)window.FindName("TxtBbUlPing");
            txtBbUlDelta           = (TextBlock)window.FindName("TxtBbUlDelta");
            borderBbUlDelta        = (Border)window.FindName("BorderBbUlDelta");
            txtBbUlMaxSpike        = (TextBlock)window.FindName("TxtBbUlMaxSpike");

            txtBbPacketsStats      = (TextBlock)window.FindName("TxtBbPacketsStats");
            txtBbMaxSpike          = (TextBlock)window.FindName("TxtBbMaxSpike");
            txtBbMedianStats       = (TextBlock)window.FindName("TxtBbMedianStats");
            txtBbStdDevStats       = (TextBlock)window.FindName("TxtBbStdDevStats");
            txtBbTargetHostDisplay = (TextBlock)window.FindName("TxtBbTargetHostDisplay");

            // Event Handlers
            if (btnBbStartTest != null) btnBbStartTest.Click += (s, e) => StartBufferbloatBenchmark();
        }

        private void StartBufferbloatBenchmark() {
            if (_isBbRunning) {
                CancelBufferbloatBenchmark();
                return;
            }
            _isBbRunning = true;
            UpdateSidebarActivitySpinners();
            _bbCts = new CancellationTokenSource();
            var ct = _bbCts.Token;

            // Target Host & Duration resolution
            string targetHost = "1.1.1.1";
            string targetHostName = "Cloudflare Anycast (1.1.1.1)";
            if (cmbBbTargetServer != null) {
                var sel = cmbBbTargetServer.SelectedItem as ComboBoxItem;
                if (sel != null && sel.Tag != null) {
                    targetHost = sel.Tag.ToString();
                    targetHostName = sel.Content != null ? sel.Content.ToString() : targetHost;
                }
            }

            int stressDurationSec = 10;
            if (cmbBbDuration != null) {
                var selDur = cmbBbDuration.SelectedItem as ComboBoxItem;
                if (selDur != null && selDur.Tag != null) {
                    int.TryParse(selDur.Tag.ToString(), out stressDurationSec);
                }
            }
            if (stressDurationSec < 3) stressDurationSec = 10;

            // Reset UI for clean live benchmark
            ResetBufferbloatUI(targetHostName);

            Task.Run(async () => {
                var rep = new BufferbloatReport {
                    TargetHost = targetHost,
                    TargetHostName = targetHostName,
                    TestDate = DateTime.Now
                };

                IPAddress targetIp = null;
                PrecisePing pinger = null;

                try {
                    targetIp = ResolveTargetIp(targetHost);
                    pinger = PrecisePing.Create();
                    // =========================================================================
                    // PHASE 1: WARMUP & UNLOADED (IDLE) LATENCY + JITTER (0% -> 25%)
                    // =========================================================================
                    HighlightPhaseChip(1);
                    UpdateLiveStatus("⏳", "Phase 1/4: Warmup & Messe unbelastete Basis-Latenz...", 0);

                    // Pre-warm socket route (ARP/Route-Cache aufwärmen)
                    double warmupRtt;
                    pinger.TryPing(targetIp, 1000, out warmupRtt);
                    pinger.TryPing(targetIp, 1000, out warmupRtt);

                    var idlePings = new List<double>();
                    int idleSent = 0;
                    int idleRecv = 0;
                    int countToMeasure = Math.Max(10, stressDurationSec * 2);

                    await RunOnDedicatedPingThread(() => {
                        RunBlockingPingLoop(pinger, targetIp,
                            () => idleSent < countToMeasure,
                            ct,
                            () => idleSent++,
                            rtt => {
                                idleRecv++;
                                lock (idlePings) idlePings.Add(rtt);
                                UpdateIdleLivePing(rtt);
                            },
                            i => UpdateLiveStatus("⏳", string.Format("Phase 1/4: Warmup & Basis-Latenz ({0}/{1} Pings)...", i, countToMeasure), ((double)i / countToMeasure) * 25.0));
                    });

                    if (ct.IsCancellationRequested) return;

                    rep.UnloadedPingsSent = idleSent;
                    rep.UnloadedPingsRecv = idleRecv;

                    if (idlePings.Count > 0) {
                        rep.UnloadedMin = idlePings.Min();
                        rep.UnloadedAvg = idlePings.Average();
                        rep.UnloadedMedian = CalculateMedian(idlePings);
                        rep.UnloadedMax = idlePings.Max();
                        rep.UnloadedJitter = CalculateJitter(idlePings);
                        rep.UnloadedStdDev = CalculateStdDev(idlePings);
                    }

                    UpdateIdleFinalUI(rep);

                    // =========================================================================
                    // PHASE 2: MULTI-STREAM DOWNLOAD STRESSTEST (25% -> 60%)
                    // =========================================================================
                    HighlightPhaseChip(2);
                    UpdateLiveStatus("🔥", "Phase 2/4: Download-Warmup (TCP-Rampup & Leitungssättigung)...", 25);

                    long totalDlBytes = 0;
                    long liveDlWindowBytes = 0;
                    var dlPings = new List<double>();
                    int dlPingsSent = 0;
                    int dlPingsRecv = 0;

                    var dlSw = Stopwatch.StartNew();
                    Stopwatch dlMeasureSw = null;
                    long dlWarmupBytes = 0;
                    bool dlWarmupDone = false;

                    using (var dlCts = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                        var dlTasks = new List<Task>();
                        int streamCount = 6;
                        string[] dlUrls = new string[] {
                            "https://fra.speedtest.clouvider.net/backend/garbage.php?ckSize=100",
                            "https://proof.ovh.net/files/1Gb.dat",
                            "http://ping.online.net/1000Mo.dat",
                            "https://ams.speedtest.clouvider.net/backend/garbage.php?ckSize=100"
                        };

                        // 1. Parallel Sockets for Line Saturation
                        for (int i = 0; i < streamCount; i++) {
                            string url = dlUrls[i % dlUrls.Length];
                            dlTasks.Add(Task.Run(async () => {
                                try {
                                    using (var handler = new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None })
                                    using (var client = new HttpClient(handler)) {
                                        client.Timeout = TimeSpan.FromSeconds(15);
                                        byte[] buf = new byte[131072];

                                        while (!dlCts.Token.IsCancellationRequested && dlSw.Elapsed.TotalSeconds < stressDurationSec) {
                                            bool streamError = false;
                                            try {
                                                using (var req = new HttpRequestMessage(HttpMethod.Get, url)) {
                                                    req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZnipeOptimizer/1.0");
                                                    using (var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, dlCts.Token)) {
                                                        if (res.IsSuccessStatusCode) {
                                                            using (var stream = await res.Content.ReadAsStreamAsync()) {
                                                                int read;
                                                                while ((read = await stream.ReadAsync(buf, 0, buf.Length, dlCts.Token)) > 0) {
                                                                    Interlocked.Add(ref totalDlBytes, read);
                                                                    Interlocked.Add(ref liveDlWindowBytes, read);
                                                                    if (dlSw.Elapsed.TotalSeconds >= stressDurationSec || dlCts.Token.IsCancellationRequested) break;
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                            } catch (OperationCanceledException) {
                                                break;
                                            } catch {
                                                streamError = true;
                                            }
                                            if (streamError) {
                                                if (dlCts.Token.IsCancellationRequested) break;
                                                await Task.Delay(100, dlCts.Token);
                                            }
                                        }
                                    }
                                } catch (OperationCanceledException) {}
                                catch {}
                            }));
                        }

                        // 2. Parallel RTT Ping Sampling during Download Saturation (dedizierter High-Priority-Thread, QPC-genau)
                        var dlPingTask = RunOnDedicatedPingThread(() => {
                            // Warmup ramp-up (1.2s) - let sockets establish full throughput
                            if (dlCts.Token.WaitHandle.WaitOne(1200)) return;

                            dlWarmupBytes = Interlocked.Read(ref totalDlBytes);
                            dlMeasureSw = Stopwatch.StartNew();
                            dlWarmupDone = true;

                            RunBlockingPingLoop(pinger, targetIp,
                                () => dlSw.Elapsed.TotalSeconds < stressDurationSec,
                                dlCts.Token,
                                () => dlPingsSent++,
                                rtt => {
                                    dlPingsRecv++;
                                    lock (dlPings) dlPings.Add(rtt);
                                });
                        });

                        // 3. Independent Asynchronous UI Updates (Throttled, non-blocking)
                        var dlUiTask = Task.Run(async () => {
                            for (int w = 0; w < 6 && !dlCts.Token.IsCancellationRequested; w++) {
                                UpdateLiveStatus("🔥", "Phase 2/4: Download-Warmup (TCP-Rampup & Leitungssättigung)...", 25.0 + (w * 0.8));
                                await Task.Delay(200, dlCts.Token);
                            }

                            while (!dlCts.Token.IsCancellationRequested && dlSw.Elapsed.TotalSeconds < stressDurationSec) {
                                double rtt = 0;
                                double runningMed = 0;
                                lock (dlPings) {
                                    if (dlPings.Count > 0) {
                                        rtt = dlPings[dlPings.Count - 1];
                                        runningMed = CalculateMedian(dlPings);
                                    }
                                }
                                double baseline = rep.UnloadedMedian > 0 ? rep.UnloadedMedian : rep.UnloadedAvg;
                                if (rtt > 0) UpdateDlLivePing(rtt, runningMed, baseline);

                                double curMbit = 0;
                                if (dlMeasureSw != null && dlMeasureSw.Elapsed.TotalSeconds > 0.2) {
                                    long bytesSinceWarmup = Interlocked.Read(ref totalDlBytes) - dlWarmupBytes;
                                    double sec = dlMeasureSw.Elapsed.TotalSeconds;
                                    curMbit = (bytesSinceWarmup * 8.0) / (sec * 1000.0 * 1000.0);
                                } else {
                                    curMbit = (Interlocked.Read(ref totalDlBytes) * 8.0) / (Math.Max(0.5, dlSw.Elapsed.TotalSeconds) * 1000.0 * 1000.0);
                                }
                                UpdateDlLiveSpeed(curMbit);

                                double frac = Math.Min(1.0, (dlSw.Elapsed.TotalSeconds - 1.2) / Math.Max(1.0, stressDurationSec - 1.2));
                                double prog = 30.0 + (frac * 30.0);
                                UpdateLiveStatus("📥", string.Format("Phase 2/4: Download-Stresstest ({0:F1} Mbit/s)...", curMbit), prog);

                                await Task.Delay(200, dlCts.Token);
                            }
                        });

                        await Task.WhenAll(dlPingTask, dlUiTask);
                        try { dlCts.Cancel(); } catch {}
                        try { await Task.WhenAny(Task.WhenAll(dlTasks), Task.Delay(250)); } catch {}
                    }

                    dlSw.Stop();
                    if (ct.IsCancellationRequested) return;

                    if (dlWarmupDone && dlMeasureSw != null && dlMeasureSw.Elapsed.TotalSeconds > 0.5) {
                        long bytesSinceWarmup = Interlocked.Read(ref totalDlBytes) - dlWarmupBytes;
                        rep.DownloadMbps = (bytesSinceWarmup * 8.0) / (dlMeasureSw.Elapsed.TotalSeconds * 1000.0 * 1000.0);
                    } else {
                        rep.DownloadMbps = (totalDlBytes * 8.0) / (Math.Max(1.0, dlSw.Elapsed.TotalSeconds) * 1000.0 * 1000.0);
                    }
                    rep.DownloadPingsSent = dlPingsSent;
                    rep.DownloadPingsRecv = dlPingsRecv;

                    if (dlPings.Count > 0) {
                        rep.DownloadPingAvg = dlPings.Average();
                        rep.DownloadPingMedian = CalculateMedian(dlPings);
                        rep.DownloadMaxSpike = dlPings.Max();
                        double dlBase = rep.UnloadedMedian > 0 ? rep.UnloadedMedian : rep.UnloadedAvg;
                        rep.DownloadDelta = Math.Max(0.0, rep.DownloadPingMedian - dlBase);
                    }

                    UpdateDlFinalUI(rep);

                    // =========================================================================
                    // PHASE 3: PUFFER-DRAIN / COOLDOWN (60% -> 65%)
                    // =========================================================================
                    HighlightPhaseChip(3);
                    UpdateLiveStatus("⏸️", "Phase 3/4: Puffer-Drain / Cooldown (Warteschlangen entleeren)...", 60);
                    await Task.Delay(1200, ct);
                    if (ct.IsCancellationRequested) return;

                    // =========================================================================
                    // PHASE 4: MULTI-STREAM UPLOAD STRESSTEST (65% -> 98%)
                    // =========================================================================
                    HighlightPhaseChip(4);
                    UpdateLiveStatus("🔥", "Phase 4/4: Upload-Warmup (Socket-Puffer aufbauen & Sättigung)...", 65);

                    long totalUlBytes = 0;
                    long liveUlWindowBytes = 0;
                    var ulPings = new List<double>();
                    int ulPingsSent = 0;
                    int ulPingsRecv = 0;

                    var ulSw = Stopwatch.StartNew();
                    Stopwatch ulMeasureSw = null;
                    long ulWarmupBytes = 0;
                    bool ulWarmupDone = false;

                    using (var ulCts = CancellationTokenSource.CreateLinkedTokenSource(ct)) {
                        var ulTasks = new List<Task>();
                        int ulStreamCount = 5;
                        string ulUrl = "https://speed.cloudflare.com/__up";

                        // 1. Parallel Physical Upload POST Streams
                        for (int i = 0; i < ulStreamCount; i++) {
                            ulTasks.Add(Task.Run(async () => {
                                try {
                                    using (var client = new HttpClient()) {
                                        client.Timeout = TimeSpan.FromSeconds(15);

                                        while (!ulCts.Token.IsCancellationRequested && ulSw.Elapsed.TotalSeconds < stressDurationSec) {
                                            bool streamError = false;
                                            try {
                                                using (var content = new ChunkedUploadContent(8 * 1024 * 1024, chunkSent => {
                                                    Interlocked.Add(ref totalUlBytes, chunkSent);
                                                    Interlocked.Add(ref liveUlWindowBytes, chunkSent);
                                                }, ulCts.Token)) {
                                                    using (var req = new HttpRequestMessage(HttpMethod.Post, ulUrl) { Content = content }) {
                                                        req.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZnipeOptimizer/1.0");
                                                        var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, ulCts.Token);
                                                    }
                                                }
                                            } catch (OperationCanceledException) {
                                                break;
                                            } catch {
                                                streamError = true;
                                            }
                                            if (streamError) {
                                                if (ulCts.Token.IsCancellationRequested) break;
                                                await Task.Delay(100, ulCts.Token);
                                            }
                                        }
                                    }
                                } catch (OperationCanceledException) {}
                                catch {}
                            }));
                        }

                        // 2. Parallel RTT Ping Sampling during Upload Saturation (dedizierter High-Priority-Thread, QPC-genau)
                        var ulPingTask = RunOnDedicatedPingThread(() => {
                            // Warmup ramp-up (1.2s) - let sockets establish full throughput
                            if (ulCts.Token.WaitHandle.WaitOne(1200)) return;

                            ulWarmupBytes = Interlocked.Read(ref totalUlBytes);
                            ulMeasureSw = Stopwatch.StartNew();
                            ulWarmupDone = true;

                            RunBlockingPingLoop(pinger, targetIp,
                                () => ulSw.Elapsed.TotalSeconds < stressDurationSec,
                                ulCts.Token,
                                () => ulPingsSent++,
                                rtt => {
                                    ulPingsRecv++;
                                    lock (ulPings) ulPings.Add(rtt);
                                });
                        });

                        // 3. Independent Asynchronous UI Updates (Throttled, non-blocking)
                        var ulUiTask = Task.Run(async () => {
                            for (int w = 0; w < 6 && !ulCts.Token.IsCancellationRequested; w++) {
                                UpdateLiveStatus("🔥", "Phase 4/4: Upload-Warmup (Socket-Puffer aufbauen & Sättigung)...", 65.0 + (w * 0.8));
                                await Task.Delay(200, ulCts.Token);
                            }

                            while (!ulCts.Token.IsCancellationRequested && ulSw.Elapsed.TotalSeconds < stressDurationSec) {
                                double rtt = 0;
                                double runningUlMed = 0;
                                lock (ulPings) {
                                    if (ulPings.Count > 0) {
                                        rtt = ulPings[ulPings.Count - 1];
                                        runningUlMed = CalculateMedian(ulPings);
                                    }
                                }
                                double baseline = rep.UnloadedMedian > 0 ? rep.UnloadedMedian : rep.UnloadedAvg;
                                if (rtt > 0) UpdateUlLivePing(rtt, runningUlMed, baseline);

                                double curMbit = 0;
                                if (ulMeasureSw != null && ulMeasureSw.Elapsed.TotalSeconds > 0.2) {
                                    long bytesSinceWarmup = Interlocked.Read(ref totalUlBytes) - ulWarmupBytes;
                                    double sec = ulMeasureSw.Elapsed.TotalSeconds;
                                    curMbit = (bytesSinceWarmup * 8.0) / (sec * 1000.0 * 1000.0);
                                } else {
                                    curMbit = (Interlocked.Read(ref totalUlBytes) * 8.0) / (Math.Max(0.5, ulSw.Elapsed.TotalSeconds) * 1000.0 * 1000.0);
                                }
                                UpdateUlLiveSpeed(curMbit);

                                double frac = Math.Min(1.0, (ulSw.Elapsed.TotalSeconds - 1.2) / Math.Max(1.0, stressDurationSec - 1.2));
                                double prog = 70.0 + (frac * 28.0);
                                UpdateLiveStatus("📤", string.Format("Phase 4/4: Upload-Stresstest ({0:F1} Mbit/s)...", curMbit), prog);

                                await Task.Delay(200, ulCts.Token);
                            }
                        });

                        await Task.WhenAll(ulPingTask, ulUiTask);
                        try { ulCts.Cancel(); } catch {}
                        try { await Task.WhenAny(Task.WhenAll(ulTasks), Task.Delay(250)); } catch {}
                    }

                    ulSw.Stop();
                    if (ct.IsCancellationRequested) return;

                    if (ulWarmupDone && ulMeasureSw != null && ulMeasureSw.Elapsed.TotalSeconds > 0.5) {
                        long bytesSinceWarmup = Interlocked.Read(ref totalUlBytes) - ulWarmupBytes;
                        rep.UploadMbps = (bytesSinceWarmup * 8.0) / (ulMeasureSw.Elapsed.TotalSeconds * 1000.0 * 1000.0);
                    } else {
                        rep.UploadMbps = (totalUlBytes * 8.0) / (Math.Max(1.0, ulSw.Elapsed.TotalSeconds) * 1000.0 * 1000.0);
                    }
                    rep.UploadPingsSent = ulPingsSent;
                    rep.UploadPingsRecv = ulPingsRecv;

                    if (ulPings.Count > 0) {
                        rep.UploadPingAvg = ulPings.Average();
                        rep.UploadPingMedian = CalculateMedian(ulPings);
                        rep.UploadMaxSpike = ulPings.Max();
                        double ulBase = rep.UnloadedMedian > 0 ? rep.UnloadedMedian : rep.UnloadedAvg;
                        rep.UploadDelta = Math.Max(0.0, rep.UploadPingMedian - ulBase);
                    }

                    UpdateUlFinalUI(rep);

                    // =========================================================================
                    // PHASE 5: WAVEFORM EVALUATION & GRADING (100% WAVEFORM STANDARD)
                    // =========================================================================
                    UpdateLiveStatus("🔬", "Berechne finale Waveform-Auswertung & Telemetrie...", 99);

                    int totalSent = rep.UnloadedPingsSent + rep.DownloadPingsSent + rep.UploadPingsSent;
                    int totalRecv = rep.UnloadedPingsRecv + rep.DownloadPingsRecv + rep.UploadPingsRecv;
                    if (totalSent > 0) {
                        rep.OverallPacketLoss = Math.Max(0.0, ((totalSent - totalRecv) / (double)totalSent) * 100.0);
                    }

                    rep.OverallMaxSpike = Math.Max(rep.UnloadedMax, Math.Max(rep.DownloadMaxSpike, rep.UploadMaxSpike));
                    rep.MaxDelta = Math.Max(rep.DownloadDelta, rep.UploadDelta);

                    // Offizielles Waveform Rating System
                    CalculateWaveformGrade(rep);

                    UpdateFinalWaveformUI(rep);

                    UpdateLiveStatus("✅", string.Format("Benchmark abgeschlossen: Note {0} (+{1:F1} ms Bufferbloat)", rep.Grade, rep.MaxDelta), 100);

                } catch (OperationCanceledException) {
                    UpdateLiveStatus("⏹️", "Benchmark vom Benutzer abgebrochen.", 0);
                } catch (Exception ex) {
                    UpdateLiveStatus("❌", "Fehler beim Benchmark: " + ex.Message, 0);
                } finally {
                    if (pinger != null) { try { pinger.Dispose(); } catch {} }
                    _isBbRunning = false;
                    UpdateSidebarActivitySpinners();
                    if (window != null) {
                        var _ = window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => {
                            if (btnBbStartTest != null) {
                                btnBbStartTest.IsEnabled = true;
                                btnBbStartTest.Content = "🚀 Benchmark starten";
                            }
                        }));
                    }
                }
            });
        }

        private void CancelBufferbloatBenchmark() {
            try {
                if (_bbCts != null && !_bbCts.IsCancellationRequested) {
                    _bbCts.Cancel();
                }
            } catch {}
        }

        // =========================================================================
        // PRÄZISIONS-PING-INFRASTRUKTUR (Raw-ICMP + QPC, dedizierter Thread)
        // =========================================================================

        private static IPAddress ResolveTargetIp(string host) {
            IPAddress ip;
            if (IPAddress.TryParse(host, out ip)) return ip;
            try {
                var addrs = Dns.GetHostAddresses(host);
                if (addrs != null && addrs.Length > 0) return addrs[0];
            } catch {}
            return null;
        }

        // Startet eine Aktion auf einem dedizierten, priorisierten Hintergrund-Thread
        // und liefert ein Task-Handle zurück (erlaubt Task.WhenAll ohne ThreadPool-Jitter).
        private static Task RunOnDedicatedPingThread(Action action) {
            var tcs = new TaskCompletionSource<object>();
            var thread = new Thread(() => {
                try {
                    Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;
                    action();
                    tcs.TrySetResult(null);
                } catch (Exception ex) {
                    tcs.TrySetException(ex);
                }
            });
            thread.IsBackground = true;
            thread.Name = "ZnipePingSampler";
            thread.Start();
            return tcs.Task;
        }

        // Blockierende Ping-Schleife (synchron, QPC-genau) auf dem aktuellen Thread.
        // onSent/onRecv/onProgress werden pro Sample gefeuert; Pacing über cancellbares WaitHandle.
        private static void RunBlockingPingLoop(
            PrecisePing pinger,
            IPAddress target,
            Func<bool> shouldContinue,
            CancellationToken ct,
            Action onSent,
            Action<double> onRecv,
            Action<int> onProgress = null,
            int intervalMs = 100) {

            int completed = 0;
            while (!ct.IsCancellationRequested && shouldContinue()) {
                onSent();
                double rtt;
                if (pinger.TryPing(target, 1000, out rtt)) onRecv(rtt);
                completed++;
                if (onProgress != null) onProgress(completed);
                if (ct.WaitHandle.WaitOne(intervalMs)) break;
            }
        }

        // =========================================================================
        // OFFIZIELLES WAVEFORM BUFFERBLOAT BEWERTUNGSSYSTEM (100% EXAKTE WAVEFORM-TABELLE)
        // =========================================================================
        private void CalculateWaveformGrade(BufferbloatReport rep) {
            double delta = rep.MaxDelta;

            // Offizielle Waveform-Grenzwerte:
            // A+: < 5 ms
            // A:  < 30 ms
            // B:  < 60 ms
            // C:  < 200 ms
            // D:  < 400 ms
            // F:  >= 400 ms (oder Paketverlust > 5%)
            if (delta >= 400.0 || rep.OverallPacketLoss > 5.0) {
                rep.Grade = "F";
                rep.Verdict = "Kritischer Bufferbloat (+ " + delta.ToString("F1") + " ms)";
                rep.Description = "Extremer Latenzanstieg unter Last (400 ms oder mehr). Verursacht starke Lags in Video-Calls, Gaming und Echtzeitanwendungen. SQM zwingend empfohlen.";
                rep.GradeColor = "#EF4444";
            } else if (delta >= 200.0) {
                rep.Grade = "D";
                rep.Verdict = "Hoher Bufferbloat (+ " + delta.ToString("F1") + " ms)";
                rep.Description = "Starker Latenzanstieg (< 400 ms). Kann Video-Calls und Online-Gaming bei Hintergrundübertragungen spürbar beeinträchtigen.";
                rep.GradeColor = "#F97316";
            } else if (delta >= 60.0) {
                rep.Grade = "C";
                rep.Verdict = "Mäßiger Bufferbloat (+ " + delta.ToString("F1") + " ms)";
                rep.Description = "Spürbare Verzögerungen (< 200 ms). Die Verbindung reagiert träge, wenn andere im Haushalt große Dateien übertragen.";
                rep.GradeColor = "#F59E0B";
            } else if (delta >= 30.0) {
                rep.Grade = "B";
                rep.Verdict = "Guter Bufferbloat (+ " + delta.ToString("F1") + " ms)";
                rep.Description = "Geringer Latenzanstieg (< 60 ms). Die Verbindung bleibt auch bei hoher Auslastung reaktionsschnell.";
                rep.GradeColor = "#EAB308";
            } else if (delta >= 5.0) {
                rep.Grade = "A";
                rep.Verdict = "Sehr geringer Bufferbloat (+ " + delta.ToString("F1") + " ms)";
                rep.Description = "Sehr hohe Stabilität (< 30 ms). Kaum spürbare Verzögerungen, sehr reaktionsschnell unter Last.";
                rep.GradeColor = "#22C55E";
            } else {
                rep.Grade = "A+";
                rep.Verdict = "Perfekt — Nahezu null Bufferbloat (+ " + delta.ToString("F1") + " ms)";
                rep.Description = "Hervorragende Verbindung (< 5 ms)! Die Leitung puffert praktisch nicht. Maximaler E-Sports Vorteil und minimale Hit-Delay.";
                rep.GradeColor = "#10B981";
            }
        }

        // =========================================================================
        // MATHEMATISCHE HILFSFUNKTIONEN (100% REALE FORMELN)
        // =========================================================================

        private double CalculateMedian(List<double> list) {
            if (list == null || list.Count == 0) return 0;
            var sorted = new List<double>(list);
            sorted.Sort();
            int n = sorted.Count;
            if (n % 2 == 1) return sorted[n / 2];
            return (sorted[(n / 2) - 1] + sorted[n / 2]) / 2.0;
        }

        // RFC 3550 Interarrival Jitter Formel: Durchschnitt der absoluten Differenzen aufeinanderfolgender Pings
        private double CalculateJitter(List<double> list) {
            if (list == null || list.Count < 2) return 0;
            double sumDiff = 0;
            for (int i = 1; i < list.Count; i++) {
                sumDiff += Math.Abs(list[i] - list[i - 1]);
            }
            return sumDiff / (list.Count - 1);
        }

        // Standardabweichung (Sigma)
        private double CalculateStdDev(List<double> list) {
            if (list == null || list.Count < 2) return 0;
            double avg = list.Average();
            double sumSq = 0;
            for (int i = 0; i < list.Count; i++) {
                double diff = list[i] - avg;
                sumSq += diff * diff;
            }
            return Math.Sqrt(sumSq / list.Count);
        }

        // =========================================================================
        // UI UPDATE METHODS (DISPATCHER-SAFE)
        // =========================================================================

        private void ResetBufferbloatUI(string targetHostName) {
            if (window == null) return;
            window.Dispatcher.Invoke(() => {
                var mutedBrush = UIHelper.BrushMuted;
                var defaultBorderBrush = UIHelper.GetBrush("#252D3E");

                if (btnBbStartTest != null) {
                    btnBbStartTest.IsEnabled = true;
                    btnBbStartTest.Content = "⏹️ Benchmark abbrechen";
                }

                // Reset Chips
                HighlightPhaseChip(0);

                if (txtBbLiveIcon != null) txtBbLiveIcon.Text = "⏳";
                if (txtBbLivePhase != null) txtBbLivePhase.Text = "Initialisiere Sockets & DNS...";
                if (txtBbLivePercent != null) txtBbLivePercent.Text = "0 %";
                if (progressBbTest != null) progressBbTest.Value = 0;

                // Grade Card
                if (txtBbGradeBadge != null) {
                    txtBbGradeBadge.Text = "—";
                    txtBbGradeBadge.Foreground = mutedBrush;
                }
                if (borderBbGradeCard != null) {
                    borderBbGradeCard.BorderBrush = defaultBorderBrush;
                }
                if (txtBbGradeDelta != null) txtBbGradeDelta.Text = "+ — ms Bufferbloat-Anstieg";
                if (txtBbGradeVerdict != null) txtBbGradeVerdict.Text = "Messung läuft...";
                if (txtBbGradeDesc != null) txtBbGradeDesc.Text = "Warte auf Abschluss der Stresstest-Phasen für die Waveform-Auswertung.";

                // Säule 1
                if (txtBbUnloadedAvg != null) txtBbUnloadedAvg.Text = "— ms";
                if (txtBbUnloadedJitter != null) txtBbUnloadedJitter.Text = "— ms";
                if (txtBbUnloadedMinMax != null) txtBbUnloadedMinMax.Text = "— / — ms";
                if (txtBbUnloadedLoss != null) txtBbUnloadedLoss.Text = "0.0 %";

                // Säule 2
                if (txtBbDlSpeed != null) txtBbDlSpeed.Text = "— Mbit/s";
                if (txtBbDlPing != null) txtBbDlPing.Text = "— ms";
                if (txtBbDlDelta != null) {
                    txtBbDlDelta.Text = "+ — ms";
                    txtBbDlDelta.Foreground = mutedBrush;
                }
                if (txtBbDlMaxSpike != null) txtBbDlMaxSpike.Text = "— ms";

                // Säule 3
                if (txtBbUlSpeed != null) txtBbUlSpeed.Text = "— Mbit/s";
                if (txtBbUlPing != null) txtBbUlPing.Text = "— ms";
                if (txtBbUlDelta != null) {
                    txtBbUlDelta.Text = "+ — ms";
                    txtBbUlDelta.Foreground = mutedBrush;
                }
                if (txtBbUlMaxSpike != null) txtBbUlMaxSpike.Text = "— ms";

                // Telemetrie
                if (txtBbPacketsStats != null) txtBbPacketsStats.Text = "— / — (0.0% Verlust)";
                if (txtBbMaxSpike != null) txtBbMaxSpike.Text = "— ms";
                if (txtBbMedianStats != null) txtBbMedianStats.Text = "Idle — · DL — · UL —";
                if (txtBbStdDevStats != null) txtBbStdDevStats.Text = "Jitter: — ms · σ: — ms";
                if (txtBbTargetHostDisplay != null) txtBbTargetHostDisplay.Text = "Ziel: " + targetHostName;
            });
        }

        private static Brush GetDeltaBrush(double delta) {
            return delta < 5.0 ? UIHelper.BrushGreenText : (delta < 30.0 ? UIHelper.BrushAmberText : UIHelper.GetBrush("#FDA4AF"));
        }

        private void HighlightPhaseChip(int phase) {
            if (window == null) return;
            Action apply = () => {
                if (phaseChips == null) return;
                var defaultBg = UIHelper.GetBrush("#161B26");
                var defaultBorder = UIHelper.GetBrush("#252D3E");
                var defaultFg = UIHelper.BrushGrayText;

                Brush[] activeBgs = { UIHelper.BrushGreenBg, UIHelper.GetBrush("#251219"), UIHelper.GetBrush("#102A45"), UIHelper.GetBrush("#251630") };
                Brush[] activeBorders = { UIHelper.BrushGreenBorder, UIHelper.GetBrush("#F43F5E"), UIHelper.BrushBlueText, UIHelper.GetBrush("#A855F7") };
                Brush[] activeFgs = { UIHelper.BrushGreenText, UIHelper.GetBrush("#FDA4AF"), UIHelper.GetBrush("#7DD3FC"), UIHelper.GetBrush("#D8B4FE") };

                for (int i = 0; i < phaseChips.Length; i++) {
                    bool isActive = (i == phase - 1);
                    if (phaseChips[i] != null) {
                        phaseChips[i].Background = isActive ? activeBgs[i] : defaultBg;
                        phaseChips[i].BorderBrush = isActive ? activeBorders[i] : defaultBorder;
                    }
                    if (txtPhaseChips != null && i < txtPhaseChips.Length && txtPhaseChips[i] != null) {
                        txtPhaseChips[i].Foreground = isActive ? activeFgs[i] : defaultFg;
                    }
                }
            };
            if (window.Dispatcher.CheckAccess()) apply();
            else window.Dispatcher.BeginInvoke(DispatcherPriority.Background, apply);
        }

        private void UpdateLiveStatus(string icon, string text, double progressPercent) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => {
                if (txtBbLiveIcon != null) txtBbLiveIcon.Text = icon;
                if (txtBbLivePhase != null) txtBbLivePhase.Text = text;
                double clamped = Math.Min(100.0, Math.Max(0.0, progressPercent));
                if (txtBbLivePercent != null) txtBbLivePercent.Text = string.Format("{0:F0} %", clamped);
                if (progressBbTest != null) progressBbTest.Value = clamped;
            }));
        }

        private void UpdateIdleLivePing(double ping) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => {
                if (txtBbUnloadedAvg != null) txtBbUnloadedAvg.Text = string.Format("{0:F1} ms", ping);
            }));
        }

        private void UpdateIdleFinalUI(BufferbloatReport rep) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => {
                if (txtBbUnloadedAvg != null) txtBbUnloadedAvg.Text = string.Format("{0:F1} ms", rep.UnloadedAvg);
                if (txtBbUnloadedJitter != null) txtBbUnloadedJitter.Text = string.Format("{0:F1} ms", rep.UnloadedJitter);
                if (txtBbUnloadedMinMax != null) txtBbUnloadedMinMax.Text = string.Format("{0:F1} / {1:F1} ms", rep.UnloadedMin, rep.UnloadedMax);
                double loss = rep.UnloadedPingsSent > 0 ? ((rep.UnloadedPingsSent - rep.UnloadedPingsRecv) / (double)rep.UnloadedPingsSent) * 100.0 : 0.0;
                if (txtBbUnloadedLoss != null) {
                    txtBbUnloadedLoss.Text = string.Format("{0:F1} %", loss);
                    txtBbUnloadedLoss.Foreground = loss > 0.0 ? UIHelper.BrushRedText : UIHelper.BrushGreenText;
                }
            }));
        }

        private void UpdateDlLivePing(double livePing, double runningMedian, double baseline) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => {
                double val = runningMedian > 0 ? runningMedian : livePing;
                if (txtBbDlPing != null) txtBbDlPing.Text = string.Format("{0:F1} ms", val);
                double delta = Math.Max(0.0, val - baseline);
                if (txtBbDlDelta != null) {
                    txtBbDlDelta.Text = string.Format("+ {0:F1} ms", delta);
                    txtBbDlDelta.Foreground = GetDeltaBrush(delta);
                }
            }));
        }

        private void UpdateDlLiveSpeed(double curMbit) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => {
                if (txtBbDlSpeed != null) txtBbDlSpeed.Text = string.Format("{0:F1} Mbit/s", curMbit);
            }));
        }

        private void UpdateDlFinalUI(BufferbloatReport rep) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => {
                if (txtBbDlSpeed != null) txtBbDlSpeed.Text = string.Format("{0:F1} Mbit/s", rep.DownloadMbps);
                if (txtBbDlPing != null) txtBbDlPing.Text = string.Format("{0:F1} ms", rep.DownloadPingMedian);
                if (txtBbDlMaxSpike != null) txtBbDlMaxSpike.Text = string.Format("{0:F1} ms", rep.DownloadMaxSpike);
                if (txtBbDlDelta != null) {
                    txtBbDlDelta.Text = string.Format("+ {0:F1} ms", rep.DownloadDelta);
                    txtBbDlDelta.Foreground = GetDeltaBrush(rep.DownloadDelta);
                }
            }));
        }

        private void UpdateUlLivePing(double livePing, double runningMedian, double baseline) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => {
                double val = runningMedian > 0 ? runningMedian : livePing;
                if (txtBbUlPing != null) txtBbUlPing.Text = string.Format("{0:F1} ms", val);
                double delta = Math.Max(0.0, val - baseline);
                if (txtBbUlDelta != null) {
                    txtBbUlDelta.Text = string.Format("+ {0:F1} ms", delta);
                    txtBbUlDelta.Foreground = GetDeltaBrush(delta);
                }
            }));
        }

        private void UpdateUlLiveSpeed(double curMbit) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Background, (Action)(() => {
                if (txtBbUlSpeed != null) txtBbUlSpeed.Text = string.Format("{0:F1} Mbit/s", curMbit);
            }));
        }

        private void UpdateUlFinalUI(BufferbloatReport rep) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => {
                if (txtBbUlSpeed != null) txtBbUlSpeed.Text = string.Format("{0:F1} Mbit/s", rep.UploadMbps);
                if (txtBbUlPing != null) txtBbUlPing.Text = string.Format("{0:F1} ms", rep.UploadPingMedian);
                if (txtBbUlMaxSpike != null) txtBbUlMaxSpike.Text = string.Format("{0:F1} ms", rep.UploadMaxSpike);
                if (txtBbUlDelta != null) {
                    txtBbUlDelta.Text = string.Format("+ {0:F1} ms", rep.UploadDelta);
                    txtBbUlDelta.Foreground = GetDeltaBrush(rep.UploadDelta);
                }
            }));
        }

        private void UpdateFinalWaveformUI(BufferbloatReport rep) {
            if (window == null) return;
            window.Dispatcher.BeginInvoke(DispatcherPriority.Normal, (Action)(() => {
                var gradeBrush = UIHelper.GetBrush(rep.GradeColor);

                // Waveform Grade Card
                if (txtBbGradeBadge != null) {
                    txtBbGradeBadge.Text = rep.Grade;
                    txtBbGradeBadge.Foreground = gradeBrush;
                }
                if (borderBbGradeCard != null) {
                    borderBbGradeCard.BorderBrush = gradeBrush;
                }
                if (txtBbGradeDelta != null) {
                    txtBbGradeDelta.Text = string.Format("+ {0:F1} ms Bufferbloat-Anstieg", rep.MaxDelta);
                    txtBbGradeDelta.Foreground = gradeBrush;
                }
                if (txtBbGradeVerdict != null) {
                    txtBbGradeVerdict.Text = rep.Verdict;
                }
                if (txtBbGradeDesc != null) {
                    txtBbGradeDesc.Text = rep.Description;
                }

                // Telemetrie Details
                int totalSent = rep.UnloadedPingsSent + rep.DownloadPingsSent + rep.UploadPingsSent;
                int totalRecv = rep.UnloadedPingsRecv + rep.DownloadPingsRecv + rep.UploadPingsRecv;
                if (txtBbPacketsStats != null) {
                    txtBbPacketsStats.Text = string.Format("{0} gesendet · {1} empfangen ({2:F1}% Verlust)", totalSent, totalRecv, rep.OverallPacketLoss);
                    txtBbPacketsStats.Foreground = rep.OverallPacketLoss > 0.0 ? UIHelper.BrushRedText : UIHelper.BrushGreenText;
                }
                if (txtBbMaxSpike != null) {
                    txtBbMaxSpike.Text = string.Format("{0:F1} ms Peak", rep.OverallMaxSpike);
                }
                if (txtBbMedianStats != null) {
                    txtBbMedianStats.Text = string.Format("Idle {0:F1} ms · DL {1:F1} ms · UL {2:F1} ms", rep.UnloadedMedian, rep.DownloadPingMedian, rep.UploadPingMedian);
                }
                if (txtBbStdDevStats != null) {
                    txtBbStdDevStats.Text = string.Format("Jitter: {0:F1} ms · σ: {1:F2} ms", rep.UnloadedJitter, rep.UnloadedStdDev);
                }
                if (txtBbTargetHostDisplay != null) {
                    txtBbTargetHostDisplay.Text = "Ziel: " + rep.TargetHostName;
                }
            }));
        }
    }
}