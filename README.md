<div align="center">

# ⚡ ZNIPE OPTIMIZATION TOOL
### *High-Performance Windows & Hardware-Tuning für maximale Gaming-Reaktionszeiten und minimale Latenzen.*

[![GitHub Release](https://img.shields.io/github/v/release/xhemo/Znipe-Optimization-Tool?style=for-the-badge&color=22C55E&logo=github)](https://github.com/xhemo/Znipe-Optimization-Tool/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-0078D4?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/xhemo/Znipe-Optimization-Tool)
[![Framework](https://img.shields.io/badge/Framework-.NET%20Framework%204.8-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Dependencies](https://img.shields.io/badge/Dependencies-Zero%20(Pure%20Native)-0ea5e9?style=for-the-badge)](#-technische-architektur--deep-dive)
[![License](https://img.shields.io/badge/License-MIT-f59e0b?style=for-the-badge)](LICENSE)

<br/>

[**🚀 Neueste Version herunterladen (v1.0.0)**](https://github.com/xhemo/Znipe-Optimization-Tool/releases/download/v1.0.0/ZnipeOptimizationTool.exe) •
[**✨ Features**](#-highlights--features) •
[**🔬 Architektur**](#-technische-architektur--deep-dive) •
[**⚖️ Vergleich**](#-vergleich-warum-znipe) •
[**🛠️ Kompilieren**](#%EF%B8%8F-aus-quellcode-kompilieren-build) •
[**🛡️ Sicherheit**](#%EF%B8%8F-sicherheit-integrität--reversibilität)

<br/>

</div>

---

## 🎯 Warum Znipe Optimization Tool?

Nahezu jeder PC-Spieler kennt das Phänomen: Der Rechner ist mit hochmoderner Hardware ausgestattet, doch in kompetitiven Titeln wie **Counter-Strike 2**, **Valorant**, **Apex Legends** oder **Call of Duty** fühlt sich das Spielgeschehen bisweilen schwammig und inkonsistent an. Unbemerkte Mikroruckler, ungleichmäßige Frame-Times, verzögerter Maus-Input oder schwankende Interrupt-Latenzen trüben das Spielgefühl.

Viele herkömmliche „Tuning-Programme“ verschlimmern die Situation oft:
- Sie installieren intransparente Hintergrunddienste, die selbst CPU-Zyklen und RAM beanspruchen.
- Sie wenden ungetestete Registry-Sammlungen an, die Kernkomponenten oder Sicherheitsmechanismen beschädigen.
- Sie sind mit Werbung, Upselling oder Registrierungszwang überfrachtet.

**Znipe Optimization Tool setzt auf das genaue Gegenteil:**
1. **Volle Transparenz:** Jeder Tweak und jeder Messwert wird im Klartext begründet. Der Anwender sieht exakt, was modifiziert wird, warum es sinnvoll ist und welche Konsequenzen es hat.
2. **Zero Bloatware:** Keine Installation erforderlich. Kein Zwangsdienst im Hintergrund, keine Datenübertragung an externe Server, keine temporären Rückstände.
3. **Echtes Systemwissen:** Statt Placebo-Reglern steuert das Tool echte Windows-NT-Kernel-Schnittstellen (`NtSetTimerResolution`, `BCDedit`, `SetDisplayConfig`, `NVML`).

---

## ⚖️ Vergleich: Warum Znipe?

| Kriterium | ⚡ Znipe Optimization Tool | ⚠️ Herkömmliche Tuning-Tools | ❌ Dubiose Registry-Skripte |
| :--- | :---: | :---: | :---: |
| **Transparenz** | 🟢 **100% Open Source** (C# / WPF) | 🔴 Closed-Source / Blackbox | 🟡 Kaum nachvollziehbar |
| **Abhängigkeiten** | 🟢 **Keine** (Ausschließlich Windows-APIs) | 🔴 Gigabyte-schwere Runtimes & DLLs | 🟢 Keine |
| **Hintergrundlast** | 🟢 **0 % CPU** (Kein Dauer-Dienst) | 🔴 Permanente Hintergrund-Agenten | 🟢 Keine |
| **Reversibilität** | 🟢 Jeder Tweak kann zurückgesetzt werden | 🔴 Oft destruktiv & unumkehrbar | 🔴 Kein Backup / Restore |
| **Hardware-Telemetrie** | 🟢 Native **NVML & SMBIOS** Live-Werte | 🟡 Träge oder ungenaue Schätzungen | 🔴 Nicht vorhanden |
| **Kernel Timer Resolution** | 🟢 Echte `NtSetTimerResolution` (**0.500 ms**) | 🟡 Meist nur rudimentäres Halbwissen | 🔴 Nicht vorhanden |
| **Multi-Monitor Engine** | 🟢 Live CCD-Umschaltung bis **540 Hz** | 🔴 Fehlt vollständig | 🔴 Nicht vorhanden |
| **Tracking & Werbung** | 🟢 **Absolut werbe- & telemetriefrei** | 🔴 Häufig Adware oder Daten-Tracking | 🟡 Versteckte Link-Weiterleitungen |

---

## 🔬 Technische Architektur & Deep-Dive

Das Tool folgt konsequent dem **KISS-Prinzip** (*Keep It Simple, Stupid*) und dem **YAGNI-Leitfaden** (*You Aren't Gonna Need It*). Statt externer DLLs werden native Win32-, NTDLL- und Treiber-Schnittstellen per P/Invoke direkt angesprochen:

```
┌────────────────────────────────────────────────────────────────────────┐
│                        Znipe Optimization Tool                         │
│               (WPF / Hardware-Beschleunigte Vektor-UI)                 │
└───────────────────┬────────────────────────────────┬───────────────────┘
                    │                                │
      ┌─────────────▼─────────────┐    ┌─────────────▼─────────────┐
      │     Win32 / NT-Kernel     │    │     Hardware & Treiber    │
      └─────────────┬─────────────┘    └─────────────┬─────────────┘
                    │                                │
  ┌─────────────────┼─────────────────┐  ┌───────────┼───────────┐
  │                 │                 │  │           │           │
┌─▼───────────┐ ┌───▼───────────┐ ┌───▼──▼────┐ ┌────▼────────┐ ┌▼──────────┐
│  ntdll.dll  │ │  user32.dll   │ │ dwmapi.dll│ │  nvml.dll   │ │    WMI    │
│ Timer-Res   │ │ CCD Display   │ │ Dark Mode │ │ GPU-Limits  │ │  Hardware │
│ 0.500 ms    │ │ Live 540 Hz   │ │ DWM Sync  │ │ & Telemetrie│ │  & Topo   │
└─────────────┘ └───────────────┘ └───────────┘ └─────────────┘ └───────────┘
```

### 1. High-Precision Kernel Timer & Sleep Jitter
- **NtSetTimerResolution:** Standardmäßig taktet der Windows-Kernel mit einer Zeitgeber-Auflösung von **15.625 ms** (156.250 Einheiten zu je 100 ns). Für verzögerungsfreies Gaming senkt das Tool diesen Wert auf bis zu **0.500 ms** (5.000 Einheiten) ab.
- **Sleep(1) Microsecond-Jitter:** Über die hochauflösende Hardware-Stoppuhr (`QueryPerformanceCounter`) wird der tatsächliche Jitter von Thread-Sleep-Zyklen in Echtzeit gemessen.
- **Kernel-Tweaks via BCDedit:** Automatisierte und sichere Konfiguration von `useplatformclock` (HPET), `disabledynamictick` und `tscsyncpolicy`.

> [!TIP]
> Durch die Kombination von `NtSetTimerResolution (0.5 ms)` mit deaktiviertem Dynamic Tick werden Mikroruckler in DirectX- und Vulkan-Spielen nachweisbar reduziert.

### 2. Multi-Monitor Engine & CCD (Connecting and Configuring Displays)
- **Live-Frequenzwechsel bis 540 Hz:** Monitore können direkt in der Anwendung auf ihre maximale Wiederholfrequenz umgeschaltet werden.
- **Win32 & CCD P/Invoke:** Verbindung von `ChangeDisplaySettingsEx` (`CDS_UPDATEREGISTRY`) und der modernen Windows Display Configuration API (`SetDisplayConfig`, `QueryDisplayConfig`, `DisplayConfigGetDeviceInfo`).
- **Klare Frequenz-Indikatoren:** Ist die maximale Bildwiederholrate aktiv, erscheint das Feld grün mit `[Hz] (Empfohlen)`. Läuft das Panel mit verringerter Frequenz, warnt das Tool in Rot mit `[Hz] (Reduziert - X Hz möglich)`.

### 3. GPU Partner-Erkennung & Subsystem-Mapping
- Windows meldet im Gerätemanager meist lediglich den Basis-Grafikchip (z. B. `NVIDIA GeForce RTX 4090`).
- Das Tool liest die hardwareseitige **PCI Subsystem ID** aus (`SUBSYS_51021462`) und schlüsselt Vendor sowie Modellbezeichnung auf (`1462` = MSI, `5102` = **SUPRIM X**), was zur präzisen Anzeige `MSI GeForce RTX 4090 SUPRIM X` führt.
- Mainboard-Herstellerangaben werden bereinigt (z. B. `ASUSTeK COMPUTER INC.` → `ASUS ROG STRIX X870-A GAMING WIFI`).

### 4. Native NVML-Anbindung (NVIDIA Management Library)
- Direkte Anbindung an die offizielle `nvml.dll` des NVIDIA-Treibers ohne Drittanbieter-Overhead.
- **Live-Telemetrie:** GPU-Temperatur, VRAM-Temperatur, Kerntakt, Auslastung, Speicherbelegung und Power-Draw (Watt).
- **GPU Limits Daemon:** Einstellen von Watt- und Temperatur-Limits direkt im Treiber. Ein schlanker, optionaler Autostart-Daemon hält diese Werte auch nach Systemneustarts zuverlässig aktiv.
- **Resizable BAR (ReBAR) Auswertung:** Direkte Abfrage des BAR1-Speicherbereichs (`nvmlDeviceGetBAR1MemoryInfo`).

### 5. Intelligente BIOS / UEFI Live-Inspektion
- **Secure Boot Status:** Überprüfung der UEFI-Firmware-Flags für Riot Vanguard (*Valorant*), FaceIT Anti-Cheat und Windows 11 Boot-Integrität.
- **Smarte LAN- vs. WLAN-Erkennung:** Das Tool analysiert den Netzwerkadapterstatus (`NetworkInterface`). Bei aktivem LAN-Kabel wird ein im BIOS deaktiviertes WLAN als optimal (grün `Deaktiviert`) gewertet, um parallele DPC-Latenzen zu vermeiden. Befindet sich der PC im reinen WLAN-Betrieb, meldet das Tool `Aktiv (In Benutzung)`.
- **Bluetooth-Funkmodul:** Klare Statusanzeige mit dem Hinweis, dass das Modul bei Nutzung kabelloser Controller oder Headsets aktiv belassen werden sollte.
- **Optionale BIOS-Empfehlungen:** Übersichtliche Empfehlungen für manuelle BIOS-Funktionen (z. B. *Restore AC Power Loss* für automatischen Start bei Stromzufuhr).

### 6. USB Topologie & Latenz-Ebenen
- Vollständiger Scan des USB-Gerätebaums über `Win32_USBController` und `Win32_PnPEntity`.
- Unterscheidet, ob Eingabegeräte direkt an den CPU-PCIe-Lanes des Mainboards oder über den Mainboard-Chipsatz (Hub-Kette mit potenzieller Zusatzlatenz) angebunden sind.
- Anpassung der Puffergröße (`MouseDataQueueSize`) in der Windows-Registry zur Vermeidung von Pufferüberläufen bei hohen Polling-Raten (1000 Hz – 8000 Hz).

### 7. Netzwerk Bufferbloat Engine
- Integrierter Latenz- und Durchsatz-Stresstest nach VESA/Bufferbloat-Standard.
- Ermittelt die Latenzerhöhung (Jitter und RTT-Spitzen) unter gleichzeitiger Download- und Upload-Volllast mit einer transparenten Notenvergabe von **A+** (exzellent) bis **F** (starke Lags).

---

## 📁 Repository- & Projektstruktur

```text
Znipe-Optimization-Tool/
├── src/
│   ├── App.cs                   # Hauptlogik, Tab-Routing, Window-Management & Telemetrie
│   ├── HardwareStressTester.cs  # Multi-Core CPU-/GPU-Stresstest via OpenCL (ohne Libs)
│   ├── ProcessRunner.cs         # Win32-Prozessstarter & CLI-Output-Parser
│   ├── RegistryHelper.cs        # Typisierte Registry-Operationen mit Error-Catching
│   ├── SoftwareDetector.cs      # Erkennung installierter Software/Apps
│   ├── UIHelper.cs              # Farbcodes, Badges, gecachte Brushes & WPF-Elemente
│   ├── WmiHelper.cs             # Performante WMI-Abfragen
│   ├── Tweaks.AioCooler.cs      # AIO-Kühler-Steuerung & GIF-Upload zum Display
│   ├── Tweaks.AioGiphy.cs       # Giphy-GIF-Explorer (Suche & Upload)
│   ├── Tweaks.Apps.cs           # Wartung von Windows-Apps & Runtimes
│   ├── Tweaks.Bcd.cs            # BCDedit (Sicherheitsoptionen, Safeboot)
│   ├── Tweaks.Bios.cs           # Live-UEFI-Inspektion (ReBAR, SecureBoot, WLAN, BT)
│   ├── Tweaks.Bufferbloat.cs    # Bufferbloat-Engine & Netzwerkadapter-Konfiguration
│   ├── Tweaks.Chipset.cs        # Mainboard-Chipsatztreiber-Status & Empfehlungen
│   ├── Tweaks.Gamebar.cs        # Xbox Game Bar & KGL State (Gaming-Priorität)
│   ├── Tweaks.GpuLimit.cs       # NVML Power- & Temperature-Limits Daemon
│   ├── Tweaks.Hardware.cs       # Multi-Monitor CCD, GPU-Subsystem & CPU/Drive-Scan
│   ├── Tweaks.Lan.cs            # LAN-Adapter-Inspektion & Treiber-Status
│   ├── Tweaks.Mouse.cs          # Maus-Input-Puffer & Sicherheitsoptimierungen
│   ├── Tweaks.Office.cs         # Microsoft Office C2R Setup & Deployment
│   ├── Tweaks.TimerRes.cs       # NtSetTimerResolution & High-Precision Sleep Jitter
│   ├── app.manifest             # UAC-Manifest für erforderliche Administratorrechte
│   └── xaml/                    # Modulare XAML-Templates
│       ├── Header.xaml          # Titelleiste, Custom Window Chrome & Controls
│       ├── Modals.xaml          # Dialoge (z. B. GPU Limits Modal)
│       ├── Sidebar.xaml         # Seitenleiste mit Tabs & Vektor-Icons
│       ├── Styles.xaml          # Globale Brushes, CardBorders, Buttons & ScrollViewer
│       ├── Window.xaml          # Hauptfenster-Gerüst (INCLUDE-Assembly-Root)
│       └── Tabs/                # Modulare XAML-Tabs (Home, Hardware, Bios, Timer, Apps, ...)
├── build/
│   ├── build.ps1                # Zero-Dependency Build-Script via csc.exe
│   └── upload_to_github.ps1     # Automatisierter Release- & Asset-Upload via GitHub API
├── .gitignore                   # Ignoriert Binaries, Build-Artefakte & Log-Dateien
└── README.md                    # Projektdokumentation
```

---

## 🛠️ Aus Quellcode kompilieren (Build)

Für die Kompilierung wird **kein Visual Studio und kein schweres SDK** vorausgesetzt. Das Build-Skript nutzt den im Windows-System vorhandenen C#-Compiler `csc.exe`:

```powershell
# 1. Repository klonen
git clone https://github.com/xhemo/Znipe-Optimization-Tool.git
cd Znipe-Optimization-Tool

# 2. Build starten
powershell -ExecutionPolicy Bypass -File build\build.ps1
```

> [!NOTE]
> Das Build-Skript assembliert alle modularen XAML-Dateien zu einer einzigen Ressourcendatei, bindet das native Anwendungs-Icon ein und kompiliert das Release mit maximaler Compiler-Optimierung (`/optimize+`). Die fertige `ZnipeOptimizationTool.exe` wird direkt im Hauptverzeichnis ausgegeben.

---

## 🛡️ Sicherheit, Integrität & Reversibilität

- **Keine Blackbox:** Der Quellcode ist vollständig quelloffen und einsehbar.
- **Keine dauerhaften Zerstörungen:** Das Tool nimmt keine irreversiblen Löschungen an Systemkomponenten vor. Alle Einstellungen können transparent wieder zurückgesetzt werden.
- **UAC-Administratorrechte:** Zur Modifikation von BCDedit-Einträgen, Kernel-Parametern und Hardware-Treibern fordert die Anwendung beim Start reguläre Administratorrechte über das eingebettete `app.manifest` an.

---

<div align="center">

### 👤 Autor & Lizenz

Entwickelt von **[xhemo](https://github.com/xhemo)** • Lizenziert unter der [MIT License](LICENSE)

*Znipe Optimization Tool — Schneller. Direkter. Kompromisslos.*

</div>
