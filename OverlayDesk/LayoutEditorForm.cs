namespace OverlayDesk;

public sealed class LayoutEditorForm : Form
{
    private readonly TextBox _name = new();
    private readonly CheckBox _topmost = new();
    private readonly CheckBox _snap = new();
    private readonly CheckBox _snapGrid = new();
    private readonly CheckBox _forceAspect = new();
    private readonly CheckBox _locked = new();
    private readonly NumericUpDown _gridSize = MakeNumber(8, 512);
    private readonly ListView _files = new();
    private readonly List<OverlayItem> _items;
    private readonly Guid _id;
    private readonly bool _pinned;
    private readonly int? _windowX;
    private readonly int? _windowY;

    private readonly NumericUpDown _x = MakeNumber(-100000, 100000);
    private readonly NumericUpDown _y = MakeNumber(-100000, 100000);
    private readonly NumericUpDown _width = MakeNumber(1, 100000);
    private readonly NumericUpDown _height = MakeNumber(1, 100000);
    private readonly CheckBox _aspectLock = new() { Text = "Keep ratio when typing", AutoSize = true, Checked = true };
    private readonly CheckBox _itemForceAspect = new() { Text = "Force ratio when dragging", AutoSize = true };
    private readonly Label _original = new() { AutoSize = true, MaximumSize = new Size(225, 0) };
    private readonly Button _resetSize = new() { Text = "Use original size", AutoSize = true };
    private readonly Panel _details = new() { Dock = DockStyle.Fill, Padding = new Padding(12), AutoScroll = true };
    private bool _loadingDetails;
    private OverlayItem? _activeItem;
    public LayoutDefinition? Result { get; private set; }

    private static NumericUpDown MakeNumber(int min, int max) => new()
    {
        Minimum = min, Maximum = max, Width = 110, ThousandsSeparator = true
    };

    public LayoutEditorForm(LayoutDefinition? layout, bool defaultTopmost, bool defaultSnap,
        bool defaultGrid = false, int defaultGridSize = 64)
    {
        _id = layout?.Id ?? Guid.NewGuid();
        _pinned = layout?.Pinned ?? false;
        _windowX = layout?.WindowX;
        _windowY = layout?.WindowY;
        _items = layout?.Items.Select(i => new OverlayItem
        {
            Path = i.Path, X = i.X, Y = i.Y, Width = i.Width, Height = i.Height,
            SizeIsAutomatic = i.SizeIsAutomatic, ForceKeepAspectRatio = i.ForceKeepAspectRatio
        }).ToList() ?? new();
        Text = layout == null ? "Create layout" : "Edit layout";
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(770, 430);
        Size = new Size(1020, 580);

        var root = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), RowCount = 5, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        var nameRow = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        nameRow.Controls.Add(new Label { Text = "Layout name:", AutoSize = true, Padding = new Padding(0, 7, 4, 0) });
        _name.Width = 300;
        _name.Text = layout?.Name ?? "New layout";
        nameRow.Controls.Add(_name);
        root.Controls.Add(nameRow, 0, 0);

        _topmost.Text = "Always on top";
        _topmost.AutoSize = true;
        _topmost.Checked = layout?.AlwaysOnTop ?? defaultTopmost;
        _locked.Text = "Lock overlay (no movement or editing)";
        _locked.AutoSize = true;
        _locked.Checked = layout?.Locked ?? false;
        _forceAspect.Text = "Force-keep aspect ratio when resizing";
        _forceAspect.AutoSize = true;
        _forceAspect.Checked = layout?.ForceKeepAspectRatio ?? false;
        var settings = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        settings.Controls.AddRange(new Control[] { _topmost, _forceAspect, _locked });
        root.Controls.Add(settings, 0, 1);

        _snap.Text = "Snap files to each other";
        _snap.AutoSize = true;
        _snap.Checked = layout?.SnapToItems ?? defaultSnap;
        _snapGrid.Text = "Snap to screen grid (half-cell steps)";
        _snapGrid.AutoSize = true;
        _snapGrid.Checked = layout?.SnapToGrid ?? defaultGrid;
        _gridSize.Value = Math.Clamp(layout?.GridSize ?? defaultGridSize, 8, 512);
        _gridSize.Increment = 2;
        _gridSize.Width = 72;
        _gridSize.Enabled = _snapGrid.Checked;
        _snapGrid.CheckedChanged += (_, _) => _gridSize.Enabled = _snapGrid.Checked;
        var snapping = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true };
        snapping.Controls.AddRange(new Control[]
        {
            _snap, _snapGrid,
            new Label { Text = "Grid cell (px):", AutoSize = true, Padding = new Padding(4, 6, 0, 0) },
            _gridSize
        });
        root.Controls.Add(snapping, 0, 2);

        var middle = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        middle.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 248));
        root.Controls.Add(middle, 0, 3);

        _files.Dock = DockStyle.Fill;
        _files.View = View.Details;
        _files.FullRowSelect = true;
        _files.MultiSelect = true;
        _files.HideSelection = false;
        _files.Columns.Add("File", 260);
        _files.Columns.Add("Position", 94);
        _files.Columns.Add("Size (display)", 115);
        _files.Columns.Add("Original pixels", 110);
        _files.SelectedIndexChanged += (_, _) => ShowSelectedDetails();
        _files.DoubleClick += (_, _) => { if (_activeItem != null) _width.Focus(); };
        middle.Controls.Add(_files, 0, 0);

        var detailsLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, RowCount = 10
        };
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 76));
        detailsLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        detailsLayout.Controls.Add(new Label { Text = "File settings", AutoSize = true, Font = new Font(Font, FontStyle.Bold) }, 0, 0);
        detailsLayout.SetColumnSpan(detailsLayout.GetControlFromPosition(0, 0)!, 2);
        AddField(detailsLayout, 1, "X", _x);
        AddField(detailsLayout, 2, "Y", _y);
        AddField(detailsLayout, 3, "Width", _width);
        AddField(detailsLayout, 4, "Height", _height);
        detailsLayout.Controls.Add(_aspectLock, 0, 5);
        detailsLayout.SetColumnSpan(_aspectLock, 2);
        detailsLayout.Controls.Add(_itemForceAspect, 0, 6);
        detailsLayout.SetColumnSpan(_itemForceAspect, 2);
        detailsLayout.Controls.Add(new Label { Text = "Source size:", AutoSize = true }, 0, 7);
        detailsLayout.SetColumnSpan(detailsLayout.GetControlFromPosition(0, 7)!, 2);
        detailsLayout.Controls.Add(_original, 0, 8);
        detailsLayout.SetColumnSpan(_original, 2);
        detailsLayout.Controls.Add(_resetSize, 0, 9);
        detailsLayout.SetColumnSpan(_resetSize, 2);
        _details.Controls.Add(detailsLayout);
        middle.Controls.Add(_details, 1, 0);

        _x.ValueChanged += (_, _) => EditPosition();
        _y.ValueChanged += (_, _) => EditPosition();
        _width.ValueChanged += (_, _) => EditDimensions(widthChanged: true);
        _height.ValueChanged += (_, _) => EditDimensions(widthChanged: false);
        _resetSize.Click += (_, _) => RestoreNaturalSize();
        _itemForceAspect.CheckedChanged += (_, _) =>
        {
            if (!_loadingDetails && _activeItem != null)
                _activeItem.ForceKeepAspectRatio = _itemForceAspect.Checked;
        };

        var buttons = new FlowLayoutPanel { AutoSize = true, Dock = DockStyle.Fill };
        buttons.Controls.Add(MakeButton("Add files", (_, _) => AddFiles()));
        buttons.Controls.Add(MakeButton("Remove selected", (_, _) => RemoveSelected()));
        buttons.Controls.Add(MakeButton("Move up", (_, _) => MoveSelectedFile(-1)));
        buttons.Controls.Add(MakeButton("Move down", (_, _) => MoveSelectedFile(1)));
        buttons.Controls.Add(MakeButton("Save", (_, _) => SaveLayout()));
        buttons.Controls.Add(MakeButton("Cancel", (_, _) => { DialogResult = DialogResult.Cancel; Close(); }));
        root.Controls.Add(buttons, 0, 4);
        RefreshFiles();
    }

    private static void AddField(TableLayoutPanel table, int row, string label, Control control)
    {
        table.Controls.Add(new Label { Text = label + ":", AutoSize = true, Padding = new Padding(0, 6, 0, 0) }, 0, row);
        table.Controls.Add(control, 1, row);
    }

    private static Button MakeButton(string text, EventHandler handler)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(3) };
        button.Click += handler;
        return button;
    }

    private void AddFiles()
    {
        using var dialog = new OpenFileDialog { Multiselect = true, Title = "Choose files to display", CheckFileExists = true };
        if (dialog.ShowDialog(this) != DialogResult.OK) return;
        OverlayItem? last = null;
        foreach (var path in dialog.FileNames)
        {
            if (_items.Any(i => string.Equals(i.Path, path, StringComparison.OrdinalIgnoreCase))) continue;
            var original = ImageDimensions.TryRead(path);
            int index = _items.Count;
            last = new OverlayItem
            {
                Path = path,
                X = (index % 3) * 360,
                Y = (index / 3) * 250,
                Width = original?.Width ?? 360,
                Height = original?.Height ?? 250,
                SizeIsAutomatic = false
            };
            _items.Add(last);
        }
        RefreshFiles(last);
    }

    private void RemoveSelected()
    {
        foreach (ListViewItem row in _files.SelectedItems)
            if (row.Tag is OverlayItem item) _items.Remove(item);
        RefreshFiles();
    }

    private void MoveSelectedFile(int delta)
    {
        if (_files.SelectedItems.Count != 1 || _files.SelectedItems[0].Tag is not OverlayItem item) return;
        int from = _items.IndexOf(item), to = from + delta;
        if (to < 0 || to >= _items.Count) return;
        _items.RemoveAt(from);
        _items.Insert(to, item);
        RefreshFiles(item);
    }

    private void RefreshFiles(OverlayItem? select = null)
    {
        _files.BeginUpdate();
        _files.Items.Clear();
        foreach (var item in _items)
        {
            var original = ImageDimensions.TryRead(item.Path);
            var row = new ListViewItem(Path.GetFileName(item.Path)) { Tag = item, ToolTipText = item.Path };
            row.SubItems.Add($"{item.X}, {item.Y}");
            row.SubItems.Add($"{item.Width} x {item.Height}");
            row.SubItems.Add(original == null ? "Unknown" : $"{original.Value.Width} x {original.Value.Height}");
            _files.Items.Add(row);
            if (ReferenceEquals(item, select)) row.Selected = true;
        }
        _files.EndUpdate();
        ShowSelectedDetails();
    }

    private void UpdateActiveRow()
    {
        if (_files.SelectedItems.Count != 1 || _activeItem == null) return;
        var row = _files.SelectedItems[0];
        if (!ReferenceEquals(row.Tag, _activeItem)) return;
        row.SubItems[1].Text = $"{_activeItem.X}, {_activeItem.Y}";
        row.SubItems[2].Text = $"{_activeItem.Width} x {_activeItem.Height}";
    }

    private void ShowSelectedDetails()
    {
        _loadingDetails = true;
        try
        {
            _activeItem = _files.SelectedItems.Count == 1 ? _files.SelectedItems[0].Tag as OverlayItem : null;
            bool active = _activeItem != null;
            _x.Enabled = _y.Enabled = _width.Enabled = _height.Enabled = active;
            bool image = active && ImageDimensions.IsImage(_activeItem!.Path);
            _aspectLock.Enabled = image;
            _itemForceAspect.Enabled = active;
            _resetSize.Enabled = image;
            if (!active)
            {
                _original.Text = "Select a file to edit its displayed dimensions.";
                return;
            }
            var item = _activeItem!;
            _x.Value = Clamp(_x, item.X);
            _y.Value = Clamp(_y, item.Y);
            _width.Value = Clamp(_width, item.Width);
            _height.Value = Clamp(_height, item.Height);
            _itemForceAspect.Checked = item.ForceKeepAspectRatio;
            Size? original = ImageDimensions.TryRead(item.Path);
            _original.Text = !image ? "Not an image" : original.HasValue
                ? $"{original.Value.Width} x {original.Value.Height} pixels"
                : "Source dimensions unavailable";
        }
        finally { _loadingDetails = false; }
    }

    private static decimal Clamp(NumericUpDown control, int value) =>
        Math.Clamp((decimal)value, control.Minimum, control.Maximum);

    private void EditPosition()
    {
        if (_loadingDetails || _activeItem == null) return;
        _activeItem.X = (int)_x.Value;
        _activeItem.Y = (int)_y.Value;
        UpdateActiveRow();
    }

    private void EditDimensions(bool widthChanged)
    {
        if (_loadingDetails || _activeItem == null) return;
        var item = _activeItem;
        int width = (int)_width.Value;
        int height = (int)_height.Value;
        var original = ImageDimensions.TryRead(item.Path);
        if (_aspectLock.Checked && ImageDimensions.IsImage(item.Path))
        {
            
            double ratio = original.HasValue
                ? (double)original.Value.Width / original.Value.Height
                : (double)Math.Max(1, item.Width) / Math.Max(1, item.Height);
            _loadingDetails = true;
            try
            {
                if (widthChanged)
                {
                    height = (int)Math.Clamp(Math.Round(width / ratio), (double)_height.Minimum, (double)_height.Maximum);
                    _height.Value = height;
                }
                else
                {
                    width = (int)Math.Clamp(Math.Round(height * ratio), (double)_width.Minimum, (double)_width.Maximum);
                    _width.Value = width;
                }
            }
            finally { _loadingDetails = false; }
        }
        item.Width = width;
        item.Height = height;
        item.SizeIsAutomatic = false;
        UpdateActiveRow();
    }

    private void RestoreNaturalSize()
    {
        if (_activeItem == null || !ImageDimensions.IsImage(_activeItem.Path)) return;
        var original = ImageDimensions.TryRead(_activeItem.Path);
        if (original != null)
        {
            _activeItem.Width = original.Value.Width;
            _activeItem.Height = original.Value.Height;
            _activeItem.SizeIsAutomatic = false;
        }
        else
        {
            MessageBox.Show(this, "The image decoder cannot determine the original size for this format.",
                "Original size unavailable", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        ShowSelectedDetails();
        UpdateActiveRow();
    }

    private void SaveLayout()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(this, "Enter a layout name.", "Missing name");
            return;
        }
        Result = new LayoutDefinition
        {
            Id = _id, Name = _name.Text.Trim(), Pinned = _pinned,
            AlwaysOnTop = _topmost.Checked, SnapToItems = _snap.Checked,
            SnapToGrid = _snapGrid.Checked, GridSize = (int)_gridSize.Value / 2 * 2,
            ForceKeepAspectRatio = _forceAspect.Checked, Locked = _locked.Checked,
            WindowX = _windowX, WindowY = _windowY, Items = _items
        };
        DialogResult = DialogResult.OK;
        Close();
    }
}
