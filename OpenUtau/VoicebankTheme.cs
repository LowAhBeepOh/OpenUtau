using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using OpenUtau.App.Controls;
using OpenUtau.Core.Ustx;
using ReactiveUI;

namespace OpenUtau.App {
    /// <summary>
    /// The value a voicebank can put in <c>voicebank_theme</c> in its character.yaml.
    /// </summary>
    public enum VoicebankThemeMode {
        None,
        Auto,
        Light,
        Dark,
        Colorful,
        Custom,
    }

    /// <summary>
    /// The resolved colors of a piano roll palette, before they are turned into brushes.
    /// </summary>
    public struct VoicebankPaletteColors {
        public Color GridBackground;
        public Color GridBackgroundAlt;
        public Color TickLine;
        public Color TickLineLow;
        public Color BarNumber;
        public Color WhiteKey;
        public Color WhiteKeyName;
        public Color BlackKey;
        public Color BlackKeyName;
        public Color CenterKey;
        public Color CenterKeyName;
        public Color Note1;
        public Color Note2;
        public Color Note3;
        public Color Neutral;
    }

    /// <summary>
    /// A set of brushes/pens used to paint the piano roll of a single voicebank.
    /// </summary>
    public class VoicebankPalette {
        public IBrush GridBackgroundBrush { get; }
        public IBrush GridBackgroundAltBrush { get; }
        public IPen TickLinePen { get; }
        public IPen TickLineLowPen { get; }
        public IPen TickLineLowDashPen { get; }
        public IBrush BarNumberBrush { get; }
        public IPen BarNumberPen { get; }
        public IBrush WhiteKeyBrush { get; }
        public IBrush WhiteKeyNameBrush { get; }
        public IBrush BlackKeyBrush { get; }
        public IBrush BlackKeyNameBrush { get; }
        public IBrush CenterKeyBrush { get; }
        public IBrush CenterKeyNameBrush { get; }
        public IBrush Note1Brush { get; }
        public IBrush Note2Brush { get; }
        public IBrush Note3Brush { get; }
        public IBrush Note3SemiBrush { get; }
        public IBrush NeutralSemiBrush { get; }
        public IPen Note3Pen { get; }
        public IPen FinalPitchPen { get; }

        public VoicebankPalette(VoicebankPaletteColors c) {
            GridBackgroundBrush = new SolidColorBrush(c.GridBackground);
            GridBackgroundAltBrush = new SolidColorBrush(c.GridBackgroundAlt);
            TickLinePen = new Pen(new SolidColorBrush(c.TickLine), 1);
            var tickLowBrush = new SolidColorBrush(c.TickLineLow);
            TickLineLowPen = new Pen(tickLowBrush, 1);
            TickLineLowDashPen = new Pen(tickLowBrush, 1) {
                DashStyle = new DashStyle(new double[] { 2, 4 }, 0),
            };
            BarNumberBrush = new SolidColorBrush(c.BarNumber);
            BarNumberPen = new Pen(BarNumberBrush, 1);
            WhiteKeyBrush = new SolidColorBrush(c.WhiteKey);
            WhiteKeyNameBrush = new SolidColorBrush(c.WhiteKeyName);
            BlackKeyBrush = new SolidColorBrush(c.BlackKey);
            BlackKeyNameBrush = new SolidColorBrush(c.BlackKeyName);
            CenterKeyBrush = new SolidColorBrush(c.CenterKey);
            CenterKeyNameBrush = new SolidColorBrush(c.CenterKeyName);
            Note1Brush = new SolidColorBrush(c.Note1);
            Note2Brush = new SolidColorBrush(c.Note2);
            Note3Brush = new SolidColorBrush(c.Note3);
            Note3SemiBrush = new SolidColorBrush(c.Note3) { Opacity = 0.5 };
            NeutralSemiBrush = new SolidColorBrush(c.Neutral) { Opacity = 0.5 };
            Note3Pen = new Pen(new SolidColorBrush(c.Note3), 1);
            FinalPitchPen = new Pen(new SolidColorBrush(c.Neutral), 1);
        }
    }

    /// <summary>
    /// Builds and holds the palette derived from the currently opened track's voicebank.
    /// It only affects the piano roll; the rest of the app keeps using <see cref="ThemeManager"/>.
    /// </summary>
    public static class VoicebankTheme {
        public static VoicebankPalette? Current { get; private set; }

        private static string? appliedKey;

        // Fallbacks to the app theme when no voicebank theme is active.
        public static IBrush PianoRollNote1 => Current?.Note1Brush ?? ThemeManager.AccentBrush1;
        public static IBrush PianoRollNote2 => Current?.Note2Brush ?? ThemeManager.AccentBrush2;
        public static IBrush PianoRollNote3 => Current?.Note3Brush ?? ThemeManager.AccentBrush3;
        public static IBrush PianoRollNote3Semi => Current?.Note3SemiBrush ?? ThemeManager.AccentBrush3Semi;
        public static IBrush PianoRollNeutralSemi => Current?.NeutralSemiBrush ?? ThemeManager.NeutralAccentBrushSemi;
        public static IPen PianoRollNote3Pen => Current?.Note3Pen ?? ThemeManager.AccentPen3;
        public static IBrush PianoRollBarNumberBrush => Current?.BarNumberBrush ?? ThemeManager.BarNumberBrush;
        public static IPen PianoRollBarNumberPen => Current?.BarNumberPen ?? ThemeManager.BarNumberPen;
        public static IPen PianoRollFinalPitchPen => Current?.FinalPitchPen ?? ThemeManager.FinalPitchPen;

        public static VoicebankThemeMode Parse(string? value) {
            switch (value?.Trim().ToLowerInvariant()) {
                case "auto":
                    return VoicebankThemeMode.Auto;
                case "light":
                    return VoicebankThemeMode.Light;
                case "dark":
                    return VoicebankThemeMode.Dark;
                case "colorful":
                    return VoicebankThemeMode.Colorful;
                case "custom":
                    return VoicebankThemeMode.Custom;
                default:
                    return VoicebankThemeMode.None;
            }
        }

        /// <summary>
        /// Applies the voicebank theme of the given singer, or clears it when the singer
        /// has no theme. Cheap to call repeatedly: nothing is rebuilt unless the singer
        /// or the resolved light/dark mode changed.
        /// </summary>
        public static void Apply(USinger? singer) {
            var mode = Parse(singer?.VoicebankTheme);
            if (singer == null || mode == VoicebankThemeMode.None) {
                Clear();
                return;
            }
            // "auto" and "custom" follow the app's light/dark setting.
            var resolved = mode is VoicebankThemeMode.Auto or VoicebankThemeMode.Custom
                ? (ThemeManager.IsDarkMode ? VoicebankThemeMode.Dark : VoicebankThemeMode.Light)
                : mode;
            // The custom color block is part of the key so editing character.yaml rebuilds it.
            int customStamp = mode == VoicebankThemeMode.Custom && singer.VoicebankThemeColors != null
                ? singer.VoicebankThemeColors.GetHashCode()
                : 0;
            string key = $"{singer.Id}|{mode}|{resolved}|{customStamp}";
            if (key == appliedKey) {
                return;
            }
            VoicebankPalette? palette = null;
            try {
                if (mode == VoicebankThemeMode.Custom) {
                    palette = BuildCustom(singer, resolved);
                } else if (LoadPortraitOrIcon(singer, out var portrait)) {
                    palette = Build(portrait!, resolved);
                }
            } catch (Exception e) {
                Serilog.Log.Error(e, "Failed to build voicebank theme.");
            }
            if (palette == null) {
                Clear();
                return;
            }
            appliedKey = key;
            Current = palette;
            TextLayoutCache.Clear();
            MessageBus.Current.SendMessage(new ThemeChangedEvent());
        }

        public static void Clear() {
            if (Current == null && appliedKey == null) {
                return;
            }
            appliedKey = null;
            Current = null;
            TextLayoutCache.Clear();
            MessageBus.Current.SendMessage(new ThemeChangedEvent());
        }

        private static VoicebankPalette? Build(byte[] portrait, VoicebankThemeMode mode) {
            var hues = ExtractHues(portrait);
            if (hues.Count == 0) {
                return null;
            }
            PickHues(hues, out double h1, out double s1, out double h2, out double h3);
            return new VoicebankPalette(BuildPaletteColors(h1, s1, h2, h3, mode));
        }

        /// <summary>
        /// Builds a palette from the voicebank's custom colors, using a portrait-derived
        /// (or neutral) palette as the base for any color the voicebank left unspecified.
        /// </summary>
        private static VoicebankPalette? BuildCustom(USinger singer, VoicebankThemeMode mode) {
            var custom = singer.VoicebankThemeColors;
            bool hasCustom = custom != null && !custom.IsEmpty;
            bool hasImage = LoadPortraitOrIcon(singer, out var portrait);
            if (!hasCustom && !hasImage) {
                return null;
            }
            var colors = DefaultPaletteColors(mode);
            if (hasImage) {
                var hues = ExtractHues(portrait!);
                if (hues.Count > 0) {
                    PickHues(hues, out double h1, out double s1, out double h2, out double h3);
                    colors = BuildPaletteColors(h1, s1, h2, h3, mode);
                }
            }
            if (custom != null) {
                colors.GridBackground = ParseHex(custom.GridBackground, colors.GridBackground);
                colors.GridBackgroundAlt = ParseHex(custom.GridBackgroundAlt, colors.GridBackgroundAlt);
                colors.TickLine = ParseHex(custom.TickLine, colors.TickLine);
                colors.TickLineLow = ParseHex(custom.TickLineLow, colors.TickLineLow);
                colors.BarNumber = ParseHex(custom.BarNumber, colors.BarNumber);
                colors.WhiteKey = ParseHex(custom.WhiteKey, colors.WhiteKey);
                colors.WhiteKeyName = ParseHex(custom.WhiteKeyName, colors.WhiteKeyName);
                colors.BlackKey = ParseHex(custom.BlackKey, colors.BlackKey);
                colors.BlackKeyName = ParseHex(custom.BlackKeyName, colors.BlackKeyName);
                colors.CenterKey = ParseHex(custom.CenterKey, colors.CenterKey);
                colors.CenterKeyName = ParseHex(custom.CenterKeyName, colors.CenterKeyName);
                colors.Note1 = ParseHex(custom.Note, colors.Note1);
                colors.Note2 = ParseHex(custom.NoteSelected, colors.Note2);
                colors.Note3 = ParseHex(custom.NoteError, colors.Note3);
                colors.Neutral = ParseHex(custom.Neutral, colors.Neutral);
            }
            return new VoicebankPalette(colors);
        }

        private static void PickHues(List<(double hue, double sat, int count)> hues,
            out double h1, out double s1, out double h2, out double h3) {
            h1 = hues[0].hue;
            s1 = hues[0].sat;
            h2 = hues.Count > 1 ? hues[1].hue : h1 + 150;
            if (hues.Count > 2) {
                h3 = hues[2].hue;
            } else if (hues.Count > 1) {
                h3 = AngleDistance(h1 + 180, h2) > 40 ? h1 + 180 : h2 + 180;
            } else {
                h3 = h1 + 210;
            }
        }

        private static VoicebankPaletteColors DefaultPaletteColors(VoicebankThemeMode mode) {
            return BuildPaletteColors(212, 0.3, 350, 32, mode);
        }

        private static Color ParseHex(string? value, Color fallback) {
            return !string.IsNullOrWhiteSpace(value) && Color.TryParse(value.Trim(), out var color)
                ? color
                : fallback;
        }

        private static bool LoadPortraitOrIcon(USinger singer, out byte[]? data) {
            data = null;
            if (!string.IsNullOrEmpty(singer.Portrait)) {
                try {
                    data = singer.LoadPortrait();
                } catch (Exception e) {
                    Serilog.Log.Error(e, "Failed to load portrait for voicebank theme.");
                }
            }
            if (data == null || data.Length == 0) {
                // Fall back to the character icon when the voicebank has no portrait.
                data = singer.AvatarData;
            }
            return data != null && data.Length > 0;
        }

        // Finds up to three distinct dominant hues in the portrait, ignoring blacks,
        // whites and greys. Neighbouring hue buckets are merged so noisy hues don't win.
        private static List<(double hue, double sat, int count)> ExtractHues(byte[] portrait) {
            var result = new List<(double hue, double sat, int count)>();
            using var stream = new MemoryStream(portrait, writable: false);
            using var bmp = Bitmap.DecodeToWidth(stream, 64, BitmapInterpolationMode.LowQuality);
            if (bmp == null || bmp.PixelSize.Width <= 0 || bmp.PixelSize.Height <= 0) {
                return result;
            }
            int w = bmp.PixelSize.Width;
            int h = bmp.PixelSize.Height;
            var pixels = new int[w * h];
            using (var wb = new WriteableBitmap(bmp.PixelSize, new Vector(96, 96),
                       Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul)) {
                using (var fb = wb.Lock()) {
                    bmp.CopyPixels(fb);
                    Marshal.Copy(fb.Address, pixels, 0, pixels.Length);
                }
            }

            const int buckets = 36; // 10 degrees each
            var count = new int[buckets];
            var sumR = new double[buckets];
            var sumG = new double[buckets];
            var sumB = new double[buckets];
            foreach (int p in pixels) {
                int a = (int)((uint)p >> 24) & 0xFF;
                if (a < 128) {
                    continue;
                }
                int r = (p >> 16) & 0xFF;
                int g = (p >> 8) & 0xFF;
                int b = p & 0xFF;
                int max = Math.Max(r, Math.Max(g, b));
                int min = Math.Min(r, Math.Min(g, b));
                if (max < 40) {
                    continue; // near black
                }
                if (min > 215) {
                    continue; // near white
                }
                double pixSat = max == 0 ? 0 : (max - min) / (double)max;
                if (pixSat < 0.15) {
                    continue; // grey
                }
                RgbToHsl(r, g, b, out double hh, out _, out _);
                int idx = (int)(hh / 360 * buckets) % buckets;
                count[idx]++;
                sumR[idx] += r;
                sumG[idx] += g;
                sumB[idx] += b;
            }

            // Merge each bucket with its neighbours into a cluster, then rank clusters.
            var ranked = Enumerable.Range(0, buckets)
                .Where(i => count[i] > 0)
                .Select(i => Refine(i, count, sumR, sumG, sumB))
                .OrderByDescending(c => c.count * (0.35 + c.sat))
                .ToList();
            foreach (var cluster in ranked) {
                if (result.Count >= 3) {
                    break;
                }
                if (cluster.count < 6) {
                    continue;
                }
                if (result.Any(r => AngleDistance(r.hue, cluster.hue) < 45)) {
                    continue;
                }
                result.Add(cluster);
            }
            return result;
        }

        private static (double hue, double sat, int count) Refine(
            int center, int[] count, double[] sumR, double[] sumG, double[] sumB) {
            int buckets = count.Length;
            double cx = 0;
            double cy = 0;
            double satSum = 0;
            int total = 0;
            for (int d = -2; d <= 2; d++) {
                int i = ((center + d) % buckets + buckets) % buckets;
                int c = count[i];
                if (c == 0) {
                    continue;
                }
                double bucketHue = (i + 0.5) * 360.0 / buckets;
                double rad = bucketHue * Math.PI / 180;
                cx += Math.Cos(rad) * c;
                cy += Math.Sin(rad) * c;
                satSum += RgbSat(sumR[i] / c, sumG[i] / c, sumB[i] / c) * c;
                total += c;
            }
            if (total == 0) {
                return (0, 0, 0);
            }
            double hue = Math.Atan2(cy, cx) * 180 / Math.PI;
            if (hue < 0) {
                hue += 360;
            }
            return (hue, satSum / total, total);
        }

        private static double RgbSat(double r, double g, double b) {
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            return max <= 0 ? 0 : (max - min) / max;
        }

        private static double AngleDistance(double a, double b) {
            double d = Math.Abs(((a - b) % 360 + 360) % 360);
            return d > 180 ? 360 - d : d;
        }

        private static VoicebankPaletteColors BuildPaletteColors(double h1, double s1, double h2, double h3, VoicebankThemeMode mode) {
            // Surfaces stay muted and accents moderate, so the result is tinted rather than neon.
            double surface = Math.Clamp(s1 * 0.55, 0.16, 0.36);
            double accent = Math.Clamp(s1 * 0.9, 0.42, 0.72);

            Color grid, gridAlt, tick, tickLow, bar;
            Color whiteKey, whiteKeyName, blackKey, blackKeyName, centerKey, centerKeyName;
            Color note1, note2, note3, neutral;
            if (mode == VoicebankThemeMode.Colorful) {
                // Spread the three extracted hues across the UI, each kept gentle.
                double cs = Math.Clamp(s1 * 0.6, 0.26, 0.48);
                double ns = Math.Clamp(s1 * 0.8, 0.4, 0.62);
                grid = FromHsl(h1, cs * 0.8, 0.30);
                gridAlt = FromHsl(h2, cs * 0.9, 0.23);
                tick = FromHsl(h3, cs * 0.8, 0.62);
                tickLow = FromHsl(h3, cs * 0.7, 0.48);
                bar = FromHsl(h3, cs * 0.6, 0.95);
                whiteKey = FromHsl(h2, cs, 0.62);
                whiteKeyName = ReadableOn(whiteKey, Avalonia.Media.Colors.White, FromHsl(h2, cs * 1.2, 0.22));
                blackKey = FromHsl(h1, cs, 0.20);
                blackKeyName = Avalonia.Media.Colors.White;
                centerKey = FromHsl(h3, cs * 1.05, 0.68);
                centerKeyName = ReadableOn(centerKey, Avalonia.Media.Colors.White, FromHsl(h3, cs * 1.2, 0.25));
                note1 = FromHsl(h1, ns, 0.60);
                note2 = FromHsl(h2, ns, 0.58);
                note3 = FromHsl(h3, ns, 0.54);
                neutral = FromHsl(h1, cs * 0.4, 0.86);
            } else if (mode == VoicebankThemeMode.Dark) {
                grid = FromHsl(h1, surface * 0.7, 0.135);
                gridAlt = FromHsl(h1, surface, 0.20);
                tick = FromHsl(h1, surface, 0.36);
                tickLow = FromHsl(h1, surface * 0.8, 0.26);
                bar = FromHsl(h1, surface * 0.8, 0.80);
                whiteKey = FromHsl(h2, surface, 0.30);
                whiteKeyName = FromHsl(h2, surface * 0.6, 0.95);
                blackKey = FromHsl(h1, surface * 0.9, 0.11);
                blackKeyName = FromHsl(h1, surface * 0.6, 0.80);
                centerKey = FromHsl(h2, surface * 1.3, 0.46);
                centerKeyName = FromHsl(h2, surface * 0.6, 0.96);
                note1 = FromHsl(h1, accent, 0.56);
                note2 = FromHsl(h2, accent, 0.62);
                note3 = FromHsl(h3, accent, 0.58);
                neutral = FromHsl(h1, surface * 0.5, 0.52);
            } else {
                grid = FromHsl(h1, surface * 0.7, 0.975);
                gridAlt = FromHsl(h1, surface, 0.925);
                tick = FromHsl(h1, surface, 0.80);
                tickLow = FromHsl(h1, surface * 0.8, 0.88);
                bar = FromHsl(h1, accent * 0.7, 0.40);
                whiteKey = FromHsl(h2, surface * 0.6, 0.99);
                whiteKeyName = ReadableOn(whiteKey, Avalonia.Media.Colors.White, FromHsl(h2, accent, 0.34));
                blackKey = FromHsl(h1, accent * 0.85, 0.52);
                blackKeyName = ReadableOn(blackKey, Avalonia.Media.Colors.White, FromHsl(h1, accent, 0.20));
                centerKey = FromHsl(h2, surface * 1.35, 0.89);
                centerKeyName = ReadableOn(centerKey, Avalonia.Media.Colors.White, FromHsl(h2, accent, 0.34));
                note1 = FromHsl(h1, accent, 0.58);
                note2 = FromHsl(h2, accent, 0.54);
                note3 = FromHsl(h3, accent, 0.50);
                neutral = FromHsl(h1, surface * 0.6, 0.62);
            }
            return new VoicebankPaletteColors {
                GridBackground = grid,
                GridBackgroundAlt = gridAlt,
                TickLine = tick,
                TickLineLow = tickLow,
                BarNumber = bar,
                WhiteKey = whiteKey,
                WhiteKeyName = whiteKeyName,
                BlackKey = blackKey,
                BlackKeyName = blackKeyName,
                CenterKey = centerKey,
                CenterKeyName = centerKeyName,
                Note1 = note1,
                Note2 = note2,
                Note3 = note3,
                Neutral = neutral,
            };
        }

        private static Color ReadableOn(Color background, Color light, Color dark) {
            return RelativeLuminance(background) > 0.5 ? dark : light;
        }

        private static double RelativeLuminance(Color color) {
            return 0.2126 * Channel(color.R) + 0.7152 * Channel(color.G) + 0.0722 * Channel(color.B);
        }

        private static double Channel(byte value) {
            double s = value / 255.0;
            return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }

        private static void RgbToHsl(double r, double g, double b, out double h, out double s, out double l) {
            r /= 255;
            g /= 255;
            b /= 255;
            double max = Math.Max(r, Math.Max(g, b));
            double min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2;
            double d = max - min;
            if (d < 1e-6) {
                h = 0;
                s = 0;
                return;
            }
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) {
                h = 60 * (((g - b) / d) % 6);
            } else if (max == g) {
                h = 60 * (((b - r) / d) + 2);
            } else {
                h = 60 * (((r - g) / d) + 4);
            }
            if (h < 0) {
                h += 360;
            }
        }

        private static Color FromHsl(double h, double s, double l) {
            h = ((h % 360) + 360) % 360;
            s = Math.Clamp(s, 0, 1);
            l = Math.Clamp(l, 0, 1);
            double c = (1 - Math.Abs(2 * l - 1)) * s;
            double x = c * (1 - Math.Abs((h / 60) % 2 - 1));
            double m = l - c / 2;
            double r, g, b;
            if (h < 60) {
                r = c; g = x; b = 0;
            } else if (h < 120) {
                r = x; g = c; b = 0;
            } else if (h < 180) {
                r = 0; g = c; b = x;
            } else if (h < 240) {
                r = 0; g = x; b = c;
            } else if (h < 300) {
                r = x; g = 0; b = c;
            } else {
                r = c; g = 0; b = x;
            }
            return Color.FromArgb(255,
                (byte)Math.Round((r + m) * 255),
                (byte)Math.Round((g + m) * 255),
                (byte)Math.Round((b + m) * 255));
        }
    }
}
