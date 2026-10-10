using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LazyCatCompanion;

/// <summary>Renders contact sheets of the poses and of a scripted wake → jump → sleep run, for checking the art.</summary>
internal static class PreviewRenderer
{
    private const double Step = 1 / 60.0;
    private static readonly Typeface LabelFace = new("Segoe UI");
    private static readonly Color Tabby = Color.FromRgb(146, 131, 115);

    public static void Render(string folder)
    {
        Directory.CreateDirectory(folder);
        var palette = new CatPalette(Tabby, true);
        (string, CatPose)[] poses =
        [
            ("Sleep", CatPoses.Sleep), ("Loaf", CatPoses.Loaf), ("Stretch", CatPoses.Stretch), ("Sit", CatPoses.Sit),
            ("Stand", CatPoses.Stand), ("Crouch", CatPoses.Crouch), ("Leap", CatPoses.Leap), ("Land", CatPoses.Land),
            ("Squash", CatPoses.Squash), ("Held", CatPoses.Held), ("Yawn", CatPoses.Yawn), ("Curious", CatPoses.Curious)
        ];
        var sitting = CatPoses.Sitting.Select(s => new Cell(s.Name, s.Pose, 1, palette, 0)).ToList();
        Save(Sheet(sitting, 4, 1, Color.FromRgb(214, 196, 168)), Path.Combine(folder, "sitting.png"));
        Save(Sheet([new Cell("Sit", CatPoses.Sit, 1, palette, 0), new Cell("Lounge", CatPoses.Lounge, -1, palette, 0)], 2, 2.4, Color.FromRgb(214, 196, 168)),
            Path.Combine(folder, "closeup.png"));
        var cells = poses.Select(p => new Cell(p.Item1, p.Item2, 1, palette, 0)).ToList();
        Save(Sheet(cells, 4, 1, Colors.White), Path.Combine(folder, "poses.png"));
        Save(Sheet(cells, 4, 1, Color.FromRgb(43, 45, 51)), Path.Combine(folder, "poses-dark.png"));

        Save(Sheet([new Cell("Tabby & white", CatPoses.Sit, 1, palette, 0)], 1, 0.8, Color.FromRgb(236, 238, 242)), Path.Combine(folder, "coat-photo.png"));
        foreach (var size in new[] { 16, 24, 32, 48, 64, 128, 256 })
        {
            Save(Face(size, palette), Path.Combine(folder, $"icon-{size}.png"));
        }

        Color[] coats =
        [
            Color.FromRgb(217, 138, 82), Color.FromRgb(235, 215, 174), Color.FromRgb(139, 148, 158), Color.FromRgb(64, 66, 76),
            Color.FromRgb(112, 76, 64), Color.FromRgb(239, 239, 231), Color.FromRgb(164, 133, 193), Color.FromRgb(94, 143, 204)
        ];
        Save(Sheet(coats.Select(c => new Cell($"#{c.R:X2}{c.G:X2}{c.B:X2}", CatPoses.Sit, 1, new CatPalette(c, false), 0)).ToList(), 4, 0.8, Color.FromRgb(236, 238, 242)),
            Path.Combine(folder, "coats.png"));

        var cat = new CatController { Anchor = new Point(260, 300) };
        var timeline = new List<Cell>();
        void Run(double seconds)
        {
            for (var t = 0.0; t < seconds; t += Step)
            {
                cat.Update(Step, null, false);
                timeline.Add(new Cell($"{cat.Time:0.00}s {cat.Mood}", cat.Pose, cat.Facing, palette, cat.TailPhase, cat.Anchor, cat.Time));
            }
        }

        Run(0.5);
        cat.Wake();
        var wakeStart = timeline.Count;
        Run(3.6);
        Save(Sheet(Every(timeline, wakeStart, 0.15), 6, 0.6, Colors.White), Path.Combine(folder, "wake.png"));

        var jumpStart = timeline.Count;
        cat.TryJump(cat.Anchor + new Vector(420, -170));
        Run(1.8);
        cat.TryJump(cat.Anchor + new Vector(-300, 120));
        Run(1.9);
        Save(Sheet(Every(timeline, jumpStart, 0.1), 8, 0.5, Colors.White), Path.Combine(folder, "jump.png"));
        Save(ArcSheet(timeline, jumpStart, palette), Path.Combine(folder, "jump-path.png"));

        Run(1);
        foreach (var (style, file) in new[] { ("Licking a paw", "groom-paw.png"), ("Leg-up grooming", "groom-leg.png") })
        {
            cat.StrikePose(CatPoses.Sitting.First(s => s.Name == style));
            var groomStart = timeline.Count;
            Run(6.4);
            Save(Sheet(Every(timeline, groomStart, 0.2), 8, 0.8, Colors.White), Path.Combine(folder, file));
        }

        cat.Sleep();
        var sleepStart = timeline.Count;
        Run(3.4);
        Save(Sheet(Every(timeline, sleepStart, 0.15), 6, 0.6, Colors.White), Path.Combine(folder, "sleep.png"));
    }

    private static List<Cell> Every(List<Cell> cells, int start, double seconds) =>
        cells.Skip(start).Where((_, i) => i % (int)Math.Round(seconds / Step) == 0).ToList();

    private static BitmapSource Sheet(List<Cell> cells, int columns, double scale, Color background)
    {
        var width = CatView.BaseWidth * scale;
        var height = CatView.BaseHeight * scale;
        var rows = (int)Math.Ceiling(cells.Count / (double)columns);
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(background), null, new Rect(0, 0, width * columns, height * rows));
            var label = background.R + background.G + background.B > 380 ? Brushes.DimGray : Brushes.Silver;
            for (var i = 0; i < cells.Count; i++)
            {
                var x = i % columns * width;
                var y = i / columns * height;
                dc.DrawRectangle(null, new Pen(Brushes.LightGray, 1), new Rect(x, y, width, height));
                dc.DrawText(new FormattedText(cells[i].Name, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, LabelFace, 11, label, 1),
                    new Point(x + 4, y + 2));
                dc.PushTransform(new TranslateTransform(x + CatView.OriginX * scale, y + CatView.OriginY * scale));
                dc.PushTransform(new ScaleTransform(scale, scale));
                CatRenderer.Draw(dc, cells[i].Pose, cells[i].Palette, cells[i].Facing, cells[i].TailPhase, cells[i].Time);
                dc.Pop();
                dc.Pop();
            }
        }

        return Bake(visual, width * columns, height * rows);
    }

    /// <summary>Onion-skins the jump frames at their real screen positions, to check the arc and the facing.</summary>
    private static BitmapSource ArcSheet(List<Cell> cells, int start, CatPalette palette)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, 1000, 420));
            var frames = cells.Skip(start).ToList();
            for (var i = 0; i < frames.Count; i += 4)
            {
                var cell = frames[i];
                dc.PushOpacity(0.35 + 0.65 * (i % 12 == 0 ? 1 : 0));
                dc.PushTransform(new TranslateTransform(cell.Anchor.X, cell.Anchor.Y));
                dc.PushTransform(new ScaleTransform(0.55, 0.55));
                CatRenderer.Draw(dc, cell.Pose, palette, cell.Facing, cell.TailPhase, cell.Time);
                dc.Pop();
                dc.Pop();
                dc.Pop();
            }
        }

        return Bake(visual, 1000, 420);
    }

    /// <summary>The cat's face on a transparent square, for the app icon.</summary>
    private static BitmapSource Face(int size, CatPalette palette)
    {
        var scale = size / 64.0;
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(32 * scale, 38 * scale));
            dc.PushTransform(new ScaleTransform(0.66 * scale, 0.66 * scale));
            CatRenderer.DrawHeadOnly(dc, CatPoses.Sit, palette);
            dc.Pop();
            dc.Pop();
        }

        return Bake(visual, size, size);
    }

    internal static BitmapSource Bake(Visual visual, double width, double height)
    {
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(width), (int)Math.Ceiling(height), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        return bitmap;
    }

    internal static void Save(BitmapSource bitmap, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path);
        encoder.Save(stream);
    }

    private sealed record Cell(string Name, CatPose Pose, double Facing, CatPalette Palette, double TailPhase, Point Anchor = default, double Time = 0);
}
