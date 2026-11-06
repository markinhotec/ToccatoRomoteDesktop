using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Toccato.Desktop;

internal static class UiSmokeTests
{
    private static T Field<T>(object instance, string name) { return (T)instance.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(instance); }
    private static object Invoke(object instance, string name) { return instance.GetType().GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance).Invoke(instance, null); }
    private static void True(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread]
    public static int Main()
    {
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            var data = DemoData.Create();
            using (var d = new ClientDialog(data)) { Field<TextBox>(d, "name").Text = "Novo cliente QA"; True((bool)Invoke(d, "Save") && d.Record.Name == "Novo cliente QA", "client editor saves validated client"); }
            using (var d = new CredentialDialog())
            {
                Field<TextBox>(d, "name").Text = "QA"; Field<TextBox>(d, "user").Text = "user"; Field<TextBox>(d, "password").Text = "test secret";
                True(Field<TextBox>(d, "password").UseSystemPasswordChar, "credential password masked by default"); True((bool)Invoke(d, "Save") && d.Record.Password == "test secret", "credential editor captures password without displaying it");
            }
            using (var d = new ConnectionDialog(data))
            {
                Field<TextBox>(d, "name").Text = "QA Linux"; Field<ComboBox>(d, "kind").SelectedItem = ConnectionKind.SSH; Field<TextBox>(d, "host").Text = "192.0.2.15"; Field<ComboBox>(d, "credential").SelectedIndex = 1;
                True((bool)Invoke(d, "Save") && d.Record.Port == 22 && d.Record.Kind == ConnectionKind.SSH && d.Record.CredentialId.Length > 0, "connection editor stores protocol port and credential link");
            }
            using (var d = new SettingsDialog(data.Settings)) { True((bool)Invoke(d, "Save") && d.Settings.IdleMinutes == 10, "settings editor preserves automatic lock"); }
            using (var d = new UnlockForm(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".tdmv")))
            {
                True(Field<TextBox>(d, "password").UseSystemPasswordChar && Field<TextBox>(d, "confirm").UseSystemPasswordChar, "master password entry masked");
            }
            using (var main = new MainForm(VaultSession.Demo(data)))
            {
                main.Show(); Application.DoEvents();
                var grid = Field<DataGridView>(main, "connections"); var search = Field<TextBox>(main, "search");
                True(grid.Rows.Count == 8, "main window loads all synthetic connections");
                search.Text = "qlik"; Application.DoEvents(); True(grid.Rows.Count == 2, "quick search matches connection names and tags");
                search.Text = "192.0.2.15"; Application.DoEvents(); True(grid.Rows.Count == 1, "quick search matches host address");
                search.Text = "Infraestrutura · Windows"; Application.DoEvents(); True(grid.Rows.Count == 5, "quick search matches linked credential name");
                search.Text = "no-result-xxxxxxxx"; Application.DoEvents(); True(grid.Rows.Count == 0, "empty search result handled without error");
                search.Text = ""; Application.DoEvents(); True(Field<DataGridView>(main, "credentials").Rows[0].Cells["password"].Value.ToString() == "••••••••", "credential table never renders password values");
                True(Field<TreeView>(main, "clients").Nodes.Count == 5, "client hierarchy rendered");
                main.ClientSize = new Size(1120, 740); Application.DoEvents(); True(grid.Width > 400 && grid.Height > 200, "minimum layout retains usable connection grid");
                if (Environment.OSVersion.Platform == PlatformID.Win32NT)
                {
                    var page = new TabPage("RDP component test"); var tabs = Field<TabControl>(main, "tabs"); tabs.TabPages.Add(page); tabs.SelectedTab = page;
                    using (var rdp = new RdpHost { Dock = DockStyle.Fill })
                    {
                        ((System.ComponentModel.ISupportInitialize)rdp).BeginInit(); page.Controls.Add(rdp); ((System.ComponentModel.ISupportInitialize)rdp).EndInit(); rdp.CreateControl(); Application.DoEvents();
                        rdp.Configure(new ConnectionRecord { Name = "QA", Host = "192.0.2.10" }, new CredentialRecord { UserName = "qa", Password = "synthetic-password" });
                        True(rdp.ConnectionState == 0, "Windows RDP ActiveX configures NLA certificate policy and password without connecting");
                    }
                    tabs.TabPages.Remove(page); page.Dispose();
                }
                main.Close();
            }
            Console.WriteLine("UI smoke tests completed."); return 0;
        }
        catch (Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
