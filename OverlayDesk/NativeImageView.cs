using System.Diagnostics;
using System.Threading;

namespace OverlayDesk;

internal sealed class NativeImageView : Control
{
    private readonly MemoryStream _source;
    private readonly Image _image;
    private readonly bool _animated;
    private int _invalidateQueued;
    private long _lastFrameRequest;
    private static readonly long MinimumFrameTicks = Math.Max(1, Stopwatch.Frequency / 30);

    public NativeImageView(string file)
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                 ControlStyles.OptimizedDoubleBuffer, true);
        _source = new MemoryStream(File.ReadAllBytes(file), writable: false);
        try { _image = Image.FromStream(_source); }
        catch { _source.Dispose(); throw; }
        _animated = ImageAnimator.CanAnimate(_image);
        if (_animated) ImageAnimator.Animate(_image, OnNextFrame);
    }

    protected override void OnSizeChanged(EventArgs e)
    {
                                bool useBuffer = (long)Width * Height <= 1920L * 1080L;
        SetStyle(ControlStyles.OptimizedDoubleBuffer, useBuffer);
        base.OnSizeChanged(e);
    }

    private void OnNextFrame(object? sender, EventArgs args)
    {
        if (!IsHandleCreated || IsDisposed || !Visible) return;
                                long now = Stopwatch.GetTimestamp();
        if (now - Interlocked.Read(ref _lastFrameRequest) < MinimumFrameTicks) return;
        if (Interlocked.Exchange(ref _invalidateQueued, 1) != 0) return;
        Interlocked.Exchange(ref _lastFrameRequest, now);
        try
        {
            BeginInvoke((Action)(() =>
            {
                Interlocked.Exchange(ref _invalidateQueued, 0);
                if (!IsDisposed && Visible) Invalidate();
            }));
        }
        catch (InvalidOperationException)
        {
            Interlocked.Exchange(ref _invalidateQueued, 0);
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_animated) ImageAnimator.UpdateFrames(_image);
        e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        e.Graphics.InterpolationMode = _animated
            ? System.Drawing.Drawing2D.InterpolationMode.Bilinear
            : System.Drawing.Drawing2D.InterpolationMode.HighQualityBilinear;
                        if (ClientSize.Width == _image.Width && ClientSize.Height == _image.Height)
            e.Graphics.DrawImageUnscaled(_image, Point.Empty);
        else
            e.Graphics.DrawImage(_image, ClientRectangle,
                0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (_animated) ImageAnimator.StopAnimate(_image, OnNextFrame);
            _image.Dispose();
            _source.Dispose();
        }
        base.Dispose(disposing);
    }
}
