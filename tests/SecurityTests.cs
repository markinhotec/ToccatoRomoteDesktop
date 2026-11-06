using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using Toccato.Desktop;

internal static class SecurityTests
{
    private static int passed, failed;
    private const string Password = "teste-somente-v1-senha-longa";
    private static string directory;
    private static void Check(string name, Action test)
    {
        try { test(); Console.WriteLine("PASS " + name); passed++; }
        catch (Exception e) { Console.WriteLine("FAIL " + name + " [" + e.GetType().Name + "] " + e.Message); failed++; }
    }
    private static void True(bool value) { if (!value) throw new Exception("Assertion failed"); }
    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); } catch (T) { return; } throw new Exception("Expected " + typeof(T).Name);
    }
    private static VaultData Sample()
    {
        var d = new VaultData { Title = "Teste de integração áéç" };
        var client = new ClientRecord { Name = "Cliente EXCLUSIVO-SECRETO" }; d.Clients.Add(client);
        var credential = new CredentialRecord { Name = "Domínio de teste", UserName = "testuser", Domain = "LAB", Password = "SENHA-QUE-NAO-PODE-VAZAR-123!" }; d.Credentials.Add(credential);
        d.Connections.Add(new ConnectionRecord { Name = "Servidor", ClientId = client.Id, CredentialId = credential.Id, Host = "192.0.2.10" });
        AuditLog.Add(d, "test.created", "Servidor", "Fixture de teste"); return d;
    }
    private static string PathFor(string name) { return Path.Combine(directory, name + ".tdmv"); }
    [STAThread]
    public static int Main(string[] args)
    {
        directory = Path.Combine(Path.GetTempPath(), "tdm-test-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(directory);
        try
        {
            Check("password policy", () => { Throws<ArgumentException>(() => VaultSession.ValidatePassword("short")); VaultSession.ValidatePassword(Password); });
            Check("data and audit JSON round trip", () => { var d = Json.Clone(Sample()); d.Validate(); True(d.Clients[0].Name.Contains("EXCLUSIVO")); });
            Check("audit alteration rejected", () => { var d = Sample(); d.Audit[0].Result = "forjado"; Throws<InvalidDataException>(() => d.Validate()); });
            Check("duplicate record identifiers rejected", () => { var d = Sample(); d.Clients.Add(Json.Clone(d.Clients[0])); Throws<InvalidDataException>(() => d.Validate()); });
            Check("dangling credential rejected", () => { var d = Sample(); d.Credentials.Clear(); Throws<InvalidDataException>(() => d.Validate()); });
            Check("missing client rejected", () => { var d = Sample(); d.Clients.Clear(); Throws<InvalidDataException>(() => d.Validate()); });
            Check("host argument injection rejected", () => { foreach (string h in new[] { "-oProxyCommand=bad", "host & calc", "host\r\npass", "host\" -pw bad", "abc..example", "bad-.example" }) Throws<ArgumentException>(() => Guard.Host(h)); });
            Check("IPv4 IPv6 DNS accepted", () => { Guard.Host("192.0.2.10"); Guard.Host("2001:db8::1"); Guard.Host("server.example.com"); });
            Check("unsafe URL schemes and credentials rejected", () => { foreach (string url in new[] { "file:///C:/a.exe", "javascript:alert(1)", "https://u:secret@example.com", "rdp://example.com" }) Throws<ArgumentException>(() => Guard.WebUri(url)); Guard.WebUri("https://example.com/admin"); });
            Check("undefined connector rejected", () => { var c = Sample().Connections[0]; c.Kind = (ConnectionKind)999; Throws<ArgumentException>(() => Guard.Connection(c)); });
            Check("windows argument quoting handles slashes and quotes", () => { True(Connector.Quote("hello world") == "\"hello world\""); True(Connector.Quote("C:\\test\\") == "\"C:\\test\\\\\""); True(Connector.Quote("a\"b") == "\"a\\\"b\""); Throws<ArgumentException>(() => Connector.Quote("a\nb")); });
            Check("external launch plans never contain passwords", () =>
            {
                string fakeExe = Path.Combine(directory, "client.exe"); File.WriteAllText(fakeExe, "not executed"); var settings = new Settings { SshPath = fakeExe, PuttyPath = fakeExe, WinScpPath = fakeExe, VncPath = fakeExe, AnyDeskPath = fakeExe, TeamViewerPath = fakeExe }; var d = Sample();
                foreach (ConnectionKind kind in Enum.GetValues(typeof(ConnectionKind)))
                {
                    var c = Json.Clone(d.Connections[0]); c.Kind = kind; c.Host = kind == ConnectionKind.Web ? "https://example.com/" : kind == ConnectionKind.Serial ? "COM3" : kind == ConnectionKind.AnyDesk || kind == ConnectionKind.TeamViewer ? "123456789" : "192.0.2.10";
                    var plan = Connector.Plan(c, d.Credentials[0], settings); True(plan.Arguments == null || !String.Join(" ", plan.Arguments).Contains(d.Credentials[0].Password));
                }
            });
            string file = PathFor("main");
            Check("encrypted save contains no metadata or secrets", () =>
            {
                using (var v = VaultSession.Create(file, "Testes", Password)) { var data = Sample(); v.Transaction(d => { d.Clients = data.Clients; d.Credentials = data.Credentials; d.Connections = data.Connections; }, "test.seeded", "Fixture", "Dados sintéticos"); }
                string raw = Encoding.UTF8.GetString(File.ReadAllBytes(file)); True(!raw.Contains("EXCLUSIVO-SECRETO") && !raw.Contains("SENHA-QUE") && !raw.Contains("192.0.2.10"));
            });
            Check("correct password restores credential and references", () => { using (var v = VaultSession.Open(file, Password)) { v.Data.Validate(); True(v.Data.Credential(v.Data.Connections[0].CredentialId).Password == "SENHA-QUE-NAO-PODE-VAZAR-123!"); } });
            Check("wrong password rejected before decryption", () => Throws<CryptographicException>(() => VaultSession.Open(file, "senha-incorreta-para-teste")));
            Check("authenticated ciphertext modification rejected", () => { var bytes = File.ReadAllBytes(file); bytes[80] ^= 1; var path = PathFor("tampered"); File.WriteAllBytes(path, bytes); Throws<CryptographicException>(() => VaultSession.Open(path, Password)); });
            Check("truncated vault rejected", () => { var bytes = File.ReadAllBytes(file); var path = PathFor("truncated"); File.WriteAllBytes(path, bytes.Take(bytes.Length - 13).ToArray()); Throws<InvalidDataException>(() => VaultSession.Open(path, Password)); });
            Check("KDF cost manipulation rejected", () => { var bytes = File.ReadAllBytes(file); Array.Copy(BitConverter.GetBytes(1), 0, bytes, 8, 4); var path = PathFor("kdf"); File.WriteAllBytes(path, bytes); Throws<InvalidDataException>(() => VaultSession.Open(path, Password)); });
            Check("concurrent open denied and lease released", () => { using (var v = VaultSession.Open(file, Password)) Throws<IOException>(() => VaultSession.Open(file, Password)); using (var v = VaultSession.Open(file, Password)) True(v.Data != null); });
            Check("transaction rollback on invalid mutation", () => { using (var v = VaultSession.Open(file, Password)) { int count = v.Data.Audit.Count; Throws<InvalidDataException>(() => v.Transaction(d => d.Credentials.Clear(), "invalid", "test", "test")); True(v.Data.Credentials.Count == 1 && v.Data.Audit.Count == count); } });
            Check("backup is encrypted and independently openable", () => { var backup = PathFor("backup"); using (var v = VaultSession.Open(file, Password)) v.Backup(backup); using (var b = VaultSession.Open(backup, Password)) True(b.Data.Connections.Count == 1); });
            Check("new IV on each save", () => { using (var v = VaultSession.Open(file, Password)) { var before = File.ReadAllBytes(file).Skip(44).Take(16).ToArray(); v.Save(); var after = File.ReadAllBytes(file).Skip(44).Take(16).ToArray(); True(!before.SequenceEqual(after)); } });
            Check("password rotation invalidates old password", () => { var path = PathFor("rotate"); using (var v = VaultSession.Create(path, "Rotação", Password)) v.ChangePassword(Password + "-nova"); Throws<CryptographicException>(() => VaultSession.Open(path, Password)); using (var v = VaultSession.Open(path, Password + "-nova")) True(v.Data.Title == "Rotação"); });
            Check("out of process modification not overwritten", () => { var path = PathFor("race"); using (var v = VaultSession.Create(path, "Concorrência", Password)) { var bytes = File.ReadAllBytes(path); bytes[80] ^= 1; File.WriteAllBytes(path, bytes); Throws<IOException>(() => v.Save()); True(File.ReadAllBytes(path).SequenceEqual(bytes)); } });
            Check("TCP connectivity verifies socket not login", () =>
            {
                var listener = new TcpListener(IPAddress.Loopback, 0); listener.Start(); try { int port = ((IPEndPoint)listener.LocalEndpoint).Port; string result = Connector.TcpTest("127.0.0.1", port).GetAwaiter().GetResult(); True(result.Contains("acessível") && result.Contains("não confirma")); } finally { listener.Stop(); }
            });
            if (args.Length == 2 && args[0] == "--fixture") File.Copy(file, args[1], true);
        }
        finally { try { Directory.Delete(directory, true); } catch { } }
        Console.WriteLine(passed + " passed, " + failed + " failed"); return failed == 0 ? 0 : 1;
    }
}
