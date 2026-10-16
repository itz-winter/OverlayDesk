using System.Drawing;
using System.Text.Json;
using OverlayDesk;

static void Expect(bool condition, string description)
{
    if (!condition) throw new Exception("FAILED: " + description);
    Console.WriteLine("PASS: " + description);
}

var other = new[] { new Rectangle(150, 5, 50, 50) };
var moving = new Rectangle(94, 10, 50, 40);
Expect(SnapEngine.Snap(moving, other, true).X == 100, "file edge snapping on horizontal approach");
Expect(SnapEngine.Snap(moving, other, false) == moving, "item snapping disabled");
Expect(SnapEngine.Snap(new Rectangle(80, 10, 50, 40), other, true).X == 80,
    "no item snapping outside threshold");
Expect(SnapEngine.Snap(new Rectangle(100, 99, 50, 40),
    new[] { new Rectangle(95, 145, 55, 60) }, true).Y == 105, "vertical edge snapping");
Expect(SnapEngine.Snap(new Rectangle(-101, -40, 100, 100),
    new[] { new Rectangle(-1, -50, 100, 120) }, true).X == -101,
    "negative positions remain valid");

var gridMove = SnapEngine.SnapMove(new Rectangle(46, 73, 40, 30),
    Array.Empty<Rectangle>(), false, true, 64, Point.Empty);
Expect(gridMove.Location == new Point(32, 64), "grid move snaps to half-cell step");
Expect(SnapEngine.SnapMove(new Rectangle(-45, -10, 30, 30),
    Array.Empty<Rectangle>(), false, true, 64, Point.Empty).X == -32,
    "grid supports negative desktop coordinates");
Expect(SnapEngine.SnapMove(new Rectangle(25, 15, 30, 30),
    Array.Empty<Rectangle>(), false, true, 64, new Point(-1920, 0)).X == 32,
    "grid uses actual screen coordinates on monitors left of primary");
Expect(SnapEngine.SnapMove(new Rectangle(60, 10, 40, 40),
    new[] { new Rectangle(105, 0, 30, 60) }, true, true, 64, Point.Empty).X == 65,
    "file edge takes priority over grid when nearby");
Expect(SnapEngine.SnapMove(new Rectangle(60, 59, 40, 30),
    new[] { new Rectangle(105, 0, 30, 50) }, true, false, 64, Point.Empty).X == 65,
    "file snapping also works when other-axis edges are within threshold");
Expect(SnapEngine.SnapMove(new Rectangle(61, 10, 40, 40),
    new[] { new Rectangle(105, 0, 30, 60) }, false, false, 64, Point.Empty).X == 61,
    "all snapping disabled");

var resized = SnapEngine.SnapResize(new Rectangle(10, 10, 117, 90),
    new[] { new Rectangle(132, 0, 100, 140) }, true, true, 64, Point.Empty);
Expect(resized.Width == 122, "resize snaps right edge to another file");
Expect(SnapEngine.SnapResize(new Rectangle(10, 10, 116, 93),
    Array.Empty<Rectangle>(), false, true, 64, Point.Empty).Width == 118,
    "resize snaps dragged edge to half-grid positions");
var ratio = SnapEngine.KeepRatio(300, 100, 16.0 / 9.0, true);
Expect(Math.Abs(ratio.Width / (double)ratio.Height - 16.0 / 9.0) < 0.02,
    "constrained resize retains image aspect ratio");
Expect(SnapEngine.KeepRatio(10, 1, 1.0, true).Width >= 24,
    "constrained resize obeys minimum size");

var data = new AppData { Layouts = new List<LayoutDefinition>
{
    new LayoutDefinition
    {
        Name = "test", Pinned = true, SnapToItems = false,
        SnapToGrid = true, GridSize = 80, Locked = true, ForceKeepAspectRatio = true,
        Items = new List<OverlayItem> { new OverlayItem
        {
            Path = @"C:\\test.gif", X = -12, Y = 25, Width = 640,
            Height = 480, ForceKeepAspectRatio = true
        } }
    }
}};
var roundtrip = JsonSerializer.Deserialize<AppData>(JsonSerializer.Serialize(data));
Expect(roundtrip != null && roundtrip.Layouts.Count == 1 &&
    roundtrip.Layouts[0].Items[0].X == -12 &&
    roundtrip.Layouts[0].Items[0].Width == 640 &&
    roundtrip.Layouts[0].Items[0].ForceKeepAspectRatio &&
    roundtrip.Layouts[0].Pinned && !roundtrip.Layouts[0].SnapToItems &&
    roundtrip.Layouts[0].SnapToGrid && roundtrip.Layouts[0].GridSize == 80 &&
    roundtrip.Layouts[0].Locked && roundtrip.Layouts[0].ForceKeepAspectRatio,
    "layout lock, aspect ratio and snap grid settings survive JSON roundtrip");

var virtualDesktop = new Rectangle(-1920, -200, 5760, 2360);
var contentOnScreen = new Rectangle(400, 150, 640, 480);
var compactHost = OverlayHostGeometry.ClipAndPad(contentOnScreen, virtualDesktop, 12);
Expect(compactHost.Width == 664 && compactHost.Height == 504,
    "compact host is sized to file content, not the entire virtual screen");
Expect(compactHost.Width < virtualDesktop.Width / 2,
    "one small file does not create a desktop-sized native window");
var oldHost = new Rectangle(400, 150, 320, 240);
var newHost = new Rectangle(300, 120, 480, 300);
var child = new Rectangle(10, 20, 100, 80);
var rebased = OverlayHostGeometry.RebasedClientBounds(child, oldHost, newHost);
Expect(rebased.Location == new Point(110, 50),
    "rebased child uses correct offset after top-level window expansion");
Expect(new Point(oldHost.X + child.X, oldHost.Y + child.Y) ==
    new Point(newHost.X + rebased.X, newHost.Y + rebased.Y),
    "expanding the host preserves file screen position exactly");
var clippedHost = OverlayHostGeometry.ClipAndPad(
    new Rectangle(-2000, -300, 250, 200), virtualDesktop, 20);
Expect(clippedHost.Left == virtualDesktop.Left && clippedHost.Top == virtualDesktop.Top,
    "host limits itself to visible monitors with negative desktop origins");

Console.WriteLine("All overlay geometry/data tests passed.");
