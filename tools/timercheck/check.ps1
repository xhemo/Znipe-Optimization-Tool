#Requires -Version 5.1
# Selbsttest für TimerPrecisionProbe (NtQueryTimerResolution + Sleep(1)-Präzision)
# Prüft: plausible Resolution, konsistente Messung (idle), Stabilität unter CPU-Last.
$baseDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$srcProbe = Join-Path $baseDir 'src\TimerPrecisionProbe.cs'
$sysRoot = if ($env:SystemRoot) { $env:SystemRoot } else { 'C:\Windows' }
$csc = Join-Path $sysRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tmp = Join-Path $env:TEMP 'ZnipeTimerCheck'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$prog = Join-Path $tmp 'Program.cs'

$code = @'
using System;
using System.Threading;
using ZnipeOptimizationTool;

class TimerSelfCheck {
    static int Main() {
        int failures = 0;

        // 1) Resolution plausibel?
        double cur, min, max;
        TimerPrecisionProbe.GetResolutions(out cur, out min, out max);
        Console.WriteLine("INFO  Resolution: cur=" + cur.ToString("0.000") + "ms  min=" + min.ToString("0.000") + "ms  max=" + max.ToString("0.000") + "ms");
        if (cur >= 0.4 && cur <= 20.0 && min >= 0.4 && min <= 1.0 && max >= 15.0 && max <= 16.0) {
            Console.WriteLine("PASS  Resolution plausibel (min~0.5ms, max~15.6ms)");
        } else {
            Console.WriteLine("FAIL  Resolution unplausibel"); failures++;
        }

        // 2) Messung idle (3 Läufe) — konsistent? (Avg-Delta + STDEV wie MeasureSleep.exe)
        double r1, s1, d1, v1, r2, s2, d2, v2, r3, s3, d3, v3;
        TimerPrecisionProbe.MeasureSleepPrecision(out r1, out s1, out d1, out v1);
        TimerPrecisionProbe.MeasureSleepPrecision(out r2, out s2, out d2, out v2);
        TimerPrecisionProbe.MeasureSleepPrecision(out r3, out s3, out d3, out v3);

        Console.WriteLine("INFO  Idle 1: sleep=" + s1.ToString("0.000") + "ms  delta=" + d1.ToString("0.000") + "ms  stdev=" + v1.ToString("0.000") + "ms");
        Console.WriteLine("INFO  Idle 2: sleep=" + s2.ToString("0.000") + "ms  delta=" + d2.ToString("0.000") + "ms  stdev=" + v2.ToString("0.000") + "ms");
        Console.WriteLine("INFO  Idle 3: sleep=" + s3.ToString("0.000") + "ms  delta=" + d3.ToString("0.000") + "ms  stdev=" + v3.ToString("0.000") + "ms");

        double minDelta = Math.Min(d1, Math.Min(d2, d3));
        double maxDelta = Math.Max(d1, Math.Max(d2, d3));
        if (v1 >= 0 && v2 >= 0 && v3 >= 0) {
            Console.WriteLine("PASS  STDEV >= 0");
        } else {
            Console.WriteLine("FAIL  STDEV negativ"); failures++;
        }
        if (maxDelta - minDelta <= 1.0) {
            Console.WriteLine("PASS  Idle-Messung konsistent (Spreizung " + (maxDelta - minDelta).ToString("0.000") + "ms)");
        } else {
            Console.WriteLine("FAIL  Idle-Messung inkonsistent"); failures++;
        }

        // 3) Messung unter CPU-Last (simuliert GUI-Rendering) — muss stabil bleiben
        using (var busyCts = new CancellationTokenSource()) {
            var busy = new Thread(() => {
                int x = 0;
                while (!busyCts.Token.IsCancellationRequested) { x++; }
            });
            busy.IsBackground = true;
            busy.Priority = ThreadPriority.Normal;
            busy.Start();

            double br1, bs1, bd1, bv1, br2, bs2, bd2, bv2;
            TimerPrecisionProbe.MeasureSleepPrecision(out br1, out bs1, out bd1, out bv1);
            TimerPrecisionProbe.MeasureSleepPrecision(out br2, out bs2, out bd2, out bv2);

            busyCts.Cancel();
            busy.Join();

            Console.WriteLine("INFO  Load 1: sleep=" + bs1.ToString("0.000") + "ms  delta=" + bd1.ToString("0.000") + "ms  stdev=" + bv1.ToString("0.000") + "ms");
            Console.WriteLine("INFO  Load 2: sleep=" + bs2.ToString("0.000") + "ms  delta=" + bd2.ToString("0.000") + "ms  stdev=" + bv2.ToString("0.000") + "ms");

            double allMin = Math.Min(minDelta, Math.Min(bd1, bd2));
            double allMax = Math.Max(maxDelta, Math.Max(bd1, bd2));
            if (allMax - allMin <= 1.5) {
                Console.WriteLine("PASS  Messung unter Last stabil (CPU-/GUI-Last beeinflusst das Ergebnis nicht)");
            } else {
                Console.WriteLine("WARN  Abweichung unter Last (Spreizung " + (allMax - allMin).ToString("0.000") + "ms)");
            }
        }

        // 4) Verdict (gleiche Schwelle wie im UI: sleptMs<=2.5 && delta<2.0)
        string verdict = (s1 <= 2.5 && d1 < 2.0) ? "OPTIMAL (~1ms)" : "GEDROSSELT";
        Console.WriteLine("INFO  Verdict (Lauf 1): " + verdict);

        Console.WriteLine(failures == 0 ? "== ALLE CHECKS BESTANDEN ==" : "== " + failures + " CHECK(S) FEHLGESCHLAGEN ==");
        return failures == 0 ? 0 : 1;
    }
}
'@

Set-Content -Path $prog -Value $code -Encoding UTF8
$exe = Join-Path $tmp 'TimerSelfCheck.exe'
& $csc /target:exe /nologo /out:$exe $srcProbe $prog
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $exe
exit $LASTEXITCODE
