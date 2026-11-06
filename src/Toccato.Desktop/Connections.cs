using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace Toccato.Desktop
{
    public sealed class LaunchPlan
    {
        public string Executable;
        public string[] Arguments;
        public bool Browser;
        public bool EmbeddedRdp;
    }

    public static class Connector
    {
        // Executables never receive stored passwords. SSH and file clients prompt themselves.
        public static LaunchPlan Plan(ConnectionRecord c, CredentialRecord credential, Settings settings)
        {
            Guard.Connection(c);
            string user = credential == null ? c.UserName : credential.UserName;
            Guard.User(user);
            string host = c.Host;
            switch (c.Kind)
            {
                case ConnectionKind.RDP:
                    if (!c.MultiMonitor) return new LaunchPlan { EmbeddedRdp = true };
                    return Native("mstsc.exe", new[] { "/v:" + Endpoint(host, c.Port), "/multimon" }.Concat(c.AdminSession ? new[] { "/admin" } : new string[0]).ToArray());
                case ConnectionKind.SSH:
                    return Tool(settings.SshPath, new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "OpenSSH", "ssh.exe") }, new[] { "-p", c.Port.ToString(), "--", (String.IsNullOrWhiteSpace(user) ? "" : user + "@") + host });
                case ConnectionKind.Telnet:
                    return Tool(settings.PuttyPath, PuttyDefaults(), new[] { "-telnet", host, "-P", c.Port.ToString() });
                case ConnectionKind.Serial:
                    return Tool(settings.PuttyPath, PuttyDefaults(), new[] { "-serial", host, "-sercfg", "9600,8,n,1,N" });
                case ConnectionKind.SFTP: case ConnectionKind.SCP: case ConnectionKind.FTP:
                    string scheme = c.Kind.ToString().ToLowerInvariant();
                    string url = scheme + "://" + (String.IsNullOrWhiteSpace(user) ? "" : Uri.EscapeDataString(user) + "@") + UriHost(host) + ":" + c.Port + "/";
                    return Tool(settings.WinScpPath, ProgramPaths("WinSCP", "WinSCP.exe"), new[] { "/ini=nul", url });
                case ConnectionKind.VNC:
                    return Tool(settings.VncPath, ProgramPaths("TigerVNC", "vncviewer.exe"), new[] { UriHost(host) + "::" + c.Port });
                case ConnectionKind.AnyDesk:
                    return Tool(settings.AnyDeskPath, ProgramPaths("AnyDesk", "AnyDesk.exe"), new[] { host });
                case ConnectionKind.TeamViewer:
                    return Tool(settings.TeamViewerPath, ProgramPaths("TeamViewer", "TeamViewer.exe"), new[] { "-i", host });
                case ConnectionKind.Web:
                    return new LaunchPlan { Browser = true, Executable = Guard.WebUri(host).AbsoluteUri, Arguments = new string[0] };
                default: throw new NotSupportedException("Tipo de conexão ainda não implementado.");
            }
        }
        public static string Endpoint(string host, int port) { return UriHost(host) + ":" + port; }
        private static string UriHost(string host) { return host.Contains(":") ? "[" + host + "]" : host; }
        private static IEnumerable<string> PuttyDefaults() { return ProgramPaths("PuTTY", "putty.exe"); }
        private static IEnumerable<string> ProgramPaths(string folder, string file)
        {
            return new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), folder, file), Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), folder, file) };
        }
        private static LaunchPlan Native(string executable, string[] arguments)
        {
            return Tool("", new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), executable) }, arguments);
        }
        private static LaunchPlan Tool(string configured, IEnumerable<string> defaults, string[] arguments)
        {
            string path = !String.IsNullOrWhiteSpace(configured) ? configured : defaults.FirstOrDefault(File.Exists);
            if (String.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path) || path.StartsWith(@"\\") || !String.Equals(Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase) || !File.Exists(path))
                throw new FileNotFoundException("Cliente externo não encontrado. Instale o aplicativo oficial e selecione seu executável em Configurações > Ferramentas.");
            return new LaunchPlan { Executable = path, Arguments = arguments };
        }
        public static Process Start(LaunchPlan plan)
        {
            if (plan.EmbeddedRdp) throw new InvalidOperationException("Use o painel de sessão para RDP integrado.");
            return Process.Start(new ProcessStartInfo { FileName = plan.Executable, Arguments = plan.Browser ? "" : String.Join(" ", plan.Arguments.Select(Quote)), UseShellExecute = plan.Browser, WorkingDirectory = plan.Browser ? "" : Path.GetDirectoryName(plan.Executable) });
        }
        // Windows CommandLineToArgvW quoting, including trailing backslashes.
        public static string Quote(string value)
        {
            if (value == null || value.IndexOf('\0') >= 0 || value.Contains("\r") || value.Contains("\n")) throw new ArgumentException("Argumento inválido.");
            var output = new StringBuilder("\""); int slashes = 0;
            foreach (char ch in value)
            {
                if (ch == '\\') slashes++;
                else if (ch == '"') { output.Append('\\', slashes * 2 + 1); output.Append(ch); slashes = 0; }
                else { output.Append('\\', slashes); output.Append(ch); slashes = 0; }
            }
            output.Append('\\', slashes * 2); output.Append('"'); return output.ToString();
        }
        public static async Task ConnectVpn(string profile)
        {
            if (String.IsNullOrWhiteSpace(profile)) return;
            Guard.Required(profile, "Perfil VPN", 200);
            var start = new ProcessStartInfo { FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "rasdial.exe"), Arguments = Quote(profile), UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            await Task.Run(() =>
            {
                using (var p = Process.Start(start))
                {
                    Task<string> output = p.StandardOutput.ReadToEndAsync(), error = p.StandardError.ReadToEndAsync();
                    if (!p.WaitForExit(45000)) { p.Kill(); throw new TimeoutException("A VPN não confirmou a conexão em 45 segundos. Verifique o perfil no Windows."); }
                    Task.WaitAll(output, error);
                    if (p.ExitCode != 0) throw new InvalidOperationException("A VPN do Windows não conectou (código " + p.ExitCode + "). Configure o perfil e as credenciais no Windows antes de continuar.");
                }
            });
        }
        public static async Task<string> TcpTest(string host, int port)
        {
            Guard.Host(host);
            using (var tcp = new TcpClient())
            {
                var connect = tcp.ConnectAsync(host, port);
                if (await Task.WhenAny(connect, Task.Delay(4000)) != connect)
                {
                    // Observe a late socket failure after timeout/disposal.
                    ObserveFault(connect);
                    return "Sem resposta em 4 segundos. Verifique rota, VPN, firewall e serviço.";
                }
                try { await connect; return "Porta TCP " + port + " acessível. Isso não confirma autenticação nem disponibilidade da aplicação."; }
                catch (SocketException) { return "A porta TCP " + port + " não respondeu. Verifique rota, VPN, firewall e serviço."; }
            }
        }
        private static void ObserveFault(Task task) { task.ContinueWith(t => { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted); }
        public static async Task<string> PingTest(string host)
        {
            Guard.Host(host);
            using (var ping = new Ping())
            {
                var reply = await ping.SendPingAsync(host, 4000);
                return reply.Status == IPStatus.Success ? "Resposta de " + reply.Address + " em " + reply.RoundtripTime + " ms." : "ICMP: " + reply.Status + ". O destino pode bloquear ping.";
            }
        }
        public static Process RunPowerShell(string script, ConnectionRecord connection)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var start = new ProcessStartInfo { FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe"), Arguments = "-NoLogo -NoProfile -NoExit -EncodedCommand " + encoded, UseShellExecute = false };
            start.EnvironmentVariables["TDM_HOST"] = connection == null ? "" : connection.Host;
            start.EnvironmentVariables["TDM_PORT"] = connection == null ? "" : connection.Port.ToString();
            start.EnvironmentVariables["TDM_CONNECTION"] = connection == null ? "" : connection.Name;
            // Scripts receive context, never vault passwords. Execution policy is not bypassed.
            return Process.Start(start);
        }
    }

    public sealed class RdpHost : AxHost
    {
        public RdpHost() : base(SelectClass()) { }
        private static string SelectClass()
        {
            foreach (string guid in new[] { "8B918B82-7985-4C24-89DF-C33AD2BBFBCD", "54D38BF7-B1EF-4479-9674-1BD6EA465258" })
                using (var key = Registry.ClassesRoot.OpenSubKey("CLSID\\{" + guid + "}")) if (key != null) return guid;
            throw new NotSupportedException("O componente RDP do Windows não está registrado. Use o cliente mstsc externo ou instale os componentes de Área de Trabalho Remota.");
        }
        public void Connect(ConnectionRecord c, CredentialRecord credential)
        {
            Configure(c, credential);
            dynamic rdp = GetOcx(); rdp.Connect();
        }
        public void Configure(ConnectionRecord c, CredentialRecord credential)
        {
            dynamic rdp = GetOcx();
            rdp.Server = c.Host; rdp.UserName = credential == null ? c.UserName : credential.UserName;
            rdp.Domain = credential == null ? c.Domain : credential.Domain;
            rdp.DesktopWidth = Math.Max(800, Width); rdp.DesktopHeight = Math.Max(600, Height); rdp.ColorDepth = 32;
            dynamic advanced = rdp.AdvancedSettings7;
            advanced.RDPPort = c.Port; advanced.EnableCredSspSupport = true; advanced.AuthenticationLevel = 2;
            advanced.SmartSizing = true; advanced.RedirectClipboard = false; advanced.RedirectDrives = false;
            advanced.RedirectPrinters = false; advanced.RedirectPorts = false; advanced.RedirectSmartCards = false;
            advanced.ConnectToAdministerServer = c.AdminSession;
            if (credential != null && !String.IsNullOrEmpty(credential.Password)) advanced.ClearTextPassword = credential.Password;
        }
        public int ConnectionState { get { try { dynamic rdp = GetOcx(); return (int)rdp.Connected; } catch { return -1; } } }
        public void Disconnect() { try { dynamic rdp = GetOcx(); if ((int)rdp.Connected != 0) rdp.Disconnect(); } catch { } }
    }
}
