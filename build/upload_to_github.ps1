[Console]::OutputEncoding = [System.Text.Encoding]::UTF8
$projectDir = if ($PSScriptRoot) { (Resolve-Path (Join-Path $PSScriptRoot "..")).Path } else { $PWD.Path }
Set-Location $projectDir

$repoUrl = "https://github.com/xhemo/Znipe-Optimization-Tool.git"
$git = (Get-Command git -ErrorAction SilentlyContinue).Source
if (-not $git) {
    $git = @(
        (Join-Path $env:ProgramFiles 'Git\cmd\git.exe'),
        (if (${env:ProgramFiles(x86)}) { Join-Path ${env:ProgramFiles(x86)} 'Git\cmd\git.exe' }),
        (Join-Path $env:LOCALAPPDATA 'Programs\Git\cmd\git.exe')
    ) | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}
if (-not $git) {
    Write-Error "Git wurde nicht gefunden. Bitte Git installieren."
    exit 1
}

Write-Host "==========================================================" -ForegroundColor Cyan
Write-Host "   ZNIPE OPTIMIZATION TOOL - FULL REPO RESET & UPLOAD     " -ForegroundColor Yellow
Write-Host "==========================================================" -ForegroundColor Cyan

# 1. Bereinige temporaere Dateien
Write-Host "-> Bereinige temporaere Dateien..." -ForegroundColor Cyan
$cleanDirs = @("scratch", ".vs", "bin", "obj")
foreach ($d in $cleanDirs) {
    $target = Join-Path $projectDir $d
    if (Test-Path $target) {
        Remove-Item -Recurse -Force $target -ErrorAction SilentlyContinue
    }
}
Get-ChildItem $projectDir -Recurse -Include *.log, *.tmp, *.old -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue

# 2. Pruefe Executable
$exePath = Join-Path $projectDir "ZnipeOptimizationTool.exe"
if (-not (Test-Path $exePath)) {
    Write-Host "-> ZnipeOptimizationTool.exe nicht gefunden. Kompiliere..." -ForegroundColor Yellow
    $buildScript = Join-Path $PSScriptRoot "build.ps1"
    if (Test-Path $buildScript) {
        & $buildScript -NoRun
    }
}

# 3. Git-Historie komplett loeschen und als Root neu initialisieren
Write-Host "-> Loesche bisherige Git-Historie vollstaendig..." -ForegroundColor Cyan
if (Test-Path (Join-Path $projectDir ".git")) {
    try {
        Get-ChildItem (Join-Path $projectDir ".git") -Recurse -Force -ErrorAction SilentlyContinue | ForEach-Object { $_.Attributes = 'Normal' }
        Remove-Item -Recurse -Force (Join-Path $projectDir ".git") -ErrorAction SilentlyContinue
    } catch {}
}

Write-Host "-> Initialisiere frisches Git-Repository..." -ForegroundColor Cyan
& $git init -b main | Out-Null
& $git config user.name "xhemo" | Out-Null
& $git config user.email "xhemo@github.com" | Out-Null
& $git remote add origin $repoUrl | Out-Null

# 4. Alle Dateien stagen und einzigen initialen Commit erstellen
& $git add -A | Out-Null
$commitMsg = "Znipe Optimization Tool v1.0.0 - Full Release"
& $git commit -m $commitMsg | Out-Null
Write-Host "-> Einziger sauberer Root-Commit erstellt: $commitMsg" -ForegroundColor Green

# 5. Remote-Tags loeschen fuer absolut sauberen GitHub-Stand
Write-Host "-> Bereinige alte Remote-Tags..." -ForegroundColor Cyan
try {
    $remoteTags = & $git ls-remote --tags origin
    if ($remoteTags) {
        $tagNames = ($remoteTags | ForEach-Object { ($_ -split '\s+')[1] -replace '^refs/tags/', '' -replace '\^\{\}$', '' }) | Select-Object -Unique | Where-Object { $_ }
        foreach ($t in $tagNames) {
            Write-Host "   Loesche Remote-Tag: $t" -ForegroundColor Yellow
            & $git push origin --delete $t 2>$null | Out-Null
        }
    }
} catch {}

# 6. Force-Push auf origin/main (ueberschreibt GitHub komplett)
Write-Host "-> Sende frischen Stand an GitHub (main --force)..." -ForegroundColor Yellow
& $git push -u origin main --force

if ($LASTEXITCODE -eq 0) {
    Write-Host "[OK] Repository auf GitHub komplett ersetzt und synchronisiert!" -ForegroundColor Green

    # 7. GitHub Token abrufen fuer Releases
    $token = $env:GITHUB_TOKEN
    if (-not $token) {
        try {
            $pinfo = New-Object System.Diagnostics.ProcessStartInfo
            $pinfo.FileName = $git
            $pinfo.Arguments = "credential fill"
            $pinfo.UseShellExecute = $false
            $pinfo.RedirectStandardInput = $true
            $pinfo.RedirectStandardOutput = $true
            $pinfo.CreateNoWindow = $true
            $proc = [System.Diagnostics.Process]::Start($pinfo)
            $proc.StandardInput.WriteLine("protocol=https`nhost=github.com`n")
            $proc.StandardInput.Flush()
            $proc.StandardInput.Close()
            $out = $proc.StandardOutput.ReadToEnd()
            $proc.WaitForExit()
            if ($out -match 'password=([^\r\n]+)') {
                $token = $matches[1].Trim()
            }
        } catch {}
    }

    # 8. GitHub Release & Asset Update
    if ($token -and (Test-Path $exePath)) {
        Write-Host "-> Bereite GitHub Release vor..." -ForegroundColor Cyan
        try {
            [Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
            $headers = @{ 
                "Authorization" = "token $token"
                "User-Agent" = "ZnipeOptimizationTool"
                "Accept" = "application/vnd.github.v3+json" 
            }
            $repo = "xhemo/Znipe-Optimization-Tool"
            $tag = "v1.0.0"

            # Alte Releases loeschen
            Write-Host "-> Entferne bestehende GitHub Releases..." -ForegroundColor Cyan
            try {
                $allReleases = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Headers $headers -Method Get
                if ($allReleases) {
                    foreach ($rel in $allReleases) {
                        Write-Host "   Entferne Release $($rel.tag_name) (ID: $($rel.id))..." -ForegroundColor Yellow
                        Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases/$($rel.id)" -Headers $headers -Method Delete
                    }
                }
            } catch {}

            # Altes Tag via API loeschen falls noch vorhanden
            try {
                Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/git/refs/tags/$tag" -Headers $headers -Method Delete
            } catch {}

            # Frisches Release erstellen
            Write-Host "-> Erstelle frisches Release $tag..." -ForegroundColor Cyan
            $releaseBody = @"
# ⚡ Znipe Optimization Tool v1.0.0

Das kompromisslose, hochperformante Windows- & Hardware-Tuning-Werkzeug fuer Gamer, Enthusiasten und Power-User.

### 🌟 Technische Highlights & Features:
- **100% Native Single Executable:** Kein Installer, keine externen NuGet-Pakete, keine Drittanbieter-DLLs.
- **Kernel Timer Resolution:** Praezise 0.500 ms Timer-Konfiguration & hochaufloesende Microsecond-Sleep-Jitter-Messung.
- **Multi-Monitor CCD Engine:** Live-Umschaltung von bis zu 540 Hz Bildwiederholraten direkt aus dem Dropdown.
- **Hardware Telemetrie:** Native NVML-Anbindung fuer Echtzeit-Graphen (CPU Tdie/Tccd, GPU Watt, VRAM, RAM).
- **GPU Partner-Erkennung:** Automatische Aufschluesselung via PCI Subsystem-IDs (z. B. MSI SUPRIM X, ASUS ROG Strix).
- **Intelligente BIOS/UEFI-Pruefung:** Live-Status fuer Secure Boot, Resizable BAR, intelligente LAN/WLAN-Umschaltung und Bluetooth-Radio.
- **USB Topologie:** Analyse von Host-Controllern, Root-Hubs und CPU-Direct Latenz-Ebenen samt MouseDataQueueSize-Tuning.
- **Bufferbloat Engine:** Latenz-Stresstest unter Volllast mit transparentem A+ bis F Rating.
- **Nvidia GPU Limits:** Direktes Power- & Temperature-Limiting im Treiber via NVML mit integriertem Autostart-Daemon.

### 📦 Download & Ausfuehrung:
Die Datei **ZnipeOptimizationTool.exe** herunterladen und mit Administratorrechten starten. Keine Installation erforderlich.
"@

            $payload = @{
                tag_name = $tag
                target_commitish = "main"
                name = "Znipe Optimization Tool v1.0.0"
                body = $releaseBody
                draft = $false
                prerelease = $false
            }
            $bodyBytes = [System.Text.Encoding]::UTF8.GetBytes(($payload | ConvertTo-Json))
            $release = Invoke-RestMethod -Uri "https://api.github.com/repos/$repo/releases" -Headers $headers -Method Post -Body $bodyBytes -ContentType "application/json; charset=utf-8"

            if ($release) {
                Write-Host "-> Lade ZnipeOptimizationTool.exe als Release-Asset hoch..." -ForegroundColor Cyan
                $uploadUri = "https://uploads.github.com/repos/$repo/releases/$($release.id)/assets?name=ZnipeOptimizationTool.exe"
                $bytes = [System.IO.File]::ReadAllBytes($exePath)
                $uploadRes = Invoke-RestMethod -Uri $uploadUri -Headers $headers -Method Post -Body $bytes -ContentType "application/octet-stream"
                Write-Host "[OK] ZnipeOptimizationTool.exe erfolgreich im GitHub Release hochgeladen!" -ForegroundColor Green
                Write-Host "     Download: $($uploadRes.browser_download_url)" -ForegroundColor Cyan
                Write-Host "     Release:  $($release.html_url)" -ForegroundColor Cyan
            }
        } catch {
            Write-Warning "Release-Asset konnte nicht hochgeladen werden: $_"
        }
    }
} else {
    Write-Warning "Push fehlgeschlagen. Bitte GitHub-Anmeldung pruefen."
}
