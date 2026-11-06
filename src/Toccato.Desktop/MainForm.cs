using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Toccato.Desktop
{
    public sealed class MainForm : ThemedForm
    {
        private VaultSession vault;
        private VaultData Data { get { return vault.Data; } }
        private TreeView clients;
        private TextBox search;
        private DataGridView connections, credentials, audit;
        private TabControl tabs;
        private TabPage workspacePage, credentialsPage, auditPage;
        private Label connectionCount, clientCount, credentialCount, sessionCount, status, detailsName, detailsMeta, detailsHost, detailsCredential, detailsTags, detailsNotes, detailsVpn;
        private Button favoriteButton, connectButton;
        private string filter = "all", activeClient, activeFolder;
        private bool rebuilding, closing, busy, forceLock;
        private DateTime clipboardExpires;
        private string clipboardValue;
        private Timer heartbeat;
        private List<SessionView> sessions = new List<SessionView>();
        public bool Reopen { get; private set; }
        public string VaultPath { get { return vault.FilePath; } }

        private sealed class FolderTag { public string ClientId, Folder; }
        private sealed class SessionView
        {
            public TabPage Page;
            public RdpHost Rdp;
            public Process Process;
            public ConnectionRecord Connection;
            public Label State;
            public int LastState = -1;
            public Form Detached;
            public bool Removed;
        }

        public MainForm(VaultSession session)
        {
            vault = session; Text = "Toccato Desktop Manager 1.0.1 — " + Data.Title + (session.IsDemo ? " [DEMONSTRAÇÃO]" : ""); Tag = ThemeRole.Canvas;
            Font = Ui.Font(); BackColor = Ui.Canvas; ForeColor = Ui.Ink; StartPosition = FormStartPosition.CenterScreen; MinimumSize = new Size(1120, 740); ClientSize = new Size(1440, 870); KeyPreview = true;
            BuildInterface(); RefreshData();
            heartbeat = new Timer { Interval = 1000 }; heartbeat.Tick += Heartbeat; heartbeat.Start();
            KeyDown += (s, e) =>
            {
                if (e.Control && e.KeyCode == Keys.Space) { QuickSearch(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.N) { EditConnection(); e.SuppressKeyPress = true; }
                else if (e.Control && e.KeyCode == Keys.L) { LockWorkspace(); e.SuppressKeyPress = true; }
                else if (e.KeyCode == Keys.F5) { RefreshData(); e.SuppressKeyPress = true; }
            };
            FormClosing += ClosingWorkspace;
            FormClosed += (s, e) => { heartbeat.Stop(); heartbeat.Dispose(); ClearClipboard(); if (Environment.OSVersion.Platform == PlatformID.Win32NT) { UnregisterHotKey(Handle, 1824); WTSUnRegisterSessionNotification(Handle); } vault.Dispose(); };
        }

        private void BuildInterface()
        {
            var sidebar = new Panel { Dock = DockStyle.Left, Width = 230, BackColor = Ui.Navy, Padding = new Padding(14, 0, 14, 0), Tag = ThemeRole.Sidebar };
            var brand = new Panel { Dock = DockStyle.Top, Height = 112 };
            var logo = new PictureBox { Image = BrandAssets.Logo, SizeMode = PictureBoxSizeMode.Zoom, Bounds = new Rectangle(8, 22, 188, 52), AccessibleName = "Logo oficial Toccato" }; brand.Controls.Add(logo);
            var brandSub = Ui.Label("DESKTOP MANAGER", 7.5f, null, true); brandSub.Tag = ThemeRole.SidebarMuted; brandSub.Location = new Point(10, 80); brand.Controls.Add(brandSub);
            var nav = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 170, FlowDirection = FlowDirection.TopDown, WrapContents = false };
            nav.Controls.Add(Nav("  Conexões", (s, e) => { tabs.SelectedTab = workspacePage; filter = "all"; activeClient = activeFolder = null; RefreshConnections(); }));
            nav.Controls.Add(Nav("  Credenciais", (s, e) => { tabs.SelectedTab = credentialsPage; RefreshCredentials(); }));
            nav.Controls.Add(Nav("  Auditoria", (s, e) => { tabs.SelectedTab = auditPage; RefreshAudit(); }));
            var treePanel = new Panel { Dock = DockStyle.Fill };
            var treeTitle = Ui.Label("CLIENTES E AMBIENTES", 8, Color.FromArgb(131, 153, 184), true); treeTitle.Dock = DockStyle.Top; treeTitle.Height = 34; treeTitle.Padding = new Padding(10, 8, 0, 0);
            treeTitle.Tag = ThemeRole.SidebarMuted;
            clients = new TreeView { Dock = DockStyle.Fill, BorderStyle = BorderStyle.None, BackColor = Ui.Navy, ForeColor = Color.FromArgb(211, 221, 235), Font = Ui.Font(9), ItemHeight = 32, Indent = 14, ShowLines = false, ShowRootLines = false, HideSelection = false, FullRowSelect = true };
            clients.AfterSelect += (s, e) => { if (rebuilding) return; var tag = e.Node.Tag as FolderTag; activeClient = tag == null ? null : tag.ClientId; activeFolder = tag == null ? null : tag.Folder; filter = "all"; tabs.SelectedTab = workspacePage; RefreshConnections(); };
            clients.NodeMouseDoubleClick += (s, e) => { if (e.Node.Tag is FolderTag && ((FolderTag)e.Node.Tag).Folder == null) EditClient(((FolderTag)e.Node.Tag).ClientId); };
            var menu = new ContextMenuStrip(); menu.Items.Add("Novo cliente", null, (s, e) => EditClient()); menu.Items.Add("Editar cliente selecionado", null, (s, e) => EditClient(activeClient)); menu.Items.Add("Excluir cliente selecionado", null, (s, e) => DeleteClient()); clients.ContextMenuStrip = menu;
            treePanel.Controls.Add(clients); treePanel.Controls.Add(treeTitle);
            var sideBottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 138, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(0, 8, 0, 0) };
            sideBottom.Controls.Add(Nav("  + Novo cliente", (s, e) => EditClient())); sideBottom.Controls.Add(Nav("  Configurações", (s, e) => Configure()));
            var version = Ui.Label("v1.0.1  /  LOCAL WORKSPACE", 7.5f, Color.FromArgb(121, 144, 174)); version.Tag = ThemeRole.SidebarMuted; version.Margin = new Padding(12, 10, 0, 0); sideBottom.Controls.Add(version);
            sidebar.Controls.Add(treePanel); sidebar.Controls.Add(sideBottom); sidebar.Controls.Add(nav); sidebar.Controls.Add(brand);

            var main = new Panel { Dock = DockStyle.Fill, Padding = new Padding(30, 0, 30, 0), Tag = ThemeRole.Canvas };
            var top = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Ui.Canvas };
            var crumb = Ui.Label("WORKSPACE  /  " + (vault.IsDemo ? "DEMONSTRAÇÃO · DADOS FICTÍCIOS" : Data.Title.ToUpperInvariant()), 8, Ui.Muted, true); crumb.Location = new Point(0, 19); crumb.MaximumSize = new Size(520, 22); top.Controls.Add(crumb);
            var headline = Ui.Label("Seu ambiente, conectado.", 22, Ui.Ink, true); headline.Location = new Point(-2, 40); top.Controls.Add(headline);
            var topButtons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 364, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 32, 0, 0) };
            topButtons.Controls.Add(Ui.Button("+ Nova conexão", (s, e) => EditConnection(), true)); topButtons.Controls.Add(Ui.Button("Bloquear cofre", (s, e) => LockWorkspace())); top.Controls.Add(topButtons);
            var metrics = new TableLayoutPanel { Dock = DockStyle.Top, Height = 114, ColumnCount = 4, Padding = new Padding(0, 6, 0, 12) };
            for (int i = 0; i < 4; i++) metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));
            metrics.Controls.Add(Metric("CONEXÕES", Ui.Blue, out connectionCount), 0, 0); metrics.Controls.Add(Metric("CLIENTES", Ui.Teal, out clientCount), 1, 0); metrics.Controls.Add(Metric("CREDENCIAIS", Color.FromArgb(125, 91, 207), out credentialCount), 2, 0); metrics.Controls.Add(Metric("PAINÉIS DE SESSÃO", Color.FromArgb(209, 140, 30), out sessionCount), 3, 0);
            var searchPanel = new Panel { Dock = DockStyle.Top, Height = 70 };
            var searchLabel = Ui.Label("BUSCAR NO COFRE", 7.5f, Ui.Muted, true); searchLabel.Location = new Point(0, 1); searchPanel.Controls.Add(searchLabel);
            search = Ui.Text(); search.Dock = DockStyle.None; search.Location = new Point(0, 24); search.Width = 620; search.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right; search.TextChanged += (s, e) => { activeClient = activeFolder = null; RefreshConnections(); };
            searchPanel.Controls.Add(search);
            var quick = Ui.Button("Ctrl + Space", (s, e) => QuickSearch()); quick.Location = new Point(640, 20); quick.Anchor = AnchorStyles.Right | AnchorStyles.Top; quick.Width = 144; searchPanel.Controls.Add(quick);
            searchPanel.Resize += (s, e) => { search.Width = Math.Max(280, searchPanel.Width - 160); quick.Left = searchPanel.Width - 146; };
            status = new Label { Dock = DockStyle.Bottom, Height = 42, Font = Ui.Font(8.5f), ForeColor = Ui.Muted, TextAlign = ContentAlignment.MiddleLeft, Tag = ThemeRole.MutedText };
            tabs = new ThemedTabControl { Dock = DockStyle.Fill, Font = Ui.Font(9.5f) };
            workspacePage = new TabPage("Conexões") { BackColor = Color.White, Padding = new Padding(12) }; credentialsPage = new TabPage("Cofre de credenciais") { BackColor = Color.White, Padding = new Padding(16) }; auditPage = new TabPage("Auditoria") { BackColor = Color.White, Padding = new Padding(16) };
            tabs.TabPages.AddRange(new[] { workspacePage, credentialsPage, auditPage });
            BuildConnections(); BuildCredentials(); BuildAudit();
            main.Controls.Add(tabs); main.Controls.Add(status); main.Controls.Add(searchPanel); main.Controls.Add(metrics); main.Controls.Add(top);
            Controls.Add(main); Controls.Add(sidebar);
            var bar = new MenuStrip { Dock = DockStyle.Top, BackColor = Color.White, Font = Ui.Font(9), Padding = new Padding(8, 4, 8, 4) };
            var file = new ToolStripMenuItem("Cofre"); file.DropDownItems.Add("Abrir / criar outro cofre", null, (s, e) => LockWorkspace()); file.DropDownItems.Add("Backup criptografado", null, (s, e) => Backup()); file.DropDownItems.Add("Alterar senha-mestra", null, (s, e) => ChangePassword()); file.DropDownItems.Add("Bloquear    Ctrl+L", null, (s, e) => LockWorkspace()); file.DropDownItems.Add("Sair", null, (s, e) => Close());
            var tools = new ToolStripMenuItem("Ferramentas"); tools.DropDownItems.Add("Testar porta TCP", null, async (s, e) => await Diagnose(false)); tools.DropDownItems.Add("Ping", null, async (s, e) => await Diagnose(true)); tools.DropDownItems.Add("Consultar DNS", null, async (s, e) => await DnsLookup()); tools.DropDownItems.Add("PowerShell local", null, (s, e) => RunScript("", SelectedConnection())); tools.DropDownItems.Add("Configurar clientes externos", null, (s, e) => Configure());
            var appearance = new ToolStripMenuItem("Exibir"); appearance.DropDownItems.Add("Temas e aparência...", null, (s, e) => { using (var d = new AppearanceDialog()) d.ShowDialog(this); });
            var help = new ToolStripMenuItem("Ajuda"); help.DropDownItems.Add("Guia de instalação e uso", null, (s, e) => OpenGuide()); help.DropDownItems.Add("Sobre a v1.0.1", null, (s, e) => Ui.Message(this, "Toccato Desktop Manager 1.0.1\n\nIdentidade oficial Toccato, três paletas institucionais e modos Claro, Escuro e Automático.\n\nCofre local individual e gerenciador de conexões. RDP integrado usa o componente do Windows; outros protocolos usam clientes externos.\n\nPAM, SSO, MFA corporativo, gravação e bancos compartilhados ainda não estão implementados. Consulte a matriz no guia.", "Sobre o TDM"));
            bar.Items.AddRange(new ToolStripItem[] { file, tools, appearance, help }); Controls.Add(bar); MainMenuStrip = bar;
        }

        private Button Nav(string title, EventHandler handler)
        {
            var b = Ui.Button(title, handler); b.Tag = ThemeRole.NavigationButton; Ui.StyleButton(b); b.Width = 200; b.Height = 43; b.TextAlign = ContentAlignment.MiddleLeft; b.Margin = new Padding(0, 1, 0, 2); return b;
        }
        private Panel Metric(string title, Color accent, out Label count)
        {
            var panel = new Panel { Dock = DockStyle.Fill, BackColor = Ui.Surface, Margin = new Padding(0, 0, 12, 0), Tag = ThemeRole.Surface };
            panel.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 3, BackColor = accent, Tag = accent.ToArgb() == Ui.Blue.ToArgb() ? ThemeRole.AccentText : ThemeRole.Preserve });
            var caption = Ui.Label(title, 7.5f, Ui.Muted, true); caption.Location = new Point(16, 16); panel.Controls.Add(caption);
            count = Ui.Label("0", 26, Ui.Ink, true); count.Location = new Point(13, 36); panel.Controls.Add(count); return panel;
        }
        private void BuildConnections()
        {
            var filters = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52, WrapContents = false };
            foreach (var pair in new[] { new[] { "Todas", "all" }, new[] { "Favoritos", "favorites" }, new[] { "Recentes", "recent" } })
            {
                string key = pair[1]; var b = Ui.Button(pair[0], (s, e) => { filter = key; activeClient = activeFolder = null; RefreshConnections(); }); b.Width = 104; b.Height = 32; filters.Controls.Add(b);
            }
            var inspector = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 258, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(18, 10, 0, 10), Tag = ThemeRole.SurfaceAlt };
            inspector.Controls.Add(Ui.Label("DETALHES DA CONEXÃO", 7.5f, Ui.Muted, true));
            detailsName = Detail(inspector, "Selecione uma conexão", 15, true); detailsMeta = Detail(inspector, "", 9); detailsHost = Detail(inspector, "", 10); detailsCredential = Detail(inspector, "", 9); detailsTags = Detail(inspector, "", 9); detailsVpn = Detail(inspector, "", 9); detailsNotes = Detail(inspector, "", 9);
            connectButton = Ui.Button("Conectar", async (s, e) => await ConnectSelected(), true); connectButton.Width = 218; inspector.Controls.Add(connectButton);
            inspector.Controls.Add(InspectorButton("Editar conexão", (s, e) => EditConnection(SelectedConnection())));
            favoriteButton = InspectorButton("Favoritar", (s, e) => Favorite()); inspector.Controls.Add(favoriteButton);
            inspector.Controls.Add(InspectorButton("Testar porta TCP", async (s, e) => await Diagnose(false)));
            inspector.Controls.Add(InspectorButton("Duplicar conexão", (s, e) => Duplicate()));
            inspector.Controls.Add(InspectorButton("Excluir conexão", (s, e) => DeleteConnection()));
            var tip = Detail(inspector, "RDP abre em abas. Clientes externos abrem em suas próprias janelas.", 8); tip.ForeColor = Ui.Muted;
            connections = Ui.Grid(); Ui.Column(connections, "name", "CONEXÃO", 27); Ui.Column(connections, "client", "CLIENTE", 20); Ui.Column(connections, "protocol", "TIPO", 10); Ui.Column(connections, "host", "ENDEREÇO", 25); Ui.Column(connections, "last", "ÚLTIMO USO", 18);
            connections.SelectionChanged += (s, e) => ShowDetails(); connections.CellDoubleClick += async (s, e) => { if (e.RowIndex >= 0) await ConnectSelected(); };
            workspacePage.Controls.Add(connections); workspacePage.Controls.Add(inspector); workspacePage.Controls.Add(filters);
        }
        private Label Detail(FlowLayoutPanel panel, string text, float size, bool bold = false)
        {
            var l = Ui.Label(text, size, bold ? Ui.Ink : Ui.Muted, bold); l.MaximumSize = new Size(218, 0); l.Margin = new Padding(2, 8, 0, 8); panel.Controls.Add(l); return l;
        }
        private Button InspectorButton(string title, EventHandler handler) { var b = Ui.Button(title, handler); b.Width = 218; b.Height = 34; b.Margin = new Padding(2, 3, 0, 3); return b; }
        private void BuildCredentials()
        {
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 54, WrapContents = false };
            actions.Controls.Add(Ui.Button("+ Nova credencial", (s, e) => EditCredential(), true)); actions.Controls.Add(Ui.Button("Editar", (s, e) => EditCredential(SelectedCredential()))); actions.Controls.Add(Ui.Button("Copiar senha · 20s", (s, e) => CopyPassword())); actions.Controls.Add(Ui.Button("Excluir", (s, e) => DeleteCredential()));
            credentials = Ui.Grid(); Ui.Column(credentials, "name", "NOME", 25); Ui.Column(credentials, "user", "USUÁRIO", 25); Ui.Column(credentials, "domain", "DOMÍNIO", 20); Ui.Column(credentials, "password", "SENHA", 15); Ui.Column(credentials, "links", "VÍNCULOS", 15);
            credentials.CellDoubleClick += (s, e) => { if (e.RowIndex >= 0) EditCredential(SelectedCredential()); };
            var description = new Label { Text = "Credenciais vinculadas podem ser atualizadas uma única vez. A senha só é injetada automaticamente no RDP integrado.", Dock = DockStyle.Bottom, Height = 48, ForeColor = Ui.Muted, Font = Ui.Font(9), TextAlign = ContentAlignment.MiddleLeft, Tag = ThemeRole.MutedText };
            credentialsPage.Controls.Add(credentials); credentialsPage.Controls.Add(description); credentialsPage.Controls.Add(actions);
        }
        private void BuildAudit()
        {
            audit = Ui.Grid(); audit.RowTemplate.Height = 40; Ui.Column(audit, "time", "DATA / HORA LOCAL", 20); Ui.Column(audit, "operator", "OPERADOR WINDOWS", 22); Ui.Column(audit, "event", "EVENTO", 22); Ui.Column(audit, "target", "ALVO", 18); Ui.Column(audit, "result", "RESULTADO", 25);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 52 }; actions.Controls.Add(Ui.Button("Atualizar", (s, e) => RefreshAudit())); actions.Controls.Add(Ui.Button("Verificar integridade", (s, e) => Safe(() => { AuditLog.Verify(Data.Audit); Ui.Message(this, "A cadeia local de eventos está íntegra. Este registro não substitui uma auditoria central independente.", "Auditoria local"); })));
            auditPage.Controls.Add(audit); auditPage.Controls.Add(actions);
        }

        private void RefreshData()
        {
            rebuilding = true; clients.BeginUpdate(); clients.Nodes.Clear();
            clients.Nodes.Add(new TreeNode("Todos os clientes") { Tag = new FolderTag() });
            foreach (var c in Data.Clients.OrderBy(x => x.Name))
            {
                var root = new TreeNode(c.Name) { Tag = new FolderTag { ClientId = c.Id } }; clients.Nodes.Add(root);
                foreach (string folder in Data.Connections.Where(x => x.ClientId == c.Id && !String.IsNullOrWhiteSpace(x.Folder)).Select(x => x.Folder).Distinct().OrderBy(x => x))
                {
                    TreeNode parent = root; string cumulative = "";
                    foreach (string part in folder.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
                    {
                        cumulative = String.IsNullOrEmpty(cumulative) ? part : cumulative + "/" + part;
                        var child = parent.Nodes.Cast<TreeNode>().FirstOrDefault(x => x.Name == cumulative);
                        if (child == null) { child = new TreeNode(part) { Name = cumulative, Tag = new FolderTag { ClientId = c.Id, Folder = cumulative } }; parent.Nodes.Add(child); }
                        parent = child;
                    }
                }
                root.Expand();
            }
            clients.EndUpdate(); rebuilding = false;
            RefreshConnections(); RefreshCredentials(); RefreshAudit(); RefreshMetrics();
        }
        private void RefreshMetrics()
        {
            connectionCount.Text = Data.Connections.Count.ToString("00"); clientCount.Text = Data.Clients.Count.ToString("00"); credentialCount.Text = Data.Credentials.Count.ToString("00"); sessionCount.Text = sessions.Count.ToString("00");
        }
        private void RefreshConnections()
        {
            if (connections == null) return;
            string selected = SelectedConnection() == null ? null : SelectedConnection().Id;
            var items = Data.Connections.AsEnumerable();
            if (activeClient != null) items = items.Where(x => x.ClientId == activeClient);
            if (activeFolder != null) items = items.Where(x => x.Folder == activeFolder || x.Folder.StartsWith(activeFolder + "/", StringComparison.OrdinalIgnoreCase));
            if (filter == "favorites") items = items.Where(x => x.Favorite);
            if (filter == "recent") items = items.Where(x => x.LastUsedUtc.HasValue).OrderByDescending(x => x.LastUsedUtc).Take(50);
            else items = items.OrderByDescending(x => x.Favorite).ThenBy(x => x.Name);
            string query = search.Text.Trim();
            if (query.Length > 0) items = items.Where(x => String.Join(" ", new[] { x.Name, x.Host, x.Tags, x.Folder, x.Kind.ToString(), Data.ClientName(x.ClientId), Data.Credential(x.CredentialId) == null ? "" : Data.Credential(x.CredentialId).Name }).IndexOf(query, StringComparison.CurrentCultureIgnoreCase) >= 0);
            connections.Rows.Clear();
            foreach (var c in items)
            {
                int row = connections.Rows.Add((c.Favorite ? "★  " : "") + c.Name, Data.ClientName(c.ClientId), c.Kind, c.Host, c.LastUsedUtc.HasValue ? c.LastUsedUtc.Value.ToLocalTime().ToString("dd/MM HH:mm") : "Ainda não usado"); connections.Rows[row].Tag = c.Id;
                if (c.Id == selected) connections.Rows[row].Selected = true;
            }
            if (connections.SelectedRows.Count > 0) connections.CurrentCell = connections.SelectedRows[0].Cells[0];
            ShowDetails(); UpdateStatus();
        }
        private void UpdateStatus()
        {
            if (status == null || vault == null || vault.Data == null) return;
            status.Text = (vault.IsDemo ? "DEMONSTRAÇÃO · Dados fictícios  |  " : "Cofre desbloqueado · AES-256 + HMAC  |  ") + connections.Rows.Count + " conexões  ·  Bloqueio: " + Data.Settings.IdleMinutes + " min  ·  " + ThemeManager.PaletteName(ThemeManager.Preferences.Palette) + " / " + (ThemeManager.Current.Dark ? "Escuro" : "Claro") + (ThemeManager.Preferences.Mode == ColorMode.Automatic ? " (automático)" : "");
        }
        protected override void OnAppearanceChanged() { UpdateStatus(); }
        private void RefreshCredentials()
        {
            credentials.Rows.Clear();
            foreach (var c in Data.Credentials.OrderBy(x => x.Name)) { int row = credentials.Rows.Add(c.Name, c.UserName, c.Domain, "••••••••", Data.Connections.Count(x => x.CredentialId == c.Id)); credentials.Rows[row].Tag = c.Id; }
        }
        private void RefreshAudit()
        {
            audit.Rows.Clear(); foreach (var e in Data.Audit.AsEnumerable().Reverse().Take(1000)) audit.Rows.Add(e.Utc.ToLocalTime().ToString("dd/MM/yyyy HH:mm:ss"), e.Operator, e.Action, e.Target, e.Result);
        }
        private ConnectionRecord SelectedConnection() { return connections == null || connections.SelectedRows.Count == 0 ? null : Data.Connections.FirstOrDefault(x => x.Id == (string)connections.SelectedRows[0].Tag); }
        private CredentialRecord SelectedCredential() { return credentials.SelectedRows.Count == 0 ? null : Data.Credential((string)credentials.SelectedRows[0].Tag); }
        private void ShowDetails()
        {
            var c = SelectedConnection(); connectButton.Enabled = c != null && !busy;
            detailsName.Text = c == null ? "Seu próximo acesso começa aqui" : c.Name;
            detailsMeta.Text = c == null ? "Adicione um cliente e cadastre sua primeira conexão." : Data.ClientName(c.ClientId) + "  /  " + c.Folder;
            detailsHost.Text = c == null ? "" : c.Host + (c.Kind == ConnectionKind.Web ? "" : "  :" + c.Port);
            detailsCredential.Text = c == null ? "" : "Credencial: " + (Data.Credential(c.CredentialId) == null ? "Solicitar ao conectar" : Data.Credential(c.CredentialId).Name);
            detailsTags.Text = c == null || String.IsNullOrWhiteSpace(c.Tags) ? "" : "Tags: " + c.Tags;
            detailsVpn.Text = c == null || String.IsNullOrWhiteSpace(c.VpnProfile) ? "" : "VPN do Windows: " + c.VpnProfile;
            detailsNotes.Text = c == null ? "" : c.Notes; favoriteButton.Text = c != null && c.Favorite ? "Remover dos favoritos" : "Adicionar aos favoritos";
        }
        private void Safe(Action action) { try { action(); } catch (Exception ex) { Ui.Error(this, ex); } }
        private void Commit(Action<VaultData> change, string action, string target, string result = "Salvo no cofre local") { vault.Transaction(change, action, target, result); }
        private void Log(string action, string target, string result) { Commit(d => { }, action, target, result); RefreshAudit(); }

        private void EditClient(string id = null)
        {
            Safe(() => { using (var d = new ClientDialog(Data, Data.Clients.FirstOrDefault(x => x.Id == id))) if (d.ShowDialog(this) == DialogResult.OK) { Commit(v => { v.Clients.RemoveAll(x => x.Id == d.Record.Id); v.Clients.Add(d.Record); }, "client.saved", d.Record.Name); RefreshData(); } });
        }
        private void DeleteClient()
        {
            Safe(() =>
            {
                var c = Data.Clients.FirstOrDefault(x => x.Id == activeClient); if (c == null) return;
                if (Data.Connections.Any(x => x.ClientId == c.Id)) throw new InvalidOperationException("Mova ou exclua as conexões deste cliente antes de excluí-lo.");
                if (Ui.Message(this, "Excluir o cliente " + c.Name + "?", "Excluir cliente", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Commit(v => v.Clients.RemoveAll(x => x.Id == c.Id), "client.deleted", c.Name); activeClient = activeFolder = null; RefreshData();
            });
        }
        private void EditConnection(ConnectionRecord original = null)
        {
            Safe(() => { if (Data.Clients.Count == 0) { EditClient(); if (Data.Clients.Count == 0) return; } using (var d = new ConnectionDialog(Data, original, original == null ? activeClient : null)) if (d.ShowDialog(this) == DialogResult.OK) { Commit(v => { v.Connections.RemoveAll(x => x.Id == d.Record.Id); v.Connections.Add(d.Record); }, "connection.saved", d.Record.Name); RefreshData(); } });
        }
        private void Duplicate()
        {
            var c = SelectedConnection(); if (c == null) return; var copy = Json.Clone(c); copy.Id = Guid.NewGuid().ToString("N"); copy.Name += " (cópia)"; copy.LastUsedUtc = null; EditConnection(copy);
        }
        private void DeleteConnection()
        {
            Safe(() => { var c = SelectedConnection(); if (c == null) return; if (sessions.Any(x => x.Connection.Id == c.Id)) throw new InvalidOperationException("Feche o painel de sessão antes de excluir a conexão."); if (Ui.Message(this, "Excluir a conexão " + c.Name + "?", "Excluir conexão", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; Commit(v => v.Connections.RemoveAll(x => x.Id == c.Id), "connection.deleted", c.Name); RefreshData(); });
        }
        private void Favorite() { Safe(() => { var c = SelectedConnection(); if (c == null) return; Commit(d => d.Connections.First(x => x.Id == c.Id).Favorite = !c.Favorite, "connection.favorite", c.Name); RefreshConnections(); }); }
        private void EditCredential(CredentialRecord original = null)
        {
            Safe(() => { using (var d = new CredentialDialog(original)) if (d.ShowDialog(this) == DialogResult.OK) { Commit(v => { v.Credentials.RemoveAll(x => x.Id == d.Record.Id); v.Credentials.Add(d.Record); }, "credential.saved", d.Record.Name); RefreshData(); } });
        }
        private void DeleteCredential()
        {
            Safe(() => { var c = SelectedCredential(); if (c == null) return; if (Data.Connections.Any(x => x.CredentialId == c.Id)) throw new InvalidOperationException("Remova os vínculos nas conexões antes de excluir esta credencial."); if (Ui.Message(this, "Excluir a credencial " + c.Name + "?", "Excluir credencial", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; Commit(v => v.Credentials.RemoveAll(x => x.Id == c.Id), "credential.deleted", c.Name); RefreshData(); });
        }
        private void CopyPassword()
        {
            Safe(() =>
            {
                var c = SelectedCredential(); if (c == null || String.IsNullOrEmpty(c.Password)) return;
                if (Ui.Message(this, "Copiar a senha de " + c.Name + " por 20 segundos?\n\nOutros aplicativos e o histórico/sincronização da área de transferência do Windows podem reter uma cópia. Prefira a injeção no RDP.", "Copiar senha", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                Log("credential.copy.requested", c.Name, "Cópia solicitada explicitamente"); Clipboard.SetText(c.Password); clipboardValue = c.Password; clipboardExpires = DateTime.UtcNow.AddSeconds(20);
            });
        }
        private void ClearClipboard()
        {
            try { if (clipboardValue != null && Clipboard.ContainsText() && Clipboard.GetText() == clipboardValue) Clipboard.Clear(); } catch { } clipboardValue = null;
        }

        private async Task ConnectSelected()
        {
            var c = SelectedConnection(); if (c == null || busy) return;
            if (vault.IsDemo) { Ui.Message(this, "Este modo usa dados fictícios e não abre conexões. Inicie o TDM normalmente para criar seu cofre.", "Demonstração"); return; }
            busy = true; ShowDetails();
            SessionView session = null;
            try
            {
                c = Json.Clone(c); var credential = Data.Credential(c.CredentialId); var plan = Connector.Plan(c, credential, Data.Settings);
                if ((c.Kind == ConnectionKind.Telnet || c.Kind == ConnectionKind.FTP) && Ui.Message(this, c.Kind + " pode transmitir dados sem criptografia. Continuar para este destino?", "Protocolo legado", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                if (!String.IsNullOrWhiteSpace(c.BeforeScript) && !RunScript(c.BeforeScript, c)) return;
                Log("connection.requested", c.Name, c.Kind + ": abertura solicitada");
                if (!String.IsNullOrWhiteSpace(c.VpnProfile)) { status.Text = "Aguardando a VPN do Windows..."; await Connector.ConnectVpn(c.VpnProfile); if (closing || IsDisposed) return; Log("vpn.connected", c.VpnProfile, "rasdial retornou sucesso"); }
                if (closing || IsDisposed) return;
                session = AddSession(c);
                if (plan.EmbeddedRdp)
                {
                    var rdp = new RdpHost { Dock = DockStyle.Fill }; session.Rdp = rdp;
                    ((System.ComponentModel.ISupportInitialize)rdp).BeginInit(); session.Page.Controls.Add(rdp); rdp.BringToFront(); ((System.ComponentModel.ISupportInitialize)rdp).EndInit();
                    session.Page.Controls["sessionBar"].BringToFront(); rdp.CreateControl(); rdp.Connect(c, credential); session.State.Text = "Conectando ao RDP. A autenticação será confirmada pelo Windows.";
                }
                else
                {
                    session.Process = Connector.Start(plan); session.State.Text = "Cliente externo aberto. A sessão e a autenticação são gerenciadas pelo aplicativo externo.";
                    var hint = new Label { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = Ui.Font(14), ForeColor = Ui.Muted, Text = c.Kind + " aberto em uma janela externa\n\n" + c.Host + "\n\nFechar este painel não encerra o aplicativo externo." }; session.Page.Controls.Add(hint); hint.BringToFront(); session.Page.Controls["sessionBar"].BringToFront();
                    Log("external.launcher.started", c.Name, "Cliente iniciado; autenticação não verificada pelo TDM");
                }
                Commit(d => d.Connections.First(x => x.Id == c.Id).LastUsedUtc = DateTime.UtcNow, "connection.dispatched", c.Name, "Solicitação entregue ao cliente"); RefreshConnections(); RefreshMetrics();
            }
            catch (Exception ex)
            {
                if (session != null) Safe(() => CloseSession(session, false));
                if (!closing && !IsDisposed) { Safe(() => Log("connection.failed", c.Name, "Falha ao iniciar conexão; autenticação não confirmada")); Ui.Error(this, ex); }
            }
            finally { busy = false; if (!IsDisposed) ShowDetails(); }
        }
        private SessionView AddSession(ConnectionRecord c)
        {
            var view = new SessionView { Connection = c, Page = new TabPage(c.Name) { BackColor = Color.White } };
            var toolbar = new Panel { Name = "sessionBar", Dock = DockStyle.Top, Height = 72, Padding = new Padding(10) };
            var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, Width = 320, FlowDirection = FlowDirection.RightToLeft };
            buttons.Controls.Add(Ui.Button("Fechar painel", (s, e) => Safe(() => CloseSession(view, true)))); buttons.Controls.Add(Ui.Button("Destacar janela", (s, e) => Detach(view)));
            view.State = new Label { Dock = DockStyle.Fill, Font = Ui.Font(9), ForeColor = Ui.Muted, TextAlign = ContentAlignment.MiddleLeft, Text = "Preparando o cliente..." };
            toolbar.Controls.Add(view.State); toolbar.Controls.Add(buttons); view.Page.Controls.Add(toolbar); tabs.TabPages.Add(view.Page); tabs.SelectedTab = view.Page; sessions.Add(view); RefreshMetrics(); return view;
        }
        private void Detach(SessionView view)
        {
            if (view.Rdp == null || view.Detached != null) return;
            view.Detached = new ThemedForm { Text = view.Connection.Name + " — TDM", Size = new Size(1200, 800), StartPosition = FormStartPosition.CenterScreen, Font = Ui.Font() };
            view.Page.Controls.Remove(view.Rdp); view.Detached.Controls.Add(view.Rdp); view.Detached.FormClosed += (s, e) => { if (!view.Removed) Safe(() => CloseSession(view, true)); }; view.Detached.Show(this);
            view.State.Text = "Sessão em janela separada. Mova para o monitor desejado.";
        }
        private void CloseSession(SessionView view, bool allowScript)
        {
            if (view.Removed) return; view.Removed = true;
            if (view.Rdp != null) { view.Rdp.Disconnect(); view.Rdp.Dispose(); }
            if (view.Detached != null && !view.Detached.IsDisposed) view.Detached.Close();
            tabs.TabPages.Remove(view.Page); view.Page.Dispose(); sessions.Remove(view); RefreshMetrics();
            if (!closing) Log("session.panel.closed", view.Connection.Name, view.Rdp != null ? "RDP local desconectado" : "Painel fechado; cliente externo continua independente");
            if (allowScript && !String.IsNullOrWhiteSpace(view.Connection.AfterScript)) RunScript(view.Connection.AfterScript, view.Connection);
        }
        private bool RunScript(string initial, ConnectionRecord connection)
        {
            if (vault.IsDemo) return false;
            using (var d = new ScriptDialog(initial, connection == null ? "nenhuma conexão selecionada" : connection.Name + " / " + connection.Host))
            {
                if (d.ShowDialog(this) != DialogResult.OK) return false;
                Log("script.requested", connection == null ? "PowerShell local" : connection.Name, "Script revisado e autorizado na interface"); Connector.RunPowerShell(d.Script, connection); Log("script.process.started", connection == null ? "Local" : connection.Name, "PowerShell aberto; conclusão não monitorada"); return true;
            }
        }
        private async Task Diagnose(bool ping)
        {
            var c = SelectedConnection(); if (c == null) return;
            try { if (vault.IsDemo) throw new InvalidOperationException("Diagnósticos desabilitados em demonstração."); string host = c.Kind == ConnectionKind.Web ? Guard.WebUri(c.Host).Host : Guard.Host(c.Host); status.Text = "Testando conectividade..."; string result = ping ? await Connector.PingTest(host) : await Connector.TcpTest(host, c.Kind == ConnectionKind.Web ? Guard.WebUri(c.Host).Port : c.Port); if (closing) return; Log(ping ? "diagnostic.ping" : "diagnostic.tcp", c.Name, result); Ui.Message(this, result, "Diagnóstico"); }
            catch (Exception ex) { if (!closing) Ui.Error(this, ex); }
        }
        private async Task DnsLookup()
        {
            var c = SelectedConnection(); if (c == null || vault.IsDemo) return;
            try { string host = c.Kind == ConnectionKind.Web ? Guard.WebUri(c.Host).Host : Guard.Host(c.Host); var addresses = await Dns.GetHostAddressesAsync(host); if (closing) return; Log("diagnostic.dns", c.Name, "Consulta DNS concluída"); Ui.Message(this, String.Join(Environment.NewLine, addresses.Select(x => x.ToString())), "DNS · " + host); } catch (Exception ex) { if (!closing) Ui.Error(this, ex); }
        }
        private void Configure() { Safe(() => { using (var d = new SettingsDialog(Data.Settings)) if (d.ShowDialog(this) == DialogResult.OK) { Commit(v => v.Settings = d.Settings, "settings.saved", Data.Title); RefreshData(); } }); }
        private void Backup()
        {
            Safe(() => { using (var d = new SaveFileDialog { Filter = "Cofre TDM (*.tdmv)|*.tdmv", FileName = "TDM_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".tdmv", OverwritePrompt = true }) if (d.ShowDialog(this) == DialogResult.OK) { Log("vault.backup.requested", Data.Title, "Backup criptografado solicitado"); vault.Backup(d.FileName); Ui.Message(this, "Backup criptografado criado. Ele usa a senha-mestra atual.", "Backup"); } });
        }
        private void ChangePassword()
        {
            if (vault.IsDemo) return;
            Safe(() => { using (var d = new PasswordDialog()) if (d.ShowDialog(this) == DialogResult.OK) { vault.ChangePassword(d.Password); Log("vault.password.changed", Data.Title, "Senha-mestra alterada; backups anteriores usam senha anterior"); Ui.Message(this, "Senha alterada. Crie um novo backup e proteja as cópias antigas.", "Senha-mestra"); } });
        }
        private void OpenGuide()
        {
            Safe(() => { string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Guia_TDM_1_0_1.html"); if (!File.Exists(path)) throw new FileNotFoundException("O guia não foi encontrado junto ao aplicativo."); Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); });
        }
        private void QuickSearch() { if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal; Activate(); tabs.SelectedTab = workspacePage; search.Focus(); search.SelectAll(); }
        private void LockWorkspace() { if (busy || closing) return; Reopen = true; forceLock = true; Close(); }
        private void ClosingWorkspace(object sender, FormClosingEventArgs e)
        {
            if (busy && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; return; }
            if (!forceLock && sessions.Any(x => x.Rdp != null) && e.CloseReason == CloseReason.UserClosing && Ui.Message(this, "Encerrar as sessões RDP integradas e fechar o TDM?", "Fechar workspace", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) { e.Cancel = true; return; }
            try
            {
                Log(Reopen ? "vault.locked" : "vault.closed", Data.Title, "RDP integrado será desconectado; clientes externos não são encerrados");
                foreach (var v in sessions.ToList()) CloseSession(v, false);
            }
            catch (Exception ex) { if (!forceLock) { e.Cancel = true; Ui.Error(this, ex); return; } }
            closing = true;
            foreach (var v in sessions.ToList()) { try { CloseSession(v, false); } catch { } }
            ClearClipboard();
        }
        private void Heartbeat(object sender, EventArgs e)
        {
            if (closing) return;
            if (clipboardValue != null && DateTime.UtcNow >= clipboardExpires) ClearClipboard();
            if (!vault.IsDemo && !busy && Environment.OSVersion.Platform == PlatformID.Win32NT)
            {
                var input = new LastInputInfo { Size = (uint)Marshal.SizeOf(typeof(LastInputInfo)) };
                if (GetLastInputInfo(ref input) && unchecked((uint)Environment.TickCount - input.Tick) >= Data.Settings.IdleMinutes * 60000U) { LockWorkspace(); return; }
            }
            foreach (var view in sessions.ToList())
            {
                if (view.Rdp == null || view.Removed) continue; int current = view.Rdp.ConnectionState;
                if (current != view.LastState)
                {
                    int previous = view.LastState; view.LastState = current;
                    if (current == 1) { view.State.Text = "Transporte RDP conectado. A autenticação da sessão é tratada pelo Windows."; Safe(() => Log("rdp.transport.connected", view.Connection.Name, "Transporte conectado; login não atestado pelo TDM")); }
                    else if (current == 0 && previous >= 0) { view.State.Text = "RDP desconectado. Feche o painel e abra novamente para reconectar."; Safe(() => Log("rdp.transport.disconnected", view.Connection.Name, "Componente RDP desconectado")); }
                }
            }
        }
        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e); if (Environment.OSVersion.Platform == PlatformID.Win32NT) { RegisterHotKey(Handle, 1824, 0x0002 | 0x4000, (uint)Keys.Space); WTSRegisterSessionNotification(Handle, 0); }
        }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == 0x0312 && m.WParam.ToInt32() == 1824) QuickSearch();
            if (m.Msg == 0x02B1 && m.WParam.ToInt32() == 7 && !closing && !vault.IsDemo) { forceLock = true; Reopen = true; BeginInvoke(new Action(() => { busy = false; Close(); })); }
            base.WndProc(ref m);
        }
        [StructLayout(LayoutKind.Sequential)] private struct LastInputInfo { public uint Size; public uint Tick; }
        [DllImport("user32.dll")] private static extern bool GetLastInputInfo(ref LastInputInfo info);
        [DllImport("user32.dll")] private static extern bool RegisterHotKey(IntPtr hwnd, int id, uint modifiers, uint key);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr hwnd, int id);
        [DllImport("wtsapi32.dll")] private static extern bool WTSRegisterSessionNotification(IntPtr hwnd, int flags);
        [DllImport("wtsapi32.dll")] private static extern bool WTSUnRegisterSessionNotification(IntPtr hwnd);
    }
}
