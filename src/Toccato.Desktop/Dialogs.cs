using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Toccato.Desktop
{
    public static partial class Ui
    {
        public static Color Navy { get { return ThemeManager.Current.Sidebar; } }
        public static Color Blue { get { return ThemeManager.Current.Accent; } }
        public static Color Teal { get { return Color.FromArgb(14, 150, 128); } }
        public static Color Ink { get { return ThemeManager.Current.Text; } }
        public static Color Muted { get { return ThemeManager.Current.Muted; } }
        public static Color Canvas { get { return ThemeManager.Current.Canvas; } }
        public static Color Line { get { return ThemeManager.Current.Border; } }
        public static Color Surface { get { return ThemeManager.Current.Surface; } }
        public static Font Font(float size = 10, FontStyle style = FontStyle.Regular) { return new Font("Segoe UI", size, style); }
        public static Button Button(string text, EventHandler click, bool primary = false)
        {
            var b = new Button { Text = text, AutoSize = false, Width = 146, Height = 38, FlatStyle = FlatStyle.Flat, Font = Font(9.5f, FontStyle.Bold), Cursor = Cursors.Hand, Margin = new Padding(4), UseVisualStyleBackColor = false, Tag = primary ? ThemeRole.PrimaryButton : ThemeRole.SecondaryButton };
            StyleButton(b); b.EnabledChanged += (s, e) => StyleButton(b); b.Click += click; return b;
        }
        public static TextBox Text(string value = "", bool secret = false, bool multiline = false)
        {
            return new TextBox { Text = value, UseSystemPasswordChar = secret, Multiline = multiline, Height = multiline ? 80 : 30, MinimumSize = new Size(0, multiline ? 80 : 30), Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle, Font = Font(10), ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None };
        }
        public static Label Label(string text, float size = 10, Color? color = null, bool bold = false)
        {
            return new Label { Text = text, Font = Font(size, bold ? FontStyle.Bold : FontStyle.Regular), ForeColor = color ?? Ink, AutoSize = true, BackColor = Color.Transparent, Tag = color.HasValue && color.Value.ToArgb() == Muted.ToArgb() ? ThemeRole.MutedText : ThemeRole.BodyText };
        }
        public static void Error(IWin32Window owner, Exception error, string operation = "Operação do TDM")
        {
            var diagnostic = ErrorDiagnostics.Capture(error, operation);
            string message = error is System.Security.Cryptography.CryptographicException ? "Senha incorreta ou cofre alterado. Use uma cópia de backup se necessário." : error is ArgumentException || error is InvalidDataException || error is FileNotFoundException || error is InvalidOperationException || error is TimeoutException ? error.Message : error is IOException ? "Não foi possível acessar ou salvar o arquivo. Verifique permissões, espaço e se o cofre já está aberto em outro processo." : "O aplicativo encontrou um erro interno. O diagnóstico abaixo identifica a falha sem incluir senhas ou dados do cadastro.";
            using (var dialog = new ThemedMessageForm(message, operation, MessageBoxButtons.OK, diagnostic)) dialog.ShowDialog(owner);
        }
        public static DataGridView Grid()
        {
            var g = new DataGridView { Dock = DockStyle.Fill, BackgroundColor = Color.White, BorderStyle = BorderStyle.None, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false, RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, MultiSelect = false, AutoGenerateColumns = false, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill, CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal, GridColor = Canvas, EnableHeadersVisualStyles = false, ColumnHeadersHeight = 44, ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing };
            g.DefaultCellStyle = new DataGridViewCellStyle { Font = Font(9.5f), ForeColor = Ink, BackColor = Color.White, SelectionBackColor = Color.FromArgb(231, 239, 255), SelectionForeColor = Ink, Padding = new Padding(10, 0, 4, 0), Alignment = DataGridViewContentAlignment.MiddleLeft, WrapMode = DataGridViewTriState.False };
            g.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { Font = Font(9, FontStyle.Bold), ForeColor = Muted, BackColor = Color.FromArgb(248, 250, 253), Padding = new Padding(10, 0, 0, 0), SelectionBackColor = Canvas };
            g.RowTemplate.Height = 52; return g;
        }
        public static void Column(DataGridView g, string name, string title, int weight)
        {
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = name, HeaderText = title, FillWeight = weight, SortMode = DataGridViewColumnSortMode.NotSortable });
        }
    }

    public class EditorForm : ThemedForm
    {
        protected TableLayoutPanel Fields;
        protected FlowLayoutPanel Actions;
        protected Button SaveButton;
        public EditorForm(string title, string subtitle, int height = 600)
        {
            Text = title + " — TDM"; Font = Ui.Font(); ForeColor = Ui.Ink; BackColor = Ui.Surface; StartPosition = FormStartPosition.CenterParent; MinimumSize = new Size(620, 350); ClientSize = new Size(720, height); ShowInTaskbar = false; MaximizeBox = false;
            var head = new Panel { Dock = DockStyle.Top, Height = 94, Padding = new Padding(26, 16, 26, 10), BackColor = Ui.Canvas, Tag = ThemeRole.Canvas };
            var titleLabel = Ui.Label(title, 18, null, true); titleLabel.Location = new Point(24, 14); head.Controls.Add(titleLabel);
            var sub = Ui.Label(subtitle, 9, Ui.Muted); sub.Location = new Point(26, 53); sub.MaximumSize = new Size(650, 36); head.Controls.Add(sub);
            var body = new Panel { Dock = DockStyle.Fill, AutoScroll = true, Padding = new Padding(24, 14, 24, 18) };
            Fields = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(0, 0, 4, 0) };
            Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 158)); Fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); body.Controls.Add(Fields);
            Actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 66, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 10, 16, 10), BackColor = Ui.Canvas, Tag = ThemeRole.Canvas };
            SaveButton = Ui.Button("Salvar", (s, e) => { try { if (Save()) DialogResult = DialogResult.OK; } catch (Exception ex) { Ui.Error(this, ex, title); } }, true);
            var cancel = Ui.Button("Cancelar", (s, e) => { DialogResult = DialogResult.Cancel; });
            Actions.Controls.Add(SaveButton); Actions.Controls.Add(cancel); AcceptButton = SaveButton; CancelButton = cancel;
            Controls.Add(body); Controls.Add(Actions); Controls.Add(head);
        }
        protected virtual bool Save() { return true; }
        protected void Add(string text, Control control)
        {
            int row = Fields.RowCount++; Fields.RowStyles.Add(new RowStyle(SizeType.AutoSize));
            var label = Ui.Label(text, 9, Ui.Muted); label.Margin = new Padding(0, 15, 10, 8); label.MaximumSize = new Size(148, 0);
            control.Dock = DockStyle.Fill; control.Margin = new Padding(0, 8, 0, 8);
            Fields.Controls.Add(label, 0, row); Fields.Controls.Add(control, 1, row);
        }
        protected ComboBox Choice(object[] values)
        {
            var c = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Font = Ui.Font(), Height = 30 }; Ui.ConfigureCombo(c); c.Items.AddRange(values); if (c.Items.Count > 0) c.SelectedIndex = 0; return c;
        }
    }

    public sealed class UnlockForm : EditorForm
    {
        private readonly TextBox path, title, password, confirm;
        private readonly CheckBox create;
        private bool busy;
        public VaultSession Session { get; private set; }
        public UnlockForm(string previous = null) : base("Seu workspace começa aqui", "Conexões, clientes e credenciais protegidos por uma senha-mestra.", 580)
        {
            path = Ui.Text(previous ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Toccato", "DesktopManager", "principal.tdmv"));
            create = new CheckBox { Text = "Criar um novo cofre", Checked = !File.Exists(path.Text), AutoSize = true };
            var choose = Ui.Button("Escolher arquivo...", Browse);
            title = Ui.Text("Workspace Toccato"); password = Ui.Text("", true); confirm = Ui.Text("", true);
            Add("Modo de acesso", create); Add("Arquivo .tdmv", path); Add("", choose); Add("Nome do cofre", title); Add("Senha-mestra", password); Add("Repita a senha", confirm);
            var help = Ui.Label("A senha-mestra não pode ser recuperada. Guarde-a em local seguro e mantenha backups. O cofre funciona localmente, inclusive offline.", 9, Ui.Muted); help.MaximumSize = new Size(440, 0); Add("Antes de começar", help);
            SaveButton.Text = "Desbloquear";
            Actions.Controls.Add(Ui.Button("Temas...", (s, e) => { using (var d = new AppearanceDialog()) d.ShowDialog(this); }));
            // Replace the base click handler by overriding Save and running the asynchronous unlock there.
            create.CheckedChanged += (s, e) => SetMode(); SetMode();
            FormClosing += (s, e) => { if (busy) e.Cancel = true; };
            Shown += (s, e) => password.Focus();
        }
        private void SetMode() { title.Enabled = create.Checked; confirm.Enabled = create.Checked; SaveButton.Text = create.Checked ? "Criar cofre" : "Desbloquear"; }
        private void Browse(object sender, EventArgs e)
        {
            if (create.Checked)
            {
                using (var d = new SaveFileDialog { Filter = "Cofre TDM (*.tdmv)|*.tdmv", DefaultExt = "tdmv", FileName = "workspace.tdmv", OverwritePrompt = true }) if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.FileName;
            }
            else using (var d = new OpenFileDialog { Filter = "Cofre TDM (*.tdmv)|*.tdmv|Backup TDM (*.bak)|*.bak|Todos os arquivos|*.*" }) if (d.ShowDialog(this) == DialogResult.OK) path.Text = d.FileName;
        }
        protected override bool Save()
        {
            if (!busy) UnlockAsync(); return false;
        }
        private async void UnlockAsync()
        {
            try
            {
                bool isNew = create.Checked; string p = password.Text, file = path.Text.Trim(), name = title.Text.Trim();
                if (!Path.IsPathRooted(file)) throw new ArgumentException("Escolha um caminho completo para o cofre.");
                if (isNew) { Guard.Required(name, "Nome do cofre", 120); VaultSession.ValidatePassword(p); if (p != confirm.Text) throw new ArgumentException("As senhas não coincidem."); }
                busy = true; Fields.Enabled = false; Actions.Enabled = false; UseWaitCursor = true;
                Session = await Task.Run(() => isNew ? VaultSession.Create(file, name, p) : VaultSession.Open(file, p));
                password.Clear(); confirm.Clear(); busy = false; DialogResult = DialogResult.OK;
            }
            catch (Exception ex) { password.Clear(); confirm.Clear(); Ui.Error(this, ex); }
            finally { busy = false; UseWaitCursor = false; if (!IsDisposed) { Fields.Enabled = true; Actions.Enabled = true; password.Focus(); } }
        }
    }

    public sealed class ClientDialog : EditorForm
    {
        public ClientRecord Record;
        private TextBox name, notes;
        private VaultData data;
        public ClientDialog(VaultData data, ClientRecord original = null) : base(original == null ? "Novo cliente" : "Editar cliente", "Organize os ambientes e as conexões por cliente.", 370)
        {
            this.data = data; Record = original == null ? new ClientRecord() : Json.Clone(original);
            name = Ui.Text(Record.Name); notes = Ui.Text(Record.Notes, false, true); Add("Nome do cliente", name); Add("Observações", notes);
        }
        protected override bool Save()
        {
            Guard.Required(name.Text.Trim(), "Cliente", 120);
            if (data.Clients.Any(x => x.Id != Record.Id && String.Equals(x.Name, name.Text.Trim(), StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Já existe um cliente com esse nome.");
            Record.Name = name.Text.Trim(); Record.Notes = notes.Text; return true;
        }
    }

    public sealed class CredentialDialog : EditorForm
    {
        public CredentialRecord Record;
        private TextBox name, user, domain, password, notes;
        public CredentialDialog(CredentialRecord original = null) : base(original == null ? "Nova credencial" : "Editar credencial", "Vincule esta credencial a várias conexões e atualize a senha uma única vez.", 570)
        {
            Record = original == null ? new CredentialRecord() : Json.Clone(original);
            name = Ui.Text(Record.Name); user = Ui.Text(Record.UserName); domain = Ui.Text(Record.Domain); password = Ui.Text(Record.Password, true); notes = Ui.Text(Record.Notes, false, true);
            Add("Nome", name); Add("Usuário", user); Add("Domínio (opcional)", domain); Add("Senha", password);
            var show = new CheckBox { Text = "Mostrar senha neste formulário", AutoSize = true }; show.CheckedChanged += (s, e) => password.UseSystemPasswordChar = !show.Checked; Add("", show); Add("Observações", notes);
            FormClosed += (s, e) => { password.Clear(); if (DialogResult != DialogResult.OK) Record.Password = ""; };
        }
        protected override bool Save()
        {
            Guard.Required(name.Text.Trim(), "Credencial", 120); Guard.User(user.Text.Trim()); Guard.User(domain.Text.Trim());
            Record.Name = name.Text.Trim(); Record.UserName = user.Text.Trim(); Record.Domain = domain.Text.Trim(); Record.Password = password.Text; Record.Notes = notes.Text; return true;
        }
    }

    public sealed class ConnectionDialog : EditorForm
    {
        private sealed class CredentialChoice { public string Id, Name; public override string ToString() { return Name; } }
        public ConnectionRecord Record;
        private TextBox name, host, user, domain, tags, notes, vpn, before, after;
        private ComboBox client, folder, kind, credential;
        private NumericUpDown port;
        private CheckBox admin, multimon, favorite;
        public ConnectionDialog(VaultData data, ConnectionRecord original = null, string clientId = null) : base(original == null ? "Nova conexão" : "Editar conexão", "RDP em abas. Outros protocolos usam os clientes configurados em Ferramentas.", 790)
        {
            Record = original == null ? new ConnectionRecord() : Json.Clone(original);
            name = Ui.Text(Record.Name); host = Ui.Text(Record.Host); user = Ui.Text(Record.UserName); domain = Ui.Text(Record.Domain); tags = Ui.Text(Record.Tags); notes = Ui.Text(Record.Notes, false, true); vpn = Ui.Text(Record.VpnProfile);
            client = Choice(data.Clients.Cast<object>().ToArray()); client.SelectedItem = data.Clients.FirstOrDefault(x => x.Id == (clientId ?? Record.ClientId)) ?? data.Clients.First();
            folder = new ComboBox { Text = Record.Folder, Font = Ui.Font(), DropDownStyle = ComboBoxStyle.DropDown }; folder.Items.AddRange(data.Connections.Select(x => x.Folder).Concat(new[] { "Produção", "Homologação", "Desenvolvimento", "Infraestrutura" }).Distinct().Cast<object>().ToArray());
            kind = Choice(Enum.GetValues(typeof(ConnectionKind)).Cast<object>().ToArray()); kind.SelectedItem = Record.Kind;
            port = new NumericUpDown { Minimum = 1, Maximum = 65535, Value = Record.Port, Font = Ui.Font(), Height = 30 };
            credential = Choice(new object[] { new CredentialChoice { Id = "", Name = "Nenhuma — solicitar no cliente remoto" } }.Concat(data.Credentials.Select(x => (object)new CredentialChoice { Id = x.Id, Name = x.Name })).ToArray());
            foreach (CredentialChoice item in credential.Items) if (item.Id == Record.CredentialId) credential.SelectedItem = item;
            kind.SelectedIndexChanged += (s, e) => { if (kind.SelectedItem is ConnectionKind) port.Value = Guard.DefaultPort((ConnectionKind)kind.SelectedItem); };
            admin = new CheckBox { Text = "Sessão administrativa RDP", Checked = Record.AdminSession, AutoSize = true };
            multimon = new CheckBox { Text = "RDP em todos os monitores (mstsc externo, login no Windows)", Checked = Record.MultiMonitor, AutoSize = true };
            favorite = new CheckBox { Text = "Adicionar aos favoritos", Checked = Record.Favorite, AutoSize = true };
            before = Ui.Text(Record.BeforeScript, false, true); after = Ui.Text(Record.AfterScript, false, true);
            Add("Nome da conexão", name); Add("Cliente", client); Add("Pasta / subpasta", folder); Add("Protocolo", kind); Add("Host, URL ou ID", host); Add("Porta", port); Add("Credencial vinculada", credential); Add("Usuário sem vínculo", user); Add("Domínio sem vínculo", domain); Add("Tags", tags); Add("VPN do Windows", vpn); Add("", admin); Add("", multimon); Add("", favorite); Add("Observações", notes);
            Add("PowerShell antes", before); Add("PowerShell ao fechar", after);
            var help = Ui.Label("Scripts abrem no PowerShell local após confirmação e não são aguardados. Variáveis: $env:TDM_HOST, $env:TDM_PORT e $env:TDM_CONNECTION. Para RAS/IPsec, informe o perfil VPN já configurado no Windows. Serial: 9600 8N1.", 9, Ui.Muted); help.MaximumSize = new Size(470, 0); Add("Como funciona", help);
        }
        protected override bool Save()
        {
            var selectedClient = client.SelectedItem as ClientRecord;
            if (selectedClient == null) throw new ArgumentException("Selecione o cliente da conexão.");
            if (!(kind.SelectedItem is ConnectionKind)) throw new ArgumentException("Selecione o protocolo da conexão.");
            var selectedCredential = credential.SelectedItem as CredentialChoice;
            if (selectedCredential == null && credential.Items.Count == 1) selectedCredential = credential.Items[0] as CredentialChoice;
            if (selectedCredential == null) throw new ArgumentException("Selecione uma credencial ou a opção Nenhuma.");
            var draft = Json.Clone(Record);
            draft.Name = name.Text.Trim(); draft.Host = host.Text.Trim(); draft.Kind = (ConnectionKind)kind.SelectedItem; draft.Port = (int)port.Value; draft.ClientId = selectedClient.Id;
            draft.Folder = folder.Text.Trim().Trim('/'); draft.CredentialId = selectedCredential.Id ?? ""; draft.UserName = user.Text.Trim(); draft.Domain = domain.Text.Trim(); draft.Tags = tags.Text.Trim(); draft.Notes = notes.Text; draft.VpnProfile = vpn.Text.Trim();
            draft.AdminSession = admin.Checked; draft.MultiMonitor = multimon.Checked; draft.Favorite = favorite.Checked; draft.BeforeScript = before.Text; draft.AfterScript = after.Text;
            Guard.Connection(draft); Record = draft; return true;
        }
    }

    public sealed class SettingsDialog : EditorForm
    {
        public Settings Settings;
        private NumericUpDown idle;
        private TextBox putty, winscp, vnc, anydesk, teamviewer, ssh;
        public SettingsDialog(Settings original) : base("Configurações", "Use clientes oficiais instalados nesta máquina. Nenhum cliente externo está incluído.", 670)
        {
            Settings = Json.Clone(original); idle = new NumericUpDown { Minimum = 1, Maximum = 120, Value = Settings.IdleMinutes };
            Add("Visual do aplicativo", Ui.Button("Temas e aparência...", (s, e) => { using (var d = new AppearanceDialog()) d.ShowDialog(this); }));
            Add("Bloquear em (min)", idle);
            AddTool("OpenSSH (ssh.exe)", Settings.SshPath, out ssh); AddTool("PuTTY", Settings.PuttyPath, out putty); AddTool("WinSCP", Settings.WinScpPath, out winscp); AddTool("TigerVNC Viewer", Settings.VncPath, out vnc); AddTool("AnyDesk", Settings.AnyDeskPath, out anydesk); AddTool("TeamViewer", Settings.TeamViewerPath, out teamviewer);
            var info = Ui.Label("Em branco: procurar nas pastas padrão. O bloqueio encerra RDP integrado, limpa o cofre da interface e pede novamente a senha-mestra. Clientes externos e VPNs continuam sob controle do Windows.", 9, Ui.Muted); info.MaximumSize = new Size(460, 0); Add("Sessões e bloqueio", info);
        }
        private void AddTool(string label, string value, out TextBox box)
        {
            var panel = new TableLayoutPanel { Height = 38, ColumnCount = 2 }; panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100)); panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82));
            var input = Ui.Text(value); var browse = Ui.Button("...", (s, e) => { using (var d = new OpenFileDialog { Filter = "Executável Windows (*.exe)|*.exe" }) if (d.ShowDialog(this) == DialogResult.OK) input.Text = d.FileName; }); browse.Width = 70; browse.Height = 30;
            panel.Controls.Add(input, 0, 0); panel.Controls.Add(browse, 1, 0); Add(label, panel); box = input;
        }
        protected override bool Save()
        {
            foreach (TextBox field in new[] { ssh, putty, winscp, vnc, anydesk, teamviewer })
                if (!String.IsNullOrWhiteSpace(field.Text) && (!Path.IsPathRooted(field.Text) || field.Text.StartsWith(@"\\") || !File.Exists(field.Text) || !String.Equals(Path.GetExtension(field.Text), ".exe", StringComparison.OrdinalIgnoreCase))) throw new ArgumentException("Escolha executáveis locais .exe existentes.");
            Settings.IdleMinutes = (int)idle.Value; Settings.SshPath = ssh.Text.Trim(); Settings.PuttyPath = putty.Text.Trim(); Settings.WinScpPath = winscp.Text.Trim(); Settings.VncPath = vnc.Text.Trim(); Settings.AnyDeskPath = anydesk.Text.Trim(); Settings.TeamViewerPath = teamviewer.Text.Trim(); return true;
        }
    }

    public sealed class ScriptDialog : EditorForm
    {
        private TextBox script;
        public string Script { get { return script.Text; } }
        public ScriptDialog(string initial = "", string context = "") : base("Executar PowerShell local", "Revise o conteúdo. O script executa com as permissões do seu usuário do Windows.", 570)
        {
            script = Ui.Text(initial, false, true); script.Height = 280; script.Font = new Font("Consolas", 10); Add("Script", script);
            var help = Ui.Label("Alvo: " + context + "\nO TDM fornece contexto em variáveis de ambiente, sem senhas. Use remoting explícito no script para atuar no servidor. A janela é aberta sem aguardar a conclusão.", 9, Ui.Muted); help.MaximumSize = new Size(460, 0); Add("Execução", help); SaveButton.Text = "Executar";
        }
        protected override bool Save() { if (String.IsNullOrWhiteSpace(Script)) throw new ArgumentException("Digite um script."); return true; }
    }

    public sealed class PasswordDialog : EditorForm
    {
        private TextBox first, second;
        public string Password { get { return first.Text; } }
        public PasswordDialog() : base("Alterar senha-mestra", "O cofre será novamente criptografado. Backups anteriores mantêm a senha anterior.", 340)
        {
            first = Ui.Text("", true); second = Ui.Text("", true); Add("Nova senha", first); Add("Repita a senha", second);
        }
        protected override bool Save() { VaultSession.ValidatePassword(first.Text); if (first.Text != second.Text) throw new ArgumentException("As senhas não coincidem."); return true; }
    }
}
