using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RotateTransform = System.Windows.Media.RotateTransform;
using ScaleTransform = System.Windows.Media.ScaleTransform;
using TranslateTransform = System.Windows.Media.TranslateTransform;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;
using WpfImage = System.Windows.Controls.Image;

namespace ZnipeOptimizationTool {
    public partial class MainWindowLogic {
        // =========================================================================
        // P/INVOKE FOR USB HID & WINUSB (DIRECT KRAKEN AIO COMMUNICATION)
        // =========================================================================
        [DllImport("hid.dll", SetLastError = true)]
        private static extern void HidD_GetHidGuid(out Guid hidGuid);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SetupDiGetClassDevs(ref Guid classGuid, string enumerator, IntPtr hwndParent, uint flags);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiEnumDeviceInterfaces(IntPtr deviceInfoSet, IntPtr deviceInfoData, ref Guid interfaceClassGuid, uint memberIndex, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData);

        [DllImport("setupapi.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr deviceInfoSet, ref SP_DEVICE_INTERFACE_DATA deviceInterfaceData, IntPtr deviceInterfaceDetailData, uint deviceInterfaceDetailDataSize, out uint requiredSize, IntPtr deviceInfoData);

        [DllImport("setupapi.dll", SetLastError = true)]
        private static extern bool SetupDiDestroyDeviceInfoList(IntPtr deviceInfoSet);

        [StructLayout(LayoutKind.Sequential)]
        private struct SP_DEVICE_INTERFACE_DATA {
            public uint cbSize;
            public Guid interfaceClassGuid;
            public uint flags;
            public IntPtr reserved;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToWrite,
            out uint lpNumberOfBytesWritten,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadFile(
            SafeFileHandle hFile,
            byte[] lpBuffer,
            uint nNumberOfBytesToRead,
            out uint lpNumberOfBytesRead,
            IntPtr lpOverlapped);

        [DllImport("winusb.dll", SetLastError = true)]
        private static extern bool WinUsb_Initialize(SafeFileHandle DeviceHandle, out IntPtr InterfaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        private static extern bool WinUsb_Free(IntPtr InterfaceHandle);

        [DllImport("winusb.dll", SetLastError = true)]
        private static extern bool WinUsb_WritePipe(
            IntPtr InterfaceHandle,
            byte PipeID,
            byte[] Buffer,
            uint BufferLength,
            out uint LengthTransferred,
            IntPtr Overlapped);

        private const uint AIO_GENERIC_READ = 0x80000000;
        private const uint AIO_GENERIC_WRITE = 0x40000000;
        private const uint AIO_FILE_SHARE_READ = 0x00000001;
        private const uint AIO_FILE_SHARE_WRITE = 0x00000002;
        private const uint AIO_OPEN_EXISTING = 3;
        private const uint AIO_FILE_FLAG_OVERLAPPED = 0x40000000;

        // UI Controls
        private TextBlock txtAioDeviceName;
        private Border pillAioStatus;
        private TextBlock txtAioStatusPill;
        private Button btnAioRefresh;
        private TextBlock txtAioLiquidTemp;
        private TextBlock txtAioPumpRpm;
        private Slider sliderAioBrightness;
        private TextBlock txtAioBrightnessVal;
        private Button btnAioRotateStep;
        private TextBlock txtAioFeedback;

        // Circular Display Preview Controls
        private Grid borderAioScreenOuter;
        private WpfImage imgAioDisplayPreview;
        private ScaleTransform scaleAioPreview;
        private TranslateTransform translateAioPreview;
        private RotateTransform rotateAioPreview;
        private StackPanel panelAioPlaceholder;
        private TextBlock txtAioCircleTemp;

        // Media & GIF Upload Controls
        private Button btnAioSelectFile;
        private Button btnAioGiphySearch;
        private ProgressBar progressBarAioUpload;
        private TextBlock txtAioUploadProgress;
        private Button btnAioResetLiquidMode;
        private string aioSelectedFilePath = null;
        private string aioMediaStatsInfo = "";

        // Media Gallery Controls
        private TextBlock txtAioMediaCount;
        private Button btnAioOpenMediaFolder;
        private Button btnAioClearAllMedia;
        private WrapPanel panelAioMediaGallery;
        private TextBlock txtAioNoMediaHint;

        // Pan & Zoom State (Crop within circular display)
        private float aioZoom = 1.0f;
        private float aioPanX = 0f;
        private float aioPanY = 0f;
        private int aioMediaSrcW = 640;
        private int aioMediaSrcH = 640;
        private System.Windows.Point aioDragStart;
        private bool aioIsDragging = false;
        private bool aioDidDragMove = false;
        private DispatcherTimer aioPanZoomDebounce;
        private float lastUploadedZoom = -1f;
        private float lastUploadedPanX = -999f;
        private float lastUploadedPanY = -999f;
        private byte lastUploadedOri = 255;
        private string lastUploadedFile = null;

        // Preview Animation & Live Brightness State
        private DispatcherTimer aioPreviewTimer;
        private List<BitmapSource> aioPreviewFrames;
        private int aioPreviewCurrentFrame = 0;
        private DispatcherTimer aioBrightnessDebounce;

        // Internal State
        private byte aioCurrentBrightness = 80;
        private byte aioCurrentOrientation = 3; // 3 = 270° (90° links von 0)
        private byte aioBaseHardwareOrientation = 3; // Hardware-Basismontage
        private bool aioIgnoreSliderChange = false;
        private string aioDetectedPath = null;
        private string aioDetectedBulkPath = null;
        private string aioDetectedModelName = null;

        private static readonly string[] SupportedAioIds = new string[] {
            "vid_1e71&pid_300c", // NZXT Kraken Elite (2023)
            "vid_1e71&pid_300e", // NZXT Kraken (2023)
            "vid_1e71&pid_3008", // NZXT Kraken Z53 / Z63 / Z73
            "vid_1e71&pid_2007", // NZXT Kraken X53 / X63 / X73
            "vid_1e71&pid_2014"  // NZXT Kraken X-Series v2
        };

        private static readonly Guid WinUsbDeviceGuid = new Guid("300c300b-7ee7-1125-0724-101503010819");

        private void InitAioCooler() {
            try {
                txtAioDeviceName      = (TextBlock)window.FindName("TxtAioDeviceName");
                pillAioStatus         = (Border)window.FindName("PillAioStatus");
                txtAioStatusPill      = (TextBlock)window.FindName("TxtAioStatusPill");
                btnAioRefresh         = (Button)window.FindName("BtnAioRefresh");
                txtAioLiquidTemp      = (TextBlock)window.FindName("TxtAioLiquidTemp");
                txtAioPumpRpm         = (TextBlock)window.FindName("TxtAioPumpRpm");
                sliderAioBrightness   = (Slider)window.FindName("SliderAioBrightness");
                txtAioBrightnessVal   = (TextBlock)window.FindName("TxtAioBrightnessVal");
                btnAioRotateStep      = (Button)window.FindName("BtnAioRotateStep");
                txtAioFeedback        = (TextBlock)window.FindName("TxtAioFeedback");

                // Circular Screen Preview
                borderAioScreenOuter  = (Grid)window.FindName("BorderAioScreenOuter");
                imgAioDisplayPreview  = (WpfImage)window.FindName("ImgAioDisplayPreview");
                scaleAioPreview       = (ScaleTransform)window.FindName("ScaleAioPreview");
                translateAioPreview   = (TranslateTransform)window.FindName("TranslateAioPreview");
                rotateAioPreview      = (RotateTransform)window.FindName("RotateAioPreview");
                panelAioPlaceholder   = (StackPanel)window.FindName("PanelAioPlaceholder");
                txtAioCircleTemp      = (TextBlock)window.FindName("TxtAioCircleTemp");

                // Media Controls
                btnAioSelectFile       = (Button)window.FindName("BtnAioSelectFile");
                btnAioGiphySearch      = (Button)window.FindName("BtnAioGiphySearch");
                progressBarAioUpload   = (ProgressBar)window.FindName("ProgressBarAioUpload");
                txtAioUploadProgress   = (TextBlock)window.FindName("TxtAioUploadProgress");
                btnAioResetLiquidMode  = (Button)window.FindName("BtnAioResetLiquidMode");

                // Media Gallery Controls
                txtAioMediaCount       = (TextBlock)window.FindName("TxtAioMediaCount");
                btnAioOpenMediaFolder  = (Button)window.FindName("BtnAioOpenMediaFolder");
                btnAioClearAllMedia    = (Button)window.FindName("BtnAioClearAllMedia");
                panelAioMediaGallery   = (WrapPanel)window.FindName("PanelAioMediaGallery");
                txtAioNoMediaHint      = (TextBlock)window.FindName("TxtAioNoMediaHint");

                if (btnAioOpenMediaFolder != null) {
                    btnAioOpenMediaFolder.Click += (s, e) => {
                        try {
                            string dir = GetAioMediaDir();
                            Process.Start(new ProcessStartInfo {
                                FileName = dir,
                                UseShellExecute = true
                            });
                        } catch {}
                    };
                }

                if (btnAioClearAllMedia != null) {
                    btnAioClearAllMedia.Click += (s, e) => ClearAllMedia();
                }

                // Initial orientation on preview (relative to physical mount)
                int? baseMount = RegistryHelper.GetDword(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "BaseMountOrientation");
                aioBaseHardwareOrientation = (byte)(baseMount ?? 3);
                int? initOri = RegistryHelper.GetDword(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifOrientation");
                aioCurrentOrientation = (byte)(initOri ?? aioBaseHardwareOrientation);
                ApplyPreviewTransform();

                // Interactive Pan (Drag) and Zoom (MouseWheel) directly on the circular display
                if (borderAioScreenOuter != null) {
                    borderAioScreenOuter.MouseLeftButtonDown += (s, e) => {
                        if (!string.IsNullOrEmpty(aioSelectedFilePath)) {
                            if (e.ClickCount == 2) {
                                aioZoom = 1.0f;
                                aioPanX = 0f;
                                aioPanY = 0f;
                                ClampPanZoom();
                                ApplyPreviewTransform();
                                TriggerDebouncedUpload();
                                return;
                            }
                            System.Windows.Point pt = e.GetPosition(borderAioScreenOuter);
                            double cx = borderAioScreenOuter.ActualWidth / 2.0;
                            double cy = borderAioScreenOuter.ActualHeight / 2.0;
                            if (cx <= 0) cx = 130;
                            if (cy <= 0) cy = 130;
                            double distSq = (pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy);
                            double maxRadius = Math.Min(cx, cy) - 9;
                            if (distSq > maxRadius * maxRadius) return; // Outside circle: ignore

                            aioDragStart = pt;
                            aioIsDragging = true;
                            aioDidDragMove = false;
                            borderAioScreenOuter.CaptureMouse();
                        }
                    };

                    borderAioScreenOuter.MouseMove += (s, e) => {
                        if (!aioIsDragging || string.IsNullOrEmpty(aioSelectedFilePath)) return;
                        System.Windows.Point cur = e.GetPosition(borderAioScreenOuter);
                        double dx = cur.X - aioDragStart.X;
                        double dy = cur.Y - aioDragStart.Y;
                        if (Math.Abs(dx) > 0.01 || Math.Abs(dy) > 0.01) aioDidDragMove = true;

                        double panDx = dx / 242.0;
                        double panDy = dy / 242.0;

                        int visualRot = ((aioCurrentOrientation - aioBaseHardwareOrientation + 4) % 4) * 90;
                        double rad = -visualRot * Math.PI / 180.0;
                        double unrotDx = panDx * Math.Cos(rad) - panDy * Math.Sin(rad);
                        double unrotDy = panDx * Math.Sin(rad) + panDy * Math.Cos(rad);

                        aioPanX += (float)unrotDx;
                        aioPanY += (float)unrotDy;
                        aioDragStart = cur;
                        ClampPanZoom();
                        ApplyPreviewTransform();

                        if (aioDidDragMove) {
                            TriggerDebouncedUpload();
                        }
                    };

                    borderAioScreenOuter.MouseLeftButtonUp += (s, e) => {
                        if (aioIsDragging) {
                            aioIsDragging = false;
                            borderAioScreenOuter.ReleaseMouseCapture();
                            if (aioDidDragMove) {
                                TriggerDebouncedUpload();
                            }
                        }
                    };

                    borderAioScreenOuter.LostMouseCapture += (s, e) => {
                        if (aioIsDragging) {
                            aioIsDragging = false;
                            if (aioDidDragMove) {
                                TriggerDebouncedUpload();
                            }
                        }
                    };

                    borderAioScreenOuter.MouseWheel += (s, e) => {
                        if (string.IsNullOrEmpty(aioSelectedFilePath)) return;

                        // Only zoom if mouse cursor is strictly inside the circular LCD display (~121px radius)
                        System.Windows.Point pt = e.GetPosition(borderAioScreenOuter);
                        double cx = borderAioScreenOuter.ActualWidth / 2.0;
                        double cy = borderAioScreenOuter.ActualHeight / 2.0;
                        if (cx <= 0) cx = 130;
                        if (cy <= 0) cy = 130;
                        double distSq = (pt.X - cx) * (pt.X - cx) + (pt.Y - cy) * (pt.Y - cy);
                        double maxRadius = Math.Min(cx, cy) - 9; // Inside bezel ring
                        if (distSq > maxRadius * maxRadius) {
                            // Outside circular display: allow normal page scrolling
                            return;
                        }

                        // Inside circular display: handle event so page does not scroll
                        e.Handled = true;

                        float delta = e.Delta > 0 ? 0.05f : -0.05f;
                        float newZoom = Math.Max(1.0f, Math.Min(3.0f, aioZoom + delta));
                        if (Math.Abs(newZoom - aioZoom) < 0.001f) return;

                        aioZoom = newZoom;
                        ClampPanZoom();
                        ApplyPreviewTransform();
                        TriggerDebouncedUpload();
                    };
                }

                // Live Brightness Slider (debounced 75ms for instant reaction without USB flooding)
                if (sliderAioBrightness != null && txtAioBrightnessVal != null) {
                    sliderAioBrightness.Value = aioCurrentBrightness;
                    txtAioBrightnessVal.Text = aioCurrentBrightness + "%";
                    sliderAioBrightness.ValueChanged += (s, e) => {
                        if (aioIgnoreSliderChange) return;
                        byte val = (byte)sliderAioBrightness.Value;
                        txtAioBrightnessVal.Text = val == 0 ? "🌑 0% (Stealth)" : (val + "%");
                        aioCurrentBrightness = val;
                        if (aioBrightnessDebounce == null) {
                            aioBrightnessDebounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(75) };
                            aioBrightnessDebounce.Tick += (ds, de) => {
                                aioBrightnessDebounce.Stop();
                                SetAioDisplayBrightness(aioCurrentBrightness, aioCurrentBrightness == 0 ? "🌑 Stealth Mode (Display Aus)" : ("Helligkeit: " + aioCurrentBrightness + "%"));
                            };
                        }
                        aioBrightnessDebounce.Stop();
                        aioBrightnessDebounce.Start();
                    };
                }

                // Step-Rotation Button (+90° pro Klick) synchron mit Hardware & Vorschau-Kreis
                if (btnAioRotateStep != null) {
                    btnAioRotateStep.Click += (s, e) => {
                        aioCurrentOrientation = (byte)((aioCurrentOrientation + 1) % 4);
                        if (string.IsNullOrEmpty(aioSelectedFilePath)) {
                            // Wenn kein GIF aktiv ist, passt der Klick direkt die Standard-Hardwareausrichtung an
                            aioBaseHardwareOrientation = aioCurrentOrientation;
                            try {
                                RegistryHelper.SetDword(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "BaseMountOrientation", aioBaseHardwareOrientation);
                            } catch {}
                        } else {
                            try {
                                RegistryHelper.SetDword(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifOrientation", aioCurrentOrientation);
                            } catch {}
                        }
                        ClampPanZoom();
                        ApplyPreviewTransform();
                        SetAioOrientation(aioCurrentOrientation, "Display gedreht.");
                        if (!string.IsNullOrEmpty(aioSelectedFilePath) && File.Exists(aioSelectedFilePath)) {
                            StartMediaUpload(aioSelectedFilePath);
                        }
                    };
                }

                // Refresh Button
                if (btnAioRefresh != null) {
                    btnAioRefresh.Click += (s, e) => RefreshAioUI(true);
                }

                // Media Selection Handler (direkter Auto-Upload nach Auswahl, gesichert in AioMedia)
                if (btnAioSelectFile != null) {
                    btnAioSelectFile.Click += (s, e) => {
                        var dlg = new OpenFileDialog();
                        dlg.Title = "GIF oder Bild für LCD-Display auswählen";
                        dlg.Filter = "Bilder & GIFs (*.gif;*.png;*.jpg;*.jpeg;*.bmp)|*.gif;*.png;*.jpg;*.jpeg;*.bmp|GIF Animationen (*.gif)|*.gif|Bilder (*.png;*.jpg;*.bmp)|*.png;*.jpg;*.bmp";
                        if (dlg.ShowDialog() == true) {
                            string mediaDir = GetAioMediaDir();
                            string fileName = Path.GetFileName(dlg.FileName);
                            string targetPath = Path.Combine(mediaDir, fileName);

                            if (!string.Equals(dlg.FileName, targetPath, StringComparison.OrdinalIgnoreCase)) {
                                try {
                                    File.Copy(dlg.FileName, targetPath, true);
                                } catch (Exception ex) {
                                    Debug.WriteLine("Copy media error: " + ex.Message);
                                    targetPath = dlg.FileName;
                                }
                            }

                            SelectSavedMedia(targetPath);
                        }
                    };
                }

                if (btnAioGiphySearch != null) {
                    btnAioGiphySearch.Click += (s, e) => {
                        AioGiphyDialog.ShowDialog(window, GetAioMediaDir(), (downloadedPath) => {
                            SelectSavedMedia(downloadedPath);
                        });
                    };
                }

                if (btnAioResetLiquidMode != null) {
                    btnAioResetLiquidMode.Click += (s, e) => {
                        StopLivePreview();
                        try {
                            RegistryHelper.DeleteValue(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifPath");
                            RegistryHelper.DeleteValue(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifOrientation");
                        } catch {}
                        aioCurrentOrientation = aioBaseHardwareOrientation;
                        aioZoom = 1.0f;
                        aioPanX = 0f;
                        aioPanY = 0f;
                        lastUploadedFile = null;
                        lastUploadedOri = 255;
                        lastUploadedZoom = -1f;
                        lastUploadedPanX = -999f;
                        lastUploadedPanY = -999f;
                        ApplyPreviewTransform();
                        RenderMediaGallery();
                        Task.Run(() => {
                            string path = GetOrFindKrakenPath();
                            if (!string.IsNullOrEmpty(path)) {
                                using (var h = CreateFile(path, AIO_GENERIC_READ | AIO_GENERIC_WRITE, AIO_FILE_SHARE_READ | AIO_FILE_SHARE_WRITE, IntPtr.Zero, AIO_OPEN_EXISTING, 0, IntPtr.Zero)) {
                                    if (!h.IsInvalid) {
                                        // 1. Hardware LCD-Ausrichtung auf physische Standardmontage zurücksetzen
                                        byte[] oriBuf = new byte[64];
                                        oriBuf[0] = 0x30; oriBuf[1] = 0x02; oriBuf[2] = 0x01;
                                        oriBuf[3] = aioCurrentBrightness;
                                        oriBuf[4] = 0x00; oriBuf[5] = 0x00; oriBuf[6] = 0x01;
                                        oriBuf[7] = aioBaseHardwareOrientation;
                                        uint wOri;
                                        WriteFile(h, oriBuf, 64, out wOri, IntPtr.Zero);

                                        // 2. Hardware Display-Modus auf Standard-Kühlmittel-Animation zurückschalten
                                        byte[] modeBuf = new byte[64];
                                        modeBuf[0] = 0x38; modeBuf[1] = 0x01; modeBuf[2] = 0x02; modeBuf[3] = 0x00;
                                        uint wMode;
                                        WriteFile(h, modeBuf, 64, out wMode, IntPtr.Zero);
                                    }
                                }
                                UpdateAioFeedback("Standard Kühlmittel-Animation & Ausrichtung wiederhergestellt.");
                                if (txtAioUploadProgress != null) {
                                    window.Dispatcher.BeginInvoke(new Action(() => {
                                        txtAioUploadProgress.Visibility = Visibility.Collapsed;
                                    }));
                                }
                            }
                        });
                    };
                }

                // Restore last active GIF into preview if available & render gallery
                try {
                    string lastGif = RegistryHelper.GetString(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifPath");
                    int? lastOri = RegistryHelper.GetDword(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifOrientation");
                    if (lastOri.HasValue) {
                        aioCurrentOrientation = (byte)lastOri.Value;
                    }
                    ApplyPreviewTransform();
                    if (!string.IsNullOrEmpty(lastGif) && File.Exists(lastGif)) {
                        string mediaDir = GetAioMediaDir();
                        string inMedia = Path.Combine(mediaDir, Path.GetFileName(lastGif));
                        if (!string.Equals(lastGif, inMedia, StringComparison.OrdinalIgnoreCase)) {
                            try {
                                File.Copy(lastGif, inMedia, true);
                                lastGif = inMedia;
                                RegistryHelper.SetString(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifPath", lastGif);
                            } catch {}
                        }
                        aioSelectedFilePath = lastGif;
                        lastUploadedFile = lastGif;
                        lastUploadedZoom = 1.0f;
                        lastUploadedPanX = 0f;
                        lastUploadedPanY = 0f;
                        lastUploadedOri = aioCurrentOrientation;
                        UpdateSelectedFileStats(lastGif);
                        LoadLivePreview(lastGif);
                    }
                } catch {}
                RenderMediaGallery();
            } catch (Exception ex) {
                Debug.WriteLine("InitAioCooler error: " + ex.Message);
            }
        }

        private static string GetAioMediaDir() {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ZnipeOptimizationTool", "AioMedia");
            if (!Directory.Exists(dir)) {
                Directory.CreateDirectory(dir);
            }
            return dir;
        }

        private void SelectSavedMedia(string filePath) {
            if (!File.Exists(filePath)) return;
            aioSelectedFilePath = filePath;
            aioZoom = 1.0f;
            aioPanX = 0f;
            aioPanY = 0f;
            UpdateSelectedFileStats(filePath);
            LoadLivePreview(filePath);
            StartMediaUpload(filePath);
            RenderMediaGallery();
        }

        private void DeleteSingleMedia(string filePath) {
            try {
                if (File.Exists(filePath)) {
                    File.Delete(filePath);
                }
                if (string.Equals(aioSelectedFilePath, filePath, StringComparison.OrdinalIgnoreCase)) {
                    StopLivePreview();
                    try {
                        RegistryHelper.DeleteValue(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifPath");
                    } catch {}
                }
            } catch (Exception ex) {
                Debug.WriteLine("Delete media error: " + ex.Message);
            }
            RenderMediaGallery();
        }

        private void ClearAllMedia() {
            try {
                string dir = GetAioMediaDir();
                if (Directory.Exists(dir)) {
                    foreach (var f in Directory.GetFiles(dir)) {
                        try { File.Delete(f); } catch {}
                    }
                }
                StopLivePreview();
                try {
                    RegistryHelper.DeleteValue(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifPath");
                } catch {}
            } catch (Exception ex) {
                Debug.WriteLine("Clear all media error: " + ex.Message);
            }
            RenderMediaGallery();
        }

        private void RenderMediaGallery() {
            if (panelAioMediaGallery == null) return;
            panelAioMediaGallery.Children.Clear();

            string dir = GetAioMediaDir();
            string[] files = Directory.Exists(dir)
                ? Directory.GetFiles(dir, "*.*")
                    .Where(f => {
                        string ext = Path.GetExtension(f).ToLowerInvariant();
                        return ext == ".gif" || ext == ".png" || ext == ".jpg" || ext == ".jpeg" || ext == ".bmp";
                    })
                    .OrderByDescending(f => new FileInfo(f).LastWriteTime)
                    .ToArray()
                : new string[0];

            if (txtAioMediaCount != null) {
                txtAioMediaCount.Text = "(" + files.Length + ")";
            }

            if (files.Length == 0) {
                if (txtAioNoMediaHint != null) txtAioNoMediaHint.Visibility = Visibility.Visible;
                return;
            }
            if (txtAioNoMediaHint != null) txtAioNoMediaHint.Visibility = Visibility.Collapsed;

            foreach (var file in files) {
                string currentFilePath = file;
                bool isActive = !string.IsNullOrEmpty(aioSelectedFilePath) &&
                                string.Equals(aioSelectedFilePath, currentFilePath, StringComparison.OrdinalIgnoreCase);

                var tile = new Border {
                    Width = 105,
                    Height = 115,
                    Margin = new Thickness(4),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x10, 0x14, 0x1D)),
                    BorderBrush = isActive
                        ? UIHelper.BrushGreenText
                        : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x1C, 0x24, 0x36)),
                    BorderThickness = new Thickness(isActive ? 1.5 : 1),
                    CornerRadius = new CornerRadius(8),
                    Cursor = System.Windows.Input.Cursors.Hand,
                    ClipToBounds = true
                };

                var grid = new Grid();

                try {
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.DecodePixelWidth = 100;
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(currentFilePath, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();

                    var img = new WpfImage {
                        Source = bi,
                        Stretch = System.Windows.Media.Stretch.UniformToFill,
                        Width = 97,
                        Height = 82,
                        Margin = new Thickness(4, 4, 4, 25),
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Top
                    };
                    grid.Children.Add(img);
                } catch {}

                var btnDel = new Button {
                    Content = "✖",
                    Width = 20,
                    Height = 20,
                    Padding = new Thickness(0),
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xEF, 0x44, 0x44)),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(0xCC, 0x10, 0x14, 0x1D)),
                    BorderBrush = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x33, 0x41, 0x55)),
                    BorderThickness = new Thickness(1),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(0, 4, 4, 0),
                    ToolTip = "Dieses Medium restlos löschen"
                };
                btnDel.Click += (s, e) => {
                    e.Handled = true;
                    DeleteSingleMedia(currentFilePath);
                };
                grid.Children.Add(btnDel);

                var txtName = new TextBlock {
                    Text = Path.GetFileName(currentFilePath),
                    FontSize = 10,
                    Foreground = isActive ? UIHelper.BrushGreenText : new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0x94, 0xA3, 0xB8)),
                    FontWeight = isActive ? FontWeights.SemiBold : FontWeights.Normal,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(4, 0, 4, 4),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    MaxWidth = 95,
                    ToolTip = Path.GetFileName(currentFilePath)
                };
                grid.Children.Add(txtName);

                tile.Child = grid;

                tile.MouseLeftButtonUp += (s, e) => {
                    SelectSavedMedia(currentFilePath);
                };

                panelAioMediaGallery.Children.Add(tile);
            }
        }

        private void ClampPanZoom() {
            if (aioZoom < 1.0f) aioZoom = 1.0f;
            float baseScale = Math.Max(1.0f / aioMediaSrcW, 1.0f / aioMediaSrcH);
            float relW = aioMediaSrcW * baseScale * aioZoom;
            float relH = aioMediaSrcH * baseScale * aioZoom;
            float maxPanW = Math.Max(0f, (relW - 1.0f) / 2.0f);
            float maxPanH = Math.Max(0f, (relH - 1.0f) / 2.0f);

            float maxPanX = maxPanW;
            float maxPanY = maxPanH;
            if (aioCurrentOrientation % 2 == 1) {
                maxPanX = maxPanH;
                maxPanY = maxPanW;
            }
            aioPanX = Math.Max(-maxPanX, Math.Min(maxPanX, aioPanX));
            aioPanY = Math.Max(-maxPanY, Math.Min(maxPanY, aioPanY));
        }

        private void ApplyPreviewTransform() {
            if (aioMediaSrcW > 0 && aioMediaSrcH > 0 && imgAioDisplayPreview != null) {
                double baseScale = Math.Max(242.0 / aioMediaSrcW, 242.0 / aioMediaSrcH);
                double totalW = aioMediaSrcW * baseScale * aioZoom;
                double totalH = aioMediaSrcH * baseScale * aioZoom;
                imgAioDisplayPreview.Width = totalW;
                imgAioDisplayPreview.Height = totalH;
            }
            if (scaleAioPreview != null) {
                scaleAioPreview.ScaleX = 1.0;
                scaleAioPreview.ScaleY = 1.0;
            }
            if (translateAioPreview != null) {
                translateAioPreview.X = aioPanX * 242.0;
                translateAioPreview.Y = aioPanY * 242.0;
            }
            if (rotateAioPreview != null) {
                rotateAioPreview.CenterX = 0;
                rotateAioPreview.CenterY = 0;
                rotateAioPreview.Angle = ((aioCurrentOrientation - aioBaseHardwareOrientation + 4) % 4) * 90;
            }
        }

        private bool HasPendingChanges() {
            if (string.IsNullOrEmpty(aioSelectedFilePath)) return false;
            if (!string.Equals(aioSelectedFilePath, lastUploadedFile, StringComparison.OrdinalIgnoreCase)) return true;
            if (aioCurrentOrientation != lastUploadedOri) return true;
            if (Math.Abs(aioZoom - lastUploadedZoom) > 0.005f) return true;
            if (Math.Abs(aioPanX - lastUploadedPanX) > 0.002f) return true;
            if (Math.Abs(aioPanY - lastUploadedPanY) > 0.002f) return true;
            return false;
        }

        private void TriggerDebouncedUpload() {
            if (string.IsNullOrEmpty(aioSelectedFilePath) || !File.Exists(aioSelectedFilePath)) return;
            if (!HasPendingChanges()) return;

            if (aioPanZoomDebounce == null) {
                aioPanZoomDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                aioPanZoomDebounce.Tick += (s, e) => {
                    aioPanZoomDebounce.Stop();
                    if (aioIsDragging) {
                        aioPanZoomDebounce.Start();
                        return;
                    }
                    if (!string.IsNullOrEmpty(aioSelectedFilePath) && File.Exists(aioSelectedFilePath) && HasPendingChanges()) {
                        StartMediaUpload(aioSelectedFilePath);
                    }
                };
            }
            aioPanZoomDebounce.Stop();
            aioPanZoomDebounce.Start();
        }

        private void StopLivePreview() {
            if (aioPreviewTimer != null) {
                aioPreviewTimer.Stop();
                aioPreviewTimer = null;
            }
            aioPreviewFrames = null;
            if (imgAioDisplayPreview != null) {
                imgAioDisplayPreview.Source = null;
                imgAioDisplayPreview.Visibility = Visibility.Collapsed;
            }
            if (panelAioPlaceholder != null) {
                panelAioPlaceholder.Visibility = Visibility.Visible;
            }
            aioSelectedFilePath = null;
        }

        private void LoadLivePreview(string filePath) {
            if (imgAioDisplayPreview == null || panelAioPlaceholder == null) return;
            try {
                if (aioPreviewTimer != null) {
                    aioPreviewTimer.Stop();
                    aioPreviewTimer = null;
                }
                aioPreviewFrames = new List<BitmapSource>();
                string ext = Path.GetExtension(filePath).ToLowerInvariant();

                if (ext == ".gif") {
                    using (var img = System.Drawing.Image.FromFile(filePath)) {
                        aioMediaSrcW = img.Width;
                        aioMediaSrcH = img.Height;
                        var dim = new FrameDimension(img.FrameDimensionsList[0]);
                        int total = img.GetFrameCount(dim);
                        int step = total > 90 ? (int)Math.Ceiling((double)total / 90) : 1;

                        int delayHundredths = 10;
                        try {
                            byte[] delayBytes = img.GetPropertyItem(0x5100).Value;
                            if (delayBytes != null && delayBytes.Length >= 4) {
                                delayHundredths = BitConverter.ToInt32(delayBytes, 0);
                            }
                        } catch {}
                        if (delayHundredths <= 1) delayHundredths = 10; // Web standard default for 0 / 1 delay (100ms = 10 FPS)
                        int effPreviewDelay = delayHundredths * step;
                        if (effPreviewDelay < 3) effPreviewDelay = 3;

                        using (var canvas = new Bitmap(img.Width, img.Height, PixelFormat.Format32bppArgb))
                        using (var g = Graphics.FromImage(canvas)) {
                            g.InterpolationMode = InterpolationMode.NearestNeighbor;
                            g.PixelOffsetMode = PixelOffsetMode.Half;
                            g.Clear(Color.Transparent);

                            for (int i = 0; i < total; i += step) {
                                img.SelectActiveFrame(dim, i);
                                g.DrawImage(img, 0, 0, img.Width, img.Height);

                                var rect = new Rectangle(0, 0, canvas.Width, canvas.Height);
                                var data = canvas.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                                var bsource = BitmapSource.Create(
                                    canvas.Width, canvas.Height, 96, 96,
                                    System.Windows.Media.PixelFormats.Bgra32, null,
                                    data.Scan0, data.Stride * canvas.Height, data.Stride);
                                canvas.UnlockBits(data);
                                bsource.Freeze();
                                aioPreviewFrames.Add(bsource);
                            }
                        }

                        if (aioPreviewFrames.Count > 0) {
                            aioPreviewCurrentFrame = 0;
                            imgAioDisplayPreview.Source = aioPreviewFrames[0];
                            panelAioPlaceholder.Visibility = Visibility.Collapsed;
                            imgAioDisplayPreview.Visibility = Visibility.Visible;

                            if (aioPreviewFrames.Count > 1) {
                                aioPreviewTimer = new DispatcherTimer();
                                aioPreviewTimer.Interval = TimeSpan.FromMilliseconds(effPreviewDelay * 10);
                                aioPreviewTimer.Tick += (s, e) => {
                                    if (aioPreviewFrames == null || aioPreviewFrames.Count == 0) return;
                                    aioPreviewCurrentFrame = (aioPreviewCurrentFrame + 1) % aioPreviewFrames.Count;
                                    imgAioDisplayPreview.Source = aioPreviewFrames[aioPreviewCurrentFrame];
                                };
                                aioPreviewTimer.Start();
                            }
                        }
                    }
                } else {
                    using (var img = System.Drawing.Image.FromFile(filePath)) {
                        aioMediaSrcW = img.Width;
                        aioMediaSrcH = img.Height;
                    }
                    var bi = new BitmapImage();
                    bi.BeginInit();
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.UriSource = new Uri(filePath, UriKind.Absolute);
                    bi.EndInit();
                    bi.Freeze();
                    imgAioDisplayPreview.Source = bi;
                    panelAioPlaceholder.Visibility = Visibility.Collapsed;
                    imgAioDisplayPreview.Visibility = Visibility.Visible;
                }

                ClampPanZoom();
                ApplyPreviewTransform();
            } catch (Exception ex) {
                Debug.WriteLine("Live preview error: " + ex.Message);
            }
        }

        private void StartMediaUpload(string filePath) {
            if (btnAioSelectFile != null) btnAioSelectFile.IsEnabled = false;
            if (btnAioGiphySearch != null) btnAioGiphySearch.IsEnabled = false;
            if (progressBarAioUpload != null) {
                progressBarAioUpload.Value = 5;
                progressBarAioUpload.Visibility = Visibility.Visible;
            }
            if (txtAioUploadProgress != null) {
                txtAioUploadProgress.Text = "Bereite Datei vor (Skalierung & Konvertierung)...";
                txtAioUploadProgress.Foreground = UIHelper.BrushMuted;
                txtAioUploadProgress.Visibility = Visibility.Visible;
            }

            byte uploadOri = aioCurrentOrientation;
            float uploadZoom = aioZoom;
            float uploadPanX = aioPanX;
            float uploadPanY = aioPanY;

            Task.Run(() => {
                try {
                    string hidPath = GetOrFindKrakenPath();
                    string bulkPath = GetOrFindKrakenBulkPath();

                    if (string.IsNullOrEmpty(hidPath) || string.IsNullOrEmpty(bulkPath)) {
                        SetUploadResult(false, "Kein Zugriff auf USB-Schnittstellen der Wasserkühlung.");
                        return;
                    }

                    byte assetMode;
                    byte[] payload = PrepareMediaPayload(filePath, uploadOri, uploadZoom, uploadPanX, uploadPanY, out assetMode);

                    window.Dispatcher.BeginInvoke(new Action(() => {
                        if (progressBarAioUpload != null) {
                            progressBarAioUpload.Value = 20;
                            progressBarAioUpload.Visibility = Visibility.Visible;
                        }
                        if (txtAioUploadProgress != null) {
                            txtAioUploadProgress.Text = string.Format("Übertrage an Display ({0} KB)... 20%", payload.Length / 1024);
                            txtAioUploadProgress.Visibility = Visibility.Visible;
                        }
                    }));

                    Action<int> onProgress = (pct) => {
                        window.Dispatcher.BeginInvoke(new Action(() => {
                            if (progressBarAioUpload != null) {
                                progressBarAioUpload.Value = pct;
                                progressBarAioUpload.Visibility = Visibility.Visible;
                            }
                            if (txtAioUploadProgress != null) {
                                txtAioUploadProgress.Text = string.Format("Übertrage an Display... {0}%", pct);
                                txtAioUploadProgress.Visibility = Visibility.Visible;
                            }
                        }));
                    };

                    bool ok = UploadPayloadToHardwareBucket(hidPath, bulkPath, payload, assetMode, onProgress);

                    window.Dispatcher.BeginInvoke(new Action(() => {
                        if (progressBarAioUpload != null) progressBarAioUpload.Value = 100;
                        if (txtAioUploadProgress != null) txtAioUploadProgress.Text = "Übertragung abgeschlossen (100%)";
                    }));

                    if (ok) {
                        try {
                            RegistryHelper.SetString(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifPath", filePath);
                        } catch {}
                        lastUploadedFile = filePath;
                        lastUploadedOri = uploadOri;
                        lastUploadedZoom = uploadZoom;
                        lastUploadedPanX = uploadPanX;
                        lastUploadedPanY = uploadPanY;
                    }

                    // Keep 100% visible briefly so user sees the completed bar
                    Thread.Sleep(800);

                    string successMsg = !string.IsNullOrEmpty(aioMediaStatsInfo) 
                        ? ("✅ Erfolgreich! " + aioMediaStatsInfo + " aktiv auf Display.") 
                        : "✅ Erfolgreich! Dein GIF / Bild läuft jetzt auf dem Display.";
                    SetUploadResult(ok, ok ? successMsg : "Fehler beim Übertragen an das Display.");
                } catch (Exception ex) {
                    SetUploadResult(false, "Fehler: " + ex.Message);
                }
            });
        }

        private void UpdateSelectedFileStats(string filePath) {
            try {
                long sizeKb = new FileInfo(filePath).Length / 1024;
                string ext = Path.GetExtension(filePath).ToLowerInvariant();
                if (ext == ".gif") {
                    using (var img = System.Drawing.Image.FromFile(filePath)) {
                        var dim = new FrameDimension(img.FrameDimensionsList[0]);
                        int frames = img.GetFrameCount(dim);
                        int maxFrames = 90;
                        int step = 1;
                        if (frames > maxFrames) step = (int)Math.Ceiling((double)frames / maxFrames);
                        int effFrames = (int)Math.Ceiling((double)frames / step);
                        int origDelay = 10;
                        try {
                            byte[] delayBytes = img.GetPropertyItem(0x5100).Value;
                            if (delayBytes != null && delayBytes.Length >= 4) {
                                origDelay = BitConverter.ToInt32(delayBytes, 0);
                            }
                        } catch {}
                        if (origDelay <= 1) origDelay = 10; // Web standard default (100ms = 10 FPS)

                        int effDelay = origDelay * step;
                        if (effDelay < 3) effDelay = 3;

                        int fps = (int)Math.Round(100.0 / effDelay);
                        double sec = effFrames * (effDelay * 0.01);
                        aioMediaStatsInfo = string.Format("{0} Frames • {1} FPS • {2:F1}s Loop", effFrames, fps, sec);
                    }
                } else {
                    aioMediaStatsInfo = "Statisch (640x640)";
                }
            } catch {
            }
        }

        private static byte[] PrepareMediaPayload(string filePath, byte orientation, float zoom, float panX, float panY, out byte assetMode) {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            RotateFlipType rotate = GetRotateFlipType(orientation);

            if (ext == ".gif") {
                using (var src = System.Drawing.Image.FromFile(filePath)) {
                    var dim = new FrameDimension(src.FrameDimensionsList[0]);
                    int totalFrames = src.GetFrameCount(dim);

                    if (totalFrames <= 1) {
                        assetMode = 0x02; // Single-frame GIF -> Static RGBA
                        return RenderImageToRgba(src, 640, 640, rotate, zoom, panX, panY);
                    }

                    assetMode = 0x01; // Animated GIF
                    return RenderAnimatedGif(src, dim, totalFrames, 640, 640, rotate, zoom, panX, panY);
                }
            } else {
                assetMode = 0x02; // Static RGBA
                using (var src = System.Drawing.Image.FromFile(filePath)) {
                    return RenderImageToRgba(src, 640, 640, rotate, zoom, panX, panY);
                }
            }
        }

        private static byte[] RenderImageToRgba(System.Drawing.Image src, int targetW, int targetH, RotateFlipType rotate, float zoom, float panX, float panY) {
            using (var bmp = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb)) {
                using (var g = Graphics.FromImage(bmp)) {
                    g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                    g.SmoothingMode = SmoothingMode.HighQuality;
                    g.Clear(Color.Black);

                    float bufPanX = panX;
                    float bufPanY = panY;
                    switch (rotate) {
                        case RotateFlipType.Rotate90FlipNone:
                            bufPanX = panY;
                            bufPanY = -panX;
                            break;
                        case RotateFlipType.Rotate180FlipNone:
                            bufPanX = -panX;
                            bufPanY = -panY;
                            break;
                        case RotateFlipType.Rotate270FlipNone:
                            bufPanX = -panY;
                            bufPanY = panX;
                            break;
                    }

                    float baseScale = Math.Max((float)targetW / src.Width, (float)targetH / src.Height);
                    float totalScale = baseScale * zoom;
                    int drawW = (int)Math.Round(src.Width * totalScale);
                    int drawH = (int)Math.Round(src.Height * totalScale);
                    int drawX = (int)Math.Round((targetW - drawW) / 2.0 + bufPanX * targetW);
                    int drawY = (int)Math.Round((targetH - drawH) / 2.0 + bufPanY * targetH);
                    g.DrawImage(src, drawX, drawY, drawW, drawH);
                }

                if (rotate != RotateFlipType.RotateNoneFlipNone) {
                    bmp.RotateFlip(rotate);
                }

                var data = bmp.LockBits(new Rectangle(0, 0, targetW, targetH), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                byte[] rawBgra = new byte[targetW * targetH * 4];
                Marshal.Copy(data.Scan0, rawBgra, 0, rawBgra.Length);
                bmp.UnlockBits(data);

                byte[] rgba = new byte[targetW * targetH * 4];
                for (int i = 0; i < targetW * targetH; i++) {
                    rgba[i * 4 + 0] = rawBgra[i * 4 + 2]; // R
                    rgba[i * 4 + 1] = rawBgra[i * 4 + 1]; // G
                    rgba[i * 4 + 2] = rawBgra[i * 4 + 0]; // B
                    rgba[i * 4 + 3] = 0;                  // 0
                }
                return rgba;
            }
        }

        private static byte[] RenderAnimatedGif(System.Drawing.Image src, FrameDimension dim, int totalFrames, int targetW, int targetH, RotateFlipType rotate, float zoom, float panX, float panY) {
            byte[] frameDelayBytes = null;
            try {
                frameDelayBytes = src.GetPropertyItem(0x5100).Value; // PropertyTagFrameDelay
            } catch {}

            // Cap frames at 90 max so the pump MCU's LZW decoder stays in real-time budget (33 FPS without slow-motion lag)
            int maxFrames = 90;
            int step = 1;
            if (totalFrames > maxFrames) {
                step = (int)Math.Ceiling((double)totalFrames / maxFrames);
            }

            var encoder = new System.Windows.Media.Imaging.GifBitmapEncoder();
            var delaysList = new List<int>();

            float bufPanX = panX;
            float bufPanY = panY;
            switch (rotate) {
                case RotateFlipType.Rotate90FlipNone:
                    bufPanX = panY;
                    bufPanY = -panX;
                    break;
                case RotateFlipType.Rotate180FlipNone:
                    bufPanX = -panX;
                    bufPanY = -panY;
                    break;
                case RotateFlipType.Rotate270FlipNone:
                    bufPanX = -panY;
                    bufPanY = panX;
                    break;
            }

            for (int i = 0; i < totalFrames; i += step) {
                src.SelectActiveFrame(dim, i);

                int origDelay = 10;
                if (frameDelayBytes != null && (i * 4 + 4) <= frameDelayBytes.Length) {
                    origDelay = BitConverter.ToInt32(frameDelayBytes, i * 4);
                } else if (frameDelayBytes != null && frameDelayBytes.Length >= 4) {
                    origDelay = BitConverter.ToInt32(frameDelayBytes, 0);
                }
                if (origDelay <= 1) origDelay = 10; // Web standard default for 0 / 1 delay (100ms = 10 FPS)

                int effDelay = origDelay * step;
                if (effDelay < 3) effDelay = 3; // Hardware floor 30ms (33.3 FPS max for MCU)
                delaysList.Add(effDelay);

                using (var bmp = new Bitmap(targetW, targetH, PixelFormat.Format32bppArgb)) {
                    using (var g = Graphics.FromImage(bmp)) {
                        g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                        g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                        g.SmoothingMode = SmoothingMode.HighQuality;
                        g.Clear(Color.Black);

                        float baseScale = Math.Max((float)targetW / src.Width, (float)targetH / src.Height);
                        float totalScale = baseScale * zoom;
                        int drawW = (int)Math.Round(src.Width * totalScale);
                        int drawH = (int)Math.Round(src.Height * totalScale);
                        int drawX = (int)Math.Round((targetW - drawW) / 2.0 + bufPanX * targetW);
                        int drawY = (int)Math.Round((targetH - drawH) / 2.0 + bufPanY * targetH);
                        g.DrawImage(src, drawX, drawY, drawW, drawH);
                    }

                    if (rotate != RotateFlipType.RotateNoneFlipNone) {
                        bmp.RotateFlip(rotate);
                    }

                    var rect = new Rectangle(0, 0, targetW, targetH);
                    var bmpData = bmp.LockBits(rect, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    var bsource = System.Windows.Media.Imaging.BitmapSource.Create(
                        targetW, targetH, 96, 96, 
                        System.Windows.Media.PixelFormats.Bgra32, null, 
                        bmpData.Scan0, bmpData.Stride * targetH, bmpData.Stride);
                    bmp.UnlockBits(bmpData);

                    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bsource));
                }
            }

            using (var ms = new MemoryStream()) {
                encoder.Save(ms);
                return EnsureGifLoopsAndDelays(ms.ToArray(), delaysList);
            }
        }

        private static byte[] EnsureGifLoopsAndDelays(byte[] gif, List<int> delays) {
            // 1. Patch exact frame delays into Graphic Control Extension (GCE) headers
            // Format: 0x21 0xF9 0x04 [packed] [delayLow] [delayHigh] [trans] 0x00
            int frameIdx = 0;
            for (int i = 0; i < gif.Length - 8; i++) {
                if (gif[i] == 0x21 && gif[i + 1] == 0xF9 && gif[i + 2] == 0x04) {
                    int d = 10;
                    if (delays != null && frameIdx < delays.Count) {
                        d = delays[frameIdx];
                    }
                    if (d <= 1) d = 10;

                    // 1. Disposal Method = 2 (Restore to Background) in bits 2-4:
                    // Clears screen before next frame, completely eliminating ghosting/distortion/artifacts!
                    gif[i + 3] = (byte)((gif[i + 3] & 0xE3) | 0x08);

                    // 2. Hardware delay matching source GIF (minimum 30ms / 33 FPS floor for MCU decoder)
                    int finalDelay = (d < 3) ? 3 : d;

                    gif[i + 4] = (byte)(finalDelay & 0xFF);
                    gif[i + 5] = (byte)((finalDelay >> 8) & 0xFF);
                    frameIdx++;
                }
            }

            // 2. Inject NETSCAPE2.0 looping block if not present
            for (int i = 0; i < gif.Length - 10; i++) {
                if (gif[i] == 'N' && gif[i+1] == 'E' && gif[i+2] == 'T' && gif[i+3] == 'S' && gif[i+4] == 'C') {
                    return gif;
                }
            }

            if (gif.Length < 13) return gif;
            bool hasGct = (gif[10] & 0x80) != 0;
            int gctSize = hasGct ? 3 * (1 << ((gif[10] & 0x07) + 1)) : 0;
            int insertPos = 13 + gctSize;
            if (insertPos > gif.Length) return gif;

            byte[] netscape = new byte[] {
                0x21, 0xFF, 0x0B,
                0x4E, 0x45, 0x54, 0x53, 0x43, 0x41, 0x50, 0x45, 0x32, 0x2E, 0x30, // NETSCAPE2.0
                0x03, 0x01, 0x00, 0x00, 0x00
            };

            byte[] result = new byte[gif.Length + netscape.Length];
            Array.Copy(gif, 0, result, 0, insertPos);
            Array.Copy(netscape, 0, result, insertPos, netscape.Length);
            Array.Copy(gif, insertPos, result, insertPos + netscape.Length, gif.Length - insertPos);
            return result;
        }

        private static RotateFlipType GetRotateFlipType(byte orientation) {
            switch (orientation % 4) {
                case 1: return RotateFlipType.Rotate90FlipNone;
                case 2: return RotateFlipType.Rotate180FlipNone;
                case 3: return RotateFlipType.Rotate270FlipNone;
                case 0:
                default: return RotateFlipType.RotateNoneFlipNone;
            }
        }

        private static bool UploadPayloadToHardwareBucket(string hidPath, string bulkPath, byte[] payload, byte assetMode, Action<int> progressCallback = null) {
            using (var hHid = CreateFile(hidPath, AIO_GENERIC_READ | AIO_GENERIC_WRITE, AIO_FILE_SHARE_READ | AIO_FILE_SHARE_WRITE, IntPtr.Zero, AIO_OPEN_EXISTING, 0, IntPtr.Zero)) {
                if (hHid.IsInvalid) return false;

                using (var hBulk = CreateFile(bulkPath, AIO_GENERIC_READ | AIO_GENERIC_WRITE, AIO_FILE_SHARE_READ | AIO_FILE_SHARE_WRITE, IntPtr.Zero, AIO_OPEN_EXISTING, AIO_FILE_FLAG_OVERLAPPED, IntPtr.Zero)) {
                    if (hBulk.IsInvalid) return false;

                    IntPtr winUsb;
                    if (!WinUsb_Initialize(hBulk, out winUsb)) return false;

                    try {
                        uint w;

                        // 1. Cancel any active transfer
                        byte[] cancelBuf = new byte[64];
                        cancelBuf[0] = 0x36; cancelBuf[1] = 0x03;
                        WriteFile(hHid, cancelBuf, 64, out w, IntPtr.Zero);
                        Thread.Sleep(15);

                        // 2. Clear buckets and reset to bucket 0
                        byte[] modeLiquid = new byte[64];
                        modeLiquid[0] = 0x38; modeLiquid[1] = 0x01; modeLiquid[2] = 0x02;
                        WriteFile(hHid, modeLiquid, 64, out w, IntPtr.Zero);
                        Thread.Sleep(10);

                        for (byte b = 0; b < 16; b++) {
                            byte[] delBuf = new byte[64];
                            delBuf[0] = 0x32; delBuf[1] = 0x02; delBuf[2] = b;
                            WriteFile(hHid, delBuf, 64, out w, IntPtr.Zero);
                        }
                        Thread.Sleep(15);

                        byte bucketIdx = 0;
                        int dataUnits = (int)Math.Ceiling((20 + payload.Length) / 1024.0);
                        byte[] sizeBytes = BitConverter.GetBytes((ushort)dataUnits);

                        // 3. Setup bucket: 0x32 0x01
                        byte[] setupBuf = new byte[64];
                        setupBuf[0] = 0x32;
                        setupBuf[1] = 0x01;
                        setupBuf[2] = bucketIdx;
                        setupBuf[3] = (byte)(bucketIdx + 1);
                        setupBuf[4] = 0x00; // memory start low
                        setupBuf[5] = 0x00; // memory start high
                        setupBuf[6] = sizeBytes[0];
                        setupBuf[7] = sizeBytes[1];
                        setupBuf[8] = 0x01;
                        WriteFile(hHid, setupBuf, 64, out w, IntPtr.Zero);
                        Thread.Sleep(15);

                        // 4. Start transfer: 0x36 0x01
                        byte[] startBuf = new byte[64];
                        startBuf[0] = 0x36;
                        startBuf[1] = 0x01;
                        startBuf[2] = bucketIdx;
                        WriteFile(hHid, startBuf, 64, out w, IntPtr.Zero);
                        Thread.Sleep(15);

                        // 5. Bulk write header (20 bytes)
                        byte[] header = new byte[20];
                        byte[] magic = new byte[] { 0x12, 0xFA, 0x01, 0xE8, 0xAB, 0xCD, 0xEF, 0x98, 0x76, 0x54, 0x32, 0x10 };
                        Array.Copy(magic, 0, header, 0, 12);
                        header[12] = assetMode; // 0x01 for GIF, 0x02 for static RGBA
                        byte[] lenBytes = BitConverter.GetBytes((uint)payload.Length);
                        Array.Copy(lenBytes, 0, header, 16, 4);

                        uint trans;
                        if (!WinUsb_WritePipe(winUsb, 0x02, header, (uint)header.Length, out trans, IntPtr.Zero)) return false;

                        // 6. Bulk write payload in 256KB chunks
                        int chunkSize = 256 * 1024;
                        for (int offset = 0; offset < payload.Length; offset += chunkSize) {
                            int cur = Math.Min(chunkSize, payload.Length - offset);
                            byte[] chunk = new byte[cur];
                            Array.Copy(payload, offset, chunk, 0, cur);
                            if (!WinUsb_WritePipe(winUsb, 0x02, chunk, (uint)cur, out trans, IntPtr.Zero)) return false;

                            if (progressCallback != null) {
                                int pct = 20 + (int)((long)(offset + cur) * 75 / payload.Length);
                                progressCallback(pct);
                            }
                        }

                        // 7. End transfer: 0x36 0x02
                        byte[] endBuf = new byte[64];
                        endBuf[0] = 0x36;
                        endBuf[1] = 0x02;
                        WriteFile(hHid, endBuf, 64, out w, IntPtr.Zero);
                        Thread.Sleep(20);

                        // 8. Switch bucket to active: 0x38 0x01 0x04 bucketIdx
                        byte[] switchBuf = new byte[64];
                        switchBuf[0] = 0x38;
                        switchBuf[1] = 0x01;
                        switchBuf[2] = 0x04;
                        switchBuf[3] = bucketIdx;
                        WriteFile(hHid, switchBuf, 64, out w, IntPtr.Zero);

                        return true;
                    } finally {
                        WinUsb_Free(winUsb);
                    }
                }
            }
        }

        private void SetUploadResult(bool success, string msg) {
            window.Dispatcher.BeginInvoke(new Action(() => {
                if (btnAioSelectFile != null) btnAioSelectFile.IsEnabled = true;
                if (btnAioGiphySearch != null) btnAioGiphySearch.IsEnabled = true;
                if (progressBarAioUpload != null) progressBarAioUpload.Visibility = Visibility.Collapsed;
                if (txtAioUploadProgress != null) {
                    if (success) {
                        txtAioUploadProgress.Visibility = Visibility.Collapsed;
                    } else {
                        txtAioUploadProgress.Text = msg;
                        txtAioUploadProgress.Foreground = UIHelper.BrushRedText;
                        txtAioUploadProgress.Visibility = Visibility.Visible;
                    }
                }
                UpdateAioFeedback(msg);
            }));
        }

        private void SetAioDisplayBrightness(byte brightness, string feedbackText) {
            aioCurrentBrightness = brightness;
            if (sliderAioBrightness != null) {
                sliderAioBrightness.Value = brightness;
            }
            Task.Run(() => {
                string path = GetOrFindKrakenPath();
                if (string.IsNullOrEmpty(path)) {
                    UpdateAioFeedback("Keine unterstützte Wasserkühlung gefunden.");
                    return;
                }

                bool success = SendKrakenBrightness(path, brightness, aioCurrentOrientation);
                UpdateAioFeedback(success ? feedbackText : "Fehler beim Senden des Helligkeitsbefehls.");
            });
        }

        private void SetAioOrientation(byte orientation, string feedbackText) {
            aioCurrentOrientation = orientation;
            Task.Run(() => {
                string path = GetOrFindKrakenPath();
                if (string.IsNullOrEmpty(path)) {
                    UpdateAioFeedback("Keine unterstützte Wasserkühlung gefunden.");
                    return;
                }

                bool success = SendKrakenBrightness(path, aioCurrentBrightness, orientation);
                UpdateAioFeedback(success ? feedbackText : "Fehler beim Einstellen der Ausrichtung.");
            });
        }

        private void SetAioPumpDuty(byte duty, string feedbackText) {
            Task.Run(() => {
                string path = GetOrFindKrakenPath();
                if (string.IsNullOrEmpty(path)) {
                    UpdateAioFeedback("Keine unterstützte Wasserkühlung gefunden.");
                    return;
                }

                bool success = SendKrakenPumpDuty(path, duty);
                UpdateAioFeedback(success ? feedbackText : "Fehler beim Setzen der Pumpenleistung.");
                if (success) {
                    System.Threading.Thread.Sleep(300);
                    RefreshAioUI(false);
                }
            });
        }

        private void UpdateAioFeedback(string msg) {
            if (txtAioFeedback == null) return;
            window.Dispatcher.BeginInvoke(new Action(() => {
                txtAioFeedback.Text = msg;
            }));
        }

        public void RefreshAioUI(bool showSpinner = false) {
            if (spinnerTabAioCooler != null && showSpinner) {
                spinnerTabAioCooler.Visibility = Visibility.Visible;
            }

            Task.Run(() => {
                string path = GetOrFindKrakenPath();
                bool connected = !string.IsNullOrEmpty(path);

                float liquidTemp = 0;
                int pumpRpm = 0;
                int pumpDuty = 0;
                byte hwBrightness = 0;
                byte hwOrientation = 0;
                bool gotLcdInfo = false;

                if (connected) {
                    ReadKrakenTelemetry(path, out liquidTemp, out pumpRpm, out pumpDuty);
                    gotLcdInfo = ReadKrakenLcdInfo(path, out hwBrightness, out hwOrientation);
                }

                window.Dispatcher.BeginInvoke(new Action(() => {
                    if (spinnerTabAioCooler != null) spinnerTabAioCooler.Visibility = Visibility.Collapsed;

                    if (connected) {
                        if (txtAioDeviceName != null) {
                            txtAioDeviceName.Text = (aioDetectedModelName ?? "NZXT Kraken Elite") + " (Erkannt)";
                        }
                        if (pillAioStatus != null && txtAioStatusPill != null) {
                            pillAioStatus.Background = UIHelper.BrushGreenBg;
                            txtAioStatusPill.Text = "● VERBUNDEN";
                            txtAioStatusPill.Foreground = UIHelper.BrushGreenText;
                        }

                        if (txtAioLiquidTemp != null) {
                            txtAioLiquidTemp.Text = liquidTemp > 0 ? (liquidTemp.ToString("F1") + " °C") : "-- °C";
                        }
                        if (txtAioCircleTemp != null) {
                            txtAioCircleTemp.Text = liquidTemp > 0 ? (liquidTemp.ToString("F1") + " °C") : "-- °C";
                        }
                        if (txtAioPumpRpm != null) {
                            txtAioPumpRpm.Text = pumpRpm > 0 ? (pumpRpm.ToString() + " RPM") : "-- RPM";
                        }

                        if (gotLcdInfo) {
                            aioCurrentBrightness = hwBrightness;
                            int? baseMount = RegistryHelper.GetDword(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "BaseMountOrientation");
                            aioBaseHardwareOrientation = (byte)(baseMount ?? 3);
                            aioIgnoreSliderChange = true;
                            if (sliderAioBrightness != null) {
                                sliderAioBrightness.Value = aioCurrentBrightness;
                            }
                            aioIgnoreSliderChange = false;
                            if (txtAioBrightnessVal != null) {
                                txtAioBrightnessVal.Text = aioCurrentBrightness == 0 ? "🌑 0% (Stealth)" : (aioCurrentBrightness + "%");
                            }

                            // Always preserve saved orientation preference if set
                            int? savedOri = RegistryHelper.GetDword(RegistryHive.CurrentUser, @"Software\ZnipeOptimizationTool\AioCooler", "LastGifOrientation");
                            aioCurrentOrientation = (byte)(savedOri ?? aioBaseHardwareOrientation);
                            ApplyPreviewTransform();
                        }
                    } else {
                        if (txtAioDeviceName != null) {
                            txtAioDeviceName.Text = "Keine NZXT Kraken Wasserkühlung erkannt";
                        }
                        if (pillAioStatus != null && txtAioStatusPill != null) {
                            pillAioStatus.Background = UIHelper.BrushRedBg;
                            txtAioStatusPill.Text = "● NICHT GEFUNDEN";
                            txtAioStatusPill.Foreground = UIHelper.BrushRedText;
                        }
                        if (txtAioLiquidTemp != null) txtAioLiquidTemp.Text = "-- °C";
                        if (txtAioCircleTemp != null) txtAioCircleTemp.Text = "-- °C";
                        if (txtAioPumpRpm != null) txtAioPumpRpm.Text = "-- RPM";
                    }
                }));
            });
        }

        // =========================================================================
        // HARDWARE CONTROLLER LOGIC (LOW-LEVEL HID & WINUSB)
        // =========================================================================
        private string GetOrFindKrakenPath() {
            if (!string.IsNullOrEmpty(aioDetectedPath)) {
                return aioDetectedPath;
            }

            Guid hidGuid;
            HidD_GetHidGuid(out hidGuid);
            IntPtr hDevInfo = SetupDiGetClassDevs(ref hidGuid, null, IntPtr.Zero, 0x10 | 0x02);
            if (hDevInfo == new IntPtr(-1)) return null;

            try {
                SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                ifData.cbSize = (uint)Marshal.SizeOf(ifData);

                for (uint i = 0; SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref hidGuid, i, ref ifData); i++) {
                    uint reqSize = 0;
                    SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                    if (reqSize > 0) {
                        IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                        try {
                            Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);
                            if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, detailBuffer, reqSize, out reqSize, IntPtr.Zero)) {
                                IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + 4);
                                string path = Marshal.PtrToStringAuto(pDevicePath);
                                if (!string.IsNullOrEmpty(path)) {
                                    string lower = path.ToLowerInvariant();
                                    for (int idIdx = 0; idIdx < SupportedAioIds.Length; idIdx++) {
                                        string id = SupportedAioIds[idIdx];
                                        if (lower.IndexOf(id, StringComparison.OrdinalIgnoreCase) >= 0) {
                                            aioDetectedPath = path;
                                            if (id.Contains("300c")) aioDetectedModelName = "NZXT Kraken Elite (2023)";
                                            else if (id.Contains("300e")) aioDetectedModelName = "NZXT Kraken (2023)";
                                            else if (id.Contains("3008")) aioDetectedModelName = "NZXT Kraken Z-Serie";
                                            else aioDetectedModelName = "NZXT Kraken X-Serie";
                                            return aioDetectedPath;
                                        }
                                    }
                                }
                            }
                        } finally {
                            Marshal.FreeHGlobal(detailBuffer);
                        }
                    }
                }
            } finally {
                SetupDiDestroyDeviceInfoList(hDevInfo);
            }

            return null;
        }

        private string GetOrFindKrakenBulkPath() {
            if (!string.IsNullOrEmpty(aioDetectedBulkPath)) {
                return aioDetectedBulkPath;
            }

            // 1. Try SetupAPI with WinUSB device interface GUID
            Guid winUsbGuid = WinUsbDeviceGuid;
            IntPtr hDevInfo = SetupDiGetClassDevs(ref winUsbGuid, null, IntPtr.Zero, 0x10 | 0x02);
            if (hDevInfo != new IntPtr(-1)) {
                try {
                    SP_DEVICE_INTERFACE_DATA ifData = new SP_DEVICE_INTERFACE_DATA();
                    ifData.cbSize = (uint)Marshal.SizeOf(ifData);

                    for (uint i = 0; SetupDiEnumDeviceInterfaces(hDevInfo, IntPtr.Zero, ref winUsbGuid, i, ref ifData); i++) {
                        uint reqSize = 0;
                        SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, IntPtr.Zero, 0, out reqSize, IntPtr.Zero);
                        if (reqSize > 0) {
                            IntPtr detailBuffer = Marshal.AllocHGlobal((int)reqSize);
                            try {
                                Marshal.WriteInt32(detailBuffer, IntPtr.Size == 8 ? 8 : 5);
                                if (SetupDiGetDeviceInterfaceDetail(hDevInfo, ref ifData, detailBuffer, reqSize, out reqSize, IntPtr.Zero)) {
                                    IntPtr pDevicePath = new IntPtr(detailBuffer.ToInt64() + 4);
                                    string path = Marshal.PtrToStringAuto(pDevicePath);
                                    if (!string.IsNullOrEmpty(path) && path.IndexOf("vid_1e71", StringComparison.OrdinalIgnoreCase) >= 0) {
                                        aioDetectedBulkPath = path;
                                        return aioDetectedBulkPath;
                                    }
                                }
                            } finally {
                                Marshal.FreeHGlobal(detailBuffer);
                            }
                        }
                    }
                } finally {
                    SetupDiDestroyDeviceInfoList(hDevInfo);
                }
            }

            // 2. Fallback: Derive bulk path from detected HID path (mi_01 -> mi_00)
            string hid = GetOrFindKrakenPath();
            if (!string.IsNullOrEmpty(hid)) {
                string derived = hid.Replace("hid#", "usb#").Replace("&mi_01#", "&mi_00#");
                int guidIdx = derived.LastIndexOf('{');
                if (guidIdx > 0) {
                    derived = derived.Substring(0, guidIdx) + "{300c300b-7ee7-1125-0724-101503010819}";
                    aioDetectedBulkPath = derived;
                    return aioDetectedBulkPath;
                }
            }

            return null;
        }

        private static bool SendKrakenBrightness(string devicePath, byte brightness, byte orientation) {
            try {
                using (SafeFileHandle handle = CreateFile(
                    devicePath,
                    AIO_GENERIC_READ | AIO_GENERIC_WRITE,
                    AIO_FILE_SHARE_READ | AIO_FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    AIO_OPEN_EXISTING,
                    0,
                    IntPtr.Zero)) {

                    if (handle.IsInvalid) return false;

                    byte[] buf = new byte[64];
                    buf[0] = 0x30;
                    buf[1] = 0x02;
                    buf[2] = 0x01;
                    buf[3] = brightness;
                    buf[4] = 0x00;
                    buf[5] = 0x00;
                    buf[6] = 0x01;
                    buf[7] = orientation;

                    uint written;
                    return WriteFile(handle, buf, 64, out written, IntPtr.Zero) && written == 64;
                }
            } catch {
                return false;
            }
        }

        private static bool SendKrakenPumpDuty(string devicePath, byte duty) {
            try {
                using (SafeFileHandle handle = CreateFile(
                    devicePath,
                    AIO_GENERIC_READ | AIO_GENERIC_WRITE,
                    AIO_FILE_SHARE_READ | AIO_FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    AIO_OPEN_EXISTING,
                    0,
                    IntPtr.Zero)) {

                    if (handle.IsInvalid) return false;

                    byte[] buf = new byte[64];
                    buf[0] = 0x72;
                    buf[1] = 0x01;
                    buf[2] = 0x01;
                    buf[3] = 0x00;

                    for (int i = 4; i < 44; i++) {
                        buf[i] = duty;
                    }

                    uint written;
                    return WriteFile(handle, buf, 64, out written, IntPtr.Zero) && written == 64;
                }
            } catch {
                return false;
            }
        }

        private static void ReadKrakenTelemetry(string devicePath, out float temp, out int pumpRpm, out int pumpDuty) {
            temp = 0;
            pumpRpm = 0;
            pumpDuty = 0;

            try {
                using (SafeFileHandle handle = CreateFile(
                    devicePath,
                    AIO_GENERIC_READ | AIO_GENERIC_WRITE,
                    AIO_FILE_SHARE_READ | AIO_FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    AIO_OPEN_EXISTING,
                    0,
                    IntPtr.Zero)) {

                    if (handle.IsInvalid) return;

                    byte[] readBuf = new byte[64];
                    uint readBytes;
                    if (ReadFile(handle, readBuf, 64, out readBytes, IntPtr.Zero) && readBytes >= 20) {
                        if (readBuf[0] == 0x75 || readBuf[1] == 0x75) {
                            int offset = (readBuf[0] == 0x75) ? 0 : 1;
                            if (19 + offset < readBuf.Length) {
                                temp = readBuf[15 + offset] + (readBuf[16 + offset] / 10.0f);
                                pumpRpm = (readBuf[18 + offset] << 8) | readBuf[17 + offset];
                                pumpDuty = readBuf[19 + offset];
                            }
                        }
                    }
                }
            } catch {}
        }

        private static bool ReadKrakenLcdInfo(string devicePath, out byte brightness, out byte orientation) {
            brightness = 80;
            orientation = 3;
            try {
                using (SafeFileHandle handle = CreateFile(
                    devicePath,
                    AIO_GENERIC_READ | AIO_GENERIC_WRITE,
                    AIO_FILE_SHARE_READ | AIO_FILE_SHARE_WRITE,
                    IntPtr.Zero,
                    AIO_OPEN_EXISTING,
                    0,
                    IntPtr.Zero)) {

                    if (handle.IsInvalid) return false;

                    byte[] query = new byte[64];
                    query[0] = 0x30;
                    query[1] = 0x01;
                    uint written;
                    if (!WriteFile(handle, query, 64, out written, IntPtr.Zero)) return false;

                    byte[] r = new byte[64];
                    uint read;
                    for (int attempt = 0; attempt < 15; attempt++) {
                        if (ReadFile(handle, r, 64, out read, IntPtr.Zero) && read >= 30) {
                            if (r[0] == 0x31 && r[1] == 0x01) {
                                brightness = r[0x18];
                                orientation = r[0x1A];
                                return true;
                            }
                        }
                    }
                }
            } catch {}
            return false;
        }
    }
}
