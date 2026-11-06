using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace Toccato.Desktop
{
    public static class ErrorDiagnostics
    {
        public static Exception Unwrap(Exception error)
        {
            for (int i = 0; i < 8 && error.InnerException != null && (error is System.Reflection.TargetInvocationException || error is AggregateException); i++) error = error.InnerException;
            return error;
        }
        // Intentionally omit Message, ToString, paths, exception.Data, arguments and object values.
        public static string SafeSummary(Exception error, string operation, string reference)
        {
            error = Unwrap(error);
            string origin = "Origem indisponível";
            var frames = new StackTrace(error, false).GetFrames();
            if (frames != null)
            {
                var methods = frames.Select(f => f.GetMethod()).Where(m => m != null && m.DeclaringType != null && m.DeclaringType.Namespace == "Toccato.Desktop").Take(6).Select(m => m.DeclaringType.Name + "." + m.Name).ToArray();
                if (methods.Length > 0) origin = String.Join(" → ", methods);
            }
            string context = operation == "Nova conexão" || operation == "Editar conexão" ? "Salvar conexão" : "Operação do TDM";
            return "Toccato Desktop Manager 1.0.1\r\nReferência: " + reference + "\r\nOperação: " + context + "\r\nTipo: " + error.GetType().Name + "\r\nCódigo: 0x" + error.HResult.ToString("X8") + "\r\nOrigem: " + origin;
        }
        public static string Capture(Exception error, string operation)
        {
            string reference = "TDM-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            string summary = SafeSummary(error, operation, reference);
            try
            {
                string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Toccato", "DesktopManager"); Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, "diagnostics.log");
                if (File.Exists(path) && new FileInfo(path).Length > 131072) { string previous = path + ".previous"; if (File.Exists(previous)) File.Delete(previous); File.Move(path, previous); }
                File.AppendAllText(path, DateTime.UtcNow.ToString("o") + "\r\n" + summary + "\r\n\r\n", Encoding.UTF8);
            }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            return summary;
        }
    }

    public sealed class ThemedMessageForm : ThemedForm
    {
        public ThemedMessageForm(string message, string title, MessageBoxButtons buttons, string diagnostic = null)
        {
            Text = title + " — TDM"; Font = Ui.Font(); StartPosition = FormStartPosition.CenterParent; ShowInTaskbar = false; MaximizeBox = false; MinimizeBox = false;
            MinimumSize = new Size(580, 300); ClientSize = new Size(680, diagnostic == null ? 340 : 470);
            var head = new Panel { Dock = DockStyle.Top, Height = 76, Padding = new Padding(24, 18, 24, 12), Tag = ThemeRole.Canvas };
            var heading = Ui.Label(title, 17, null, true); heading.Dock = DockStyle.Fill; head.Controls.Add(heading);
            var body = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true, Padding = new Padding(24, 16, 24, 12) };
            var text = Ui.Label(message, 10); text.MaximumSize = new Size(610, 0); text.Margin = new Padding(0, 0, 0, 18); body.Controls.Add(text);
            if (diagnostic != null)
            {
                var details = Ui.Text(diagnostic, false, true); details.ReadOnly = true; details.Width = 610; details.Height = 156; details.Font = new Font("Consolas", 9); details.Dock = DockStyle.None; details.WordWrap = true; body.Controls.Add(details);
            }
            var actions = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 70, FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(16, 12, 16, 10), Tag = ThemeRole.Canvas };
            if (buttons == MessageBoxButtons.YesNo)
            {
                var no = Ui.Button("Não", (s, e) => DialogResult = DialogResult.No); var yes = Ui.Button("Sim", (s, e) => DialogResult = DialogResult.Yes, true);
                actions.Controls.Add(yes); actions.Controls.Add(no); AcceptButton = yes; CancelButton = no;
            }
            else
            {
                var ok = Ui.Button("Fechar", (s, e) => DialogResult = DialogResult.OK, true); actions.Controls.Add(ok); AcceptButton = ok; CancelButton = ok;
            }
            if (diagnostic != null)
            {
                var copy = Ui.Button("Copiar diagnóstico", (s, e) => { try { Clipboard.SetText(diagnostic); } catch { } }); copy.Width = 164; actions.Controls.Add(copy);
            }
            Controls.Add(body); Controls.Add(actions); Controls.Add(head);
        }
    }
}
