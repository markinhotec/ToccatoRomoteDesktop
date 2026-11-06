using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("Toccato Desktop Manager")]
[assembly: AssemblyDescription("Gerenciador local de conexões e credenciais")]
[assembly: AssemblyCompany("Toccato")]
[assembly: AssemblyProduct("Toccato Desktop Manager")]
[assembly: AssemblyVersion("1.0.1.0")]
[assembly: AssemblyFileVersion("1.0.1.0")]
[assembly: ComVisible(false)]

namespace Toccato.Desktop
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try { using (var running = new Mutex(false, "Local\\ToccatoDesktopManager.Running")) return Run(args); }
            finally { ThemeManager.Shutdown(); }
        }
        private static int Run(string[] args)
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            ThemeManager.Initialize();
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            bool screenshot = args.Contains("--screenshot") || args.Contains("--screenshot-appearance");
            Application.ThreadException += (s, e) => { if (screenshot) { Console.Error.WriteLine(e.Exception); Application.ExitThread(); } else Ui.Error(null, e.Exception); };
            try
            {
                if (screenshot || args.Contains("--demo"))
                {
                    var prefs = ThemeManager.Preferences;
                    string mode = Argument(args, "--mode"), palette = Argument(args, "--palette");
                    ColorMode chosenMode; InstitutionalPalette chosenPalette;
                    if (mode != null && Enum.TryParse<ColorMode>(mode, true, out chosenMode)) prefs.Mode = chosenMode;
                    if (palette != null && Enum.TryParse<InstitutionalPalette>(palette, true, out chosenPalette)) prefs.Palette = chosenPalette;
                    ThemeManager.Preview(prefs.Mode, prefs.Palette);
                }
                if (screenshot)
                {
                    string output = Argument(args, args.Contains("--screenshot-appearance") ? "--screenshot-appearance" : "--screenshot");
                    if (String.IsNullOrWhiteSpace(output)) throw new ArgumentException("Informe o arquivo PNG de destino.");
                    using (Form form = args.Contains("--screenshot-appearance") ? (Form)new AppearanceDialog() : new MainForm(VaultSession.Demo(DemoData.Create())))
                    {
                        form.Show(); Application.DoEvents(); form.Refresh(); Application.DoEvents();
                        Size size = Environment.OSVersion.Platform == PlatformID.Win32NT ? form.Size : form.ClientSize;
                        using (var bitmap = new Bitmap(size.Width, size.Height))
                        {
                            if (Environment.OSVersion.Platform == PlatformID.Win32NT) form.DrawToBitmap(bitmap, new Rectangle(Point.Empty, bitmap.Size));
                            else using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(form.PointToScreen(Point.Empty), Point.Empty, bitmap.Size);
                            bitmap.Save(output, ImageFormat.Png);
                        }
                        form.Close();
                    }
                    return 0;
                }
                if (args.Contains("--demo")) { Application.Run(new MainForm(VaultSession.Demo(DemoData.Create()))); return 0; }
                string previous = null;
                while (true)
                {
                    VaultSession session;
                    using (var unlock = new UnlockForm(previous)) { if (unlock.ShowDialog() != DialogResult.OK) break; session = unlock.Session; }
                    using (var main = new MainForm(session))
                    {
                        previous = session.FilePath; Application.Run(main); if (!main.Reopen) break;
                    }
                }
                return 0;
            }
            catch (Exception e) { if (screenshot) Console.Error.WriteLine(e); else Ui.Error(null, e); return 1; }
        }
        private static string Argument(string[] args, string name)
        {
            int index = Array.IndexOf(args, name); return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }

    public static class DemoData
    {
        public static VaultData Create()
        {
            var data = new VaultData { Title = "Workspace de demonstração" };
            var names = new[] { "Atlas Tecnologia", "Horizonte Saúde", "Norte Educação", "Órbita Logística" };
            foreach (string name in names) data.Clients.Add(new ClientRecord { Name = name, Notes = "Cliente fictício para demonstração" });
            var windows = new CredentialRecord { Name = "Infraestrutura · Windows", UserName = "suporte", Domain = "DEMO", Password = "" };
            var linux = new CredentialRecord { Name = "Operações · Linux", UserName = "analista", Password = "" };
            data.Credentials.Add(windows); data.Credentials.Add(linux);
            string[] labels = { "Servidor de aplicações", "Banco de dados", "Qlik Sense · Produção", "Servidor de arquivos", "Qlik Sense · Homologação", "Gateway Linux", "Console de virtualização", "Acesso assistido" };
            for (int i = 0; i < labels.Length; i++)
            {
                var kind = i == 5 ? ConnectionKind.SSH : i == 6 ? ConnectionKind.Web : i == 7 ? ConnectionKind.AnyDesk : ConnectionKind.RDP;
                data.Connections.Add(new ConnectionRecord { Name = labels[i], ClientId = data.Clients[i % data.Clients.Count].Id, Folder = i == 4 ? "Homologação" : "Produção", Kind = kind, Host = kind == ConnectionKind.Web ? "https://console.example.com" : kind == ConnectionKind.AnyDesk ? "123456789" : "192.0.2." + (10 + i), Port = Guard.DefaultPort(kind), CredentialId = kind == ConnectionKind.SSH ? linux.Id : kind == ConnectionKind.RDP ? windows.Id : "", Favorite = i < 3, Tags = i == 2 || i == 4 ? "qlik, analytics" : "infraestrutura", LastUsedUtc = i < 4 ? DateTime.UtcNow.AddHours(-i - 1) : (DateTime?)null, Notes = "Ambiente fictício. Nenhuma conexão é iniciada neste modo." });
            }
            AuditLog.Add(data, "demo.opened", "Demonstração", "Dados fictícios; sem conexão externa"); return data;
        }
    }
}
