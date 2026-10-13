namespace OverlayDesk;

public sealed class MainForm : Form
{
    private readonly AppData _data = DataStore.Load();
    private readonly ListView _layouts = new();
    private readonly Label _status = new();
    private readonly Button _launch = new();
    private readonly Button _edit = new();
    private readonly Button _duplicate = new();
    private readonly Button _delete = new();
    private readonly Button _pin = new();
    private readonly Button _lock = new();
    private readonly Button _arrange = new();
    private readonly List<OverlayForm> _openOverlays = new();

    public MainForm()
    {
        Text = "OverlayDesk";
        StartPosition = FormStartPosition.CenterScreen;
        MinimumSize = new Size(600, 400);
        Size = new Size(_data.Settings.MainWindowWidth, _data.Settings.MainWindowHeight);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3, Padding = new Padding(12) };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        header.Controls.Add(MakeButton("New layout", (_, _) => CreateLayout()));
        _launch.Text = "Launch";
        _launch.Click += (_, _) => LaunchSelected();
        header.Controls.Add(_launch);
        _arrange.Text = "Arrange overlay";
        _arrange.AutoSize = true;
        _arrange.Click += (_, _) => ArrangeSelected();
        header.Controls.Add(_arrange);
        _lock.Text = "Lock";
        _lock.AutoSize = true;
        _lock.Click += (_, _) => ToggleLock();
        header.Controls.Add(_lock);
        _edit.Text = "Edit";
        _edit.Click += (_, _) => EditSelected();
        header.Controls.Add(_edit);
        _duplicate.Text = "Duplicate";
        _duplicate.Click += (_, _) => DuplicateSelected();
        header.Controls.Add(_duplicate);
        _delete.Text = "Delete";
        _delete.Click += (_, _) => DeleteSelected();
        header.Controls.Add(_delete);
        _pin.Text = "Pin / unpin";
        _pin.Click += (_, _) => TogglePin();
        header.Controls.Add(_pin);
        header.Controls.Add(MakeButton("Settings", (_, _) => ShowSettings()));
        header.Controls.Add(MakeButton("Kill all", (_, _) => KillAll()));
        root.Controls.Add(header, 0, 0);

        _layouts.Dock = DockStyle.Fill;
        _layouts.View = View.Details;
        _layouts.FullRowSelect = true;
        _layouts.MultiSelect = false;
        _layouts.HideSelection = false;
        _layouts.Columns.Add("Layout", 260);
        _layouts.Columns.Add("Items", 70);
        _layouts.Columns.Add("Pinned", 70);
        _layouts.Columns.Add("Locked", 70);
        _layouts.Columns.Add("Always on top", 110);
        _layouts.DoubleClick += (_, _) => LaunchSelected();
        _layouts.SelectedIndexChanged += (_, _) => UpdateButtons();
        root.Controls.Add(_layouts, 0, 1);

        _status.AutoSize = true;
        _status.Padding = new Padding(0, 8, 0, 0);
        root.Controls.Add(_status, 0, 2);
        RefreshLayouts();
        FormClosed += (_, _) => VlcEngine.Shutdown();
        FormClosing += (_, e) =>
        {
            int activeOverlays = _openOverlays.Count(x => !x.IsDisposed);
            if (activeOverlays > 0)
            {
                var answer = MessageBox.Show(this,
                    $"Exit OverlayDesk and close its {activeOverlays} active overlay window(s)?",
                    "Confirm exit", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes)
                {
                    e.Cancel = true;
                    return;
                }
                foreach (var overlay in _openOverlays.ToArray())
                    if (!overlay.IsDisposed) overlay.CloseWithoutPrompt();
                _openOverlays.Clear();
            }
            _data.Settings.MainWindowWidth = Width;
            _data.Settings.MainWindowHeight = Height;
            DataStore.Save(_data);
        };
    }

    private Button MakeButton(string text, EventHandler click)
    {
        var b = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
        b.Click += click;
        return b;
    }

    private LayoutDefinition? SelectedLayout =>
        _layouts.SelectedItems.Count == 0 ? null : _layouts.SelectedItems[0].Tag as LayoutDefinition;

    private void RefreshLayouts(Guid? selectId = null)
    {
        _layouts.Items.Clear();
        foreach (var layout in _data.Layouts.OrderByDescending(x => x.Pinned).ThenBy(x => x.Name))
        {
            var item = new ListViewItem(layout.Name) { Tag = layout };
            item.SubItems.Add(layout.Items.Count.ToString());
            item.SubItems.Add(layout.Pinned ? "Yes" : "No");
            item.SubItems.Add(layout.Locked ? "Yes" : "No");
            item.SubItems.Add(layout.AlwaysOnTop ? "Yes" : "No");
            _layouts.Items.Add(item);
            if (selectId == layout.Id) item.Selected = true;
        }
        UpdateButtons();
        _status.Text = $"{_data.Layouts.Count} saved layout(s). Overlays work while this launcher is inactive or minimized.";
    }

    private void UpdateButtons()
    {
        bool selected = SelectedLayout != null;
        _launch.Enabled = _duplicate.Enabled = _delete.Enabled = _pin.Enabled = _lock.Enabled = selected;
        _edit.Enabled = _arrange.Enabled = selected && !SelectedLayout!.Locked;
        _lock.Text = SelectedLayout?.Locked == true ? "Unlock" : "Lock";
    }

    private void CreateLayout()
    {
        using var dialog = new LayoutEditorForm(null, _data.Settings.DefaultAlwaysOnTop, _data.Settings.DefaultSnapToItems,
            _data.Settings.DefaultSnapToGrid, _data.Settings.DefaultGridSize);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) return;
        _data.Layouts.Add(dialog.Result);
        DataStore.Save(_data);
        RefreshLayouts(dialog.Result.Id);
    }

    private void EditSelected()
    {
        var old = SelectedLayout;
        if (old == null) return;
        if (old.Locked)
        {
            MessageBox.Show(this, "Unlock this layout before editing it.", "Layout locked");
            return;
        }
        var running = _openOverlays.Where(o => !o.IsDisposed && o.LayoutId == old.Id).ToArray();
        if (running.Length > 0)
        {
            if (MessageBox.Show(this, "Close this layout's active overlay before editing? Its current positions will be saved.",
                "Edit active layout", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                return;
            foreach (var window in running) window.CloseWithoutPrompt();
        }
        using var dialog = new LayoutEditorForm(Clone(old), _data.Settings.DefaultAlwaysOnTop, _data.Settings.DefaultSnapToItems,
            _data.Settings.DefaultSnapToGrid, _data.Settings.DefaultGridSize);
        if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Result == null) return;
        int index = _data.Layouts.FindIndex(x => x.Id == old.Id);
        if (index >= 0) _data.Layouts[index] = dialog.Result;
        DataStore.Save(_data);
        RefreshLayouts(dialog.Result.Id);
    }

    private static LayoutDefinition Clone(LayoutDefinition source) => new()
    {
        Id = source.Id, Name = source.Name, Pinned = source.Pinned, AlwaysOnTop = source.AlwaysOnTop,
        SnapToItems = source.SnapToItems, SnapToGrid = source.SnapToGrid,
        GridSize = source.GridSize, ForceKeepAspectRatio = source.ForceKeepAspectRatio,
        Locked = source.Locked, WindowX = source.WindowX, WindowY = source.WindowY,
        Items = source.Items.Select(i => new OverlayItem { Path = i.Path, X = i.X, Y = i.Y,
            Width = i.Width, Height = i.Height, SizeIsAutomatic = i.SizeIsAutomatic,
            ForceKeepAspectRatio = i.ForceKeepAspectRatio }).ToList()
    };

    private void DuplicateSelected()
    {
        var source = SelectedLayout;
        if (source == null) return;
        var copy = Clone(source);
        copy.Id = Guid.NewGuid();
        copy.Name += " (copy)";
        copy.Pinned = false;
        copy.Locked = false;
        copy.WindowX = null;
        copy.WindowY = null;
        _data.Layouts.Add(copy);
        DataStore.Save(_data);
        RefreshLayouts(copy.Id);
    }

    private void DeleteSelected()
    {
        var selected = SelectedLayout;
        if (selected == null) return;
        if (MessageBox.Show(this, $"Delete layout '{selected.Name}'?", "Confirm delete",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        _data.Layouts.Remove(selected);
        DataStore.Save(_data);
        RefreshLayouts();
    }

    private void ToggleLock()
    {
        var layout = SelectedLayout;
        if (layout == null) return;
        layout.Locked = !layout.Locked;
        foreach (var overlay in _openOverlays.Where(o => !o.IsDisposed && o.LayoutId == layout.Id))
            overlay.SetLocked(layout.Locked);
        DataStore.Save(_data);
        RefreshLayouts(layout.Id);
    }

    private void TogglePin()
    {
        var selected = SelectedLayout;
        if (selected == null) return;
        selected.Pinned = !selected.Pinned;
        DataStore.Save(_data);
        RefreshLayouts(selected.Id);
    }

    private void LaunchSelected()
    {
        var layout = SelectedLayout;
        if (layout == null) return;
        if (layout.Items.Count == 0)
        {
            MessageBox.Show(this, "This layout has no files. Edit it and add one or more files.", "Nothing to launch");
            return;
        }
        if (!layout.Items.Any(i => File.Exists(i.Path)))
        {
            MessageBox.Show(this, "None of the files in this layout could be found.", "Files unavailable");
            return;
        }
        try
        {
            var overlay = new OverlayForm(layout, () => DataStore.Save(_data));
            overlay.FormClosed += (_, _) => _openOverlays.Remove(overlay);
            _openOverlays.Add(overlay);
            overlay.Show();
        }
        catch (Exception ex)
        {
            MessageBox.Show(this, "Unable to launch this overlay:\n\n" + ex.Message,
                "OverlayDesk", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void ArrangeSelected()
    {
        var layout = SelectedLayout;
        if (layout == null || layout.Locked) return;
        var current = _openOverlays.LastOrDefault(o => !o.IsDisposed && o.LayoutId == layout.Id);
        if (current == null)
        {
            MessageBox.Show(this, "Launch this layout before arranging its files.", "No running overlay");
            return;
        }
        current.ToggleArrange();
        if (current.IsArranging) current.Activate();
    }

    private void KillAll()
    {
        var active = _openOverlays.Where(x => !x.IsDisposed).ToArray();
        if (active.Length == 0)
        {
            MessageBox.Show(this, "There are no active overlay windows.", "Kill all", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        var answer = MessageBox.Show(this,
            $"Close all {active.Length} active overlay window(s)? This cannot be undone.",
            "Confirm kill all", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (answer != DialogResult.Yes) return;
        foreach (var overlay in active) overlay.CloseWithoutPrompt();
        _openOverlays.Clear();
    }

    private void ShowSettings()
    {
        using var form = new Form
        {
            Text = "Settings", StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog, MinimizeBox = false,
            MaximizeBox = false, ClientSize = new Size(420, 188)
        };
        var top = new CheckBox
        {
            Text = "New layouts default to always on top",
            AutoSize = true, Left = 16, Top = 18,
            Checked = _data.Settings.DefaultAlwaysOnTop
        };
        var snap = new CheckBox
        {
            Text = "New layouts default to snapping enabled",
            AutoSize = true, Left = 16, Top = 48,
            Checked = _data.Settings.DefaultSnapToItems
        };
        var note = new Label
        {
            Text = "Existing layouts have their own snapping setting.\nData is stored in %LOCALAPPDATA%\\OverlayDesk\\data.json",
            Left = 16, Top = 88, Width = 385, Height = 42
        };
        var save = new Button
        {
            Text = "Save", DialogResult = DialogResult.OK,
            Left = 320, Top = 143, Width = 80
        };
        form.Controls.AddRange(new Control[] { top, snap, note, save });
        form.AcceptButton = save;
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            _data.Settings.DefaultAlwaysOnTop = top.Checked;
            _data.Settings.DefaultSnapToItems = snap.Checked;
            DataStore.Save(_data);
        }
    }
}
