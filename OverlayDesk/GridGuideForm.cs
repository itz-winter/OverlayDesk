using System.Drawing.Drawing2D;

namespace OverlayDesk;





internal sealed class GridGuideForm : Form
{
    private const int WsExTransparent = 0x00000020;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExNoActivate = 0x08000000;
    private const int WmNcHitTest = 0x0084;
    private const int HtTransparent = -1;
    private readonly int _size;
    private readonly Rectangle _screenBounds;
    private readonly Color _transparent = Color.FromArgb(255, 0, 255);

    protected override CreateParams CreateParams
    {
        get
        {
            CreateParams cp = base.CreateParams;
            cp.ExStyle |= WsExTransparent | WsExToolWindow | WsExNoActivate;
            return cp;
        }
    }

    protected override bool ShowWithoutActivation => true;

    public GridGuideForm(Rectangle monitorBounds, int gridSize)
    {
        _screenBounds = monitorBounds;
        _size = Math.Max(8, gridSize / 2 * 2);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.Manual;
        AutoScaleMode = AutoScaleMode.None;
        Bounds = monitorBounds;
        ShowInTaskbar = false;
        TopMost = true;
        BackColor = _transparent;
        TransparencyKey = _transparent;
        DoubleBuffered = true;
        SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
    }

    protected override void WndProc(ref Message m)
    {
        if (m.Msg == WmNcHitTest)
        {
            m.Result = (IntPtr)HtTransparent;
            return;
        }
        base.WndProc(ref m);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.Clear(_transparent);
        e.Graphics.SmoothingMode = SmoothingMode.None;
        using var major = new Pen(Color.FromArgb(75, 130, 160), 1f);
        using var minor = new Pen(Color.FromArgb(47, 91, 112), 1f) { DashStyle = DashStyle.Dot };
        int step = Math.Max(1, _size / 2);
        int minX = (int)Math.Floor((double)_screenBounds.Left / step) * step;
        int minY = (int)Math.Floor((double)_screenBounds.Top / step) * step;
        for (int globalX = minX; globalX < _screenBounds.Right; globalX += step)
        {
            int local = globalX - _screenBounds.Left;
            e.Graphics.DrawLine(globalX % _size == 0 ? major : minor, local, 0, local, Height);
        }
        for (int globalY = minY; globalY < _screenBounds.Bottom; globalY += step)
        {
            int local = globalY - _screenBounds.Top;
            e.Graphics.DrawLine(globalY % _size == 0 ? major : minor, 0, local, Width, local);
        }
    }
}
