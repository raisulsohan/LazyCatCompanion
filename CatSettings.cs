using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows.Media;

namespace LazyCatCompanion;

/// <summary>Coat, markings, size, click mode and position, kept in %AppData%\LazyCatCompanion\settings.json.</summary>
internal sealed class CatSettings
{
    private static readonly string FilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LazyCatCompanion", "settings.json");

    public string FurColor { get; set; } = "#928373";

    /// <summary>White face, bib, belly and legs, dark tail tip and a collar, like the reference photo.</summary>
    public bool Markings { get; set; } = true;
    public double Scale { get; set; } = 1;
    public bool JumpOnAnyClick { get; set; }

    /// <summary>Which way the cat sits facing: "Auto" (into the screen), "Left" or "Right".</summary>
    public string Facing { get; set; } = "Auto";
    public double? AnchorX { get; set; }
    public double? AnchorY { get; set; }

    [JsonIgnore]
    public Color Fur
    {
        get
        {
            try
            {
                return (Color)ColorConverter.ConvertFromString(FurColor);
            }
            catch (FormatException)
            {
                return Color.FromRgb(146, 131, 115);
            }
        }
        set => FurColor = $"#{value.R:X2}{value.G:X2}{value.B:X2}";
    }

    public static CatSettings Load()
    {
        try
        {
            return File.Exists(FilePath)
                ? JsonSerializer.Deserialize<CatSettings>(File.ReadAllText(FilePath)) ?? new CatSettings()
                : new CatSettings();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new CatSettings();
        }
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Settings are a convenience; the cat keeps working without them.
        }
    }
}
