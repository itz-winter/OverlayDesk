using System.Drawing.Imaging;

namespace OverlayDesk;





internal sealed class NativeImageView : Control
{
    private readonly MemoryStream _source;
    private readonly Image _image;
    private readonly bool _animated;

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

    private void OnNextFrame(object? sender, EventArgs args)
    {
        if (!IsHandleCreated || IsDisposed) return;
        try { BeginInvoke((Action)(() => { if (!IsDisposed) Invalidate(); })); }
        catch (InvalidOperationException) { } 
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (_animated) ImageAnimator.UpdateFrames(_image);
        e.Graphics.CompositingMode = System.Drawing.Drawing2D.CompositingMode.SourceOver;
        e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
        e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
        e.Graphics.DrawImage(_image, ClientRectangle, 0, 0, _image.Width, _image.Height, GraphicsUnit.Pixel);
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
