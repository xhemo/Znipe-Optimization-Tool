using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// GUI-freier Mess-Kern für die Timer-Resolution-Engine.
    /// Misst die aktuelle Kernel-Timer-Resolution (NtQueryTimerResolution) und die
    /// reale Sleep(1)-Präzision über QueryPerformanceCounter (Stopwatch) auf einem
    /// priorisierten Thread — unabhängig von UI- oder ThreadPool-Scheduling.
    /// </summary>
    public static class TimerPrecisionProbe {
        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtQueryTimerResolution(out uint MinimumResolution, out uint MaximumResolution, out uint CurrentResolution);

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern void Sleep(uint dwMilliseconds);



        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetCurrentThread();

        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern UIntPtr SetThreadAffinityMask(IntPtr hThread, UIntPtr dwThreadAffinityMask);

        [DllImport("ntdll.dll", SetLastError = true)]
        private static extern int NtSetTimerResolution(uint DesiredResolution, bool SetResolution, out uint CurrentResolution);

        public static void NativeSleep(uint dwMilliseconds) {
            Sleep(dwMilliseconds);
        }

        public static bool SetResolution(double targetMs, out double actualMs) {
            actualMs = 0;
            try {
                uint desiredTicks = (uint)Math.Round(targetMs * 10000.0);
                uint prevTicks;
                int status = NtSetTimerResolution(desiredTicks, true, out prevTicks);
                uint coarseTicks, fineTicks, curTicks;
                if (NtQueryTimerResolution(out coarseTicks, out fineTicks, out curTicks) == 0) {
                    actualMs = curTicks / 10000.0;
                } else {
                    actualMs = targetMs;
                }
                return status == 0;
            } catch {
                return false;
            }
        }

        public static bool ResetResolution() {
            try {
                uint actualTicks;
                int status = NtSetTimerResolution(0, false, out actualTicks);
                return status == 0;
            } catch {
                return false;
            }
        }

        public static void GetResolutions(out double currentMs, out double minMs, out double maxMs) {
            currentMs = 15.625;
            minMs = 0.500;
            maxMs = 15.625;
            try {
                uint coarseTicks, fineTicks, curTicks;
                if (NtQueryTimerResolution(out coarseTicks, out fineTicks, out curTicks) == 0) {
                    currentMs = curTicks / 10000.0;
                    maxMs = coarseTicks / 10000.0;  // 15.625ms
                    minMs = fineTicks / 10000.0;    // 0.5ms
                }
            } catch {}
        }

        /// <summary>
        /// 1:1 identische Messung zu MeasureSleep.exe im Live-Modus (Zeilen 84-90):
        /// 1 einzelnes Sleep(1) via nativem kernel32!Sleep mit QueryPerformanceCounter
        /// auf ThreadPriority.Highest, um jedes GUI-, GC- oder ThreadPool-Rauschen zu eliminieren.
        /// </summary>
        public static void MeasureSingleSleep(out double resMs, out double sleptMs, out double deltaMs) {
            resMs = 0;
            sleptMs = 0;
            deltaMs = 0;
            try {
                double cur, min, max;
                GetResolutions(out cur, out min, out max);
                resMs = cur;

                var prevPriority = Thread.CurrentThread.Priority;
                IntPtr hThread = GetCurrentThread();
                UIntPtr prevAffinity = UIntPtr.Zero;
                try {
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    UIntPtr targetAffinity = (UIntPtr)(1UL << (Environment.ProcessorCount > 2 ? 2 : 0));
                    prevAffinity = SetThreadAffinityMask(hThread, targetAffinity);

                    long start = Stopwatch.GetTimestamp();
                    Sleep(1);
                    long end = Stopwatch.GetTimestamp();

                    double deltaS = (double)(end - start) / Stopwatch.Frequency;
                    sleptMs = deltaS * 1000.0;
                    deltaMs = sleptMs - 1.0;
                } finally {
                    if (prevAffinity != UIntPtr.Zero) {
                        try { SetThreadAffinityMask(hThread, prevAffinity); } catch {}
                    }
                    Thread.CurrentThread.Priority = prevPriority;
                }
            } catch {}
        }

        /// <summary>
        /// 1:1 identische Messung zu MeasureSleep.exe --samples (Zeilen 76-137):
        /// Misst 'samples' Durchläufe, verwirft das allererste Sample wegen mid-tick Jitter,
        /// und berechnet Mittelwert sowie Stichproben-Standardabweichung (Bessel-Korrektur N-1).
        /// </summary>
        public static void MeasureSleepPrecision(out double resMs, out double avgSleepMs, out double avgDeltaMs, out double stdevMs) {
            MeasureSleepPrecision(out resMs, out avgSleepMs, out avgDeltaMs, out stdevMs, 20);
        }

        public static void MeasureSleepPrecision(out double resMs, out double avgSleepMs, out double avgDeltaMs, out double stdevMs, int samples) {
            double maxDelta;
            int spikes;
            MeasureSleepPrecision(out resMs, out avgSleepMs, out avgDeltaMs, out stdevMs, out maxDelta, out spikes, samples);
        }

        public static void MeasureSleepPrecision(out double resMs, out double avgSleepMs, out double avgDeltaMs, out double stdevMs, out double maxDeltaMs, out int spikeCount, int samples) {
            double p99;
            MeasureSleepPrecision(out resMs, out avgSleepMs, out avgDeltaMs, out stdevMs, out maxDeltaMs, out p99, out spikeCount, samples);
        }

        public static void MeasureSleepPrecision(out double resMs, out double avgSleepMs, out double avgDeltaMs, out double stdevMs, out double maxDeltaMs, out double p99DeltaMs, out int spikeCount, int samples, Func<bool> isCancelled = null, Action<int, int, int> onProgress = null) {
            resMs = 0;
            avgSleepMs = 0;
            avgDeltaMs = 0;
            stdevMs = 0;
            maxDeltaMs = 0;
            p99DeltaMs = 0;
            spikeCount = 0;
            try {
                double cur, min, max;
                GetResolutions(out cur, out min, out max);
                resMs = cur;

                if (samples < 2) samples = 2;

                var prevPriority = Thread.CurrentThread.Priority;
                IntPtr hThread = GetCurrentThread();
                UIntPtr prevAffinity = UIntPtr.Zero;
                try {
                    Thread.CurrentThread.Priority = ThreadPriority.Highest;
                    // Pin to isolated core (Core 2) to eliminate context switching and cross-core APIC drift
                    UIntPtr targetAffinity = (UIntPtr)(1UL << (Environment.ProcessorCount > 2 ? 2 : 0));
                    prevAffinity = SetThreadAffinityMask(hThread, targetAffinity);

                    double[] delays = new double[samples];
                    int completedSamples = 0;
                    int runningSpikes = 0;
                    int stride = samples >= 100 ? 5 : (samples >= 40 ? 2 : 1);

                    for (int i = 0; i < samples; i++) {
                        if (isCancelled != null && isCancelled()) break;

                        long start = Stopwatch.GetTimestamp();
                        Sleep(1);
                        long end = Stopwatch.GetTimestamp();

                        double deltaS = (double)(end - start) / Stopwatch.Frequency;
                        double d = (deltaS * 1000.0) - 1.0;
                        delays[i] = d;
                        completedSamples++;

                        if (i > 0 && d >= 0.250) {
                            runningSpikes++;
                        }

                        if (onProgress != null && (completedSamples == 1 || completedSamples % stride == 0 || completedSamples == samples)) {
                            try { onProgress(completedSamples, samples, runningSpikes); } catch {}
                        }

                        if (i < samples - 1) {
                            for (int s = 0; s < 10; s++) {
                                if (isCancelled != null && isCancelled()) break;
                                Sleep(10); // 10x 10ms = 100ms (10 Hz wie MeasureSleep.cpp), alle 10ms abbrechbar!
                            }
                        }
                    }

                    if (completedSamples < 2) return;

                    // Wie in MeasureSleep.cpp Zeile 109: "discard first trial due to first sleep call mid-tick"
                    int validCount = completedSamples - 1;
                    double sum = 0.0;
                    double maxD = double.MinValue;
                    int spikes = 0;

                    for (int i = 1; i < completedSamples; i++) {
                        double d = delays[i];
                        sum += d;
                        if (d > maxD) maxD = d;
                        if (d >= 0.250) spikes++; // Sleep dauerte >= 1.25 ms (3. Tick Miss / Stutter!)
                    }
                    avgDeltaMs = sum / validCount;
                    avgSleepMs = avgDeltaMs + 1.0;
                    maxDeltaMs = maxD > double.MinValue ? maxD : 0.0;
                    spikeCount = spikes;

                    // P99 Tail Latency Percentile
                    double[] sorted = new double[validCount];
                    Array.Copy(delays, 1, sorted, 0, validCount);
                    Array.Sort(sorted);
                    int p99Idx = (int)Math.Min(validCount - 1, Math.Max(0, Math.Ceiling(validCount * 0.99) - 1));
                    p99DeltaMs = sorted[p99Idx];

                    // Stichproben-Standardabweichung mit Bessel-Korrektur (N - 1) wie MeasureSleep.cpp Zeile 129
                    double sumSqDiff = 0.0;
                    for (int i = 1; i < completedSamples; i++) {
                        double diff = delays[i] - avgDeltaMs;
                        sumSqDiff += diff * diff;
                    }
                    stdevMs = validCount > 1 ? Math.Sqrt(sumSqDiff / (validCount - 1)) : 0.0;
                } finally {
                    if (prevAffinity != UIntPtr.Zero) {
                        SetThreadAffinityMask(hThread, prevAffinity);
                    }
                    Thread.CurrentThread.Priority = prevPriority;
                }
            } catch {}
        }
    }

    public class TimerBenchmarkResult {
        public double RequestedResolutionMs { get; set; }
        public double ActualResolutionMs { get; set; }
        public double AvgSleepMs { get; set; }
        public double AvgDeltaMs { get; set; }
        public double StdevMs { get; set; }
        public double MaxDeltaMs { get; set; }
        public int SpikeCount { get; set; }
        public bool IsOptimal { get; set; }
    }
}
