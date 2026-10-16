using System.Drawing;

namespace OverlayDesk;

public static class OverlayHostGeometry
{
    public static Rectangle ClipAndPad(Rectangle contentsOnScreen, Rectangle virtualDesktop, int padding)
    {
        if (contentsOnScreen.Width <= 0 || contentsOnScreen.Height <= 0)
            throw new ArgumentException("The content bounds must be nonempty.", nameof(contentsOnScreen));
        if (virtualDesktop.Width <= 0 || virtualDesktop.Height <= 0)
            throw new ArgumentException("The virtual desktop must be nonempty.", nameof(virtualDesktop));

        var padded = contentsOnScreen;
        padded.Inflate(Math.Max(0, padding), Math.Max(0, padding));
        Rectangle clipped = Rectangle.Intersect(padded, virtualDesktop);
        if (!clipped.IsEmpty) return clipped;

                        return new Rectangle(virtualDesktop.Left, virtualDesktop.Top,
            Math.Min(320, virtualDesktop.Width), Math.Min(240, virtualDesktop.Height));
    }

    public static Point ClientTranslation(Rectangle oldScreenHost, Rectangle newScreenHost) =>
        new(oldScreenHost.Left - newScreenHost.Left,
            oldScreenHost.Top - newScreenHost.Top);

    public static Rectangle RebasedClientBounds(Rectangle childClientBounds,
        Rectangle oldScreenHost, Rectangle newScreenHost)
    {
        childClientBounds.Offset(ClientTranslation(oldScreenHost, newScreenHost));
        return childClientBounds;
    }
}
