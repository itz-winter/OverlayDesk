namespace OverlayDesk;






public static class SnapEngine
{
    public const int Threshold = 12;

    public static Rectangle Snap(Rectangle moving, IEnumerable<Rectangle> other, bool enabled)
        => SnapMove(moving, other, enabled, false, 64, Point.Empty);

    public static Rectangle SnapMove(Rectangle moving, IEnumerable<Rectangle> other,
        bool snapItems, bool snapGrid, int gridSize, Point desktopOrigin)
    {
        int? itemX = null, itemY = null;
        if (snapItems)
        {
            foreach (Rectangle r in other)
            {
                if (VerticalOverlap(moving, r))
                {
                    Consider(ref itemX, r.Left - moving.Right);
                    Consider(ref itemX, r.Right - moving.Left);
                    Consider(ref itemX, r.Left - moving.Left);
                    Consider(ref itemX, r.Right - moving.Right);
                }
                if (HorizontalOverlap(moving, r))
                {
                    Consider(ref itemY, r.Top - moving.Bottom);
                    Consider(ref itemY, r.Bottom - moving.Top);
                    Consider(ref itemY, r.Top - moving.Top);
                    Consider(ref itemY, r.Bottom - moving.Bottom);
                }
            }
        }
        int dx = itemX ?? (snapGrid ? GridDelta(moving.Left + desktopOrigin.X, gridSize) : 0);
        int dy = itemY ?? (snapGrid ? GridDelta(moving.Top + desktopOrigin.Y, gridSize) : 0);
        moving.Offset(dx, dy);
        return moving;
    }

    
    public static Rectangle SnapResize(Rectangle proposed, IEnumerable<Rectangle> other,
        bool snapItems, bool snapGrid, int gridSize, Point desktopOrigin,
        int minimumSize = 24)
    {
        int? widthDelta = null, heightDelta = null;
        if (snapItems)
        {
            foreach (Rectangle r in other)
            {
                if (VerticalOverlap(proposed, r))
                {
                    Consider(ref widthDelta, r.Left - proposed.Right);
                    Consider(ref widthDelta, r.Right - proposed.Right);
                }
                if (HorizontalOverlap(proposed, r))
                {
                    Consider(ref heightDelta, r.Top - proposed.Bottom);
                    Consider(ref heightDelta, r.Bottom - proposed.Bottom);
                }
            }
        }
        int dw = widthDelta ?? (snapGrid ? GridDelta(proposed.Right + desktopOrigin.X, gridSize) : 0);
        int dh = heightDelta ?? (snapGrid ? GridDelta(proposed.Bottom + desktopOrigin.Y, gridSize) : 0);
        proposed.Width = Math.Max(minimumSize, proposed.Width + dw);
        proposed.Height = Math.Max(minimumSize, proposed.Height + dh);
        return proposed;
    }

    public static int GridDelta(int screenCoordinate, int gridSize)
    {
        int halfStep = Math.Max(1, gridSize / 2);
        int nearest = (int)Math.Round((double)screenCoordinate / halfStep,
            MidpointRounding.AwayFromZero) * halfStep;
        return nearest - screenCoordinate;
    }

    public static Size KeepRatio(int requestedWidth, int requestedHeight, double ratio,
        bool widthPrimary, int minSize = 24)
    {
        ratio = double.IsFinite(ratio) && ratio > 0 ? ratio : 1;
        if (widthPrimary)
        {
            int width = Math.Max(minSize, requestedWidth);
            int height = Math.Max(minSize, (int)Math.Round(width / ratio));
            width = Math.Max(minSize, (int)Math.Round(height * ratio));
            return new Size(width, height);
        }
        else
        {
            int height = Math.Max(minSize, requestedHeight);
            int width = Math.Max(minSize, (int)Math.Round(height * ratio));
            height = Math.Max(minSize, (int)Math.Round(width / ratio));
            return new Size(width, height);
        }
    }

    public static bool VerticalOverlap(Rectangle a, Rectangle b) =>
        a.Top < b.Bottom + Threshold && a.Bottom > b.Top - Threshold;
    public static bool HorizontalOverlap(Rectangle a, Rectangle b) =>
        a.Left < b.Right + Threshold && a.Right > b.Left - Threshold;

    private static void Consider(ref int? best, int delta)
    {
        if (Math.Abs(delta) <= Threshold && (!best.HasValue || Math.Abs(delta) < Math.Abs(best.Value)))
            best = delta;
    }
}
