using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace NeoruInstallerSkin
{
    internal enum SkinStyle
    {
        PixelSharp,
        Win95Bevel,
        ParchmentGold,
        ArchiveTerminal
    }

    internal enum SkinButtonKind
    {
        Primary,
        Secondary,
        SubtleLink,
        BoostyHighlight
    }

    internal sealed class SkinSpec
    {
        public SkinStyle Style;
        public bool IsLightSurface;
        public string Win95Caption;

        public Color BgDeep;
        public Color CardSurface;
        public Color RecessedBg;
        public Color Border;
        public Color BorderHi;
        public Color BorderLo;

        public Color AccentPrimary;
        public Color AccentHover;
        public Color AccentPressed;
        public Color AccentText;

        public Color SecondaryBg;
        public Color SecondaryHover;
        public Color SecondaryPressed;
        public Color SecondaryBorder;

        public Color BoostyGlowBg;
        public Color BoostyGlowHover;
        public Color BoostyGlowBorder;
        public Color BoostyGlowText;

        public Color TextPrimary;
        public Color TextMuted;
        public Color TextDim;

        public Color SuccessBg;
        public Color SuccessBorder;
        public Color SuccessFg;

        public Color WarnBg;
        public Color WarnBorder;
        public Color WarnFg;

        public Color Win95TitleStart;
        public Color Win95TitleEnd;

        public static SkinSpec CreatePixelSharp(
            Color bgDeep,
            Color cardSurface,
            Color recessedBg,
            Color border,
            Color accentPrimary,
            Color accentText)
        {
            SkinSpec s = new SkinSpec();
            s.Style = SkinStyle.PixelSharp;
            s.IsLightSurface = false;
            s.BgDeep = bgDeep;
            s.CardSurface = cardSurface;
            s.RecessedBg = recessedBg;
            s.Border = border;
            s.BorderHi = ShiftColor(border, 28);
            s.BorderLo = ShiftColor(bgDeep, -12);

            s.AccentPrimary = accentPrimary;
            s.AccentHover = ShiftColor(accentPrimary, 18);
            s.AccentPressed = ShiftColor(accentPrimary, -20);
            s.AccentText = accentText;

            s.SecondaryBg = ShiftColor(cardSurface, 12);
            s.SecondaryHover = ShiftColor(cardSurface, 24);
            s.SecondaryPressed = ShiftColor(cardSurface, -8);
            s.SecondaryBorder = ShiftColor(border, 16);

            s.BoostyGlowBg = Color.FromArgb(0x3B, 0x26, 0x10);
            s.BoostyGlowHover = Color.FromArgb(0x4D, 0x31, 0x14);
            s.BoostyGlowBorder = Color.FromArgb(0xF1, 0x8F, 0x32);
            s.BoostyGlowText = Color.FromArgb(0xFF, 0xE2, 0xB8);

            s.TextPrimary = Color.FromArgb(0xEE, 0xF2, 0xF7);
            s.TextMuted = Color.FromArgb(0xA4, 0xB2, 0xC4);
            s.TextDim = Color.FromArgb(0x6D, 0x7D, 0x90);

            s.SuccessBg = Color.FromArgb(0x13, 0x2F, 0x22);
            s.SuccessBorder = Color.FromArgb(0x2B, 0x7A, 0x52);
            s.SuccessFg = Color.FromArgb(0x6C, 0xEC, 0xA4);

            s.WarnBg = Color.FromArgb(0x2F, 0x22, 0x12);
            s.WarnBorder = Color.FromArgb(0x78, 0x54, 0x24);
            s.WarnFg = Color.FromArgb(0xF4, 0xB8, 0x58);
            return s;
        }

        public static SkinSpec CreateParchmentGold(
            Color bgDeep,
            Color cardSurface,
            Color recessedBg,
            Color border,
            Color accentPrimary,
            Color accentText)
        {
            SkinSpec s = CreatePixelSharp(bgDeep, cardSurface, recessedBg, border, accentPrimary, accentText);
            s.Style = SkinStyle.ParchmentGold;
            s.BorderHi = Color.FromArgb(0xC9, 0xA2, 0x5B);
            s.TextPrimary = Color.FromArgb(0xF5, 0xEE, 0xDE);
            s.TextMuted = Color.FromArgb(0xB8, 0xAD, 0x98);
            s.TextDim = Color.FromArgb(0x7D, 0x74, 0x64);
            return s;
        }

        public static SkinSpec CreateArchiveTerminal(
            Color bgDeep,
            Color cardSurface,
            Color recessedBg,
            Color border,
            Color accentPrimary,
            Color accentText)
        {
            SkinSpec s = CreatePixelSharp(bgDeep, cardSurface, recessedBg, border, accentPrimary, accentText);
            s.Style = SkinStyle.ArchiveTerminal;
            s.BorderHi = accentPrimary;
            s.SuccessBg = Color.FromArgb(0x12, 0x2C, 0x27);
            s.SuccessBorder = Color.FromArgb(0x3B, 0x99, 0x80);
            s.SuccessFg = Color.FromArgb(0x7E, 0xF2, 0xC8);
            return s;
        }

        public static SkinSpec CreateWin95Bevel(string captionBar)
        {
            SkinSpec s = new SkinSpec();
            s.Style = SkinStyle.Win95Bevel;
            s.IsLightSurface = true;
            s.Win95Caption = string.IsNullOrEmpty(captionBar) ? "Playrooms95 - Установка" : captionBar;

            s.BgDeep = Color.FromArgb(0xC0, 0xC0, 0xC0);
            s.CardSurface = Color.FromArgb(0xC8, 0xC8, 0xC8);
            s.RecessedBg = Color.FromArgb(0xFF, 0xFF, 0xFF);
            s.Border = Color.FromArgb(0x80, 0x80, 0x80);
            s.BorderHi = Color.FromArgb(0xFF, 0xFF, 0xFF);
            s.BorderLo = Color.FromArgb(0x40, 0x40, 0x40);

            s.AccentPrimary = Color.FromArgb(0x00, 0x58, 0x88);
            s.AccentHover = Color.FromArgb(0x08, 0x6E, 0xA6);
            s.AccentPressed = Color.FromArgb(0x00, 0x43, 0x68);
            s.AccentText = Color.FromArgb(0xFF, 0xFF, 0xFF);

            s.SecondaryBg = Color.FromArgb(0xC8, 0xC8, 0xC8);
            s.SecondaryHover = Color.FromArgb(0xD6, 0xD6, 0xD6);
            s.SecondaryPressed = Color.FromArgb(0xB4, 0xB4, 0xB4);
            s.SecondaryBorder = Color.FromArgb(0x80, 0x80, 0x80);

            s.BoostyGlowBg = Color.FromArgb(0xFF, 0xEB, 0xB8);
            s.BoostyGlowHover = Color.FromArgb(0xFF, 0xF3, 0xD1);
            s.BoostyGlowBorder = Color.FromArgb(0xB0, 0x64, 0x10);
            s.BoostyGlowText = Color.FromArgb(0x2A, 0x18, 0x04);

            s.TextPrimary = Color.FromArgb(0x11, 0x11, 0x11);
            s.TextMuted = Color.FromArgb(0x36, 0x36, 0x36);
            s.TextDim = Color.FromArgb(0x55, 0x55, 0x55);

            s.SuccessBg = Color.FromArgb(0xD7, 0xEE, 0xDC);
            s.SuccessBorder = Color.FromArgb(0x28, 0x7A, 0x42);
            s.SuccessFg = Color.FromArgb(0x0E, 0x4F, 0x24);

            s.WarnBg = Color.FromArgb(0xF5, 0xE6, 0xC0);
            s.WarnBorder = Color.FromArgb(0x9E, 0x6B, 0x18);
            s.WarnFg = Color.FromArgb(0x5A, 0x39, 0x06);

            s.Win95TitleStart = Color.FromArgb(0x00, 0x00, 0x80);
            s.Win95TitleEnd = Color.FromArgb(0x10, 0x84, 0xD0);
            return s;
        }

        private static Color ShiftColor(Color c, int delta)
        {
            int r = ClampByte(c.R + delta);
            int g = ClampByte(c.G + delta);
            int b = ClampByte(c.B + delta);
            return Color.FromArgb(c.A, r, g, b);
        }

        private static int ClampByte(int value)
        {
            if (value < 0) return 0;
            if (value > 255) return 255;
            return value;
        }
    }

    internal static class SkinLinks
    {
        public const string DefaultBoostyUrl = "https://boosty.to/cd_tixo";
        public const string DefaultSiteUrl = "https://neoru-droid.github.io/";

        public static string NormalizeCampaignSlug(string raw)
        {
            if (string.IsNullOrEmpty(raw))
            {
                return "hub";
            }
            string lower = raw.Trim().ToLowerInvariant();
            string cleaned = Regex.Replace(lower, "[^a-z0-9]+", "_").Trim('_');
            return string.IsNullOrEmpty(cleaned) ? "hub" : cleaned;
        }

        public static string BuildUtmUrl(string baseUrl, string fallbackUrl, string gameName)
        {
            string url = string.IsNullOrEmpty(baseUrl) ? fallbackUrl : baseUrl.Trim();
            int qIdx = url.IndexOf('?');
            if (qIdx >= 0)
            {
                url = url.Substring(0, qIdx);
            }
            if (string.IsNullOrEmpty(url))
            {
                url = fallbackUrl;
            }
            int schemeIdx = url.IndexOf("://", StringComparison.Ordinal);
            int pathStart = schemeIdx >= 0 ? url.IndexOf('/', schemeIdx + 3) : -1;
            if (pathStart >= 0 && pathStart < url.Length - 1)
            {
                url = url.TrimEnd('/');
            }
            else if (pathStart < 0 && url.EndsWith(".github.io", StringComparison.OrdinalIgnoreCase))
            {
                url = url + "/";
            }
            string slug = NormalizeCampaignSlug(gameName);
            return url + "?utm_source=installer&utm_medium=windows&utm_campaign=" + slug;
        }
    }

    internal static class SkinResources
    {
        public const string BannerResourceName = "setup_banner.jpg";
        public const string LogoResourceName = "setup_logo.png";
        public const string TitleFontResourceName = "setup_title_font.ttf";

        public static Image LoadEmbeddedImage(string resourceName)
        {
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    string[] names = asm.GetManifestResourceNames();
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (names[i].EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
                        {
                            stream = asm.GetManifestResourceStream(names[i]);
                            break;
                        }
                    }
                }
                if (stream == null)
                {
                    return null;
                }
                using (stream)
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[16384];
                    int read;
                    while ((read = stream.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, read);
                    }
                    ms.Position = 0;
                    using (Image raw = Image.FromStream(ms))
                    {
                        return new Bitmap(raw);
                    }
                }
            }
            catch
            {
                return null;
            }
        }

        public static FontFamily LoadEmbeddedFontFamily(string resourceName, out PrivateFontCollection collection)
        {
            collection = null;
            IntPtr fontPtr = IntPtr.Zero;
            try
            {
                Assembly asm = Assembly.GetExecutingAssembly();
                Stream stream = asm.GetManifestResourceStream(resourceName);
                if (stream == null)
                {
                    string[] names = asm.GetManifestResourceNames();
                    for (int i = 0; i < names.Length; i++)
                    {
                        if (names[i].EndsWith(resourceName, StringComparison.OrdinalIgnoreCase))
                        {
                            stream = asm.GetManifestResourceStream(names[i]);
                            break;
                        }
                    }
                }
                if (stream == null)
                {
                    return null;
                }
                byte[] data;
                using (stream)
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[16384];
                    int read;
                    while ((read = stream.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, read);
                    }
                    data = ms.ToArray();
                }
                if (data.Length < 128)
                {
                    return null;
                }
                collection = new PrivateFontCollection();
                fontPtr = Marshal.AllocCoTaskMem(data.Length);
                Marshal.Copy(data, 0, fontPtr, data.Length);
                collection.AddMemoryFont(fontPtr, data.Length);
                if (collection.Families != null && collection.Families.Length > 0)
                {
                    return collection.Families[0];
                }
            }
            catch
            {
            }
            finally
            {
                if (fontPtr != IntPtr.Zero)
                {
                    try { Marshal.FreeCoTaskMem(fontPtr); } catch { }
                }
            }
            return null;
        }

        public static Font CreateHeadingFont(FontFamily embeddedFamily, string fallbackName, float pxSize, FontStyle style)
        {
            if (embeddedFamily != null)
            {
                FontStyle chosen = embeddedFamily.IsStyleAvailable(style)
                    ? style
                    : (embeddedFamily.IsStyleAvailable(FontStyle.Regular) ? FontStyle.Regular : FontStyle.Bold);
                try
                {
                    return new Font(embeddedFamily, pxSize, chosen, GraphicsUnit.Pixel);
                }
                catch
                {
                }
            }
            return new Font(string.IsNullOrEmpty(fallbackName) ? "Segoe UI" : fallbackName, pxSize, style, GraphicsUnit.Pixel);
        }

        public static Icon TryExtractExeIcon()
        {
            try
            {
                string exe = Application.ExecutablePath;
                if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
                {
                    return Icon.ExtractAssociatedIcon(exe);
                }
            }
            catch
            {
            }
            return null;
        }
    }

    internal static class SkinPainter
    {
        public static GraphicsPath CreateRoundedRect(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0 || r.Width <= 2 || r.Height <= 2)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = Math.Min(radius * 2, Math.Min(r.Width, r.Height));
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void DrawWindowBackdrop(Graphics g, Rectangle clientRect, SkinSpec spec)
        {
            using (SolidBrush bg = new SolidBrush(spec.BgDeep))
            {
                g.FillRectangle(bg, clientRect);
            }
            if (spec.Style == SkinStyle.Win95Bevel)
            {
                DrawWin95BevelFrame(g, new Rectangle(0, 0, clientRect.Width - 1, clientRect.Height - 1), false, spec);
                return;
            }
            if (spec.Style == SkinStyle.ArchiveTerminal)
            {
                using (Pen gridPen = new Pen(Color.FromArgb(14, spec.AccentPrimary)))
                {
                    for (int x = 24; x < clientRect.Width; x += 28)
                    {
                        g.DrawLine(gridPen, x, 0, x, clientRect.Height);
                    }
                }
                return;
            }
            if (spec.Style == SkinStyle.ParchmentGold)
            {
                using (LinearGradientBrush wash = new LinearGradientBrush(
                    clientRect,
                    Color.FromArgb(20, spec.BorderHi),
                    Color.FromArgb(0, spec.BgDeep),
                    90f))
                {
                    g.FillRectangle(wash, clientRect);
                }
            }
        }

        public static void DrawWin95BevelFrame(Graphics g, Rectangle r, bool sunken, SkinSpec spec)
        {
            Color tlOuter = sunken ? spec.Border : spec.BorderHi;
            Color tlInner = sunken ? spec.BorderLo : Color.FromArgb(0xDF, 0xDF, 0xDF);
            Color brOuter = sunken ? spec.BorderHi : Color.FromArgb(0x00, 0x00, 0x00);
            Color brInner = sunken ? Color.FromArgb(0xDF, 0xDF, 0xDF) : spec.Border;

            using (Pen pTlO = new Pen(tlOuter))
            using (Pen pTlI = new Pen(tlInner))
            using (Pen pBrO = new Pen(brOuter))
            using (Pen pBrI = new Pen(brInner))
            {
                g.DrawLine(pTlO, r.Left, r.Top, r.Right - 1, r.Top);
                g.DrawLine(pTlO, r.Left, r.Top, r.Left, r.Bottom - 1);
                g.DrawLine(pBrO, r.Left, r.Bottom, r.Right, r.Bottom);
                g.DrawLine(pBrO, r.Right, r.Top, r.Right, r.Bottom);

                g.DrawLine(pTlI, r.Left + 1, r.Top + 1, r.Right - 2, r.Top + 1);
                g.DrawLine(pTlI, r.Left + 1, r.Top + 1, r.Left + 1, r.Bottom - 2);
                g.DrawLine(pBrI, r.Left + 1, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pBrI, r.Right - 1, r.Top + 1, r.Right - 1, r.Bottom - 1);
            }
        }

        public static void DrawCard(Graphics g, Rectangle r, SkinSpec spec, int radiusPx)
        {
            if (spec.Style == SkinStyle.Win95Bevel)
            {
                using (SolidBrush fill = new SolidBrush(spec.CardSurface))
                {
                    g.FillRectangle(fill, r);
                }
                DrawWin95BevelFrame(g, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), false, spec);
                return;
            }
            if (spec.Style == SkinStyle.PixelSharp)
            {
                using (SolidBrush fill = new SolidBrush(spec.CardSurface))
                {
                    g.FillRectangle(fill, r);
                }
                using (Pen outer = new Pen(spec.Border, 2f))
                {
                    g.DrawRectangle(outer, r.X + 1, r.Y + 1, r.Width - 2, r.Height - 2);
                }
                using (Pen innerHi = new Pen(Color.FromArgb(46, spec.BorderHi), 1f))
                {
                    g.DrawLine(innerHi, r.X + 3, r.Y + 3, r.Right - 4, r.Y + 3);
                    g.DrawLine(innerHi, r.X + 3, r.Y + 3, r.X + 3, r.Bottom - 4);
                }
                return;
            }

            int rad = spec.Style == SkinStyle.ArchiveTerminal ? Math.Min(4, radiusPx) : radiusPx;
            using (GraphicsPath path = CreateRoundedRect(r, rad))
            using (SolidBrush fill = new SolidBrush(spec.CardSurface))
            using (Pen pen = new Pen(spec.Border, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }

            if (spec.Style == SkinStyle.ParchmentGold)
            {
                DrawGoldCornerBrackets(g, r, spec.BorderHi, 10);
            }
            else if (spec.Style == SkinStyle.ArchiveTerminal)
            {
                using (SolidBrush bar = new SolidBrush(spec.AccentPrimary))
                {
                    g.FillRectangle(bar, r.X + 1, r.Y + 8, 3, Math.Max(12, r.Height - 16));
                }
            }
        }

        public static void DrawInputFrame(Graphics g, Rectangle r, SkinSpec spec, int radiusPx)
        {
            if (spec.Style == SkinStyle.Win95Bevel)
            {
                using (SolidBrush fill = new SolidBrush(spec.RecessedBg))
                {
                    g.FillRectangle(fill, r);
                }
                DrawWin95BevelFrame(g, new Rectangle(r.X, r.Y, r.Width - 1, r.Height - 1), true, spec);
                return;
            }
            if (spec.Style == SkinStyle.PixelSharp)
            {
                using (SolidBrush fill = new SolidBrush(spec.RecessedBg))
                using (Pen pen = new Pen(spec.Border, 1f))
                {
                    g.FillRectangle(fill, r);
                    g.DrawRectangle(pen, r.X, r.Y, r.Width - 1, r.Height - 1);
                }
                return;
            }
            using (GraphicsPath path = CreateRoundedRect(r, Math.Min(5, radiusPx)))
            using (SolidBrush fill = new SolidBrush(spec.RecessedBg))
            using (Pen pen = new Pen(spec.Border, 1f))
            {
                g.FillPath(fill, path);
                g.DrawPath(pen, path);
            }
        }

        public static void DrawHeroBanner(
            Graphics g,
            Rectangle rect,
            Image banner,
            Image logo,
            SkinSpec spec,
            string title,
            string versionText,
            string subtitle,
            string statusText,
            Color statusFg,
            Color statusBg,
            Color statusBorder,
            Font titleFont,
            Font badgeFont,
            Font subFont,
            float scale)
        {
            int s8 = ScalePx(8, scale);
            int s12 = ScalePx(12, scale);
            int s16 = ScalePx(16, scale);
            int s24 = ScalePx(24, scale);

            Rectangle artRect = rect;
            if (spec.Style == SkinStyle.Win95Bevel)
            {
                DrawWin95BevelFrame(g, new Rectangle(rect.X, rect.Y, rect.Width - 1, rect.Height - 1), false, spec);
                Rectangle barRect = new Rectangle(rect.X + 3, rect.Y + 3, rect.Width - 6, s24);
                using (LinearGradientBrush barBrush = new LinearGradientBrush(barRect, spec.Win95TitleStart, spec.Win95TitleEnd, 0f))
                {
                    g.FillRectangle(barBrush, barRect);
                }
                TextRenderer.DrawText(
                    g,
                    spec.Win95Caption,
                    badgeFont,
                    new Rectangle(barRect.X + s8, barRect.Y, barRect.Width - s16, barRect.Height),
                    Color.White,
                    TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix | TextFormatFlags.EndEllipsis);
                artRect = new Rectangle(rect.X + 3, barRect.Bottom + 2, rect.Width - 6, rect.Bottom - barRect.Bottom - 5);
            }

            int clipRad = (spec.Style == SkinStyle.PixelSharp || spec.Style == SkinStyle.Win95Bevel) ? 0 : s8;
            GraphicsState saved = g.Save();
            using (GraphicsPath clipPath = CreateRoundedRect(artRect, clipRad))
            {
                g.SetClip(clipPath, CombineMode.Intersect);
                using (SolidBrush fallbackFill = new SolidBrush(Color.FromArgb(0x12, 0x16, 0x1E)))
                {
                    g.FillRectangle(fallbackFill, artRect);
                }
                if (banner != null)
                {
                    DrawImageCover(g, banner, artRect);
                }
                DrawBannerScrim(g, artRect);
                DrawHeroForeground(
                    g, artRect, logo, spec, title, versionText, subtitle,
                    statusText, statusFg, statusBg, statusBorder,
                    titleFont, badgeFont, subFont, scale);
            }
            g.Restore(saved);

            if (spec.Style == SkinStyle.Win95Bevel)
            {
                DrawWin95BevelFrame(g, new Rectangle(artRect.X, artRect.Y, artRect.Width - 1, artRect.Height - 1), true, spec);
            }
            else if (spec.Style == SkinStyle.PixelSharp)
            {
                using (Pen outer = new Pen(spec.AccentPrimary, 2f))
                {
                    g.DrawRectangle(outer, artRect.X + 1, artRect.Y + 1, artRect.Width - 2, artRect.Height - 2);
                }
            }
            else if (spec.Style == SkinStyle.ParchmentGold)
            {
                using (GraphicsPath borderPath = CreateRoundedRect(artRect, clipRad))
                using (Pen pen = new Pen(spec.Border, 1f))
                {
                    g.DrawPath(pen, borderPath);
                }
                DrawGoldCornerBrackets(g, artRect, spec.BorderHi, ScalePx(13, scale));
            }
            else
            {
                using (GraphicsPath borderPath = CreateRoundedRect(artRect, clipRad))
                using (Pen pen = new Pen(spec.Border, 1f))
                {
                    g.DrawPath(pen, borderPath);
                }
                DrawReticleCorners(g, artRect, spec.AccentPrimary, ScalePx(10, scale));
            }
        }

        private static void DrawBannerScrim(Graphics g, Rectangle r)
        {
            using (LinearGradientBrush horiz = new LinearGradientBrush(
                r,
                Color.FromArgb(236, 10, 13, 18),
                Color.FromArgb(52, 10, 13, 18),
                0f))
            {
                horiz.WrapMode = WrapMode.TileFlipXY;
                ColorBlend blend = new ColorBlend();
                blend.Positions = new float[] { 0.0f, 0.45f, 0.72f, 1.0f };
                blend.Colors = new Color[]
                {
                    Color.FromArgb(236, 10, 13, 18),
                    Color.FromArgb(196, 10, 13, 18),
                    Color.FromArgb(118, 10, 13, 18),
                    Color.FromArgb(58, 10, 13, 18)
                };
                horiz.InterpolationColors = blend;
                g.FillRectangle(horiz, r);
            }
            using (LinearGradientBrush bottom = new LinearGradientBrush(
                r,
                Color.FromArgb(0, 8, 10, 14),
                Color.FromArgb(172, 8, 10, 14),
                90f))
            {
                bottom.WrapMode = WrapMode.TileFlipXY;
                ColorBlend vblend = new ColorBlend();
                vblend.Positions = new float[] { 0.0f, 0.52f, 1.0f };
                vblend.Colors = new Color[]
                {
                    Color.FromArgb(0, 8, 10, 14),
                    Color.FromArgb(0, 8, 10, 14),
                    Color.FromArgb(172, 8, 10, 14)
                };
                bottom.InterpolationColors = vblend;
                g.FillRectangle(bottom, r);
            }
        }

        private static void DrawHeroForeground(
            Graphics g,
            Rectangle r,
            Image logo,
            SkinSpec spec,
            string title,
            string versionText,
            string subtitle,
            string statusText,
            Color statusFg,
            Color statusBg,
            Color statusBorder,
            Font titleFont,
            Font badgeFont,
            Font subFont,
            float scale)
        {
            int padX = ScalePx(18, scale);
            int padY = ScalePx(14, scale);
            int statusReserve = ScalePx(180, scale);
            int maxLeftW = Math.Max(ScalePx(240, scale), r.Width - padX * 2 - statusReserve);

            int nextY = r.Y + padY;
            if (logo != null && logo.Width > 0 && logo.Height > 0)
            {
                int maxLogoW = Math.Min(maxLeftW, ScalePx(330, scale));
                int maxLogoH = Math.Min(ScalePx(62, scale), r.Height - ScalePx(54, scale));
                float ratio = Math.Min((float)maxLogoW / logo.Width, (float)maxLogoH / logo.Height);
                int drawW = Math.Max(16, (int)Math.Round(logo.Width * ratio));
                int drawH = Math.Max(16, (int)Math.Round(logo.Height * ratio));
                Rectangle logoRect = new Rectangle(r.X + padX, nextY, drawW, drawH);

                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.DrawImage(logo, logoRect);
                nextY = logoRect.Bottom + ScalePx(7, scale);
            }
            else
            {
                Rectangle titleRect = new Rectangle(r.X + padX, nextY, maxLeftW, ScalePx(36, scale));
                DrawShadowText(g, title, titleFont, titleRect, Color.FromArgb(0xFA, 0xF8, 0xF2));
                nextY = titleRect.Bottom + ScalePx(4, scale);
            }

            if (!string.IsNullOrEmpty(versionText))
            {
                Size vSize = TextRenderer.MeasureText(g, versionText, badgeFont, new Size(ScalePx(140, scale), ScalePx(24, scale)), TextFormatFlags.NoPrefix);
                int pillW = vSize.Width + ScalePx(16, scale);
                int pillH = ScalePx(22, scale);
                Rectangle vPill = new Rectangle(r.X + padX, nextY, pillW, pillH);
                int pillRad = (spec.Style == SkinStyle.PixelSharp || spec.Style == SkinStyle.Win95Bevel) ? 0 : ScalePx(4, scale);
                using (GraphicsPath vp = CreateRoundedRect(vPill, pillRad))
                using (SolidBrush vb = new SolidBrush(spec.AccentPrimary))
                {
                    g.FillPath(vb, vp);
                }
                using (StringFormat vfmt = new StringFormat(StringFormatFlags.NoWrap))
                using (SolidBrush vtb = new SolidBrush(spec.AccentText))
                {
                    vfmt.Alignment = StringAlignment.Center;
                    vfmt.LineAlignment = StringAlignment.Center;
                    g.DrawString(versionText, badgeFont, vtb, new RectangleF(vPill.X, vPill.Y, vPill.Width, vPill.Height), vfmt);
                }

                int subX = vPill.Right + ScalePx(10, scale);
                Rectangle subRect = new Rectangle(subX, nextY, Math.Max(ScalePx(120, scale), r.Right - padX - subX), pillH);
                DrawBannerText(g, subtitle ?? string.Empty, subFont, subRect, Color.FromArgb(0xEE, 0xF2, 0xF7));
            }
            else if (!string.IsNullOrEmpty(subtitle))
            {
                Rectangle subRect = new Rectangle(r.X + padX, nextY, r.Width - padX * 2, ScalePx(22, scale));
                DrawBannerText(g, subtitle, subFont, subRect, Color.FromArgb(0xEE, 0xF2, 0xF7));
            }

            if (!string.IsNullOrEmpty(statusText))
            {
                Size stSize = TextRenderer.MeasureText(g, statusText, badgeFont, new Size(ScalePx(220, scale), ScalePx(28, scale)), TextFormatFlags.NoPrefix);
                int pillW = stSize.Width + ScalePx(26, scale);
                int pillH = ScalePx(26, scale);
                Rectangle stRect = new Rectangle(r.Right - padX - pillW, r.Y + padY, pillW, pillH);
                int stRad = (spec.Style == SkinStyle.PixelSharp || spec.Style == SkinStyle.Win95Bevel) ? 0 : ScalePx(13, scale);
                using (GraphicsPath sp = CreateRoundedRect(stRect, stRad))
                using (SolidBrush sb = new SolidBrush(Color.FromArgb(236, statusBg)))
                using (Pen spen = new Pen(statusBorder, 1f))
                {
                    g.FillPath(sb, sp);
                    g.DrawPath(spen, sp);
                }
                int dot = ScalePx(7, scale);
                using (SolidBrush dotBrush = new SolidBrush(statusFg))
                {
                    g.FillEllipse(dotBrush, stRect.X + ScalePx(10, scale), stRect.Y + (pillH - dot) / 2, dot, dot);
                }
                Rectangle txtRect = new Rectangle(stRect.X + ScalePx(20, scale), stRect.Y, stRect.Width - ScalePx(24, scale), pillH);
                DrawCrispText(g, statusText, badgeFont, txtRect, statusFg, StringAlignment.Near);
            }
        }

        public static void DrawStatusRibbon(
            Graphics g,
            Rectangle rect,
            SkinSpec spec,
            bool highlighted,
            string text,
            Font font,
            float scale)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            if (highlighted)
            {
                int rad = (spec.Style == SkinStyle.PixelSharp || spec.Style == SkinStyle.Win95Bevel) ? 0 : ScalePx(5, scale);
                using (GraphicsPath p = CreateRoundedRect(rect, rad))
                using (SolidBrush fill = new SolidBrush(spec.SuccessBg))
                using (Pen pen = new Pen(spec.SuccessBorder, 1f))
                {
                    g.FillPath(fill, p);
                    g.DrawPath(pen, p);
                }
                Rectangle tr = new Rectangle(rect.X + ScalePx(12, scale), rect.Y, rect.Width - ScalePx(24, scale), rect.Height);
                DrawCrispText(g, text, font, tr, spec.SuccessFg, StringAlignment.Near);
            }
            else
            {
                Rectangle tr = new Rectangle(rect.X + ScalePx(2, scale), rect.Y, rect.Width - ScalePx(4, scale), rect.Height);
                DrawCrispText(g, text, font, tr, spec.TextMuted, StringAlignment.Near);
            }
        }

        public static void DrawCrispText(
            Graphics g,
            string text,
            Font font,
            Rectangle rect,
            Color fg,
            StringAlignment hAlign)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            TextRenderingHint prev = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            try
            {
                using (StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap))
                using (SolidBrush brush = new SolidBrush(fg))
                {
                    fmt.Alignment = hAlign;
                    fmt.LineAlignment = StringAlignment.Center;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    g.DrawString(text, font, brush, new RectangleF(rect.X, rect.Y, rect.Width, rect.Height), fmt);
                }
            }
            finally
            {
                g.TextRenderingHint = prev;
            }
        }

        private static void DrawBannerText(Graphics g, string text, Font font, Rectangle rect, Color fg)
        {
            TextRenderingHint prev = g.TextRenderingHint;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            try
            {
                using (StringFormat fmt = new StringFormat(StringFormatFlags.NoWrap))
                using (SolidBrush shadow = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
                using (SolidBrush brush = new SolidBrush(fg))
                {
                    fmt.Alignment = StringAlignment.Near;
                    fmt.LineAlignment = StringAlignment.Center;
                    fmt.Trimming = StringTrimming.EllipsisCharacter;
                    RectangleF shRect = new RectangleF(rect.X + 1f, rect.Y + 1f, rect.Width, rect.Height);
                    RectangleF fgRect = new RectangleF(rect.X, rect.Y, rect.Width, rect.Height);
                    g.DrawString(text, font, shadow, shRect, fmt);
                    g.DrawString(text, font, brush, fgRect, fmt);
                }
            }
            finally
            {
                g.TextRenderingHint = prev;
            }
        }

        private static void DrawShadowText(Graphics g, string text, Font font, Rectangle rect, Color fg)
        {
            DrawBannerText(g, text, font, rect, fg);
        }

        private static void DrawImageCover(Graphics g, Image img, Rectangle dest)
        {
            if (img.Width <= 0 || img.Height <= 0 || dest.Width <= 0 || dest.Height <= 0)
            {
                return;
            }
            float targetAspect = (float)dest.Width / dest.Height;
            float imgAspect = (float)img.Width / img.Height;
            Rectangle src;
            if (imgAspect > targetAspect)
            {
                int srcW = Math.Max(1, (int)Math.Round(img.Height * targetAspect));
                int srcX = Math.Max(0, (img.Width - srcW) / 2);
                src = new Rectangle(srcX, 0, srcW, img.Height);
            }
            else
            {
                int srcH = Math.Max(1, (int)Math.Round(img.Width / targetAspect));
                int srcY = Math.Max(0, (img.Height - srcH) / 2);
                src = new Rectangle(0, srcY, img.Width, srcH);
            }
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.DrawImage(img, dest, src, GraphicsUnit.Pixel);
        }

        private static void DrawGoldCornerBrackets(Graphics g, Rectangle r, Color color, int len)
        {
            using (Pen pen = new Pen(color, 2f))
            {
                int x1 = r.X + 2;
                int y1 = r.Y + 2;
                int x2 = r.Right - 3;
                int y2 = r.Bottom - 3;
                g.DrawLine(pen, x1, y1, x1 + len, y1);
                g.DrawLine(pen, x1, y1, x1, y1 + len);
                g.DrawLine(pen, x2 - len, y1, x2, y1);
                g.DrawLine(pen, x2, y1, x2, y1 + len);
                g.DrawLine(pen, x1, y2, x1 + len, y2);
                g.DrawLine(pen, x1, y2 - len, x1, y2);
                g.DrawLine(pen, x2 - len, y2, x2, y2);
                g.DrawLine(pen, x2, y2 - len, x2, y2);
            }
        }

        private static void DrawReticleCorners(Graphics g, Rectangle r, Color color, int len)
        {
            using (Pen pen = new Pen(color, 1.5f))
            {
                int x1 = r.X + 1;
                int y1 = r.Y + 1;
                int x2 = r.Right - 2;
                int y2 = r.Bottom - 2;
                g.DrawLine(pen, x1, y1, x1 + len, y1);
                g.DrawLine(pen, x1, y1, x1, y1 + len);
                g.DrawLine(pen, x2 - len, y2, x2, y2);
                g.DrawLine(pen, x2, y2 - len, x2, y2);
            }
        }

        public static int ScalePx(int px, float scale)
        {
            return (int)Math.Round(px * scale);
        }
    }

    internal sealed class SkinnedButton : Control
    {
        private bool _hover;
        private bool _pressed;
        private bool _focused;

        public SkinSpec Spec;
        public SkinButtonKind Kind = SkinButtonKind.Secondary;
        public int CornerRadius = 6;

        public SkinnedButton()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw
                | ControlStyles.UserPaint
                | ControlStyles.Selectable
                | ControlStyles.StandardClick,
                true);
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnEnabledChanged(EventArgs e)
        {
            base.OnEnabledChanged(e);
            Cursor = Enabled ? Cursors.Hand : Cursors.Default;
            Invalidate();
        }

        protected override void OnMouseEnter(EventArgs e)
        {
            base.OnMouseEnter(e);
            _hover = true;
            Invalidate();
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            base.OnMouseLeave(e);
            _hover = false;
            _pressed = false;
            Invalidate();
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (e.Button == MouseButtons.Left && Enabled)
            {
                _pressed = true;
                Focus();
                Invalidate();
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            if (_pressed)
            {
                _pressed = false;
                Invalidate();
            }
        }

        protected override void OnGotFocus(EventArgs e)
        {
            base.OnGotFocus(e);
            _focused = true;
            Invalidate();
        }

        protected override void OnLostFocus(EventArgs e)
        {
            base.OnLostFocus(e);
            _focused = false;
            Invalidate();
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (Enabled && (e.KeyCode == Keys.Space || e.KeyCode == Keys.Enter))
            {
                e.Handled = true;
                OnClick(EventArgs.Empty);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            SkinSpec spec = Spec ?? SkinSpec.CreatePixelSharp(
                Color.FromArgb(0x0F, 0x14, 0x19),
                Color.FromArgb(0x18, 0x21, 0x2A),
                Color.FromArgb(0x10, 0x16, 0x1D),
                Color.FromArgb(0x2A, 0x38, 0x46),
                Color.FromArgb(0x2E, 0xCC, 0x71),
                Color.FromArgb(0x08, 0x15, 0x0E));

            Color parentBg = Parent != null ? Parent.BackColor : spec.BgDeep;
            using (SolidBrush clear = new SolidBrush(parentBg))
            {
                g.FillRectangle(clear, ClientRectangle);
            }

            Color bg;
            Color border;
            Color fg;
            ResolveColors(spec, out bg, out border, out fg);

            Rectangle rect = new Rectangle(0, 0, Math.Max(1, Width - 1), Math.Max(1, Height - 1));
            if (spec.Style == SkinStyle.Win95Bevel)
            {
                using (SolidBrush fill = new SolidBrush(bg))
                {
                    g.FillRectangle(fill, rect);
                }
                if (Kind != SkinButtonKind.SubtleLink || _hover || _pressed)
                {
                    SkinPainter.DrawWin95BevelFrame(g, rect, _pressed, spec);
                }
                else
                {
                    using (Pen subtlePen = new Pen(spec.Border, 1f))
                    {
                        g.DrawRectangle(subtlePen, rect);
                    }
                }
            }
            else if (spec.Style == SkinStyle.PixelSharp)
            {
                using (SolidBrush fill = new SolidBrush(bg))
                using (Pen pen = new Pen(border, Kind == SkinButtonKind.BoostyHighlight ? 2f : 1f))
                {
                    g.FillRectangle(fill, rect);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }
                if (Enabled && !_pressed && (Kind == SkinButtonKind.Primary || Kind == SkinButtonKind.BoostyHighlight))
                {
                    using (Pen topHi = new Pen(Color.FromArgb(90, 255, 255, 255), 1f))
                    {
                        g.DrawLine(topHi, rect.X + 2, rect.Y + 2, rect.Right - 2, rect.Y + 2);
                    }
                }
            }
            else
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                int rad = spec.Style == SkinStyle.ArchiveTerminal ? Math.Min(4, CornerRadius) : CornerRadius;
                using (GraphicsPath path = SkinPainter.CreateRoundedRect(rect, rad))
                using (SolidBrush fill = new SolidBrush(bg))
                using (Pen pen = new Pen(border, Kind == SkinButtonKind.BoostyHighlight ? 1.5f : 1f))
                {
                    g.FillPath(fill, path);
                    if (border.A > 0)
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }

            if (_focused && Enabled)
            {
                Rectangle focusRect = new Rectangle(rect.X + 3, rect.Y + 3, Math.Max(1, rect.Width - 6), Math.Max(1, rect.Height - 6));
                using (Pen fp = new Pen(Color.FromArgb(140, fg), 1f))
                {
                    fp.DashStyle = DashStyle.Dot;
                    g.DrawRectangle(fp, focusRect);
                }
            }

            Rectangle textRect = new Rectangle(
                rect.X + 4 + (_pressed ? 1 : 0),
                rect.Y + (_pressed ? 1 : 0),
                Math.Max(1, rect.Width - 8),
                Math.Max(1, rect.Height));
            SkinPainter.DrawCrispText(g, Text ?? string.Empty, Font, textRect, fg, StringAlignment.Center);
        }

        private void ResolveColors(SkinSpec spec, out Color bg, out Color border, out Color fg)
        {
            if (!Enabled)
            {
                bg = spec.IsLightSurface ? Color.FromArgb(0xC2, 0xC2, 0xC2) : spec.RecessedBg;
                border = spec.Border;
                fg = spec.TextDim;
                return;
            }
            if (Kind == SkinButtonKind.Primary)
            {
                bg = _pressed ? spec.AccentPressed : (_hover ? spec.AccentHover : spec.AccentPrimary);
                border = spec.Style == SkinStyle.ParchmentGold ? spec.BorderHi : bg;
                fg = spec.AccentText;
                return;
            }
            if (Kind == SkinButtonKind.BoostyHighlight)
            {
                bg = _pressed ? spec.BoostyGlowBg : (_hover ? spec.BoostyGlowHover : spec.BoostyGlowBg);
                border = spec.BoostyGlowBorder;
                fg = spec.BoostyGlowText;
                return;
            }
            if (Kind == SkinButtonKind.SubtleLink)
            {
                bg = _pressed ? spec.SecondaryPressed : (_hover ? spec.SecondaryBg : spec.BgDeep);
                border = _hover ? spec.SecondaryBorder : Color.FromArgb(spec.IsLightSurface ? 150 : 90, spec.Border);
                fg = _hover ? spec.TextPrimary : spec.TextMuted;
                return;
            }
            bg = _pressed ? spec.SecondaryPressed : (_hover ? spec.SecondaryHover : spec.SecondaryBg);
            border = spec.SecondaryBorder;
            fg = spec.TextPrimary;
        }
    }

    internal static class PreviewRenderer
    {
        private const int StateVisibleBit = 0x00000002;

        public static void RenderFormToPng(Form form, string outputPath)
        {
            if (form == null)
            {
                throw new ArgumentNullException("form");
            }
            string dir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-32000, -32000);
            form.CreateControl();
            form.PerformLayout();

            Size clientSz = form.ClientSize;
            using (Bitmap outBmp = new Bitmap(Math.Max(1, clientSz.Width), Math.Max(1, clientSz.Height), PixelFormat.Format24bppRgb))
            using (Graphics g = Graphics.FromImage(outBmp))
            {
                g.Clear(form.BackColor);
                InvokeControlOnPaint(form, g, new Rectangle(0, 0, clientSz.Width, clientSz.Height));

                for (int i = form.Controls.Count - 1; i >= 0; i--)
                {
                    Control ctrl = form.Controls[i];
                    if (ctrl == null || !IsOwnVisible(ctrl) || ctrl.Width <= 0 || ctrl.Height <= 0)
                    {
                        continue;
                    }
                    RenderChildControl(g, ctrl);
                }

                outBmp.Save(outputPath, ImageFormat.Png);
            }
        }

        private static bool IsOwnVisible(Control ctrl)
        {
            try
            {
                MethodInfo getState = typeof(Control).GetMethod(
                    "GetState",
                    BindingFlags.Instance | BindingFlags.NonPublic,
                    null,
                    new Type[] { typeof(int) },
                    null);
                if (getState != null)
                {
                    object val = getState.Invoke(ctrl, new object[] { StateVisibleBit });
                    if (val is bool)
                    {
                        return (bool)val;
                    }
                }
            }
            catch
            {
            }
            TextBox tb = ctrl as TextBox;
            if (tb != null && tb.ReadOnly)
            {
                return false;
            }
            return true;
        }

        private static void InvokeControlOnPaint(Control ctrl, Graphics g, Rectangle clip)
        {
            MethodInfo onPaint = ctrl.GetType().GetMethod(
                "OnPaint",
                BindingFlags.Instance | BindingFlags.NonPublic,
                null,
                new Type[] { typeof(PaintEventArgs) },
                null);
            if (onPaint != null)
            {
                onPaint.Invoke(ctrl, new object[] { new PaintEventArgs(g, clip) });
            }
        }

        private static void RenderChildControl(Graphics g, Control ctrl)
        {
            TextBox tb = ctrl as TextBox;
            if (tb != null)
            {
                using (SolidBrush bg = new SolidBrush(tb.BackColor))
                {
                    g.FillRectangle(bg, tb.Bounds);
                }
                SkinPainter.DrawCrispText(g, tb.Text ?? string.Empty, tb.Font, tb.Bounds, tb.ForeColor, StringAlignment.Near);
                return;
            }

            using (Bitmap childBmp = new Bitmap(ctrl.Width, ctrl.Height, PixelFormat.Format24bppRgb))
            using (Graphics cg = Graphics.FromImage(childBmp))
            {
                cg.Clear(ctrl.Parent != null ? ctrl.Parent.BackColor : Color.Black);
                InvokeControlOnPaint(ctrl, cg, new Rectangle(0, 0, ctrl.Width, ctrl.Height));
                g.DrawImageUnscaled(childBmp, ctrl.Left, ctrl.Top);
            }
        }
    }
}
