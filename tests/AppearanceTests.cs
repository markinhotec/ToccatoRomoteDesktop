using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using Toccato.Desktop;

internal static class AppearanceTests
{
    private static int checks;
    private static void Check(bool condition, string name) { if (!condition) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
    private static T Field<T>(object value, string name) { return (T)value.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(value); }
    private static bool Save(EditorForm form) { return (bool)form.GetType().GetMethod("Save", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(form, null); }
    private static void Invalid(Action action, string name)
    {
        try { action(); } catch (TargetInvocationException e) { Check(e.InnerException is ArgumentException, name); return; } throw new Exception("Expected controlled validation: " + name);
    }
    private static double Component(int value) { double s = value / 255.0; return s <= 0.04045 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4); }
    private static double Luminance(Color c) { return 0.2126 * Component(c.R) + 0.7152 * Component(c.G) + 0.0722 * Component(c.B); }
    private static double Contrast(Color a, Color b) { double x = Luminance(a), y = Luminance(b); return (Math.Max(x, y) + 0.05) / (Math.Min(x, y) + 0.05); }
    [STAThread]
    public static int Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
        string directory = Path.Combine(Path.GetTempPath(), "tdm-appearance-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        string settings = Path.Combine(directory, "appearance.json"); bool systemDark = false;
        try
        {
            ThemeManager.Initialize(settings, () => systemDark);
            Check(ThemeManager.Preferences.Mode == ColorMode.Automatic && !ThemeManager.Current.Dark, "initial appearance follows Windows with light fallback");
            systemDark = true; ThemeManager.RefreshSystemTheme(); Check(ThemeManager.Current.Dark, "automatic mode observes Windows theme changes");
            ThemeManager.Save(ColorMode.Light, InstitutionalPalette.Cyan); ThemeManager.RefreshSystemTheme(); Check(!ThemeManager.Current.Dark, "explicit light overrides a dark Windows theme");
            systemDark = false; ThemeManager.Save(ColorMode.Dark, InstitutionalPalette.Graphite); ThemeManager.RefreshSystemTheme(); Check(ThemeManager.Current.Dark, "explicit dark overrides a light Windows theme");
            ThemeManager.Initialize(settings, () => systemDark); Check(ThemeManager.Preferences.Mode == ColorMode.Dark && ThemeManager.Preferences.Palette == InstitutionalPalette.Graphite, "palette and mode persist across initialization");
            File.WriteAllText(settings, "not valid JSON"); ThemeManager.Initialize(settings, () => systemDark); Check(ThemeManager.Preferences.Mode == ColorMode.Automatic, "corrupt preferences recover with safe defaults");
            File.WriteAllText(settings, "null"); ThemeManager.Initialize(settings, () => systemDark); Check(ThemeManager.Preferences.Mode == ColorMode.Automatic, "null preferences recover without preventing startup");
            Check(BrandAssets.Logo != null && BrandAssets.Logo.Width == 392 && BrandAssets.Logo.Height == 108, "official wordmark is embedded without aspect changes");
            Check(BrandAssets.Icon != null && BrandAssets.Icon.Width >= 16, "official crest icon is embedded");
            foreach (InstitutionalPalette palette in Enum.GetValues(typeof(InstitutionalPalette)))
            foreach (ColorMode mode in new[] { ColorMode.Light, ColorMode.Dark })
            {
                var c = ThemeColors.Create(mode, palette, false);
                Check(Contrast(c.Text, c.Surface) >= 4.5 && Contrast(c.Muted, c.Surface) >= 4.5, palette + " " + mode + " readable text contrast");
                Check(Contrast(Color.White, c.Accent) >= 4.5, palette + " " + mode + " primary button contrast");
                Check(ThemeColors.Create(mode, palette, true).Dark == c.Dark, palette + " " + mode + " is independent of Windows preference");
            }
            using (var main = new MainForm(VaultSession.Demo(DemoData.Create())))
            {
                main.Show(); Application.DoEvents();
                var grid = Field<DataGridView>(main, "connections");
                grid.CurrentCell = grid.Rows[0].Cells[0]; grid.Rows[0].Selected = true; Application.DoEvents();
                Check(Field<Button>(main, "connectButton").Enabled, "selecting a connection enables its primary action on Windows and Mono");
                foreach (InstitutionalPalette palette in Enum.GetValues(typeof(InstitutionalPalette)))
                foreach (ColorMode mode in new[] { ColorMode.Light, ColorMode.Dark })
                {
                    ThemeManager.Preview(mode, palette); Application.DoEvents();
                    Check(grid.BackgroundColor.ToArgb() == ThemeManager.Current.Surface.ToArgb() && grid.DefaultCellStyle.ForeColor.ToArgb() == ThemeManager.Current.Text.ToArgb(), palette + " " + mode + " updates a live connection grid [bg=" + grid.BackgroundColor.ToArgb().ToString("X8") + ", expected=" + ThemeManager.Current.Surface.ToArgb().ToString("X8") + ", fg=" + grid.DefaultCellStyle.ForeColor.ToArgb().ToString("X8") + ", expected=" + ThemeManager.Current.Text.ToArgb().ToString("X8") + "]");
                    Check(Field<Button>(main, "connectButton").ForeColor.ToArgb() == Color.White.ToArgb(), palette + " " + mode + " preserves primary button text");
                    var primary = Field<Button>(main, "connectButton");
                    Check(Contrast(primary.ForeColor, primary.FlatAppearance.MouseOverBackColor) >= 4.5 && Contrast(primary.ForeColor, primary.FlatAppearance.MouseDownBackColor) >= 4.5, palette + " " + mode + " keeps hover and pressed button states readable");
                }
                using (var dialog = new AppearanceDialog())
                {
                    dialog.Show(main); Application.DoEvents();
                    var cards = Field<PaletteCard[]>(dialog, "cards"); cards[1].Checked = true; Field<ComboBox>(dialog, "mode").SelectedIndex = (int)ColorMode.Dark;
                    Check(Save(dialog) && ThemeManager.Preferences.Palette == InstitutionalPalette.Cyan && ThemeManager.Current.Dark, "appearance dialog applies selected palette and mode");
                    dialog.Close();
                }
                main.Close();
            }

            // The reported save case: one client, no stored credential, RDP and a custom port.
            var data = new VaultData(); var client = new ClientRecord { Name = "Cliente de homologação" }; data.Clients.Add(client);
            using (var dialog = new ConnectionDialog(data))
            {
                Field<TextBox>(dialog, "name").Text = "RDP de homologação"; Field<TextBox>(dialog, "host").Text = "192.0.2.13";
                Field<TextBox>(dialog, "user").Text = "test_user"; Field<TextBox>(dialog, "domain").Text = "EXAMPLE"; Field<NumericUpDown>(dialog, "port").Value = 3371;
                dialog.Show(); Application.DoEvents();
                ThemeManager.Preview(ColorMode.Light, InstitutionalPalette.Blue); ThemeManager.Preview(ColorMode.Dark, InstitutionalPalette.Cyan); Application.DoEvents();
                Check(Save(dialog) && dialog.Record.Port == 3371 && dialog.Record.CredentialId == "", "visible RDP form saves without a credential and preserves custom port through a theme change");
                string original = dialog.Record.Name;
                Field<ComboBox>(dialog, "client").SelectedIndex = -1; Invalid(() => Save(dialog), "missing client produces controlled validation instead of null reference");
                Check(dialog.Record.Name == original, "failed validation preserves the original connection record");
                Field<ComboBox>(dialog, "client").SelectedIndex = 0; Field<ComboBox>(dialog, "kind").SelectedIndex = -1;
                Invalid(() => Save(dialog), "missing protocol produces controlled validation");
                Field<ComboBox>(dialog, "kind").SelectedItem = ConnectionKind.RDP;
                Field<ComboBox>(dialog, "credential").SelectedIndex = -1;
                Check(Save(dialog) && dialog.Record.CredentialId == "", "sole optional None credential safely tolerates an empty native selection");
                string file = Path.Combine(directory, "legacy-compatible.tdmv");
                using (var vault = VaultSession.Create(file, "Compatibilidade", "long-test-password-only"))
                    vault.Transaction(d => { d.Clients.Add(client); d.Connections.Add(dialog.Record); }, "connection.saved", "test", "test");
                using (var vault = VaultSession.Open(file, "long-test-password-only")) Check(vault.Data.SchemaVersion == 1 && vault.Data.Connections.Count == 1 && vault.Data.Credentials.Count == 0, "vault schema and no-credential connection round trip remain compatible");
                Check(Encoding.ASCII.GetString(File.ReadAllBytes(file), 0, 8) == "TDMVLT01", "appearance does not change the encrypted vault format");
                dialog.Close();
            }
            var error = new InvalidCastException("password=DO-NOT-LOG-THIS, host=192.0.2.200"); error.Data["secret"] = "DO-NOT-LOG-THIS";
            string safe = ErrorDiagnostics.SafeSummary(new TargetInvocationException(error), "Nova conexão", "QA-REF");
            Check(safe.Contains("InvalidCastException") && safe.Contains("Salvar conexão") && safe.Contains("0x"), "copyable diagnostics identify exception type and operation");
            Check(!safe.Contains("DO-NOT-LOG-THIS") && !safe.Contains("192.0.2.200") && !safe.Contains("password="), "diagnostics exclude exception messages payloads hosts and secrets");
            Check(!ErrorDiagnostics.SafeSummary(error, "secret operation", "QA").Contains("secret operation"), "diagnostic context is limited to controlled labels");
            Console.WriteLine(checks + " appearance and regression checks passed."); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        finally { ThemeManager.Shutdown(); try { Directory.Delete(directory, true); } catch { } }
    }
}
