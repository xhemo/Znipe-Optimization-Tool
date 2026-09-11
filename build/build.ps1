#Requires -Version 5.1
param(
    [switch]$NoRun
)
<#
    .SYNOPSIS
    100% Pure Native C# .NET WPF Direct-Root Build Pipeline
    Unterstuetzt Live-Hot-Reload & automatisches Einbetten von ZLogo
#>

$baseDir = Split-Path $PSScriptRoot -Parent
$srcDir  = Join-Path $baseDir 'src'
$rootExe = Join-Path $baseDir 'ZnipeOptimizationTool.exe'
$oldExe  = Join-Path $baseDir 'ZnipeOptimizationTool.old'

$sysRoot      = if ($env:SystemRoot) { $env:SystemRoot } elseif ($env:windir) { $env:windir } else { 'C:\Windows' }
$frameworkDir = Join-Path $sysRoot 'Microsoft.NET\Framework64\v4.0.30319'
$wpfDir       = Join-Path $frameworkDir 'WPF'
$cscPath      = Join-Path $frameworkDir 'csc.exe'

$manifestPath = Join-Path $srcDir 'app.manifest'
$xamlPath     = Join-Path $srcDir 'MainWindow.xaml'
$appCsPath    = Join-Path $srcDir 'App.cs'
$iconIcoPath  = Join-Path $srcDir 'app.ico'
$iconPngPath  = Join-Path $srcDir 'app_icon.png'

Write-Host '==========================================================' -ForegroundColor Cyan
Write-Host '   ZNIPE OPTIMIZATION TOOL - DIRECT ROOT BUILD            ' -ForegroundColor Yellow
Write-Host '==========================================================' -ForegroundColor Cyan
Write-Host ''

# 1. Beende alle laufenden Instanzen (auch wenn mit Admin-Rechten gestartet)
$running = Get-Process -Name 'ZnipeOptimizationTool' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host '-> Beende laufende Instanz(en) von ZnipeOptimizationTool...' -ForegroundColor Yellow

    # 1a. Signalisiere der App sofortiges Beenden (funktioniert auch ohne Admin-Rechte)
    try {
        $evt = [System.Threading.EventWaitHandle]::OpenExisting('Global\ZnipeOptimizationTool_CloseEvent')
        $evt.Set() | Out-Null
        $evt.Dispose()
    } catch {}

    # 1b. Standard-Stop
    Stop-Process -Name 'ZnipeOptimizationTool' -Force -ErrorAction SilentlyContinue
    $null = Start-Process -FilePath 'taskkill.exe' -ArgumentList '/F /IM ZnipeOptimizationTool.exe /T' -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue

    # 1c. Falls Instanz mit erhoehten Rechten laeuft: Kill via RunAs
    if (Get-Process -Name 'ZnipeOptimizationTool' -ErrorAction SilentlyContinue) {
        Write-Host '-> Erzwinge Beenden mit Administrator-Rechten...' -ForegroundColor Yellow
        Start-Process -FilePath 'taskkill.exe' -ArgumentList '/F /IM ZnipeOptimizationTool.exe /T' -Verb RunAs -WindowStyle Hidden -Wait -ErrorAction SilentlyContinue
    }

    $running | Wait-Process -Timeout 3 -ErrorAction SilentlyContinue
}

$runningCt = Get-Process -Name 'CoreTemp' -ErrorAction SilentlyContinue | Where-Object {
    try { $_.Path -like "*ZnipeSensors*" } catch { $false }
}
if ($runningCt) {
    Write-Host '-> Beende verwaiste CoreTemp-Sensorinstanz...' -ForegroundColor Yellow
    Stop-Process -InputObject $runningCt -Force -ErrorAction SilentlyContinue
    $runningCt | Wait-Process -Timeout 2 -ErrorAction SilentlyContinue
}

$cacheSensors = Join-Path $env:TEMP 'ZnipeSensors'
if (Test-Path $cacheSensors) {
    Get-ChildItem -Path $cacheSensors -Filter 'CT-Log*.csv' -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
}

# 2. Modulare XAML-Assembly & native Ressourcen
$xamlTemplatePath = Join-Path $srcDir 'xaml\Window.xaml'
if (Test-Path $xamlTemplatePath) {
    Write-Host '-> Assembliere modulare XAML-Dateien (src/xaml/)...' -ForegroundColor White
    $template = [System.IO.File]::ReadAllText($xamlTemplatePath, [System.Text.Encoding]::UTF8)
    $assembled = [System.Text.RegularExpressions.Regex]::Replace($template, '<!-- \[INCLUDE:([^\]]+)\] -->', {
        param($m)
        $incPath = Join-Path (Join-Path $srcDir 'xaml') $m.Groups[1].Value
        if (Test-Path $incPath) { return [System.IO.File]::ReadAllText($incPath, [System.Text.Encoding]::UTF8) }
        Write-Warning "XAML-Include nicht gefunden: $incPath"
        return ""
    })
    [System.IO.File]::WriteAllText($xamlPath, $assembled, [System.Text.Encoding]::UTF8)
}
Write-Host '-> Bereite native Ressourcen fuer XAML-Layout & ZLogo vor...' -ForegroundColor White

# 3. Live-Replace Vorbereitung
if (Test-Path $rootExe) {
    Remove-Item $oldExe -Force -ErrorAction SilentlyContinue
    Move-Item $rootExe $oldExe -Force -ErrorAction SilentlyContinue
}

# 4. Kompiliere direkt nach ZnipeOptimizationTool.exe
Write-Host '-> Kompiliere 100% natives C# Release mit ZLogo Exe-Icon...' -ForegroundColor White

$refList = @(
    (Join-Path $wpfDir 'PresentationFramework.dll'),
    (Join-Path $wpfDir 'PresentationCore.dll'),
    (Join-Path $wpfDir 'WindowsBase.dll'),
    (Join-Path $frameworkDir 'System.Xaml.dll'),
    (Join-Path $frameworkDir 'System.Xml.dll'),
    (Join-Path $frameworkDir 'System.Management.dll'),
    (Join-Path $frameworkDir 'System.Drawing.dll'),
    (Join-Path $frameworkDir 'System.Net.Http.dll'),
    (Join-Path $frameworkDir 'System.Web.Extensions.dll')
)
$refArgs = ($refList | ForEach-Object { "/reference:`"$_`"" }) -join ' '

$iconArg = ""
if (Test-Path $iconIcoPath) {
    $iconArg = "/win32icon:`"$iconIcoPath`""
}

$resourceList = @()
if (Test-Path $xamlPath) { $resourceList += "`"/resource:$xamlPath,MainWindow.xaml`"" }
if (Test-Path $iconPngPath) { $resourceList += "`"/resource:$iconPngPath,app_icon.png`"" }
$sensorExe = Join-Path $baseDir 'tools\sensors\Core Temp.exe'
if (Test-Path $sensorExe) { $resourceList += "`"/resource:$sensorExe,CoreTempBinary`"" }
$resourceArg = $resourceList -join ' '

$csFiles = Get-ChildItem -Path $srcDir -Filter "*.cs" | ForEach-Object { "`"$($_.FullName)`"" }
$csFilesArg = $csFiles -join ' '
$compileCmd = "& `"$cscPath`" /target:winexe /platform:x64 /optimize+ /nologo /codepage:65001 /nowarn:0649,0169 /win32manifest:`"$manifestPath`" $iconArg $refArgs $resourceArg /out:`"$rootExe`" $csFilesArg"
Invoke-Expression $compileCmd


# Alte EXEs bereinigen
Remove-Item $oldExe -Force -ErrorAction SilentlyContinue

if (Test-Path $rootExe) {
    $size = (Get-Item $rootExe).Length
    Write-Host ''
    Write-Host ('✓ ERFOLGREICH KOMPILIERT: ' + $rootExe + ' (' + $size + ' Bytes)') -ForegroundColor Green
    
    # Windows Explorer Icon-Cache Refresh (aktualisiert Anwendungs-Icon sofort)
    try {
        $code = '[DllImport("shell32.dll")] public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);'
        $type = Add-Type -MemberDefinition $code -Name ShellApi -Namespace Win32 -PassThru
        $type::SHChangeNotify(0x08000000, 0, [IntPtr]::Zero, [IntPtr]::Zero)
    } catch {}

    # 5. Automatischer Neustart der App (direkt im Benutzer-Desktop)
    if (-not $NoRun) {
        Write-Host '-> Starte ZnipeOptimizationTool.exe neu...' -ForegroundColor Cyan
        Start-Process -FilePath $rootExe
        Start-Sleep -Milliseconds 400
    }
} else {
    Write-Host 'FEHLER beim Kompilieren!' -ForegroundColor Red
    exit 1
}

Write-Host ''
Write-Host '==========================================================' -ForegroundColor Green
