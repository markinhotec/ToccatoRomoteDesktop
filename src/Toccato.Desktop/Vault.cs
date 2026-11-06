using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Toccato.Desktop
{
    // One local user, one unlocked vault. No claim of server-enforced PAM or RBAC.
    public sealed class VaultSession : IDisposable
    {
        public const int Iterations = 600000;
        private const int MaximumBytes = 32 * 1024 * 1024;
        private static readonly byte[] Magic = Encoding.ASCII.GetBytes("TDMVLT01");
        private byte[] salt, encryptionKey, authenticationKey;
        public string FilePath { get; private set; }
        public VaultData Data { get; private set; }
        public bool IsDemo { get; private set; }
        private int rounds;
        private byte[] diskHash;
        private FileStream lease;

        private VaultSession() { }
        public static VaultSession Demo(VaultData data) { return new VaultSession { Data = data, IsDemo = true }; }
        public static VaultSession Create(string path, string title, string password)
        {
            ValidatePassword(password);
            if (File.Exists(path)) throw new IOException("O arquivo já existe. Escolha outro nome ou abra o cofre existente.");
            string parent = Path.GetDirectoryName(Path.GetFullPath(path)); Directory.CreateDirectory(parent);
            var session = new VaultSession { FilePath = Path.GetFullPath(path), Data = new VaultData { Title = title }, salt = RandomBytes(32), rounds = Iterations };
            try
            {
                session.AcquireLease(); session.Derive(password); AuditLog.Add(session.Data, "vault.created", title, "Cofre local criado"); session.Save(); return session;
            }
            catch { session.Dispose(); throw; }
        }
        public static VaultSession Open(string path, string password)
        {
            var s = new VaultSession { FilePath = Path.GetFullPath(path) };
            try
            {
                s.AcquireLease();
                byte[] file = File.ReadAllBytes(s.FilePath);
                if (file.Length < 112 || file.Length > MaximumBytes) throw new InvalidDataException("Arquivo de cofre inválido ou muito grande.");
                byte[] iv, encrypted, suppliedMac, authenticated;
                using (var reader = new BinaryReader(new MemoryStream(file)))
                {
                    if (!reader.ReadBytes(8).SequenceEqual(Magic)) throw new InvalidDataException("Este arquivo não é um cofre TDM v1.");
                    s.rounds = reader.ReadInt32();
                    if (s.rounds < Iterations || s.rounds > 2000000) throw new InvalidDataException("Parâmetros criptográficos inválidos.");
                    s.salt = reader.ReadBytes(32); iv = reader.ReadBytes(16);
                    int length = reader.ReadInt32();
                    if (length < 16 || length % 16 != 0 || length != file.Length - 96) throw new InvalidDataException("Comprimento do cofre inválido.");
                    encrypted = reader.ReadBytes(length); suppliedMac = reader.ReadBytes(32);
                    authenticated = file.Take(file.Length - 32).ToArray();
                }
                s.Derive(password);
                using (var hmac = new HMACSHA256(s.authenticationKey))
                    if (!FixedEquals(hmac.ComputeHash(authenticated), suppliedMac)) throw new CryptographicException("Senha incorreta ou arquivo de cofre alterado.");
                byte[] plain = null;
                try
                {
                    using (var aes = Aes.Create())
                    {
                        aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; aes.Key = s.encryptionKey; aes.IV = iv;
                        using (var decryptor = aes.CreateDecryptor()) plain = decryptor.TransformFinalBlock(encrypted, 0, encrypted.Length);
                    }
                    s.Data = Json.Decode<VaultData>(plain); s.Data.Validate();
                }
                finally { Clear(plain); }
                s.diskHash = Digest(file);
                AuditLog.Add(s.Data, "vault.unlocked", s.Data.Title, "Desbloqueado por senha-mestra");
                s.Save(); return s;
            }
            catch { s.Dispose(); throw; }
        }
        private void AcquireLease()
        {
            // Do not delete the sidecar: keeping its inode avoids lock replacement races.
            lease = new FileStream(FilePath + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        }
        private void Derive(string password)
        {
            using (var kdf = new Rfc2898DeriveBytes(password, salt, rounds, HashAlgorithmName.SHA256))
            {
                var key = kdf.GetBytes(64);
                encryptionKey = key.Take(32).ToArray(); authenticationKey = key.Skip(32).Take(32).ToArray(); Clear(key);
            }
        }
        public void Save()
        {
            if (IsDemo) return;
            if (encryptionKey == null || Data == null) throw new InvalidOperationException("Cofre bloqueado.");
            Data.Validate();
            if (diskHash != null && (!File.Exists(FilePath) || !FixedEquals(Digest(File.ReadAllBytes(FilePath)), diskHash)))
                throw new IOException("O arquivo foi alterado fora do TDM. Feche o cofre sem sobrescrever e reabra o arquivo correto.");
            byte[] plain = Json.Encode(Data), ciphertext, iv = RandomBytes(16), output;
            try
            {
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7; aes.Key = encryptionKey; aes.IV = iv;
                    using (var encryptor = aes.CreateEncryptor()) ciphertext = encryptor.TransformFinalBlock(plain, 0, plain.Length);
                }
            }
            finally { Clear(plain); }
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            {
                writer.Write(Magic); writer.Write(rounds); writer.Write(salt); writer.Write(iv); writer.Write(ciphertext.Length); writer.Write(ciphertext); writer.Flush();
                using (var hmac = new HMACSHA256(authenticationKey)) writer.Write(hmac.ComputeHash(ms.ToArray()));
                output = ms.ToArray();
            }
            if (output.Length > MaximumBytes) throw new IOException("O cofre atingiu o limite de 32 MB. Arquive um backup e crie outro cofre.");
            string temp = FilePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var fs = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { fs.Write(output, 0, output.Length); fs.Flush(true); }
                if (File.Exists(FilePath)) File.Replace(temp, FilePath, FilePath + ".bak", true);
                else File.Move(temp, FilePath);
                diskHash = Digest(output);
            }
            finally { if (File.Exists(temp)) File.Delete(temp); }
        }
        public void ChangePassword(string password)
        {
            ValidatePassword(password);
            byte[] oldSalt = salt, oldEncryption = encryptionKey, oldAuth = authenticationKey;
            salt = RandomBytes(32); Derive(password);
            try { Save(); Clear(oldEncryption); Clear(oldAuth); }
            catch { Clear(encryptionKey); Clear(authenticationKey); salt = oldSalt; encryptionKey = oldEncryption; authenticationKey = oldAuth; throw; }
        }
        public void Transaction(Action<VaultData> mutation, string action, string target, string result)
        {
            var snapshot = Json.Clone(Data);
            try { mutation(Data); AuditLog.Add(Data, action, target, result); Save(); }
            catch { Data = snapshot; throw; }
        }
        public void Backup(string destination)
        {
            if (IsDemo) throw new InvalidOperationException("Backup indisponível no modo demonstração.");
            if (String.Equals(Path.GetFullPath(destination), FilePath, StringComparison.OrdinalIgnoreCase) || String.Equals(Path.GetFullPath(destination), FilePath + ".bak", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Escolha um destino diferente do cofre e do backup automático.");
            Save(); File.Copy(FilePath, destination, false);
        }
        public static void ValidatePassword(string password)
        {
            if (password == null || password.Length < 12 || password.Length > 512) throw new ArgumentException("Use uma senha-mestra de 12 a 512 caracteres. Prefira uma frase longa e exclusiva.");
        }
        public static bool FixedEquals(byte[] a, byte[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0; for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i]; return diff == 0;
        }
        private static byte[] Digest(byte[] bytes) { using (var sha = SHA256.Create()) return sha.ComputeHash(bytes); }
        private static byte[] RandomBytes(int count) { var value = new byte[count]; using (var random = RandomNumberGenerator.Create()) random.GetBytes(value); return value; }
        private static void Clear(byte[] bytes) { if (bytes != null) Array.Clear(bytes, 0, bytes.Length); }
        public void Dispose()
        {
            Clear(encryptionKey); Clear(authenticationKey); encryptionKey = authenticationKey = null;
            if (Data != null) { foreach (var c in Data.Credentials) c.Password = ""; Data.Credentials.Clear(); Data.Connections.Clear(); Data.Clients.Clear(); Data.Audit.Clear(); Data = null; }
            if (lease != null) { lease.Dispose(); lease = null; }
        }
    }
}
