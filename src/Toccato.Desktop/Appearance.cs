using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Toccato.Desktop
{
    public enum ColorMode { Automatic, Light, Dark }
    public enum InstitutionalPalette { Blue, Cyan, Graphite }
    public enum ThemeRole { Surface, Canvas, SurfaceAlt, Sidebar, SidebarText, SidebarMuted, BodyText, MutedText, PrimaryButton, SecondaryButton, NavigationButton, AccentText, Preserve }

    [DataContract]
    public sealed class AppearancePreferences
    {
        [DataMember] public int Version = 1;
        [DataMember] public ColorMode Mode = ColorMode.Automatic;
        [DataMember] public InstitutionalPalette Palette = InstitutionalPalette.Blue;
        public void Validate()
        {
            if (Version != 1 || !Enum.IsDefined(typeof(ColorMode), Mode) || !Enum.IsDefined(typeof(InstitutionalPalette), Palette))
                throw new InvalidDataException("Preferências de aparência inválidas.");
        }
    }

    public sealed class ThemeColors
    {
        public bool Dark;
        public InstitutionalPalette Palette;
        public Color Canvas, Surface, SurfaceAlt, Input, Text, Muted, Border, Accent, AccentText, Selection, Sidebar, SidebarText, SidebarMuted, SidebarHover;
        public static ThemeColors Create(ColorMode mode, InstitutionalPalette palette, bool windowsDark)
        {
            bool dark = mode == ColorMode.Dark || (mode == ColorMode.Automatic && windowsDark);
            var c = new ThemeColors { Dark = dark, Palette = palette };
            c.Sidebar = palette == InstitutionalPalette.Cyan ? Hex("0C3343") : palette == InstitutionalPalette.Graphite ? Hex("202D3D") : Hex("102D4F");
            c.Accent = palette == InstitutionalPalette.Cyan ? Hex(dark ? "0B7F92" : "087286") : palette == InstitutionalPalette.Graphite ? Hex(dark ? "426CA6" : "285797") : Hex(dark ? "3672DF" : "1955BE");
            c.AccentText = dark ? (palette == InstitutionalPalette.Cyan ? Hex("78DEEA") : palette == InstitutionalPalette.Graphite ? Hex("AAC8F1") : Hex("99BEFF")) : c.Accent;
            c.Canvas = Hex(dark ? "0F1724" : "F3F6FB"); c.Surface = Hex(dark ? "172333" : "FFFFFF");
            c.SurfaceAlt = Hex(dark ? "1D2C3E" : "F7F9FD"); c.Input = Hex(dark ? "111D2C" : "FFFFFF");
            c.Text = Hex(dark ? "ECF2FA" : "1A293F"); c.Muted = Hex(dark ? "A9BBD1" : "60728B"); c.Border = Hex(dark ? "34485F" : "DAE3EF");
            c.Selection = palette == InstitutionalPalette.Cyan ? Hex(dark ? "16424D" : "E1F4F7") : palette == InstitutionalPalette.Graphite ? Hex(dark ? "2B3F59" : "EAF0F6") : Hex(dark ? "263F64" : "E8EFFF");
            c.SidebarText = Hex("ECF3FF"); c.SidebarMuted = Hex("A3BBD5"); c.SidebarHover = palette == InstitutionalPalette.Cyan ? Hex("174C5C") : palette == InstitutionalPalette.Graphite ? Hex("35465B") : Hex("203F65");
            return c;
        }
        private static Color Hex(string value) { return ColorTranslator.FromHtml("#" + value); }
    }

    public static class ThemeManager
    {
        private static AppearancePreferences preferences = new AppearancePreferences();
        private static ThemeColors current = ThemeColors.Create(ColorMode.Light, InstitutionalPalette.Blue, false);
        private static string preferencesPath;
        private static Func<bool> systemReader = ReadWindowsDarkMode;
        private static bool subscribed;
        public static event EventHandler Changed;
        public static ThemeColors Current { get { return current; } }
        public static AppearancePreferences Preferences { get { return new AppearancePreferences { Mode = preferences.Mode, Palette = preferences.Palette }; } }
        public static string PaletteName(InstitutionalPalette palette) { return palette == InstitutionalPalette.Cyan ? "Azul e ciano" : palette == InstitutionalPalette.Graphite ? "Azul e grafite" : "Azul institucional"; }
        public static string ModeName(ColorMode mode) { return mode == ColorMode.Light ? "Claro" : mode == ColorMode.Dark ? "Escuro" : "Automático (Windows)"; }
        public static void Initialize(string path = null, Func<bool> readSystemDark = null)
        {
            preferencesPath = path ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Toccato", "DesktopManager", "appearance.json");
            systemReader = readSystemDark ?? ReadWindowsDarkMode;
            preferences = new AppearancePreferences();
            try
            {
                if (File.Exists(preferencesPath) && new FileInfo(preferencesPath).Length <= 8192)
                {
                    var loaded = Json.Decode<AppearancePreferences>(File.ReadAllBytes(preferencesPath)); loaded.Validate(); preferences = loaded;
                }
            }
            catch (Exception ex) { if (!(ex is IOException || ex is UnauthorizedAccessException || ex is System.Runtime.Serialization.SerializationException || ex is ArgumentException || ex is FormatException)) throw; }
            if (!subscribed && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                SystemEvents.UserPreferenceChanged += SystemPreferenceChanged; subscribed = true;
            }
            Refresh(true);
        }
        public static void Save(ColorMode mode, InstitutionalPalette palette)
        {
            var next = new AppearancePreferences { Mode = mode, Palette = palette }; next.Validate();
            if (preferencesPath == null) Initialize();
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(preferencesPath)));
            string temp = preferencesPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temp, Json.Encode(next));
                if (File.Exists(preferencesPath)) File.Replace(temp, preferencesPath, null); else File.Move(temp, preferencesPath);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
            preferences = next; Refresh(true);
        }
        // Screenshot/demo overrides do not persist or change the Windows registry.
        public static void Preview(ColorMode mode, InstitutionalPalette palette)
        {
            var next = new AppearancePreferences { Mode = mode, Palette = palette }; next.Validate(); preferences = next; Refresh(true);
        }
        public static void RefreshSystemTheme() { if (preferences.Mode == ColorMode.Automatic) Refresh(false); }
        private static void SystemPreferenceChanged(object sender, UserPreferenceChangedEventArgs e) { RefreshSystemTheme(); }
        private static void Refresh(bool force)
        {
            bool dark = false; try { dark = systemReader(); } catch { }
            var next = ThemeColors.Create(preferences.Mode, preferences.Palette, dark);
            if (!force && next.Dark == current.Dark && next.Palette == current.Palette) return;
            current = next; var handler = Changed; if (handler != null) handler(null, EventArgs.Empty);
        }
        private static bool ReadWindowsDarkMode()
        {
            if (Environment.OSVersion.Platform != PlatformID.Win32NT) return false;
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                { var value = key == null ? null : key.GetValue("AppsUseLightTheme"); return value != null && Convert.ToInt32(value) == 0; }
            }
            catch { return false; }
        }
        public static void Shutdown()
        {
            if (subscribed) { SystemEvents.UserPreferenceChanged -= SystemPreferenceChanged; subscribed = false; }
        }
    }

    public static class BrandAssets
    {
        private static Image logo;
        private static Icon icon;
        public static Image Logo
        {
            get
            {
                if (logo == null) using (var stream = typeof(BrandAssets).Assembly.GetManifestResourceStream("Toccato.Desktop.BrandLogo.png"))
                { if (stream != null) using (var original = Image.FromStream(stream)) logo = new Bitmap(original); }
                return logo;
            }
        }
        public static Icon Icon
        {
            get
            {
                if (icon == null) using (var stream = typeof(BrandAssets).Assembly.GetManifestResourceStream("Toccato.Desktop.BrandIcon.ico"))
                { if (stream != null) using (var original = new Icon(stream)) icon = (Icon)original.Clone(); }
                return icon;
            }
        }
    }

    public class ThemedForm : Form
    {
        private bool listening;
        public ThemedForm() { if (BrandAssets.Icon != null) Icon = BrandAssets.Icon; }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            if (!listening) { ThemeManager.Changed += ThemeChanged; listening = true; }
            ApplyTitleBar();
        }
        protected override void OnLoad(EventArgs e) { base.OnLoad(e); ApplyAppearance(); }
        protected virtual void OnAppearanceChanged() { }
        public void ApplyAppearance()
        {
            if (IsDisposed || Disposing) return; Ui.ApplyTheme(this); ApplyTitleBar(); OnAppearanceChanged();
        }
        private void ThemeChanged(object sender, EventArgs e)
        {
            if (IsDisposed || Disposing || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(new Action(ApplyAppearance)); } catch (InvalidOperationException) { } }
            else ApplyAppearance();
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            if (listening) { ThemeManager.Changed -= ThemeChanged; listening = false; } base.OnHandleDestroyed(e);
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x001A || m.Msg == 0x031A) ThemeManager.RefreshSystemTheme(); base.WndProc(ref m);
        }
        private void ApplyTitleBar()
        {
            if (!IsHandleCreated || Environment.OSVersion.Platform != PlatformID.Win32NT) return;
            try { int dark = ThemeManager.Current.Dark ? 1 : 0; if (DwmSetWindowAttribute(Handle, 20, ref dark, 4) != 0) DwmSetWindowAttribute(Handle, 19, ref dark, 4); } catch (DllNotFoundException) { } catch (EntryPointNotFoundException) { }
        }
        [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
    }

    public sealed class ThemedTabControl : TabControl
    {
        public ThemedTabControl()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);
            Padding = new Point(18, 10); DrawMode = TabDrawMode.OwnerDrawFixed;
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var c = ThemeManager.Current; e.Graphics.Clear(c.Canvas);
            using (var surface = new SolidBrush(c.Surface)) e.Graphics.FillRectangle(surface, DisplayRectangle);
            for (int i = 0; i < TabCount; i++)
            {
                var r = GetTabRect(i); bool selected = i == SelectedIndex;
                using (var fill = new SolidBrush(selected ? c.Surface : c.SurfaceAlt)) e.Graphics.FillRectangle(fill, r);
                using (var border = new Pen(c.Border)) e.Graphics.DrawRectangle(border, r.X, r.Y, r.Width - 1, r.Height - 1);
                if (selected) using (var accent = new SolidBrush(c.Accent)) e.Graphics.FillRectangle(accent, r.X, r.Y, r.Width, 3);
                TextRenderer.DrawText(e.Graphics, TabPages[i].Text, Font, r, selected ? c.Text : c.Muted, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }
        }
    }

    internal sealed class ThemeColorTable : ProfessionalColorTable
    {
        public override Color MenuStripGradientBegin { get { return ThemeManager.Current.Surface; } }
        public override Color MenuStripGradientEnd { get { return ThemeManager.Current.Surface; } }
        public override Color ToolStripGradientBegin { get { return ThemeManager.Current.Surface; } }
        public override Color ToolStripGradientMiddle { get { return ThemeManager.Current.Surface; } }
        public override Color ToolStripGradientEnd { get { return ThemeManager.Current.Surface; } }
        public override Color ToolStripDropDownBackground { get { return ThemeManager.Current.Surface; } }
        public override Color MenuBorder { get { return ThemeManager.Current.Border; } }
        public override Color MenuItemBorder { get { return ThemeManager.Current.Border; } }
        public override Color MenuItemSelected { get { return ThemeManager.Current.Selection; } }
        public override Color MenuItemSelectedGradientBegin { get { return ThemeManager.Current.Selection; } }
        public override Color MenuItemSelectedGradientEnd { get { return ThemeManager.Current.Selection; } }
        public override Color MenuItemPressedGradientBegin { get { return ThemeManager.Current.Selection; } }
        public override Color MenuItemPressedGradientMiddle { get { return ThemeManager.Current.Selection; } }
        public override Color MenuItemPressedGradientEnd { get { return ThemeManager.Current.Selection; } }
        public override Color ImageMarginGradientBegin { get { return ThemeManager.Current.Surface; } }
        public override Color ImageMarginGradientMiddle { get { return ThemeManager.Current.Surface; } }
        public override Color ImageMarginGradientEnd { get { return ThemeManager.Current.Surface; } }
        public override Color SeparatorDark { get { return ThemeManager.Current.Border; } }
        public override Color SeparatorLight { get { return ThemeManager.Current.Border; } }
    }

    public sealed class PaletteCard : RadioButton
    {
        public InstitutionalPalette Palette { get; private set; }
        public PaletteCard(InstitutionalPalette palette)
        {
            Palette = palette; Text = ThemeManager.PaletteName(palette); Dock = DockStyle.Fill; Height = 146;
            AutoSize = false; Appearance = Appearance.Button; FlatStyle = FlatStyle.Flat; Cursor = Cursors.Hand;
            Margin = new Padding(3, 2, 7, 2); AccessibleName = Text; Font = Ui.Font(9, FontStyle.Bold);
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }
        protected override void OnPaint(PaintEventArgs e)
        {
            var current = ThemeManager.Current; var sample = ThemeColors.Create(ColorMode.Light, Palette, false);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias; e.Graphics.Clear(current.Surface);
            using (var pen = new Pen(Checked ? current.Accent : current.Border, Checked ? 2 : 1)) e.Graphics.DrawRectangle(pen, 1, 1, Width - 3, Height - 3);
            using (var pen = new Pen(Checked ? current.AccentText : current.Muted)) e.Graphics.DrawEllipse(pen, Width / 2 - 7, 17, 14, 14);
            if (Checked) using (var fill = new SolidBrush(current.AccentText)) e.Graphics.FillEllipse(fill, Width / 2 - 4, 20, 8, 8);
            Color[] swatches = { sample.Sidebar, sample.Accent, sample.Selection };
            for (int i = 0; i < swatches.Length; i++) using (var fill = new SolidBrush(swatches[i])) e.Graphics.FillRectangle(fill, Width / 2 - 39 + 26 * i, 49, 26, 28);
            TextRenderer.DrawText(e.Graphics, Text, Font, new Rectangle(4, 91, Width - 8, 40), current.Text, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.WordBreak);
            if (Focused) ControlPaint.DrawFocusRectangle(e.Graphics, new Rectangle(6, 6, Width - 12, Height - 12), current.Text, current.Surface);
        }
    }

    public sealed class AppearanceDialog : EditorForm
    {
        private readonly ComboBox mode;
        private readonly PaletteCard[] cards;
        private readonly Label summary;
        public AppearanceDialog() : base("Temas e aparência", "Escolha a paleta institucional e o modo de cor para todas as janelas do TDM.", 620)
        {
            ClientSize = new Size(820, 620);
            var prefs = ThemeManager.Preferences;
            var palettes = new TableLayoutPanel { Height = 150, ColumnCount = 3, RowCount = 1 };
            cards = new[] { new PaletteCard(InstitutionalPalette.Blue), new PaletteCard(InstitutionalPalette.Cyan), new PaletteCard(InstitutionalPalette.Graphite) };
            for (int i = 0; i < cards.Length; i++) { palettes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.333f)); palettes.Controls.Add(cards[i], i, 0); cards[i].Checked = cards[i].Palette == prefs.Palette; }
            mode = Choice(new object[] { "Automático (Windows)", "Claro", "Escuro" }); mode.SelectedIndex = (int)prefs.Mode;
            Add("Paleta institucional", palettes); Add("Modo de cor", mode);
            summary = Ui.Label("", 10, Ui.Muted); summary.MaximumSize = new Size(560, 0); Add("Como funciona", summary);
            var help = Ui.Label("As três paletas têm variantes claras e escuras. Automático acompanha o modo de aplicativos do Windows; sem essa informação, utiliza o modo claro. A preferência é salva para seu usuário e aplicada sem reiniciar o aplicativo.", 9, Ui.Muted); help.MaximumSize = new Size(560, 0); Add("Preferência do usuário", help);
            foreach (var card in cards) card.CheckedChanged += (s, e) => UpdateSummary(); mode.SelectedIndexChanged += (s, e) => UpdateSummary();
            SaveButton.Text = "Aplicar"; UpdateSummary();
        }
        private void UpdateSummary()
        {
            if (summary == null) return;
            summary.Text = mode.SelectedIndex == 0 ? "O TDM alterna entre claro e escuro quando você muda o modo dos aplicativos no Windows." : mode.SelectedIndex == 2 ? "Escuro: superfícies escuras e texto claro em todas as áreas do aplicativo." : "Claro: superfícies claras com as cores da paleta selecionada.";
        }
        protected override bool Save()
        {
            PaletteCard selected = null; foreach (var card in cards) if (card.Checked) selected = card;
            if (selected == null || mode.SelectedIndex < 0) throw new ArgumentException("Selecione uma paleta e um modo de cor.");
            ThemeManager.Save((ColorMode)mode.SelectedIndex, selected.Palette); return true;
        }
    }
}
