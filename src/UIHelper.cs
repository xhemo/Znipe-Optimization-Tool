using System;
using System.Collections.Generic;
using System.Windows.Controls;
using System.Windows.Media;

namespace ZnipeOptimizationTool {
    /// <summary>
    /// Zentraler Helper für UI-Styling, Status-Pills und gecachte Brushes nach dem KISS-Prinzip.
    /// Vermeidet hunderte wiederholte new BrushConverter()-Aufrufe und manuelle Farbcodes.
    /// </summary>
    public static class UIHelper {
        private static readonly BrushConverter _conv = new BrushConverter();
        private static readonly Dictionary<string, Brush> _brushCache = new Dictionary<string, Brush>(StringComparer.OrdinalIgnoreCase);

        // Standard-Farben für Znipe Optimization Tool Dark-Theme
        public static readonly Brush BrushGreenText   = GetBrush("#34D399");
        public static readonly Brush BrushGreenBg     = GetBrush("#162B23");
        public static readonly Brush BrushGreenBorder = GetBrush("#059669");

        public static readonly Brush BrushGrayText    = GetBrush("#94A3B8");
        public static readonly Brush BrushGrayBg      = GetBrush("#262C3A");
        public static readonly Brush BrushGrayBorder  = GetBrush("#334155");

        public static readonly Brush BrushAmberText   = GetBrush("#FBBF24");
        public static readonly Brush BrushAmberBg     = GetBrush("#2A2213");
        public static readonly Brush BrushAmberBorder = GetBrush("#D97706");

        public static readonly Brush BrushRedText     = GetBrush("#F87171");
        public static readonly Brush BrushRedBg       = GetBrush("#2B1618");
        public static readonly Brush BrushRedBorder   = GetBrush("#DC2626");

        public static readonly Brush BrushBlueText    = GetBrush("#60A5FA");
        public static readonly Brush BrushBlueBg      = GetBrush("#172554");
        public static readonly Brush BrushBlueBorder  = GetBrush("#2563EB");

        public static readonly Brush BrushWhite       = GetBrush("#FFFFFF");
        public static readonly Brush BrushMuted       = GetBrush("#64748B");

        /// <summary>
        /// Gibt einen gecachten SolidColorBrush für einen Hex-Farbcode zurück.
        /// </summary>
        public static Brush GetBrush(string hexColor) {
            if (string.IsNullOrEmpty(hexColor)) return Brushes.Transparent;
            lock (_brushCache) {
                Brush brush;
                if (_brushCache.TryGetValue(hexColor, out brush)) return brush;
                try {
                    brush = (Brush)_conv.ConvertFromString(hexColor);
                    if (brush != null && brush.CanFreeze) brush.Freeze();
                    _brushCache[hexColor] = brush;
                    return brush;
                } catch {
                    return Brushes.Transparent;
                }
            }
        }

        private static void ApplyPill(Border pill, TextBlock txt, string text, Brush fg, Brush bg, Brush border) {
            if (pill != null) {
                pill.Background = bg;
                pill.BorderBrush = border;
            }
            if (txt != null) {
                txt.Text = text;
                txt.Foreground = fg;
            }
        }

        /// <summary>
        /// Setzt eine Status-Pill (Badge) auf aktiv (Grün) oder inaktiv (Grau).
        /// </summary>
        public static void SetPill(Border pill, TextBlock txt, bool active, string activeText = "AKTIV", string inactiveText = "DEAKTIVIERT") {
            if (active) ApplyPill(pill, txt, activeText, BrushGreenText, BrushGreenBg, BrushGreenBorder);
            else ApplyPill(pill, txt, inactiveText, BrushGrayText, BrushGrayBg, BrushGrayBorder);
        }

        /// <summary>
        /// Setzt eine Status-Pill auf Warnung / Update verfügbar (Gelb/Amber).
        /// </summary>
        public static void SetPillWarning(Border pill, TextBlock txt, string text) {
            ApplyPill(pill, txt, text, BrushAmberText, BrushAmberBg, BrushAmberBorder);
        }

        /// <summary>
        /// Setzt eine Status-Pill auf Gefahr / Nicht empfohlen (Rot).
        /// </summary>
        public static void SetPillDanger(Border pill, TextBlock txt, string text) {
            ApplyPill(pill, txt, text, BrushRedText, BrushRedBg, BrushRedBorder);
        }

        /// <summary>
        /// Bereinigt CPU-Namen und entfernt redundante Anhänge wie z. B. '8-Core Processor'.
        /// </summary>
        public static string CleanCpuName(string name) {
            if (string.IsNullOrEmpty(name)) return "";
            string s = System.Text.RegularExpressions.Regex.Replace(name, @"\s+\d+-Core\s+Processor", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s+Processor$", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            s = System.Text.RegularExpressions.Regex.Replace(s, @"\s{2,}", " ");
            return s.Trim();
        }
    }
}
