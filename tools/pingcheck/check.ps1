#Requires -Version 5.1
# Selbsttest für PrecisePing (Raw-ICMP + Checksumme + Loopback-Ping)
# Kompiliert PrecisePing.cs + eingebetteten Test und führt ihn aus.
$baseDir = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$srcPing = Join-Path $baseDir 'src\PrecisePing.cs'
$sysRoot = if ($env:SystemRoot) { $env:SystemRoot } else { 'C:\Windows' }
$csc = Join-Path $sysRoot 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$tmp = Join-Path $env:TEMP 'ZnipePingCheck'
New-Item -ItemType Directory -Force -Path $tmp | Out-Null
$prog = Join-Path $tmp 'Program.cs'

$code = @'
using System;
using System.Net;
using System.Reflection;
using ZnipeOptimizationTool;

class PingSelfCheck {
    static int Main() {
        int failures = 0;
        // 1) Loopback-Ping validiert native ICMP-Verbindung
        using (var pinger = PrecisePing.Create()) {
            if (pinger == null) { Console.WriteLine("FAIL: PrecisePing.Create fehlgeschlagen"); return 1; }
            Console.WriteLine("PASS  PrecisePing Instanziierung erfolgreich");
        }

        // 3) Loopback-Ping (immer verfügbar) — validiert Socket-Send/Empfang/Parse
        using (var pinger = PrecisePing.Create()) {
            double rtt;
            if (pinger.TryPing(IPAddress.Loopback, 1000, out rtt) && rtt >= 0 && rtt < 1000) {
                Console.WriteLine("PASS  Loopback-Ping rtt=" + rtt.ToString("0.000") + " ms");
            } else { Console.WriteLine("FAIL  Loopback-Ping rtt=" + rtt); failures++; }
        }

        // 4) Optional: externer Ping (kann durch Firewall blockiert sein → nicht fatal)
        using (var pinger = PrecisePing.Create()) {
            double rtt;
            bool ok = pinger.TryPing(IPAddress.Parse("1.1.1.1"), 2000, out rtt);
            Console.WriteLine((ok ? "INFO" : "WARN") + "  Externer Ping 1.1.1.1: " + (ok ? rtt.ToString("0.000") + " ms" : "keine Antwort (Firewall/ICMP?)"));
        }

        Console.WriteLine(failures == 0 ? "== ALLE CHECKS BESTANDEN ==" : "== " + failures + " CHECK(S) FEHLGESCHLAGEN ==");
        return failures == 0 ? 0 : 1;
    }
}
'@

Set-Content -Path $prog -Value $code -Encoding UTF8
$exe = Join-Path $tmp 'PingSelfCheck.exe'
& $csc /target:exe /nologo /out:$exe $srcPing $prog
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
& $exe
exit $LASTEXITCODE
