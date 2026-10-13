using System.Text.Json;

namespace OverlayDesk;

public sealed class OverlayItem
{
    public string Path { get; set; } = "";
    public int X { get; set; } = 100;
    public int Y { get; set; } = 100;
    public int Width { get; set; } = 480;
    public int Height { get; set; } = 320;
    
    
    public bool SizeIsAutomatic { get; set; }
    public bool ForceKeepAspectRatio { get; set; }
}

public sealed class LayoutDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "New layout";
    public bool Pinned { get; set; }
    public bool AlwaysOnTop { get; set; }
    public bool SnapToItems { get; set; } = true;
    public bool SnapToGrid { get; set; }
    public int GridSize { get; set; } = 64;
    public bool ForceKeepAspectRatio { get; set; }
    public bool Locked { get; set; }
    public int? WindowX { get; set; }
    public int? WindowY { get; set; }
    public List<OverlayItem> Items { get; set; } = new();
}

public sealed class AppSettings
{
    public bool DefaultAlwaysOnTop { get; set; }
    public bool DefaultSnapToItems { get; set; } = true;
    public bool DefaultSnapToGrid { get; set; }
    public int DefaultGridSize { get; set; } = 64;
    public int MainWindowWidth { get; set; } = 720;
    public int MainWindowHeight { get; set; } = 500;
}

public sealed class AppData
{
    public AppSettings Settings { get; set; } = new();
    public List<LayoutDefinition> Layouts { get; set; } = new();
}

public static class DataStore
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public static string DirectoryPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OverlayDesk");
    private static string DataPath => Path.Combine(DirectoryPath, "data.json");

    public static AppData Load()
    {
        try
        {
            if (File.Exists(DataPath))
                return JsonSerializer.Deserialize<AppData>(File.ReadAllText(DataPath)) ?? new AppData();
        }
        catch { }
        return new AppData();
    }

    public static void Save(AppData data)
    {
        Directory.CreateDirectory(DirectoryPath);
        
        string backup = Path.Combine(DirectoryPath, "data.backup.json");
        if (File.Exists(DataPath) && !File.Exists(backup))
            File.Copy(DataPath, backup);
        var temp = DataPath + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(data, Options));
        File.Move(temp, DataPath, true);
    }
}
