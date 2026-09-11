using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace ZnipeOptimizationTool {
    public static class AioGiphyDialog {
        private const string RegistryKeyPath = @"Software\ZnipeOptimizationTool\AioCooler";
        private const string RegistryValueName = "GiphyApiKey";
        private const string DefaultApiKey = "p4y62z9mhxHVxHXctO4geAwhFJiGvP9U";

        public static string GetSavedApiKey() {
            try {
                string k = RegistryHelper.GetString(RegistryHive.CurrentUser, RegistryKeyPath, RegistryValueName);
                if (!string.IsNullOrEmpty(k)) return k.Trim();
            } catch {}
            return DefaultApiKey;
        }

        public static void SaveApiKey(string key) {
            try {
                RegistryHelper.SetString(RegistryHive.CurrentUser, RegistryKeyPath, RegistryValueName, (key ?? "").Trim());
            } catch {}
        }

        public static void ShowDialog(Window owner, string mediaDir, Action<string> onGifDownloadedAndSelected) {
            try {
                ServicePointManager.DefaultConnectionLimit = Math.Max(ServicePointManager.DefaultConnectionLimit, 64);
                ServicePointManager.Expect100Continue = false;
            } catch {}

            var dlg = new Window {
                Title = "🌐 GIPHY — GIF Explorer",
                Width = 760,
                Height = 640,
                MinWidth = 550,
                MinHeight = 450,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = owner,
                Background = UIHelper.GetBrush("#0B0F19"),
                Foreground = Brushes.White
            };

            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 0: Header
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 1: Search Bar
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 2: Quick Tags
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) }); // 3: Results Grid
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto }); // 4: Footer Status

            // --- 0. HEADER ---
            var titleStack = new StackPanel { Margin = new Thickness(4, 0, 0, 14) };
            titleStack.Children.Add(new TextBlock {
                Text = "🌐 GIPHY GIF Explorer",
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White
            });
            titleStack.Children.Add(new TextBlock {
                Text = "Wähle ein GIF per Klick aus — wird sofort geladen und auf dein AiO LCD Display übertragen.",
                FontSize = 11.5,
                Foreground = UIHelper.GetBrush("#94A3B8"),
                Margin = new Thickness(0, 2, 0, 0)
            });
            Grid.SetRow(titleStack, 0);
            root.Children.Add(titleStack);

            // --- 1. SEARCH BAR ---
            var searchGrid = new Grid { Margin = new Thickness(0, 0, 0, 8) };
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var txtSearch = new TextBox {
                Background = UIHelper.GetBrush("#131B2A"),
                Foreground = Brushes.White,
                BorderBrush = UIHelper.GetBrush("#1E293B"),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(12, 8, 12, 8),
                FontSize = 13,
                VerticalContentAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            };
            Grid.SetColumn(txtSearch, 0);
            searchGrid.Children.Add(txtSearch);

            var btnSearch = new Button {
                Content = "🔍 Suchen",
                Height = 36,
                Padding = new Thickness(20, 0, 20, 0),
                FontSize = 12.5,
                FontWeight = FontWeights.SemiBold,
                Background = UIHelper.GetBrush("#2563EB"),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0),
                Cursor = Cursors.Hand
            };
            Grid.SetColumn(btnSearch, 1);
            searchGrid.Children.Add(btnSearch);
            Grid.SetRow(searchGrid, 1);
            root.Children.Add(searchGrid);

            // --- 2. QUICK TAGS ---
            var tagsPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 10) };
            string[] popularTags = new string[] {
                "🔥 Trending", "🎮 Gaming", "⚡ Cyberpunk", "🐱 Anime", "👾 Pixel Art", "🌌 Sci-Fi", "🚗 Cars", "🔥 Matrix", "🌊 Vaporwave"
            };

            Grid.SetRow(tagsPanel, 2);
            root.Children.Add(tagsPanel);

            // --- 3. RESULTS SCROLLVIEWER & INFINITE SCROLL ---
            var scrollResults = new ScrollViewer {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Background = UIHelper.GetBrush("#090D16")
            };
            var resultsStack = new StackPanel {
                HorizontalAlignment = HorizontalAlignment.Center
            };
            var resultsWrap = new WrapPanel {
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(4)
            };
            resultsStack.Children.Add(resultsWrap);

            var txtLoadingIndicator = new TextBlock {
                Text = "⏳ Lade weitere GIFs...",
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = UIHelper.GetBrush("#38BDF8"),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 12, 0, 24),
                Visibility = Visibility.Collapsed
            };
            resultsStack.Children.Add(txtLoadingIndicator);

            scrollResults.Content = resultsStack;
            Grid.SetRow(scrollResults, 3);
            root.Children.Add(scrollResults);

            // --- 4. FOOTER / STATUS ---
            var txtStatus = new TextBlock {
                FontSize = 11.5,
                Foreground = UIHelper.GetBrush("#94A3B8"),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                Text = "Lade Trending-GIFs..."
            };
            Grid.SetRow(txtStatus, 4);
            root.Children.Add(txtStatus);

            dlg.Content = root;

            // Shared high-precision Animation Timer driven by Stopwatch at 60 FPS (exact native speed)
            var activeCards = new List<AnimatedCardState>();
            var sw = System.Diagnostics.Stopwatch.StartNew();
            long lastMs = sw.ElapsedMilliseconds;

            var animTimer = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
            animTimer.Tick += (s, e) => {
                long now = sw.ElapsedMilliseconds;
                int delta = (int)(now - lastMs);
                lastMs = now;
                if (delta <= 0) return;
                if (delta > 100) delta = 100;

                lock (activeCards) {
                    for (int i = 0; i < activeCards.Count; i++) {
                        activeCards[i].Advance(delta);
                    }
                }
            };
            animTimer.Start();

            dlg.Closed += (s, e) => {
                animTimer.Stop();
                lock (activeCards) {
                    activeCards.Clear();
                }
            };

            // Search & Pagination State
            string activeQuery = "Trending";
            int currentOffset = 0;
            int totalLoadedCount = 0;
            bool isLoadingMore = false;
            bool hasMore = true;

            Action<string, bool> doSearch = null;
            doSearch = (query, append) => {
                query = (query ?? "").Trim();
                if (query.StartsWith("🔥 ") || query.StartsWith("🎮 ") || query.StartsWith("⚡ ") ||
                    query.StartsWith("🐱 ") || query.StartsWith("👾 ") || query.StartsWith("🌌 ") ||
                    query.StartsWith("🚗 ") || query.StartsWith("🌊 ")) {
                    query = query.Substring(2).Trim();
                }

                if (!append) {
                    currentOffset = 0;
                    totalLoadedCount = 0;
                    hasMore = true;
                    activeQuery = string.IsNullOrEmpty(query) ? "Trending" : query;
                    lock (activeCards) {
                        activeCards.Clear();
                    }
                    resultsWrap.Children.Clear();
                    scrollResults.ScrollToTop();
                    txtStatus.Text = activeQuery.Equals("Trending", StringComparison.OrdinalIgnoreCase)
                        ? "Lade aktuelle Trending-GIFs..." 
                        : string.Format("Suche nach \"{0}\"...", activeQuery);
                } else {
                    txtLoadingIndicator.Visibility = Visibility.Visible;
                }

                // If user pasted a direct GIF link
                if (query.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                    query.StartsWith("https://", StringComparison.OrdinalIgnoreCase)) {
                    txtStatus.Text = "Lade direktes GIF herunter...";
                    Task.Run(() => {
                        try {
                            string local = DownloadGifFromUrl(query, mediaDir);
                            dlg.Dispatcher.BeginInvoke(new Action(() => {
                                dlg.Close();
                                if (onGifDownloadedAndSelected != null && !string.IsNullOrEmpty(local)) {
                                    onGifDownloadedAndSelected(local);
                                }
                            }));
                        } catch (Exception ex) {
                            dlg.Dispatcher.BeginInvoke(new Action(() => {
                                txtStatus.Text = "Fehler beim Laden der URL: " + ex.Message;
                            }));
                        }
                    });
                    return;
                }

                string apiKey = GetSavedApiKey();
                isLoadingMore = true;
                int queryOffset = currentOffset;
                string queryToRun = activeQuery;

                Task.Run(() => {
                    try {
                        List<GiphyItem> items = FetchGiphyGifs(apiKey, queryToRun, 50, queryOffset);
                        dlg.Dispatcher.BeginInvoke(new Action(() => {
                            isLoadingMore = false;
                            txtLoadingIndicator.Visibility = Visibility.Collapsed;

                            if (!append) {
                                resultsWrap.Children.Clear();
                                scrollResults.ScrollToTop();
                            }

                            if (items == null || items.Count == 0) {
                                hasMore = false;
                                if (!append) {
                                    txtStatus.Text = "Keine GIFs gefunden. Versuche einen anderen Begriff.";
                                }
                                return;
                            }

                            if (items.Count < 50) {
                                hasMore = false;
                            }

                            totalLoadedCount += items.Count;
                            txtStatus.Text = string.Format("{0} animierte GIFs geladen. Scrolle weiter nach unten für mehr.", totalLoadedCount);

                            foreach (var item in items) {
                                GiphyItem currentItem = item;
                                var card = CreateGifCard(dlg, scrollResults, currentItem, activeCards, (selectedItem) => {
                                    txtStatus.Text = "Lade GIF herunter und sende an AiO Display...";
                                    Task.Run(() => {
                                        try {
                                            string downloadedPath = DownloadGifFromUrl(selectedItem.OriginalUrl, mediaDir);
                                            dlg.Dispatcher.BeginInvoke(new Action(() => {
                                                dlg.Close();
                                                if (onGifDownloadedAndSelected != null && !string.IsNullOrEmpty(downloadedPath)) {
                                                    onGifDownloadedAndSelected(downloadedPath);
                                                }
                                            }));
                                        } catch (Exception ex) {
                                            dlg.Dispatcher.BeginInvoke(new Action(() => {
                                                txtStatus.Text = "Fehler beim Herunterladen: " + ex.Message;
                                            }));
                                        }
                                    });
                                });
                                resultsWrap.Children.Add(card);
                            }
                        }));
                    } catch (Exception ex) {
                        dlg.Dispatcher.BeginInvoke(new Action(() => {
                            isLoadingMore = false;
                            txtLoadingIndicator.Visibility = Visibility.Collapsed;
                            txtStatus.Text = "GIPHY Fehler: " + ex.Message;
                        }));
                    }
                });
            };

            // Infinite Scroll: triggers next batch automatically when scrolling near bottom
            scrollResults.ScrollChanged += (s, e) => {
                if (scrollResults.ScrollableHeight > 0 &&
                    scrollResults.VerticalOffset >= scrollResults.ScrollableHeight - 300) {
                    if (!isLoadingMore && hasMore && totalLoadedCount >= 50) {
                        currentOffset += 50;
                        doSearch(activeQuery, true);
                    }
                }
            };

            // Hook up Tag Buttons
            foreach (var tag in popularTags) {
                var btnTag = new Button {
                    Content = tag,
                    Height = 26,
                    Padding = new Thickness(10, 0, 10, 0),
                    FontSize = 11,
                    Background = UIHelper.GetBrush("#141D2D"),
                    Foreground = UIHelper.GetBrush("#CBD5E1"),
                    BorderBrush = UIHelper.GetBrush("#1E293B"),
                    BorderThickness = new Thickness(1),
                    Margin = new Thickness(0, 0, 6, 4),
                    Cursor = Cursors.Hand
                };
                string t = tag;
                btnTag.Click += (s, e) => {
                    txtSearch.Text = t;
                    doSearch(t, false);
                };
                tagsPanel.Children.Add(btnTag);
            }

            // Hook up Search Button and Enter key
            btnSearch.Click += (s, e) => doSearch(txtSearch.Text, false);
            txtSearch.KeyDown += (s, e) => {
                if (e.Key == Key.Enter) {
                    doSearch(txtSearch.Text, false);
                }
            };

            // Load initial trending GIFs
            doSearch("Trending", false);

            dlg.ShowDialog();
        }

        private static Border CreateGifCard(Window dlg, ScrollViewer scrollParent, GiphyItem item, List<AnimatedCardState> activeCards, Action<GiphyItem> onSelect) {
            var card = new Border {
                Width = 160,
                Height = 150,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(8),
                Background = UIHelper.GetBrush("#101726"),
                BorderBrush = UIHelper.GetBrush("#1E293B"),
                BorderThickness = new Thickness(1.5),
                Cursor = Cursors.Hand,
                ClipToBounds = true
            };

            var grid = new Grid();

            var img = new Image {
                Stretch = Stretch.UniformToFill,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Immediate static preview so tiles appear instantly
            if (!string.IsNullOrEmpty(item.StillUrl)) {
                try {
                    var bmp = new BitmapImage();
                    bmp.BeginInit();
                    bmp.UriSource = new Uri(item.StillUrl);
                    bmp.EndInit();
                    img.Source = bmp;
                } catch {}
            }

            // Asynchronously download preview GIF and composite frames with GDI+ canvas at exact native speed
            Task.Run(() => {
                try {
                    using (var wc = new WebClient()) {
                        wc.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZnipeAio/1.0");
                        byte[] data = wc.DownloadData(item.PreviewUrl);
                        using (var ms = new MemoryStream(data))
                        using (var gdiImg = System.Drawing.Image.FromStream(ms)) {
                            var dim = new System.Drawing.Imaging.FrameDimension(gdiImg.FrameDimensionsList[0]);
                            int total = gdiImg.GetFrameCount(dim);

                            int step = 1;
                            if (total > 80) {
                                step = (int)Math.Ceiling((double)total / 80);
                            }

                            byte[] delayBytes = null;
                            try {
                                delayBytes = gdiImg.GetPropertyItem(0x5100).Value;
                            } catch {}

                            var delaysList = new List<int>();
                            var frames = new List<BitmapSource>();

                            using (var canvas = new System.Drawing.Bitmap(gdiImg.Width, gdiImg.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb))
                            using (var g = System.Drawing.Graphics.FromImage(canvas)) {
                                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.Half;
                                g.Clear(System.Drawing.Color.Transparent);

                                for (int i = 0; i < total; i += step) {
                                    int d = 10;
                                    if (delayBytes != null && (i * 4 + 4) <= delayBytes.Length) {
                                        d = BitConverter.ToInt32(delayBytes, i * 4);
                                    } else if (delayBytes != null && delayBytes.Length >= 4) {
                                        d = BitConverter.ToInt32(delayBytes, 0);
                                    }
                                    if (d <= 1) d = 10;
                                    int delayMs = d * 10 * step;
                                    if (delayMs < 20) delayMs = 20;
                                    delaysList.Add(delayMs);

                                    gdiImg.SelectActiveFrame(dim, i);
                                    g.DrawImage(gdiImg, 0, 0, gdiImg.Width, gdiImg.Height);

                                    var rect = new System.Drawing.Rectangle(0, 0, canvas.Width, canvas.Height);
                                    var bmpData = canvas.LockBits(rect, System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                                    var bsource = BitmapSource.Create(
                                        canvas.Width, canvas.Height, 96, 96,
                                        System.Windows.Media.PixelFormats.Bgra32, null,
                                        bmpData.Scan0, bmpData.Stride * canvas.Height, bmpData.Stride);
                                    canvas.UnlockBits(bmpData);
                                    bsource.Freeze();
                                    frames.Add(bsource);
                                }
                            }

                            if (frames.Count > 0) {
                                dlg.Dispatcher.BeginInvoke(new Action(() => {
                                    var cardState = new AnimatedCardState {
                                        CardElement = card,
                                        ScrollParent = scrollParent,
                                        ImageControl = img,
                                        Delays = delaysList.ToArray(),
                                        Frames = frames
                                    };
                                    img.Source = frames[0];
                                    lock (activeCards) {
                                        activeCards.Add(cardState);
                                    }
                                }));
                            }
                        }
                    }
                } catch {}
            });

            grid.Children.Add(img);

            var overlay = new Border {
                VerticalAlignment = VerticalAlignment.Bottom,
                Height = 32,
                Background = new LinearGradientBrush(
                    Color.FromArgb(0, 0, 0, 0),
                    Color.FromArgb(200, 0, 0, 0),
                    90
                )
            };
            grid.Children.Add(overlay);

            var titleText = new TextBlock {
                Text = string.IsNullOrEmpty(item.Title) ? "GIPHY GIF" : item.Title,
                FontSize = 10,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Bottom,
                Margin = new Thickness(6, 0, 6, 6),
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            grid.Children.Add(titleText);

            card.Child = grid;

            card.MouseEnter += (s, e) => {
                card.BorderBrush = UIHelper.GetBrush("#38BDF8");
            };
            card.MouseLeave += (s, e) => {
                card.BorderBrush = UIHelper.GetBrush("#1E293B");
            };
            card.MouseLeftButtonUp += (s, e) => {
                onSelect(item);
            };

            return card;
        }

        private static List<GiphyItem> FetchGiphyGifs(string apiKey, string query, int limit, int offset) {
            var list = new List<GiphyItem>();
            string url;
            if (string.IsNullOrEmpty(query) || query.Equals("Trending", StringComparison.OrdinalIgnoreCase)) {
                url = string.Format("https://api.giphy.com/v1/gifs/trending?api_key={0}&limit={1}&offset={2}&rating=g", Uri.EscapeDataString(apiKey), limit, offset);
            } else {
                url = string.Format("https://api.giphy.com/v1/gifs/search?api_key={0}&q={1}&limit={2}&offset={3}&rating=g", Uri.EscapeDataString(apiKey), Uri.EscapeDataString(query), limit, offset);
            }

            using (var client = new WebClient()) {
                client.Headers.Add("User-Agent", "ZnipeOptimizationTool/1.0");
                string json = client.DownloadString(url);

                var serializer = new JavaScriptSerializer();
                var root = serializer.Deserialize<Dictionary<string, object>>(json);
                if (root == null || !root.ContainsKey("data")) return list;

                var dataArray = root["data"] as ArrayList;
                if (dataArray == null) return list;

                foreach (var entry in dataArray) {
                    var gifDict = entry as Dictionary<string, object>;
                    if (gifDict == null) continue;

                    string id = gifDict.ContainsKey("id") ? gifDict["id"] as string : "";
                    string title = gifDict.ContainsKey("title") ? gifDict["title"] as string : "";

                    if (!gifDict.ContainsKey("images")) continue;
                    var images = gifDict["images"] as Dictionary<string, object>;
                    if (images == null) continue;

                    string stillUrl = "";
                    string previewUrl = "";
                    string originalUrl = "";

                    // Fast static thumbnail for instant tile display
                    if (images.ContainsKey("fixed_width_still")) {
                        var fws = images["fixed_width_still"] as Dictionary<string, object>;
                        if (fws != null && fws.ContainsKey("url")) stillUrl = fws["url"] as string;
                    }
                    if (string.IsNullOrEmpty(stillUrl) && images.ContainsKey("fixed_height_still")) {
                        var fhs = images["fixed_height_still"] as Dictionary<string, object>;
                        if (fhs != null && fhs.ContainsKey("url")) stillUrl = fhs["url"] as string;
                    }

                    // Fluid high-fidelity preview (full native framerate 200px)
                    if (images.ContainsKey("fixed_width")) {
                        var fw = images["fixed_width"] as Dictionary<string, object>;
                        if (fw != null && fw.ContainsKey("url")) previewUrl = fw["url"] as string;
                    }
                    if (string.IsNullOrEmpty(previewUrl) && images.ContainsKey("fixed_height")) {
                        var fh = images["fixed_height"] as Dictionary<string, object>;
                        if (fh != null && fh.ContainsKey("url")) previewUrl = fh["url"] as string;
                    }
                    if (string.IsNullOrEmpty(previewUrl) && images.ContainsKey("fixed_width_downsampled")) {
                        var fwd = images["fixed_width_downsampled"] as Dictionary<string, object>;
                        if (fwd != null && fwd.ContainsKey("url")) previewUrl = fwd["url"] as string;
                    }
                    if (string.IsNullOrEmpty(previewUrl) && images.ContainsKey("fixed_height_downsampled")) {
                        var fhd = images["fixed_height_downsampled"] as Dictionary<string, object>;
                        if (fhd != null && fhd.ContainsKey("url")) previewUrl = fhd["url"] as string;
                    }

                    // Original/downsized for high quality upload to display
                    if (images.ContainsKey("downsized")) {
                        var down = images["downsized"] as Dictionary<string, object>;
                        if (down != null && down.ContainsKey("url")) originalUrl = down["url"] as string;
                    }
                    if (string.IsNullOrEmpty(originalUrl) && images.ContainsKey("original")) {
                        var orig = images["original"] as Dictionary<string, object>;
                        if (orig != null && orig.ContainsKey("url")) originalUrl = orig["url"] as string;
                    }

                    if (string.IsNullOrEmpty(previewUrl)) previewUrl = originalUrl;
                    if (string.IsNullOrEmpty(stillUrl)) stillUrl = previewUrl;

                    if (!string.IsNullOrEmpty(originalUrl) && !string.IsNullOrEmpty(previewUrl)) {
                        list.Add(new GiphyItem {
                            Id = id,
                            Title = title,
                            StillUrl = stillUrl,
                            PreviewUrl = previewUrl,
                            OriginalUrl = originalUrl
                        });
                    }
                }
            }

            return list;
        }

        private static string DownloadGifFromUrl(string url, string targetDir) {
            if (!Directory.Exists(targetDir)) {
                Directory.CreateDirectory(targetDir);
            }

            string ext = ".gif";
            try {
                var uri = new Uri(url);
                string pathExt = Path.GetExtension(uri.AbsolutePath);
                if (!string.IsNullOrEmpty(pathExt)) ext = pathExt;
            } catch {}

            string fileName = "giphy_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ext;
            string destPath = Path.Combine(targetDir, fileName);

            using (var client = new WebClient()) {
                client.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ZnipeAio/1.0");
                client.DownloadFile(url, destPath);
            }

            return destPath;
        }

        private class AnimatedCardState {
            public FrameworkElement CardElement;
            public ScrollViewer ScrollParent;
            public Image ImageControl;
            public List<BitmapSource> Frames;
            public int[] Delays;
            public int CurrentFrame;
            public int ElapsedMs;

            public void Advance(int deltaMs) {
                if (Frames == null || Frames.Count <= 1 || ImageControl == null) return;

                // Viewport Culling: do not waste UI thread invalidating cards that are scrolled off-screen
                if (CardElement != null && ScrollParent != null && CardElement.IsLoaded) {
                    try {
                        Point p = CardElement.TranslatePoint(new Point(0, 0), ScrollParent);
                        if (p.Y + CardElement.ActualHeight < 0 || p.Y > ScrollParent.ActualHeight) {
                            return;
                        }
                    } catch {}
                }

                ElapsedMs += deltaMs;
                int currentDelay = (Delays != null && CurrentFrame < Delays.Length) ? Delays[CurrentFrame] : 100;
                if (currentDelay < 20) currentDelay = 20;

                bool changed = false;
                while (ElapsedMs >= currentDelay) {
                    ElapsedMs -= currentDelay;
                    CurrentFrame = (CurrentFrame + 1) % Frames.Count;
                    currentDelay = (Delays != null && CurrentFrame < Delays.Length) ? Delays[CurrentFrame] : 100;
                    if (currentDelay < 20) currentDelay = 20;
                    changed = true;
                }

                if (changed) {
                    ImageControl.Source = Frames[CurrentFrame];
                }
            }
        }

        private class GiphyItem {
            public string Id { get; set; }
            public string Title { get; set; }
            public string StillUrl { get; set; }
            public string PreviewUrl { get; set; }
            public string OriginalUrl { get; set; }
        }
    }
}
