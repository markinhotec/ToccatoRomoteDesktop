using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace Toccato.Desktop
{
    [DataContract]
    public sealed class ClientRecord
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Name = "";
        [DataMember] public string Notes = "";
        public override string ToString() { return Name; }
    }

    [DataContract]
    public sealed class CredentialRecord
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string Name = "";
        [DataMember] public string UserName = "";
        [DataMember] public string Domain = "";
        [DataMember] public string Password = "";
        [DataMember] public string Notes = "";
        public override string ToString() { return Name; }
    }

    public enum ConnectionKind { RDP, SSH, Telnet, VNC, SFTP, FTP, SCP, Serial, Web, AnyDesk, TeamViewer }

    [DataContract]
    public sealed class ConnectionRecord
    {
        [DataMember] public string Id = Guid.NewGuid().ToString("N");
        [DataMember] public string ClientId = "";
        [DataMember] public string Folder = "Produção";
        [DataMember] public string Name = "";
        [DataMember] public ConnectionKind Kind = ConnectionKind.RDP;
        [DataMember] public string Host = "";
        [DataMember] public int Port = 3389;
        [DataMember] public string UserName = "";
        [DataMember] public string Domain = "";
        [DataMember] public string CredentialId = "";
        [DataMember] public string Tags = "";
        [DataMember] public string Notes = "";
        [DataMember] public bool Favorite;
        [DataMember] public bool AdminSession;
        [DataMember] public bool MultiMonitor;
        [DataMember] public string VpnProfile = "";
        [DataMember] public string BeforeScript = "";
        [DataMember] public string AfterScript = "";
        [DataMember] public DateTime? LastUsedUtc;
    }

    [DataContract]
    public sealed class AuditEntry
    {
        [DataMember] public long Sequence;
        [DataMember] public DateTime Utc;
        [DataMember] public string Operator = "";
        [DataMember] public string Action = "";
        [DataMember] public string Target = "";
        [DataMember] public string Result = "";
        [DataMember] public string PreviousHash = "";
        [DataMember] public string Hash = "";
    }

    [DataContract]
    public sealed class Settings
    {
        [DataMember] public int IdleMinutes = 10;
        [DataMember] public string PuttyPath = "";
        [DataMember] public string WinScpPath = "";
        [DataMember] public string VncPath = "";
        [DataMember] public string AnyDeskPath = "";
        [DataMember] public string TeamViewerPath = "";
        [DataMember] public string SshPath = "";
    }

    [DataContract]
    public sealed class VaultData
    {
        [DataMember] public int SchemaVersion = 1;
        [DataMember] public string Title = "Meu workspace";
        [DataMember] public DateTime CreatedUtc = DateTime.UtcNow;
        [DataMember] public List<ClientRecord> Clients = new List<ClientRecord>();
        [DataMember] public List<CredentialRecord> Credentials = new List<CredentialRecord>();
        [DataMember] public List<ConnectionRecord> Connections = new List<ConnectionRecord>();
        [DataMember] public List<AuditEntry> Audit = new List<AuditEntry>();
        [DataMember] public Settings Settings = new Settings();

        public string ClientName(string id)
        {
            var client = Clients.FirstOrDefault(c => c.Id == id);
            return client == null ? "Sem cliente" : client.Name;
        }
        public CredentialRecord Credential(string id) { return Credentials.FirstOrDefault(c => c.Id == id); }
        public void Validate()
        {
            if (SchemaVersion != 1 || Clients == null || Credentials == null || Connections == null || Audit == null || Settings == null)
                throw new InvalidDataException("Formato ou versão do cofre não reconhecido.");
            Guard.Required(Title, "Nome do cofre", 120);
            if (Settings.IdleMinutes < 1 || Settings.IdleMinutes > 120) throw new InvalidDataException("Bloqueio inválido.");
            UniqueIds(Clients.Select(x => x.Id)); UniqueIds(Credentials.Select(x => x.Id)); UniqueIds(Connections.Select(x => x.Id));
            foreach (var c in Clients) Guard.Required(c.Name, "Cliente", 120);
            foreach (var c in Credentials) { Guard.Required(c.Name, "Credencial", 120); Guard.User(c.UserName); }
            foreach (var c in Connections)
            {
                Guard.Connection(c);
                if (!Clients.Any(x => x.Id == c.ClientId)) throw new InvalidDataException("Conexão sem cliente válido.");
                if (!String.IsNullOrEmpty(c.CredentialId) && Credential(c.CredentialId) == null)
                    throw new InvalidDataException("Referência de credencial inválida.");
            }
            AuditLog.Verify(Audit);
        }
        private static void UniqueIds(IEnumerable<string> ids)
        {
            var seen = new HashSet<string>();
            foreach (string id in ids) { Guid g; if (!Guid.TryParseExact(id, "N", out g) || !seen.Add(id)) throw new InvalidDataException("Identificadores inválidos ou duplicados."); }
        }
    }

    public static class Guard
    {
        public static void Required(string value, string field, int max)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length > max || value.Any(Char.IsControl))
                throw new ArgumentException(field + ": preencha um valor válido (até " + max + " caracteres).");
        }
        public static void User(string value)
        {
            if (value == null || value.Length > 256 || value.Any(Char.IsControl) || value.StartsWith("-") || value.IndexOf('"') >= 0)
                throw new ArgumentException("Nome de usuário inválido.");
        }
        public static string Host(string value)
        {
            if (String.IsNullOrWhiteSpace(value) || value.Length > 253) throw new ArgumentException("Informe um IP ou nome DNS válido.");
            IPAddress parsed;
            if (IPAddress.TryParse(value, out parsed)) return value;
            if (!Regex.IsMatch(value, @"^(?=.{1,253}$)[a-zA-Z0-9](?:[a-zA-Z0-9.-]*[a-zA-Z0-9])?$") || value.Contains("..") || value.Split('.').Any(x => x.Length == 0 || x.Length > 63 || x.StartsWith("-") || x.EndsWith("-")))
                throw new ArgumentException("Host inválido. Use apenas IP ou nome DNS, sem parâmetros.");
            return value;
        }
        public static Uri WebUri(string value)
        {
            Uri uri;
            if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || (uri.Scheme != "https" && uri.Scheme != "http") || !String.IsNullOrEmpty(uri.UserInfo) || String.IsNullOrWhiteSpace(uri.Host))
                throw new ArgumentException("Informe uma URL HTTP/HTTPS sem credenciais na URL.");
            return uri;
        }
        public static void Connection(ConnectionRecord c)
        {
            Required(c.Name, "Nome", 120);
            if (!Enum.IsDefined(typeof(ConnectionKind), c.Kind)) throw new ArgumentException("Protocolo desconhecido.");
            if (c.Kind == ConnectionKind.Web) WebUri(c.Host);
            else if (c.Kind == ConnectionKind.Serial)
            {
                if (!Regex.IsMatch(c.Host ?? "", @"^COM[1-9][0-9]{0,2}$", RegexOptions.IgnoreCase)) throw new ArgumentException("Use uma porta COM, por exemplo COM3.");
            }
            else if (c.Kind == ConnectionKind.AnyDesk || c.Kind == ConnectionKind.TeamViewer)
            {
                if (!Regex.IsMatch(c.Host ?? "", @"^[a-zA-Z0-9][a-zA-Z0-9@._-]{1,127}$")) throw new ArgumentException("ID remoto inválido.");
            }
            else Host(c.Host);
            if (c.Port < 1 || c.Port > 65535) throw new ArgumentException("A porta deve estar entre 1 e 65535.");
            User(c.UserName); User(c.Domain);
            if (c.Folder == null || c.Folder.Length > 200 || c.Folder.Any(Char.IsControl)) throw new ArgumentException("Pasta inválida.");
            if (!String.IsNullOrWhiteSpace(c.VpnProfile)) Required(c.VpnProfile, "Perfil VPN", 200);
            if ((c.BeforeScript ?? "").Length > 100000 || (c.AfterScript ?? "").Length > 100000) throw new ArgumentException("Script muito longo.");
        }
        public static int DefaultPort(ConnectionKind kind)
        {
            switch (kind) { case ConnectionKind.RDP: return 3389; case ConnectionKind.SSH: case ConnectionKind.SFTP: case ConnectionKind.SCP: return 22; case ConnectionKind.Telnet: return 23; case ConnectionKind.VNC: return 5900; case ConnectionKind.FTP: return 21; case ConnectionKind.Web: return 443; case ConnectionKind.Serial: return 9600; default: return 1; }
        }
    }

    public static class Json
    {
        public static byte[] Encode<T>(T value)
        {
            using (var ms = new MemoryStream()) { new DataContractJsonSerializer(typeof(T)).WriteObject(ms, value); return ms.ToArray(); }
        }
        public static T Decode<T>(byte[] bytes)
        {
            using (var ms = new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(ms);
        }
        public static T Clone<T>(T value) { return Decode<T>(Encode(value)); }
    }

    public static class AuditLog
    {
        public static void Add(VaultData data, string action, string target, string result)
        {
            var e = new AuditEntry { Sequence = data.Audit.Count + 1, Utc = DateTime.UtcNow, Operator = Environment.UserDomainName + "\\" + Environment.UserName, Action = action, Target = target, Result = result, PreviousHash = data.Audit.Count == 0 ? "" : data.Audit.Last().Hash };
            e.Hash = Hash(e); data.Audit.Add(e);
        }
        private static string Hash(AuditEntry e)
        {
            // Length-prefixed serialization prevents ambiguous separators in the audit chain.
            using (var ms = new MemoryStream())
            using (var w = new BinaryWriter(ms, Encoding.UTF8))
            {
                w.Write(e.Sequence); w.Write(e.Utc.ToUniversalTime().Ticks / TimeSpan.TicksPerMillisecond); w.Write(e.Operator); w.Write(e.Action); w.Write(e.Target); w.Write(e.Result); w.Write(e.PreviousHash); w.Flush();
                using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(ms.ToArray())).Replace("-", "").ToLowerInvariant();
            }
        }
        public static void Verify(IList<AuditEntry> entries)
        {
            string previous = "";
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                if (e.Sequence != i + 1 || e.PreviousHash != previous || e.Hash != Hash(e)) throw new InvalidDataException("A cadeia de auditoria do cofre é inválida.");
                previous = e.Hash;
            }
        }
    }
}
