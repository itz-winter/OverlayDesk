using System.Diagnostics;

namespace OverlayDesk;







public sealed class OverlayForm : Form
{
    private sealed class FileCell
    {
        public required OverlayItem Item { get; init; }
        public required Panel Surface { get; init; }
        public required Button MoveGrip { get; init; }
        public required Button SizeGrip { get; init; }
        public double AspectRatio { get; init; }
    }

    private enum DragMode { None, MoveOne, ResizeOne, MoveAll }

    private readonly LayoutDefinition _layout;
    private readonly Action _save;
    private readonly List<FileCell> _cells = new();
    private readonly System.Windows.Forms.Timer _saveTimer = new() { Interval = 400 };
    private readonly Rectangle _desktop;
    private Point _anchor;
    private Point _cursorStart;
    private Rectangle _startBounds;
    private Rectangle[] _allStartBounds = Array.Empty<Rectangle>();
    private DragMode _dragMode;
    private FileCell? _active;
    private bool _arranging;
    private bool _skipCloseQuestion;
    private readonly Button _moveAllGrip = new();
    private readonly List<GridGuideForm> _gridGuides = new();
    private readonly System.Windows.Forms.Timer _paintTimer = new() { Interval = 16 };
    private bool _regionDirty;

    public Guid LayoutId => _layout.Id;
    public bool IsArranging => _arranging;
    public bool IsLocked => _layout.Locked;

    public OverlayForm(LayoutDefinition layout, Action save)
    {
        _layout = layout;
        _layout.GridSize = Math.Clamp(_layout.GridSize / 2 * 2, 8, 512);
        _save = save;
        _desktop = SystemInformation.VirtualScreen;
        _anchor = GetInitialAnchor(layout);

        Text = "OverlayDesk - " + layout.Name;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        ShowIcon = false;
        ControlBox = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = layout.AlwaysOnTop;
        BackColor = Color.Black;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        KeyPreview = true;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = _desktop;
        
        

        foreach (OverlayItem item in layout.Items)
        {
            if (!File.Exists(item.Path)) continue;
            try
            {
                
                
                if (item.SizeIsAutomatic && new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                    { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico" }
                    .Contains(Path.GetExtension(item.Path)) && ImageDimensions.TryRead(item.Path) is Size dimensions)
                {
                    item.Width = dimensions.Width;
                    item.Height = dimensions.Height;
                    item.SizeIsAutomatic = false;
                }
                AddCell(item);
            }
            catch (Exception ex) { AddCell(item, ex.Message); }
        }

        _moveAllGrip.Text = "ALL";
        _moveAllGrip.FlatStyle = FlatStyle.Flat;
        _moveAllGrip.FlatAppearance.BorderSize = 0;
        _moveAllGrip.ForeColor = Color.White;
        _moveAllGrip.BackColor = Color.FromArgb(45, 65, 90);
        _moveAllGrip.Size = new Size(46, 23);
        _moveAllGrip.Visible = false;
        _moveAllGrip.Cursor = Cursors.SizeAll;
        Controls.Add(_moveAllGrip);
        AttachDragEvents(_moveAllGrip, null, DragMode.MoveAll);

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); Persist(); };
        _paintTimer.Tick += (_, _) =>
        {
            if (!_regionDirty) { _paintTimer.Stop(); return; }
            _regionDirty = false;
            RefreshRegion();
        };
        KeyDown += OnOverlayKeyDown;
        FormClosing += (_, e) =>
        {
            if (!_skipCloseQuestion && e.CloseReason == CloseReason.UserClosing &&
                MessageBox.Show(this, "Close this overlay?", "Confirm close",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            { e.Cancel = true; return; }
            _saveTimer.Stop();
            Persist();
        };
        FormClosed += (_, _) =>
        {
            DisposeGrid();
            _paintTimer.Dispose();
            _saveTimer.Dispose();
        };
        RefreshRegion();
    }

    private static Point GetInitialAnchor(LayoutDefinition layout)
    {
        if (layout.WindowX.HasValue && layout.WindowY.HasValue)
            return new Point(layout.WindowX.Value, layout.WindowY.Value);

        Rectangle bounds = GetLayoutBounds(layout.Items);
        Rectangle work = Screen.FromPoint(Cursor.Position).WorkingArea;
        return new Point(work.Left + (work.Width - bounds.Width) / 2 - bounds.Left,
                         work.Top + (work.Height - bounds.Height) / 2 - bounds.Top);
    }

    private static Rectangle GetLayoutBounds(IReadOnlyList<OverlayItem> items)
    {
        if (items.Count == 0) return new Rectangle(0, 0, 320, 240);
        return Rectangle.FromLTRB(items.Min(x => x.X), items.Min(x => x.Y),
            items.Max(x => x.X + Math.Max(1, x.Width)),
            items.Max(x => x.Y + Math.Max(1, x.Height)));
    }

    private void AddCell(OverlayItem item, string? error = null)
    {
        var surface = new Panel
        {
            BackColor = Color.Black, BorderStyle = BorderStyle.None, Margin = Padding.Empty,
            Padding = Padding.Empty, Bounds = ToDesktopRectangle(item),
        };
        Size? original = ImageDimensions.TryRead(item.Path);
        double aspectRatio = original.HasValue
            ? (double)original.Value.Width / Math.Max(1, original.Value.Height)
            : (double)Math.Max(1, item.Width) / Math.Max(1, item.Height);
        var cell = new FileCell
        {
            Item = item, Surface = surface, AspectRatio = aspectRatio,
            MoveGrip = MakeGrip("Move", Cursors.SizeAll),
            SizeGrip = MakeGrip("Size", Cursors.SizeNWSE),
        };
        Control content = error != null ? MakeMessage(Path.GetFileName(item.Path), error) : MakeContent(item.Path);
        content.Dock = DockStyle.Fill;
        surface.Controls.Add(content);
        Controls.Add(surface);
        _cells.Add(cell);

        
        
        if (content is NativeImageView)
            AttachDragEvents(content, cell, DragMode.MoveOne);
        else if (content is not MediaView)
            AttachDragEvents(content, cell, DragMode.MoveOne, requireAlt: true);
        AttachDragEvents(surface, cell, DragMode.MoveOne, requireAlt: true);

        Controls.Add(cell.MoveGrip);
        Controls.Add(cell.SizeGrip);
        AttachDragEvents(cell.MoveGrip, cell, DragMode.MoveOne);
        AttachDragEvents(cell.SizeGrip, cell, DragMode.ResizeOne);
        PositionGrips(cell);
    }

    private Rectangle ToDesktopRectangle(OverlayItem item) => new(
        _anchor.X + item.X - _desktop.Left, _anchor.Y + item.Y - _desktop.Top,
        Math.Max(1, item.Width), Math.Max(1, item.Height));

    private static Button MakeGrip(string title, Cursor cursor)
    {
        var button = new Button
        {
            Text = title, Size = new Size(48, 23), Visible = false,
            BackColor = Color.FromArgb(50, 58, 68), ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat, Cursor = cursor, TabStop = false
        };
        button.FlatAppearance.BorderSize = 0;
        return button;
    }

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".m4v", ".mov", ".avi", ".mkv", ".webm", ".wmv", ".mpeg", ".mpg", ".ts" };
    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".ogg", ".opus", ".wav", ".flac", ".m4a", ".aac" };
    private static readonly HashSet<string> GdiImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".ico" };
    private static readonly HashSet<string> TextExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".log", ".csv", ".ini", ".cs", ".js", ".html", ".css", ".py" };

    private static Control MakeContent(string path)
    {
        string ext = Path.GetExtension(path);
        if (GdiImageExtensions.Contains(ext)) return new NativeImageView(path);
        if (VideoExtensions.Contains(ext)) return new MediaView(path, video: true);
        if (AudioExtensions.Contains(ext)) return new MediaView(path, video: false);
        if (TextExtensions.Contains(ext))
            return new RichTextBox
            {
                ReadOnly = true, BorderStyle = BorderStyle.None,
                ScrollBars = RichTextBoxScrollBars.None,
                BackColor = Color.FromArgb(25, 25, 25), ForeColor = Color.White,
                Font = new Font("Consolas", 10), Text = File.ReadAllText(path), WordWrap = true
            };
        return MakeMessage(Path.GetFileName(path), "No built-in preview. Double-click to open in another application.", path);
    }

    private static Control MakeMessage(string name, string description, string? path = null)
    {
        var label = new Label
        {
            BackColor = Color.FromArgb(35, 35, 35), ForeColor = Color.White,
            TextAlign = ContentAlignment.MiddleCenter, Text = name + "\n" + description
        };
        if (path != null) label.DoubleClick += (_, _) =>
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Unable to open file"); }
        };
        return label;
    }

    private void AttachDragEvents(Control control, FileCell? cell, DragMode mode, bool requireAlt = false)
    {
        control.MouseDown += (_, e) =>
        {
            if (e.Button == MouseButtons.Left && cell != null &&
                (ModifierKeys & (Keys.Shift | Keys.Alt)) == (Keys.Shift | Keys.Alt))
                StartDrag(cell, DragMode.ResizeOne);
            else if (e.Button == MouseButtons.Left && mode != DragMode.ResizeOne &&
                (ModifierKeys & Keys.Shift) != 0)
                StartDrag(cell, DragMode.MoveAll);
            else if (e.Button == MouseButtons.Left && (!requireAlt || (ModifierKeys & Keys.Alt) != 0))
                StartDrag(cell, mode);
            else if (e.Button == MouseButtons.Right && cell != null && (ModifierKeys & Keys.Alt) != 0)
                StartDrag(cell, DragMode.ResizeOne);
            else return;
            if (_dragMode != DragMode.None) control.Capture = true;
        };
        control.MouseMove += (_, _) => { if (_dragMode != DragMode.None) ContinueDrag(); };
        control.MouseUp += (_, _) =>
        {
            if (_dragMode == DragMode.None) return;
            FinishDrag();
            control.Capture = false;
        };
        control.MouseCaptureChanged += (_, _) =>
        {
            
            if (_dragMode != DragMode.None && Control.MouseButtons == MouseButtons.None) FinishDrag();
        };
    }

    private void StartDrag(FileCell? cell, DragMode mode)
    {
        if (_dragMode != DragMode.None || _layout.Locked) return;
        _dragMode = mode;
        _active = cell;
        _cursorStart = Cursor.Position;
        _startBounds = cell?.Surface.Bounds ?? Rectangle.Empty;
        _allStartBounds = _cells.Select(c => c.Surface.Bounds).ToArray();
    }

    private void ContinueDrag()
    {
        if (_dragMode == DragMode.None || _layout.Locked) return;
        Point cursor = Cursor.Position;
        int dx = cursor.X - _cursorStart.X;
        int dy = cursor.Y - _cursorStart.Y;
        Point origin = new(_desktop.Left, _desktop.Top);
        switch (_dragMode)
        {
            case DragMode.MoveOne when _active != null:
            {
                var proposed = new Rectangle(_startBounds.X + dx, _startBounds.Y + dy,
                    _startBounds.Width, _startBounds.Height);
                var targets = _cells.Where(c => !ReferenceEquals(c, _active))
                    .Select(c => c.Surface.Bounds);
                proposed = SnapEngine.SnapMove(proposed, targets, _layout.SnapToItems,
                    _layout.SnapToGrid, _layout.GridSize, origin);
                ApplyBounds(_active, proposed);
                break;
            }
            case DragMode.ResizeOne when _active != null:
            {
                bool keepAspect = _layout.ForceKeepAspectRatio || _active.Item.ForceKeepAspectRatio ||
                    ((ModifierKeys & (Keys.Shift | Keys.Alt)) == (Keys.Shift | Keys.Alt));
                bool widthPrimary = Math.Abs(dx) >= Math.Abs(dy);
                int width = Math.Max(24, _startBounds.Width + dx);
                int height = Math.Max(24, _startBounds.Height + dy);
                var targets = _cells.Where(c => !ReferenceEquals(c, _active))
                    .Select(c => c.Surface.Bounds).ToArray();
                if (keepAspect)
                {
                    Size proportional = SnapEngine.KeepRatio(width, height, _active.AspectRatio, widthPrimary);
                    var proposed = new Rectangle(_startBounds.Location, proportional);
                    Rectangle snapped = SnapEngine.SnapResize(proposed, targets,
                        _layout.SnapToItems, _layout.SnapToGrid, _layout.GridSize, origin);
                    
                    
                    int primaryWidth = widthPrimary ? snapped.Width : proportional.Width;
                    int primaryHeight = widthPrimary ? proportional.Height : snapped.Height;
                    proportional = SnapEngine.KeepRatio(primaryWidth, primaryHeight,
                        _active.AspectRatio, widthPrimary);
                    ApplyBounds(_active, new Rectangle(_startBounds.Location, proportional));
                }
                else
                {
                    var proposed = new Rectangle(_startBounds.X, _startBounds.Y, width, height);
                    proposed = SnapEngine.SnapResize(proposed, targets,
                        _layout.SnapToItems, _layout.SnapToGrid, _layout.GridSize, origin);
                    ApplyBounds(_active, proposed);
                }
                break;
            }
            case DragMode.MoveAll:
            {
                int moveDx = dx, moveDy = dy;
                if (_layout.SnapToGrid && _allStartBounds.Length > 0)
                {
                    int left = _allStartBounds.Min(r => r.Left);
                    int top = _allStartBounds.Min(r => r.Top);
                    moveDx += SnapEngine.GridDelta(left + dx + _desktop.Left, _layout.GridSize);
                    moveDy += SnapEngine.GridDelta(top + dy + _desktop.Top, _layout.GridSize);
                }
                
                var planned = new Rectangle[_cells.Count];
                for (int i = 0; i < _cells.Count; i++)
                {
                    planned[i] = _allStartBounds[i];
                    planned[i].Offset(moveDx, moveDy);
                }
                
                
                RefreshRegion(planned);
                SuspendLayout();
                for (int i = 0; i < _cells.Count; i++)
                {
                    if (_cells[i].Surface.Bounds != planned[i]) _cells[i].Surface.Bounds = planned[i];
                    PositionGrips(_cells[i]);
                }
                ResumeLayout(false);
                PositionMoveAllGrip();
                QueueRegionRefresh();
                break;
            }
        }
    }

    private void ApplyBounds(FileCell cell, Rectangle proposed)
    {
        if (cell.Surface.Bounds == proposed) return;
        RefreshRegion(new[] { proposed });
        cell.Surface.Bounds = proposed;
        PositionGrips(cell);
        QueueRegionRefresh();
    }

    private void QueueRegionRefresh()
    {
        
        
        
        _regionDirty = true;
        if (!_paintTimer.Enabled) _paintTimer.Start();
    }

    private void FinishDrag()
    {
        if (_dragMode == DragMode.None) return;
        _dragMode = DragMode.None;
        _active = null;
        _paintTimer.Stop();
        _regionDirty = false;
        RefreshRegion();
        SaveGeometry();
    }

    private void SaveGeometry()
    {
        
        
        foreach (var cell in _cells)
        {
            cell.Item.X = cell.Surface.Left + _desktop.Left - _anchor.X;
            cell.Item.Y = cell.Surface.Top + _desktop.Top - _anchor.Y;
            cell.Item.Width = cell.Surface.Width;
            cell.Item.Height = cell.Surface.Height;
            cell.Item.SizeIsAutomatic = false;
        }
        _layout.WindowX = _anchor.X;
        _layout.WindowY = _anchor.Y;
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void Persist()
    {
        try { _save(); }
        catch (Exception ex) { Debug.WriteLine("OverlayDesk save failed: " + ex); }
    }

    private void PositionGrips(FileCell cell)
    {
        Rectangle r = cell.Surface.Bounds;
        int toolbarY = r.Y - 25 >= 0 ? r.Y - 25 : r.Y + 2;
        cell.MoveGrip.Location = new Point(r.X, toolbarY);
        cell.SizeGrip.Location = new Point(r.X + 50, toolbarY);
        cell.MoveGrip.BringToFront();
        cell.SizeGrip.BringToFront();
    }

    private void PositionMoveAllGrip()
    {
        if (_cells.Count == 0) return;
        int y = _cells.Min(c => c.Surface.Top);
        _moveAllGrip.Location = new Point(_cells.Min(c => c.Surface.Left) + 103,
            y - 25 >= 0 ? y - 25 : y + 2);
        _moveAllGrip.BringToFront();
    }

    protected override void OnPaintBackground(PaintEventArgs e)
    {
        
        
    }

    private void RefreshRegion(IEnumerable<Rectangle>? upcoming = null)
    {
        
        
        using var region = new Region();
        region.MakeEmpty();
        foreach (var cell in _cells)
        {
            region.Union(cell.Surface.Bounds);
            if (_arranging)
            {
                region.Union(cell.MoveGrip.Bounds);
                region.Union(cell.SizeGrip.Bounds);
            }
        }
        if (_arranging && _cells.Count > 0) region.Union(_moveAllGrip.Bounds);
        if (upcoming != null)
            foreach (Rectangle r in upcoming) region.Union(r);
        Region? previous = Region;
        Region = region.Clone();
        previous?.Dispose();
    }

    public void ToggleArrange()
    {
        if (_layout.Locked) return;
        if (_dragMode != DragMode.None) FinishDrag();
        _arranging = !_arranging;
        foreach (var cell in _cells)
        {
            cell.MoveGrip.Visible = _arranging;
            cell.SizeGrip.Visible = _arranging;
            PositionGrips(cell);
        }
        _moveAllGrip.Visible = _arranging && _cells.Count > 0;
        PositionMoveAllGrip();
        RefreshRegion();
        RefreshGrid();
        if (_arranging) Activate();
    }

    public void SetLocked(bool locked)
    {
        if (_dragMode != DragMode.None) FinishDrag();
        _layout.Locked = locked;
        if (locked && _arranging)
        {
            _arranging = false;
            foreach (var cell in _cells)
            {
                cell.MoveGrip.Visible = false;
                cell.SizeGrip.Visible = false;
            }
            _moveAllGrip.Visible = false;
        }
        DisposeGrid();
        RefreshRegion();
        Persist();
    }

    private void RefreshGrid()
    {
        DisposeGrid();
        if (!_arranging || _layout.Locked || !_layout.SnapToGrid) return;
        foreach (Screen screen in Screen.AllScreens)
        {
            var guide = new GridGuideForm(screen.Bounds, _layout.GridSize);
            _gridGuides.Add(guide);
            guide.Show();
        }
    }

    private void DisposeGrid()
    {
        foreach (var guide in _gridGuides) { if (!guide.IsDisposed) guide.Dispose(); }
        _gridGuides.Clear();
    }

    public void SetSnap(bool enabled)
    {
        if (_layout.Locked) return;
        _layout.SnapToItems = enabled;
        SaveGeometry();
    }

    private void OnOverlayKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Escape) { Close(); e.Handled = true; }
        if (e.Control && e.KeyCode == Keys.E)
        {
            if (!_layout.Locked) ToggleArrange(); e.SuppressKeyPress = true;
        }
        if (e.Control && e.KeyCode == Keys.Space && !_layout.Locked)
        {
            TopMost = !TopMost;
            _layout.AlwaysOnTop = TopMost;
            SaveGeometry(); e.SuppressKeyPress = true;
        }
        if (e.Control && e.Shift && e.KeyCode == Keys.S && !_layout.Locked)
        {
            SetSnap(!_layout.SnapToItems); e.SuppressKeyPress = true;
        }
        if (e.Control && e.Shift && e.KeyCode == Keys.G && !_layout.Locked)
        {
            _layout.SnapToGrid = !_layout.SnapToGrid;
            RefreshGrid();
            Persist();
            e.SuppressKeyPress = true;
        }
    }

    public void CloseWithoutPrompt()
    {
        _skipCloseQuestion = true;
        Close();
    }
}
