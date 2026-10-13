using System.Globalization;
using System.Text;
using System.Xml;

namespace OverlayDesk;
public static class ImageDimensions
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".tif", ".tiff", ".webp", ".svg", ".avif"
    };

    public static bool IsImage(string path) => Extensions.Contains(Path.GetExtension(path));

    public static Size? TryRead(string path)
    {
        if (!IsImage(path) || !File.Exists(path)) return null;
        string extension = Path.GetExtension(path).ToLowerInvariant();
        try
        {
            if (extension == ".webp") return ReadWebp(path);
            if (extension == ".svg") return ReadSvg(path);
            if (extension == ".avif") return null; 
            using var image = Image.FromFile(path);
            return Valid(image.Width, image.Height);
        }
        catch { return null; }
    }

    private static Size? Valid(int width, int height) =>
        width > 0 && height > 0 && width <= 100000 && height <= 100000
            ? new Size(width, height) : null;

    private static Size? ReadWebp(string path)
    {
        using var stream = File.OpenRead(path);
        Span<byte> head = stackalloc byte[30];
        int count = stream.Read(head);
        if (count < 30 || Encoding.ASCII.GetString(head[..4]) != "RIFF" ||
            Encoding.ASCII.GetString(head.Slice(8, 4)) != "WEBP") return null;
        string fourcc = Encoding.ASCII.GetString(head.Slice(12, 4));
        if (fourcc == "VP8X")
        {
            int w = 1 + head[24] + (head[25] << 8) + (head[26] << 16);
            int h = 1 + head[27] + (head[28] << 8) + (head[29] << 16);
            return Valid(w, h);
        }
        if (fourcc == "VP8L" && head[20] == 0x2f)
        {
            int w = 1 + (((head[22] & 0x3f) << 8) | head[21]);
            int h = 1 + (((head[24] & 0x0f) << 10) | (head[23] << 2) | ((head[22] & 0xc0) >> 6));
            return Valid(w, h);
        }
        if (fourcc == "VP8 " && head[23] == 0x9d && head[24] == 0x01 && head[25] == 0x2a)
        {
            int w = (head[26] | head[27] << 8) & 0x3fff;
            int h = (head[28] | head[29] << 8) & 0x3fff;
            return Valid(w, h);
        }
        return null;
    }

    private static Size? ReadSvg(string path)
    {
        using var reader = XmlReader.Create(path, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true
        });
        reader.MoveToContent();
        if (reader.LocalName != "svg") return null;
        float? w = ParseSvgLength(reader.GetAttribute("width"));
        float? h = ParseSvgLength(reader.GetAttribute("height"));
        string? box = reader.GetAttribute("viewBox");
        if (box != null)
        {
            var parts = box.Split(new[] { ' ', ',', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 4 && float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float bw)
                && float.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out float bh)
                && bw > 0 && bh > 0)
            {
                if (w == null && h == null) { w = bw; h = bh; }
                else if (w == null) w = h * bw / bh;
                else if (h == null) h = w * bh / bw;
            }
        }
        return w.HasValue && h.HasValue ? Valid((int)Math.Round(w.Value), (int)Math.Round(h.Value)) : null;
    }

    private static float? ParseSvgLength(string? source)
    {
        if (string.IsNullOrWhiteSpace(source)) return null;
        string s = source.Trim().ToLowerInvariant();
        if (s.EndsWith("px")) s = s[..^2];
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value > 0)
            return value;
        return null;
    }
}
