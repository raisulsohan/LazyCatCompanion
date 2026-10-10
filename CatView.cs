using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace LazyCatCompanion;

/// <summary>The element the cat is drawn on. Transparent pixels pass clicks through to the desktop.</summary>
internal sealed class CatView : FrameworkElement
{
    /// <summary>Unscaled size of the cat's window and where its ground point sits inside it.</summary>
    public const double BaseWidth = 300, BaseHeight = 260, OriginX = 150, OriginY = 228;

    private readonly CatController _cat;

    public CatView(CatController cat)
    {
        _cat = cat;
        Effect = new DropShadowEffect
        {
            Color = Color.FromRgb(30, 20, 16), BlurRadius = 6, ShadowDepth = 1.5, Direction = 270, Opacity = 0.35
        };
    }

    public CatPalette Palette { get; set; } = new(Color.FromRgb(146, 131, 115), true);

    protected override void OnRender(DrawingContext dc)
    {
        var scale = _cat.Scale;
        dc.PushTransform(new TranslateTransform(OriginX * scale, OriginY * scale));
        dc.PushTransform(new ScaleTransform(scale, scale));
        CatRenderer.Draw(dc, _cat.Pose, Palette, _cat.Facing, _cat.TailPhase, _cat.Time, _cat.Hearts);
        dc.Pop();
        dc.Pop();
    }
}
