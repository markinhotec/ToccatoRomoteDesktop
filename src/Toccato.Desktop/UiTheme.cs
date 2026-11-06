using System;
using System.Drawing;
using System.Windows.Forms;

namespace Toccato.Desktop
{
    public static partial class Ui
    {
        public static void StyleButton(Button button)
        {
            var c = ThemeManager.Current; var role = button.Tag is ThemeRole ? (ThemeRole)button.Tag : ThemeRole.SecondaryButton;
            bool primary = role == ThemeRole.PrimaryButton, nav = role == ThemeRole.NavigationButton;
            button.UseVisualStyleBackColor = false;
            button.BackColor = !button.Enabled ? c.SurfaceAlt : nav ? c.Sidebar : primary ? c.Accent : c.Surface;
            button.ForeColor = !button.Enabled ? c.Muted : nav ? c.SidebarText : primary ? Color.White : c.Text;
            button.FlatAppearance.BorderSize = nav ? 0 : 1; button.FlatAppearance.BorderColor = primary ? c.Accent : c.Border;
            button.FlatAppearance.MouseOverBackColor = nav ? c.SidebarHover : primary ? ControlPaint.Dark(c.Accent, 0.03f) : c.Selection;
            button.FlatAppearance.MouseDownBackColor = nav ? c.SidebarHover : primary ? ControlPaint.Dark(c.Accent, 0.1f) : c.Selection;
        }
        public static void ConfigureCombo(ComboBox combo)
        {
            if (combo.DrawMode == DrawMode.OwnerDrawFixed) return;
            combo.DrawMode = DrawMode.OwnerDrawFixed; combo.ItemHeight = Math.Max(23, combo.Font.Height + 6);
            combo.DrawItem += (s, e) =>
            {
                if (e.Index < 0) return; var c = ThemeManager.Current; bool selected = (e.State & DrawItemState.Selected) != 0;
                using (var background = new SolidBrush(selected ? c.Selection : c.Input)) e.Graphics.FillRectangle(background, e.Bounds);
                TextRenderer.DrawText(e.Graphics, combo.GetItemText(combo.Items[e.Index]), combo.Font, new Rectangle(e.Bounds.X + 5, e.Bounds.Y, e.Bounds.Width - 10, e.Bounds.Height), c.Text, TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                if ((e.State & DrawItemState.Focus) != 0) e.DrawFocusRectangle();
            };
        }
        public static void ApplyTheme(Control root) { ApplyControl(root, false, ThemeRole.Surface); root.Invalidate(true); }
        private static void ApplyControl(Control control, bool sidebar, ThemeRole inheritedSurface)
        {
            if (control.IsDisposed || control is AxHost) return;
            var c = ThemeManager.Current; var role = control.Tag is ThemeRole ? (ThemeRole)control.Tag : inheritedSurface;
            if (role == ThemeRole.Preserve) return;
            sidebar = sidebar || role == ThemeRole.Sidebar;
            ThemeRole surfaceRole = role == ThemeRole.Canvas || role == ThemeRole.Surface || role == ThemeRole.SurfaceAlt ? role : inheritedSurface;
            if (control is TabPage) surfaceRole = ThemeRole.Surface;
            Color background = sidebar ? c.Sidebar : surfaceRole == ThemeRole.Canvas ? c.Canvas : surfaceRole == ThemeRole.SurfaceAlt ? c.SurfaceAlt : c.Surface;
            Color foreground = sidebar ? c.SidebarText : c.Text;
            var button = control as Button;
            var label = control as Label;
            var grid = control as DataGridView;
            var tree = control as TreeView;
            var strip = control as ToolStrip;
            if (button != null) StyleButton(button);
            else if (label != null)
            {
                label.BackColor = Color.Transparent;
                label.ForeColor = role == ThemeRole.SidebarMuted ? c.SidebarMuted : role == ThemeRole.AccentText ? c.AccentText : role == ThemeRole.MutedText ? (sidebar ? c.SidebarMuted : c.Muted) : foreground;
            }
            else if (grid != null)
            {
                grid.BackgroundColor = c.Surface; grid.GridColor = c.Border; grid.ForeColor = c.Text; grid.BackColor = c.Surface;
                grid.DefaultCellStyle.BackColor = c.Surface; grid.DefaultCellStyle.ForeColor = c.Text;
                grid.DefaultCellStyle.SelectionBackColor = c.Selection; grid.DefaultCellStyle.SelectionForeColor = c.Text;
                grid.AlternatingRowsDefaultCellStyle.BackColor = c.SurfaceAlt;
                grid.ColumnHeadersDefaultCellStyle.BackColor = c.SurfaceAlt; grid.ColumnHeadersDefaultCellStyle.ForeColor = c.Muted;
                grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = c.SurfaceAlt; grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = c.Text;
                grid.RowHeadersDefaultCellStyle.BackColor = c.SurfaceAlt; grid.RowHeadersDefaultCellStyle.ForeColor = c.Muted;
                grid.EnableHeadersVisualStyles = false;
            }
            else if (tree != null)
            {
                tree.BackColor = c.Sidebar; tree.ForeColor = c.SidebarText; tree.LineColor = c.SidebarMuted;
                if (tree.DrawMode == TreeViewDrawMode.Normal)
                {
                    tree.DrawMode = TreeViewDrawMode.OwnerDrawText;
                    tree.DrawNode += (s, e) =>
                    {
                        var colors = ThemeManager.Current; bool selected = (e.State & TreeNodeStates.Selected) != 0;
                        using (var fill = new SolidBrush(selected ? colors.SidebarHover : colors.Sidebar)) e.Graphics.FillRectangle(fill, e.Bounds);
                        TextRenderer.DrawText(e.Graphics, e.Node.Text, tree.Font, e.Bounds, colors.SidebarText, TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
                    };
                }
            }
            else if (strip != null) ApplyStrip(strip);
            else if (control is TextBoxBase || control is ComboBox || control is NumericUpDown)
            {
                control.BackColor = control is TextBoxBase && ((TextBoxBase)control).ReadOnly ? c.SurfaceAlt : c.Input;
                control.ForeColor = c.Text;
                var combo = control as ComboBox; if (combo != null) ConfigureCombo(combo);
            }
            else
            {
                control.BackColor = role == ThemeRole.AccentText ? c.Accent : background; control.ForeColor = foreground;
                var check = control as CheckBox; if (check != null) { check.FlatStyle = FlatStyle.Flat; check.UseVisualStyleBackColor = false; }
                var radio = control as RadioButton; if (radio != null) radio.UseVisualStyleBackColor = false;
            }
            if (control.ContextMenuStrip != null) ApplyStrip(control.ContextMenuStrip);
            if (grid != null || tree != null || control is ComboBox) return;
            foreach (Control child in control.Controls) ApplyControl(child, sidebar, surfaceRole);
        }
        private static void ApplyStrip(ToolStrip strip)
        {
            var c = ThemeManager.Current; strip.BackColor = c.Surface; strip.ForeColor = c.Text;
            strip.Renderer = new ToolStripProfessionalRenderer(new ThemeColorTable()) { RoundedEdges = false };
            foreach (ToolStripItem item in strip.Items) ApplyStripItem(item);
        }
        private static void ApplyStripItem(ToolStripItem item)
        {
            var c = ThemeManager.Current; item.BackColor = c.Surface; item.ForeColor = c.Text;
            var menu = item as ToolStripMenuItem;
            if (menu != null)
            {
                menu.DropDown.BackColor = c.Surface; menu.DropDown.ForeColor = c.Text;
                foreach (ToolStripItem child in menu.DropDownItems) ApplyStripItem(child);
            }
        }
        public static DialogResult Message(IWin32Window owner, string text, string title, MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.None)
        {
            using (var dialog = new ThemedMessageForm(text, title, buttons)) return dialog.ShowDialog(owner);
        }
    }
}
