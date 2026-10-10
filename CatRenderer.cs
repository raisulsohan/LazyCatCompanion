using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LazyCatCompanion;

/// <summary>
/// Brushes for one coat. With <c>markings</c> the cat is a tabby-and-white like the reference photo:
/// white face blaze, bib, belly and legs, a dark tail tip, green eyes and a collar.
/// Without them the whole cat is one tabby colour.
/// </summary>
internal sealed class CatPalette
{
    private static readonly Color Ink = Color.FromRgb(46, 36, 30);

    public CatPalette(Color fur, bool markings)
    {
        Fur = fur;
        Markings = markings;
        var light = Mix(fur, Colors.White, 0.25);
        var deep = Mix(fur, Ink, 0.22);
        var stripe = Mix(fur, Ink, markings ? 0.72 : 0.55);
        var white = markings ? Color.FromRgb(246, 243, 237) : Mix(fur, Color.FromRgb(255, 249, 240), 0.74);
        var clearWhite = Color.FromArgb(0, white.R, white.G, white.B);

        FurBrush = Solid(fur);
        ShadeBrush = Solid(Mix(fur, Ink, 0.18));
        StripeBrush = Solid(Color.FromArgb(215, stripe.R, stripe.G, stripe.B));
        SpineBrush = Solid(Color.FromArgb(110, stripe.R, stripe.G, stripe.B));
        TipBrush = Solid(Mix(stripe, Ink, 0.4));
        WhiteBrush = Solid(white);
        ShadeWhiteBrush = Solid(Mix(white, Ink, 0.13));
        LegBrush = markings ? WhiteBrush : FurBrush;
        FarLegBrush = markings ? ShadeWhiteBrush : ShadeBrush;
        SoftWhiteBrush = Freeze(new RadialGradientBrush(
            [new GradientStop(white, 0), new GradientStop(white, 0.62), new GradientStop(clearWhite, 1)]));
        PatchBrush = markings
            ? Freeze(new RadialGradientBrush(Color.FromArgb(150, 196, 138, 84), Color.FromArgb(0, 196, 138, 84)))
            : null;
        BodyBrush = Freeze(new LinearGradientBrush(
            [new GradientStop(light, 0), new GradientStop(fur, 0.5), new GradientStop(deep, 1)],
            new Point(0, -140), new Point(0, 0)) { MappingMode = BrushMappingMode.Absolute });
        HeadBrush = Freeze(new RadialGradientBrush(light, fur)
        {
            GradientOrigin = new Point(0.42, 0.3), Center = new Point(0.45, 0.38), RadiusX = 0.75, RadiusY = 0.75
        });
        HaunchBrush = Freeze(new RadialGradientBrush(
            [new GradientStop(light, 0), new GradientStop(fur, 0.65), new GradientStop(Mix(fur, Ink, 0.1), 1)])
        {
            GradientOrigin = new Point(0.45, 0.35), Center = new Point(0.5, 0.45), RadiusX = 0.55, RadiusY = 0.55
        });
        IrisBrush = Freeze(markings
            ? new RadialGradientBrush(
                [new GradientStop(Color.FromRgb(214, 216, 150), 0), new GradientStop(Color.FromRgb(165, 182, 104), 0.55), new GradientStop(Color.FromRgb(98, 126, 64), 1)])
            : new RadialGradientBrush(
                [new GradientStop(Color.FromRgb(246, 214, 120), 0), new GradientStop(Color.FromRgb(206, 160, 66), 0.6), new GradientStop(Color.FromRgb(126, 140, 58), 1)]));
    }

    public Color Fur { get; }
    public bool Markings { get; }
    public Brush FurBrush { get; }
    public Brush ShadeBrush { get; }
    public Brush StripeBrush { get; }
    public Brush SpineBrush { get; }
    public Brush TipBrush { get; }
    public Brush WhiteBrush { get; }
    public Brush ShadeWhiteBrush { get; }
    public Brush LegBrush { get; }
    public Brush FarLegBrush { get; }
    public Brush SoftWhiteBrush { get; }
    public Brush? PatchBrush { get; }
    public Brush BodyBrush { get; }
    public Brush HeadBrush { get; }
    public Brush HaunchBrush { get; }
    public Brush IrisBrush { get; }

    public static Color Mix(Color a, Color b, double t) => Color.FromRgb(
        (byte)Math.Round(a.R + (b.R - a.R) * t),
        (byte)Math.Round(a.G + (b.G - a.G) * t),
        (byte)Math.Round(a.B + (b.B - a.B) * t));

    internal static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static Brush Solid(Color color) => Freeze(new SolidColorBrush(color));
}

/// <summary>A tileable fur grain: thousands of short light and dark hairs, drawn once and laid over every furry part.</summary>
internal static class FurTexture
{
    public static readonly Brush Grain = Build();

    private static Brush Build()
    {
        const int size = 128;
        var random = new Random(7);
        var lightHair = CatPalette.Freeze(new Pen(new SolidColorBrush(Color.FromArgb(64, 255, 255, 255)), 0.9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        var darkHair = CatPalette.Freeze(new Pen(new SolidColorBrush(Color.FromArgb(56, 0, 0, 0)), 0.9) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round });
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            for (var i = 0; i < 1600; i++)
            {
                var start = new Point(random.NextDouble() * size, random.NextDouble() * size);
                var angle = (random.NextDouble() - 0.5) * 0.55;
                var hair = new Vector(Math.Cos(angle), Math.Sin(angle)) * (3 + random.NextDouble() * 6);
                var pen = random.NextDouble() < 0.5 ? lightHair : darkHair;

                // Draw the wrapped copies too, so the tile has no seams.
                for (var dx = -size; dx <= size; dx += size)
                {
                    for (var dy = -size; dy <= size; dy += size)
                    {
                        var shifted = start + new Vector(dx, dy);
                        if (shifted.X > -10 && shifted.X < size + 1 && shifted.Y > -10 && shifted.Y < size + 10)
                        {
                            dc.DrawLine(pen, shifted, shifted + hair);
                        }
                    }
                }
            }
        }

        var bitmap = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        bitmap.Freeze();
        return CatPalette.Freeze(new ImageBrush(bitmap)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(0, 0, 56, 56),
            Stretch = Stretch.Fill
        });
    }
}

/// <summary>A heart that floats up while the cat is being petted.</summary>
internal readonly record struct Heart(Point Position, double Age);

/// <summary>Draws the cat from a <see cref="CatPose"/>. Nothing here is a fixed picture, so every pose can blend into every other.</summary>
internal static class CatRenderer
{
    private const double Slim = 0.92, HeadScale = 0.9;
    private const double FrontUpper = 32, FrontLower = 33, BackUpper = 32, BackLower = 36;

    private static readonly Color Ink = Color.FromRgb(43, 33, 28);
    private static readonly Brush InkBrush = Solid(Ink);
    private static readonly Pen EyeLinePen = CatPalette.Freeze(new Pen(InkBrush, 1.7) { LineJoin = PenLineJoin.Round });
    private static readonly Pen LidPen = RoundPen(InkBrush, 2.3, freeze: true);
    private static readonly Pen MouthPen = CatPalette.Freeze(new Pen(Solid(Color.FromRgb(90, 66, 60)), 1.3) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round });
    private static readonly Pen WhiskerShadow = RoundPen(Solid(Color.FromArgb(70, 40, 30, 26)), 1.5, freeze: true);
    private static readonly Pen WhiskerPen = RoundPen(Solid(Color.FromArgb(235, 250, 250, 248)), 0.8, freeze: true);
    private static readonly Pen IrisFiber = CatPalette.Freeze(new Pen(Solid(Color.FromArgb(46, 30, 40, 10)), 0.6));
    private static readonly Pen LimbalPen = CatPalette.Freeze(new Pen(Solid(Color.FromArgb(120, 40, 50, 20)), 1.3));
    private static readonly Pen ToePen = RoundPen(Solid(Color.FromArgb(80, 90, 80, 75)), 0.8, freeze: true);
    private static readonly Pen EarTuftPen = RoundPen(Solid(Color.FromArgb(210, 255, 255, 255)), 1.1, freeze: true);
    private static readonly Pen NoseRim = CatPalette.Freeze(new Pen(Solid(Color.FromRgb(196, 120, 130)), 0.8));
    private static readonly Pen TagPen = CatPalette.Freeze(new Pen(Solid(Color.FromRgb(120, 124, 132)), 0.7));
    private static readonly Brush WhiteBrush = Solid(Colors.White);
    private static readonly Brush LidShadow = Solid(Color.FromArgb(55, 0, 0, 0));
    private static readonly Brush GroundShadow = Solid(Color.FromArgb(46, 0, 0, 0));
    private static readonly Brush TailShadow = Solid(Color.FromArgb(60, 40, 30, 24));
    private static readonly Brush InnerEarBrush = CatPalette.Freeze(new LinearGradientBrush(Color.FromRgb(236, 168, 166), Color.FromRgb(214, 140, 140), 90));
    private static readonly Brush NoseBrush = Solid(Color.FromRgb(232, 154, 164));
    private static readonly Brush BlushBrush = Solid(Color.FromArgb(70, 255, 120, 140));
    private static readonly Brush MouthBrush = Solid(Color.FromRgb(110, 47, 53));
    private static readonly Brush TongueBrush = Solid(Color.FromRgb(240, 127, 140));
    private static readonly Brush FollicleBrush = Solid(Color.FromArgb(120, 110, 100, 95));
    private static readonly Brush CollarBrush = Solid(Color.FromRgb(36, 35, 40));
    private static readonly Brush TagBrush = Solid(Color.FromRgb(206, 210, 216));
    private static readonly Brush HeartBrush = Solid(Color.FromRgb(242, 92, 124));
    private static readonly Brush ZBrush = Solid(Color.FromRgb(96, 110, 150));
    private static readonly Pen ZOutline = CatPalette.Freeze(new Pen(Solid(Color.FromArgb(220, 255, 255, 255)), 4) { LineJoin = PenLineJoin.Round });
    private static readonly Pen HeartOutline = CatPalette.Freeze(new Pen(Solid(Color.FromArgb(220, 255, 255, 255)), 1.6));
    private const double ZGlyphSize = 20;
    private static readonly Geometry ZGlyph = BuildZ();
    private static readonly Geometry HeadShape = BuildHead();
    private static readonly Geometry EarShape = BuildEar(inner: false);
    private static readonly Geometry InnerEarShape = BuildEar(inner: true);
    private static readonly Geometry HeadTop = HalfPlane(new Point(-100, 5), new Point(100, 5), new Vector(0, -1));
    private static readonly Geometry HeadBottom = HalfPlane(new Point(-100, 5), new Point(100, 5), new Vector(0, 1));
    private static readonly Geometry HeadHair = HairEdge(HeadShape, 2.3, 2.5, 100);
    private static readonly Geometry HeartShape = Geometry.Parse("M 0,4 C -7,-1 -5,-8 0,-4 C 5,-8 7,-1 0,4 Z");

    /// <summary>Draws the cat with its ground point at the current origin.</summary>
    public static void Draw(DrawingContext dc, CatPose p, CatPalette pal, double facing, double tailPhase, double time,
        IReadOnlyList<Heart>? hearts = null)
    {
        if (p.Shadow > 0.01)
        {
            dc.PushOpacity(Math.Min(1, p.Shadow));
            dc.DrawEllipse(GroundShadow, null, new Point(-6 * facing, 1), 52, 6);
            dc.Pop();
        }

        var matrix = BodyMatrix(p, facing);
        dc.PushTransform(new MatrixTransform(matrix));
        DrawCat(dc, p, pal, tailPhase);
        dc.Pop();

        if (p.Sleepy > 0.01)
        {
            DrawZzz(dc, matrix.Transform((Point)(p.Chest + p.Head) + new Vector(20, -34)), p.Sleepy, time);
        }

        if (hearts is not null)
        {
            foreach (var heart in hearts)
            {
                DrawHeart(dc, heart);
            }
        }
    }

    /// <summary>Maps cat space to the drawing surface: squash, then body rotation, then the facing flip.</summary>
    public static Matrix BodyMatrix(CatPose p, double facing)
    {
        var matrix = Matrix.Identity;
        matrix.ScaleAt(1 / Math.Sqrt(p.Squash), p.Squash, 0, 0);
        var pivot = (Point)((p.Hip + p.Chest) / 2);
        matrix.RotateAt(p.Rotation, pivot.X, pivot.Y);
        matrix.Scale(facing, 1);
        return matrix;
    }

    /// <summary>Draws only the head, centred on the origin (used for the tray icon).</summary>
    public static void DrawHeadOnly(DrawingContext dc, CatPose p, CatPalette pal) => DrawHead(dc, p, pal);

    private static void DrawCat(DrawingContext dc, CatPose p, CatPalette pal, double tailPhase)
    {
        var hip = (Point)p.Hip;
        var chest = (Point)p.Chest;
        var hipRadius = p.HipRadius * Slim;
        var chestRadius = p.ChestRadius * Slim;
        var body = BodyGeometry(hip, hipRadius, chest, chestRadius, p.Arch, out var along, out var up);
        var bodyAngle = Math.Atan2(along.Y, along.X) * 180 / Math.PI;
        var frontJoint = chest + along * (chestRadius * 0.2) - up * (chestRadius * 0.35);
        var hindJoint = hip + along * (hipRadius * 0.1) - up * (hipRadius * 0.2);
        var tailRoot = hip - along * (hipRadius * 0.85) + up * (hipRadius * 0.25);
        var reach = p.LegLength;

        if (p.TailFront < 0.5)
        {
            DrawTail(dc, tailRoot, p, tailPhase, pal);
        }

        var farHind = Leg(dc, hindJoint - along * 5, (Point)p.BackFar, BackUpper * reach, BackLower * reach, 14, 10, pal.FarLegBrush);
        DrawPaw(dc, farHind, 10, pal.ShadeWhiteBrush);
        var farFront = Leg(dc, frontJoint - along * 5, (Point)p.FrontFar, FrontUpper * reach, FrontLower * reach, 12, 10, pal.FarLegBrush);
        DrawPaw(dc, farFront, 10, pal.ShadeWhiteBrush);

        dc.DrawGeometry(pal.BodyBrush, null, body);
        dc.PushClip(body);
        DrawBodyCoat(dc, hip, hipRadius, chest, chestRadius, p.Arch, along, up, bodyAngle, pal);
        Grain(dc, body, (Point)((p.Hip + p.Chest) / 2), bodyAngle);
        dc.Pop();
        var splitFrom = hip - up * (hipRadius * 0.35);
        var splitTo = chest + up * (chestRadius * 0.15) + along * (chestRadius * 0.9);
        DrawFuzz(dc, HairEdge(body, 3, 2.2, 0), pal.BodyBrush, pal, pal.Markings ? (splitFrom, splitTo, up) : null);

        var nearHind = Leg(dc, hindJoint, (Point)p.BackNear, BackUpper * reach, BackLower * reach, 15, 11, pal.LegBrush, textured: true);
        DrawHaunch(dc, hindJoint + along * 2 + up * 2, hipRadius, p.Haunch, bodyAngle, pal);
        DrawPaw(dc, nearHind, 11, pal.WhiteBrush);

        // A paw held up to the mouth (licking) is drawn over the face; otherwise the head would hide it.
        var pawOverHead = p.PawInFront >= 0.5;
        if (!pawOverHead)
        {
            DrawNearFrontLeg(dc, frontJoint, p, pal);
        }

        if (p.TailFront >= 0.5)
        {
            DrawTail(dc, tailRoot, p, tailPhase, pal);
        }

        var head = chest + p.Head;
        if (pal.Markings)
        {
            DrawCollar(dc, head, chest);
        }

        dc.PushTransform(new TranslateTransform(head.X, head.Y));
        dc.PushTransform(new RotateTransform(p.HeadTilt));
        dc.PushTransform(new ScaleTransform(HeadScale, HeadScale));
        DrawHead(dc, p, pal);
        dc.Pop();
        dc.Pop();
        dc.Pop();

        if (pawOverHead)
        {
            DrawNearFrontLeg(dc, frontJoint, p, pal);
        }
    }

    private static void DrawNearFrontLeg(DrawingContext dc, Point joint, CatPose p, CatPalette pal)
    {
        var foot = Leg(dc, joint, (Point)p.FrontNear, FrontUpper * p.LegLength, FrontLower * p.LegLength, 13, 11, pal.LegBrush, textured: true);
        DrawPaw(dc, foot, 11, pal.WhiteBrush);
    }

    /// <summary>A bendable capsule: a circle at the hip and one at the chest, joined by an arched back and belly.</summary>
    private static StreamGeometry BodyGeometry(Point hip, double hipRadius, Point chest, double chestRadius, double arch,
        out Vector along, out Vector up)
    {
        const double k = 0.5523;
        var axis = chest - hip;
        var length = Math.Max(1, axis.Length);
        along = axis / length;
        up = new Vector(along.Y, -along.X);

        var hipTop = hip + up * hipRadius;
        var chestTop = chest + up * chestRadius;
        var chestFront = chest + along * chestRadius;
        var chestBottom = chest - up * chestRadius;
        var hipBottom = hip - up * hipRadius;
        var hipBack = hip - along * hipRadius;
        var sag = 3 - arch * 0.3;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(hipTop, true, true);
            ctx.BezierTo(hipTop + along * (length * 0.35) + up * arch, chestTop - along * (length * 0.35) + up * arch, chestTop, true, true);
            ctx.BezierTo(chestTop + along * (chestRadius * k), chestFront + up * (chestRadius * k), chestFront, true, true);
            ctx.BezierTo(chestFront - up * (chestRadius * k), chestBottom + along * (chestRadius * k), chestBottom, true, true);
            ctx.BezierTo(chestBottom - along * (length * 0.35) - up * sag, hipBottom + along * (length * 0.35) - up * sag, hipBottom, true, true);
            ctx.BezierTo(hipBottom - along * (hipRadius * k), hipBack - up * (hipRadius * k), hipBack, true, true);
            ctx.BezierTo(hipBack + up * (hipRadius * k), hipTop - along * (hipRadius * k), hipTop, true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>Mackerel tabby stripes from the spine down the flank, a darker spine, and the white underside.</summary>
    private static void DrawBodyCoat(DrawingContext dc, Point hip, double hipRadius, Point chest, double chestRadius,
        double arch, Vector along, Vector up, double bodyAngle, CatPalette pal)
    {
        var length = (chest - hip).Length;
        for (var i = 0; i < 7; i++)
        {
            var t = 0.02 + i * 0.15;
            var centre = hip + (chest - hip) * t;
            var radius = hipRadius + (chestRadius - hipRadius) * t;
            var bulge = arch * 3 * t * (1 - t);
            var depth = radius * (0.95 + 0.25 * Math.Sin(i * 2.1));
            var wobble = 3 * Math.Sin(i * 1.7);
            var width = 4.4 + 1.4 * Math.Sin(i * 1.3 + 0.5);
            var top = centre + up * (radius + bulge + 3);
            var middle = centre + up * (radius - depth * 0.45) + along * (2 + wobble * 0.5);
            var bottom = centre + up * (radius - depth) + along * (wobble - 2);
            DrawStroke(dc, pal.StripeBrush, width, top, top - up * 6 + along * 2, middle);
            DrawStroke(dc, pal.StripeBrush, width * 0.55, middle, middle - up * 6 + along * (wobble * 0.3), bottom);
        }

        DrawStroke(dc, pal.SpineBrush, 9,
            hip + up * hipRadius - along * (hipRadius * 0.6),
            hip + (chest - hip) * 0.5 + up * ((hipRadius + chestRadius) / 2 + arch * 0.75),
            chest + up * chestRadius);

        var bellyBrush = pal.Markings ? pal.SoftWhiteBrush : pal.WhiteBrush;
        var bellyCentre = hip + (chest - hip) * 0.5 - up * (hipRadius * (pal.Markings ? 1.05 : 0.95));
        dc.PushTransform(new RotateTransform(bodyAngle, bellyCentre.X, bellyCentre.Y));
        dc.DrawEllipse(bellyBrush, null, bellyCentre, length * (pal.Markings ? 0.75 : 0.6) + 8, hipRadius * (pal.Markings ? 0.8 : 0.45));
        dc.Pop();
        var bibCentre = chest + along * (chestRadius * 0.6) - up * (chestRadius * 0.08);
        if (pal.Markings)
        {
            dc.DrawEllipse(pal.SoftWhiteBrush, null, bibCentre, chestRadius * 0.95, chestRadius * 1.15);
        }
        else
        {
            dc.DrawEllipse(pal.WhiteBrush, null, bibCentre, chestRadius * 0.55, chestRadius * 0.75);
        }
    }

    private static void DrawHaunch(DrawingContext dc, Point centre, double hipRadius, double size, double bodyAngle, CatPalette pal)
    {
        var rx = hipRadius * 0.82 * size;
        var ry = hipRadius * 0.74 * size;
        var spin = new RotateTransform(bodyAngle - 12, centre.X, centre.Y);
        var shape = new EllipseGeometry(centre, rx, ry, spin);
        dc.DrawGeometry(pal.HaunchBrush, null, shape);
        dc.PushClip(shape);
        for (var i = 0; i < 3; i++)
        {
            var x = rx * 0.1 - i * rx * 0.36;
            DrawStroke(dc, pal.StripeBrush, 4.2 - i * 0.6,
                spin.Transform(centre + new Vector(x, -ry - 2)),
                spin.Transform(centre + new Vector(x - 5, -ry * 0.35)),
                spin.Transform(centre + new Vector(x - 2, ry * 0.2)));
        }

        if (pal.Markings)
        {
            // The white of the leg reaches up the front of the thigh.
            dc.DrawEllipse(pal.SoftWhiteBrush, null, spin.Transform(centre + new Vector(rx * 0.55, ry * 0.75)), rx * 0.7, ry * 0.55);
        }

        Grain(dc, shape, centre, bodyAngle);
        dc.Pop();
        dc.PushClip(HalfPlane(centre - new Vector(rx, 0), centre + new Vector(rx, -ry * 0.4), new Vector(0, -1)));
        dc.DrawGeometry(pal.FurBrush, null, HairEdge(shape, 3.2, 2.0, 40));
        dc.Pop();
    }

    /// <summary>
    /// Two-bone leg: places the knee with the law of cosines. The knee always sits on the same side of the
    /// joint-to-paw line (behind it for a paw below, under it for a paw reaching forward), so it can never
    /// snap across when the paw passes some direction.
    /// </summary>
    private static Point Leg(DrawingContext dc, Point joint, Point target, double upper, double lower,
        double upperWidth, double lowerWidth, Brush brush, bool textured = false)
    {
        target.Y -= lowerWidth * 0.45;
        var toTarget = target - joint;
        var distance = toTarget.Length;
        var maxReach = (upper + lower) * 0.999;
        if (distance > maxReach)
        {
            target = joint + toTarget / distance * maxReach;
            distance = maxReach;
        }

        Point knee;
        if (distance < 0.01)
        {
            knee = joint + new Vector(-upper, 0);
        }
        else
        {
            var direction = toTarget / toTarget.Length;
            var alongLeg = (upper * upper - lower * lower + distance * distance) / (2 * distance);
            var height = Math.Sqrt(Math.Max(0, upper * upper - alongLeg * alongLeg));
            var side = new Vector(-direction.Y, direction.X);
            knee = joint + direction * alongLeg + side * height;
        }

        var upperPen = RoundPen(brush, upperWidth);
        var lowerPen = RoundPen(brush, lowerWidth);
        dc.DrawLine(upperPen, joint, knee);
        dc.DrawLine(lowerPen, knee, target);
        if (textured)
        {
            var shin = new LineGeometry(knee, target).GetWidenedPathGeometry(lowerPen);
            var limb = new GeometryGroup { FillRule = FillRule.Nonzero };
            limb.Children.Add(new LineGeometry(joint, knee).GetWidenedPathGeometry(upperPen));
            limb.Children.Add(shin);
            var direction = target - joint;
            Grain(dc, limb, knee, Math.Atan2(direction.Y, direction.X) * 180 / Math.PI, 0.7);
            dc.DrawGeometry(brush, null, HairEdge(shin, 3.6, 1.6, 200));
        }

        return target;
    }

    private static void DrawPaw(DrawingContext dc, Point foot, double width, Brush brush)
    {
        var centre = foot + new Vector(2.5, 1.5);
        var rx = width * 0.68;
        var ry = width * 0.45;
        dc.DrawEllipse(brush, null, centre, rx, ry);
        dc.DrawLine(ToePen, centre + new Vector(rx * 0.2, -ry * 0.1), centre + new Vector(rx * 0.25, ry * 0.75));
        dc.DrawLine(ToePen, centre + new Vector(rx * 0.58, -ry * 0.2), centre + new Vector(rx * 0.62, ry * 0.65));
    }

    private static void DrawTail(DrawingContext dc, Point root, CatPose p, double phase, CatPalette pal)
    {
        const int segments = 18;
        var step = 4.7 * p.TailLength;
        var points = new Point[segments + 1];
        points[0] = root;
        for (var i = 1; i <= segments; i++)
        {
            var f = i / (double)segments;
            var angle = p.TailBase + p.TailCurl * Math.Pow(f, p.TailEase) + p.TailWag * Math.Sin(phase - f * 2.4) * (0.35 + f);
            var radians = angle * Math.PI / 180;
            points[i] = points[i - 1] + new Vector(Math.Cos(radians), Math.Sin(radians)) * step;
        }

        // The reference cat's tail gets bushier towards its dark tip.
        double HalfWidth(int i) => pal.Markings ? 5.4 + 1.8 * i / segments : 7 - 2.2 * i / segments;
        var left = new Point[segments + 1];
        var right = new Point[segments + 1];
        for (var i = 0; i <= segments; i++)
        {
            var tangent = points[Math.Min(i + 1, segments)] - points[Math.Max(i - 1, 0)];
            tangent.Normalize();
            var normal = new Vector(-tangent.Y, tangent.X) * HalfWidth(i);
            left[i] = points[i] + normal;
            right[i] = points[i] - normal;
        }

        var outline = new StreamGeometry();
        using (var ctx = outline.Open())
        {
            ctx.BeginFigure(left[0], true, true);
            ctx.PolyLineTo(left.Skip(1).ToArray(), true, true);
            ctx.PolyLineTo(right.Reverse().ToArray(), true, true);
        }

        outline.Freeze();
        if (p.TailFront >= 0.5)
        {
            // Wrapped in front of the paws, the tail casts a soft shadow on them, which shows it lies across them.
            dc.PushTransform(new TranslateTransform(1.2, 2.4));
            dc.DrawGeometry(TailShadow, null, outline);
            dc.Pop();
        }

        dc.DrawGeometry(pal.FurBrush, null, outline);
        dc.PushClip(outline);
        foreach (var f in new[] { 0.22, 0.37, 0.52, 0.66, 0.8 })
        {
            var i = (int)Math.Round(f * segments);
            var across = left[i] - right[i];
            across.Normalize();
            dc.DrawLine(RoundPen(pal.StripeBrush, 4.2), left[i] + across * 2, right[i] - across * 2);
        }

        if (pal.Markings)
        {
            var tipPen = RoundPen(pal.TipBrush, 20);
            for (var i = (int)(segments * 0.8); i < segments; i++)
            {
                dc.DrawLine(tipPen, points[i], points[i + 1]);
            }
        }

        var direction = points[segments] - points[0];
        Grain(dc, outline, points[segments / 2], Math.Atan2(direction.Y, direction.X) * 180 / Math.PI);
        dc.Pop();
        var hair = HairEdge(outline, 3, 2.0, 70);
        dc.DrawGeometry(pal.FurBrush, null, hair);
        var tip = HalfWidth(segments);
        if (pal.Markings)
        {
            dc.PushClip(new EllipseGeometry(points[segments], step * segments * 0.24, step * segments * 0.24));
            dc.DrawGeometry(pal.TipBrush, null, hair);
            dc.Pop();
        }

        dc.DrawEllipse(pal.Markings ? pal.TipBrush : pal.StripeBrush, null, points[segments], tip, tip);
    }

    private static void DrawCollar(DrawingContext dc, Point head, Point chest)
    {
        var down = chest - head;
        if (down.Length < 1)
        {
            return;
        }

        down.Normalize();
        var centre = head + down * (24 * HeadScale);
        var side = new Vector(-down.Y, down.X);
        DrawStroke(dc, CollarBrush, 5.2, centre - side * 12.5, centre + down * 5, centre + side * 12.5);
        var front = side.X >= 0 ? side : -side;
        var tag = centre + down * 6 + front * 5;
        dc.DrawEllipse(TagBrush, TagPen, tag, 2.6, 2.6);
    }

    private static void DrawHead(DrawingContext dc, CatPose p, CatPalette pal)
    {
        var turn = p.HeadTurn;
        var fx = turn * 5;
        var noseX = fx * 1.15;

        dc.PushTransform(new TranslateTransform(turn * 2, 0));
        DrawEar(dc, pal, p.EarAngle + p.EarNear);
        dc.PushTransform(new ScaleTransform(-1, 1));
        DrawEar(dc, pal, p.EarAngle + p.EarFar);
        dc.Pop();
        dc.Pop();

        dc.DrawGeometry(pal.HeadBrush, null, HeadShape);
        dc.PushClip(HeadShape);
        if (pal.PatchBrush is not null)
        {
            dc.DrawEllipse(pal.PatchBrush, null, new Point(-17 + fx, -14), 15, 12);
        }

        var stripes = pal.StripeBrush;
        var browX = fx * 0.6;
        if (pal.Markings)
        {
            dc.DrawEllipse(stripes, null, new Point(browX - 1, -25), 8, 5.5);
        }

        DrawStroke(dc, stripes, 2.8, new Point(browX, -29), new Point(browX, -23), new Point(browX, -17));
        DrawStroke(dc, stripes, 2.4, new Point(browX - 8, -27), new Point(browX - 7, -22), new Point(browX - 5, -17));
        DrawStroke(dc, stripes, 2.4, new Point(browX + 8, -27), new Point(browX + 7, -22), new Point(browX + 5, -17));
        foreach (var side in new[] { -1, 1 })
        {
            DrawStroke(dc, stripes, 2.2, new Point(side * 38, -6), new Point(side * 32, -5), new Point(side * 27, -3));
            DrawStroke(dc, stripes, 2.2, new Point(side * 39, 2), new Point(side * 33, 3), new Point(side * 28, 4));
        }

        if (pal.Markings)
        {
            dc.DrawEllipse(pal.SoftWhiteBrush, null, new Point(fx, 17), 31, 19);
            var blaze = new StreamGeometry();
            using (var ctx = blaze.Open())
            {
                ctx.BeginFigure(new Point(noseX - 10, 9), true, true);
                ctx.QuadraticBezierTo(new Point(noseX - 4, -5), new Point(browX - 2, -17), true, true);
                ctx.QuadraticBezierTo(new Point(browX, -20), new Point(browX + 2, -17), true, true);
                ctx.QuadraticBezierTo(new Point(noseX + 4, -5), new Point(noseX + 10, 9), true, true);
            }

            dc.DrawGeometry(pal.WhiteBrush, null, blaze);
            dc.DrawEllipse(pal.WhiteBrush, null, new Point(noseX - 5.5, 13.5), 7, 5.2);
            dc.DrawEllipse(pal.WhiteBrush, null, new Point(noseX + 5.5, 13.5), 7, 5.2);
            dc.DrawEllipse(pal.WhiteBrush, null, new Point(noseX, 19), 6.5, 5);
        }
        else
        {
            dc.DrawEllipse(pal.WhiteBrush, null, new Point(fx, 16), 23, 14);
        }

        Grain(dc, HeadShape, new Point(0, 0), 90, 0.8);
        dc.Pop();

        dc.PushClip(HeadTop);
        dc.DrawGeometry(pal.FurBrush, null, HeadHair);
        dc.Pop();
        dc.PushClip(HeadBottom);
        dc.DrawGeometry(pal.Markings ? pal.WhiteBrush : pal.FurBrush, null, HeadHair);
        dc.Pop();

        if (!pal.Markings)
        {
            dc.DrawEllipse(BlushBrush, null, new Point(-23 + fx, 9), 6, 3.5);
            dc.DrawEllipse(BlushBrush, null, new Point(23 + fx, 9), 6, 3.5);
        }

        DrawEye(dc, new Point(-14.5 + fx, -3), 1 - Math.Max(0, turn) * 0.18, -1, p, pal);
        DrawEye(dc, new Point(14.5 + fx, -3), 1 - Math.Max(0, -turn) * 0.18, 1, p, pal);

        foreach (var side in new[] { -1, 1 })
        {
            for (var k = 0; k < 3; k++)
            {
                dc.DrawEllipse(FollicleBrush, null, new Point(noseX + side * (6 + k * 2.6), 12.5 + k % 2 * 2), 0.6, 0.6);
            }
        }

        if (p.MouthOpen > 0.02)
        {
            var mouthCentre = new Point(noseX, 17 + p.MouthOpen * 1.5);
            var mouth = new EllipseGeometry(mouthCentre, 4.2 + p.MouthOpen * 2, 6.5 * p.MouthOpen);
            dc.DrawGeometry(MouthBrush, null, mouth);
            dc.PushClip(mouth);
            dc.DrawEllipse(TongueBrush, null, mouthCentre + new Vector(0, 5 * p.MouthOpen), 4.2, 3.6 * p.MouthOpen);
            dc.Pop();
        }

        if (p.Tongue > 0.02)
        {
            dc.DrawEllipse(TongueBrush, null, new Point(noseX, 17.5 + 2.5 * p.Tongue), 3.1, 3 * p.Tongue);
        }

        dc.PushOpacity(Math.Clamp(1 - p.MouthOpen * 2.5, 0, 1));
        var smile = new StreamGeometry();
        using (var ctx = smile.Open())
        {
            ctx.BeginFigure(new Point(noseX - 6, 14), false, false);
            ctx.QuadraticBezierTo(new Point(noseX - 2.5, 17), new Point(noseX, 12.5), true, true);
            ctx.QuadraticBezierTo(new Point(noseX + 2.5, 17), new Point(noseX + 6, 14), true, true);
        }

        dc.DrawGeometry(null, MouthPen, smile);
        dc.Pop();

        var nose = new StreamGeometry();
        using (var ctx = nose.Open())
        {
            ctx.BeginFigure(new Point(noseX - 4.4, 7.5), true, true);
            ctx.QuadraticBezierTo(new Point(noseX, 6), new Point(noseX + 4.4, 7.5), true, true);
            ctx.QuadraticBezierTo(new Point(noseX + 1.6, 11.6), new Point(noseX, 12), true, true);
            ctx.QuadraticBezierTo(new Point(noseX - 1.6, 11.6), new Point(noseX - 4.4, 7.5), true, true);
        }

        dc.DrawGeometry(NoseBrush, NoseRim, nose);

        foreach (var side in new[] { -1, 1 })
        {
            var root = new Point(side * 12 + fx, 12.5);
            foreach (var (reach, drop) in new[] { (28.0, -7.0), (30.0, 0.0), (27.0, 7.0) })
            {
                var end = root + new Vector(side * reach, drop);
                var bend = root + new Vector(side * reach * 0.55, drop * 0.4 - 2);
                var whisker = new StreamGeometry();
                using (var ctx = whisker.Open())
                {
                    ctx.BeginFigure(root, false, false);
                    ctx.QuadraticBezierTo(bend, end, true, true);
                }

                dc.DrawGeometry(null, WhiskerShadow, whisker);
                dc.DrawGeometry(null, WhiskerPen, whisker);
            }
        }
    }

    private static void DrawEar(DrawingContext dc, CatPalette pal, double angle)
    {
        dc.PushTransform(new RotateTransform(angle, 22, -17));
        dc.DrawGeometry(pal.FurBrush, null, EarShape);
        Grain(dc, EarShape, new Point(24, -30), -70, 0.8);
        dc.DrawGeometry(InnerEarBrush, null, InnerEarShape);
        dc.DrawLine(EarTuftPen, new Point(17, -20), new Point(22, -35));
        dc.DrawLine(EarTuftPen, new Point(21, -19), new Point(26, -33));
        dc.DrawLine(EarTuftPen, new Point(25, -18), new Point(28, -30));
        dc.Pop();
    }

    private static void DrawEye(DrawingContext dc, Point centre, double squeeze, double side, CatPose p, CatPalette pal)
    {
        const double rx = 8.4, ry = 8.4;
        var width = rx * squeeze;
        dc.PushTransform(new RotateTransform(-7 * side, centre.X, centre.Y));
        if (p.EyeOpen < 0.14)
        {
            // Closed: a relaxed "u" when asleep, a happy "n" when content.
            var lift = 4 - 9 * p.EyeHappy;
            var lid = new StreamGeometry();
            using (var ctx = lid.Open())
            {
                ctx.BeginFigure(centre + new Vector(-width * 0.95, 0.5), false, false);
                ctx.QuadraticBezierTo(centre + new Vector(0, 0.5 + lift), centre + new Vector(width * 0.95, 0.5), true, true);
            }

            dc.DrawGeometry(null, LidPen, lid);
            dc.Pop();
            return;
        }

        var top = ry * 1.1 * p.EyeOpen;
        var bottom = ry * 0.95 * p.EyeOpen;
        var eye = new StreamGeometry();
        using (var ctx = eye.Open())
        {
            ctx.BeginFigure(centre + new Vector(-width, 0), true, true);
            ctx.BezierTo(centre + new Vector(-width * 0.55, -top * 1.33), centre + new Vector(width * 0.55, -top * 1.33), centre + new Vector(width, 0), true, true);
            ctx.BezierTo(centre + new Vector(width * 0.55, bottom * 1.33), centre + new Vector(-width * 0.55, bottom * 1.33), centre + new Vector(-width, 0), true, true);
        }

        eye.Freeze();
        dc.PushClip(eye);
        var iris = centre + new Vector(Math.Clamp(p.Look.X, -1, 1) * 2.4, Math.Clamp(p.Look.Y, -1, 1) * 2);
        dc.DrawEllipse(pal.IrisBrush, null, iris, 7.6 * squeeze, 7.6);
        for (var k = 0; k < 14; k++)
        {
            var spoke = new Vector(Math.Cos(k * Math.PI / 7) * squeeze, Math.Sin(k * Math.PI / 7));
            dc.DrawLine(IrisFiber, iris + spoke * 2.6, iris + spoke * 7.2);
        }

        dc.DrawEllipse(null, LimbalPen, iris, 7.2 * squeeze, 7.2);
        var pupil = Math.Clamp(p.Pupil, 0, 1);
        dc.DrawEllipse(InkBrush, null, iris, (1.4 + pupil * 4.4) * squeeze, 6.6);
        dc.DrawEllipse(WhiteBrush, null, iris + new Vector(-2.3, -3.4), 2.1, 2.1);
        dc.DrawEllipse(WhiteBrush, null, iris + new Vector(2.4, 2.8), 0.9, 0.9);
        dc.DrawEllipse(LidShadow, null, centre + new Vector(0, -top * 1.05), width * 1.2, top * 0.55);
        dc.Pop();
        dc.DrawGeometry(null, EyeLinePen, eye);
        dc.Pop();
    }

    /// <summary>Lays the fur grain over <paramref name="shape"/>, with the hairs running along <paramref name="angle"/>.</summary>
    private static void Grain(DrawingContext dc, Geometry shape, Point centre, double angle, double opacity = 1)
    {
        var grain = FurTexture.Grain.Clone();
        grain.Transform = new RotateTransform(angle, centre.X, centre.Y);
        grain.Opacity = opacity;
        grain.Freeze();
        dc.DrawGeometry(grain, null, shape);
    }

    /// <summary>Fills loose-fur tufts; the side of <paramref name="split"/> its vector points away from gets white tufts.</summary>
    private static void DrawFuzz(DrawingContext dc, Geometry hair, Brush furBrush, CatPalette pal, (Point From, Point To, Vector Up)? split)
    {
        if (split is not { } line)
        {
            dc.DrawGeometry(furBrush, null, hair);
            return;
        }

        dc.PushClip(HalfPlane(line.From, line.To, line.Up));
        dc.DrawGeometry(furBrush, null, hair);
        dc.Pop();
        dc.PushClip(HalfPlane(line.From, line.To, -line.Up));
        dc.DrawGeometry(pal.WhiteBrush, null, hair);
        dc.Pop();
    }

    /// <summary>
    /// Small pointed tufts every <paramref name="spacing"/> units along a silhouette, leaning slightly along it,
    /// so edges read as fur instead of a clean cut-out. Tuft sizes come from a hash of their index, so they don't flicker.
    /// </summary>
    private static Geometry HairEdge(Geometry shape, double spacing, double length, int seed)
    {
        var flat = shape.GetFlattenedPathGeometry(0.8, ToleranceType.Absolute);
        var tufts = new StreamGeometry();
        using (var ctx = tufts.Open())
        {
            var index = seed;
            foreach (var figure in flat.Figures)
            {
                var points = new List<Point> { figure.StartPoint };
                foreach (var segment in figure.Segments)
                {
                    if (segment is PolyLineSegment poly)
                    {
                        points.AddRange(poly.Points);
                    }
                    else if (segment is LineSegment straight)
                    {
                        points.Add(straight.Point);
                    }
                }

                points.Add(figure.StartPoint);
                if (points.Count < 4)
                {
                    continue;
                }

                // The winding decides which side of each edge is outside.
                var area = 0.0;
                for (var i = 0; i + 1 < points.Count; i++)
                {
                    area += points[i].X * points[i + 1].Y - points[i + 1].X * points[i].Y;
                }

                var outward = area > 0 ? 1 : -1;
                var carried = 0.0;
                for (var i = 0; i + 1 < points.Count; i++)
                {
                    var edge = points[i + 1] - points[i];
                    var edgeLength = edge.Length;
                    if (edgeLength < 1e-6)
                    {
                        continue;
                    }

                    var tangent = edge / edgeLength;
                    var normal = new Vector(tangent.Y, -tangent.X) * outward;
                    var distance = spacing - carried;
                    for (; distance <= edgeLength; distance += spacing)
                    {
                        var root = points[i] + tangent * distance;
                        var size = length * (0.55 + 0.9 * Hash(index++));
                        var lean = (Hash(index++) - 0.5) * 1.2 + 0.35;
                        ctx.BeginFigure(root - tangent * 1.5 - normal, true, true);
                        ctx.LineTo(root + normal * size + tangent * (size * lean), true, false);
                        ctx.LineTo(root + tangent * 1.5 - normal, true, false);
                    }

                    carried = edgeLength - (distance - spacing);
                }
            }
        }

        tufts.Freeze();
        return tufts;
    }

    private static double Hash(int i)
    {
        var x = Math.Sin(i * 12.9898) * 43758.5453;
        return x - Math.Floor(x);
    }

    private static Geometry HalfPlane(Point a, Point b, Vector towards)
    {
        var direction = b - a;
        direction.Normalize();
        var normal = new Vector(-direction.Y, direction.X);
        if (normal * towards < 0)
        {
            normal = -normal;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(a - direction * 400, true, true);
            ctx.LineTo(b + direction * 400, false, false);
            ctx.LineTo(b + direction * 400 + normal * 400, false, false);
            ctx.LineTo(a - direction * 400 + normal * 400, false, false);
        }

        geometry.Freeze();
        return geometry;
    }

    private static void DrawZzz(DrawingContext dc, Point origin, double amount, double time)
    {
        for (var i = 0; i < 3; i++)
        {
            var phase = (time / 2.8 + i / 3.0) % 1.0;
            var alpha = Math.Sin(phase * Math.PI) * Math.Min(1, amount);
            var size = 10 + phase * 9;
            var position = origin + new Vector(phase * 16 + Math.Sin(phase * 6 + i) * 3, -phase * 42 - size);
            dc.PushOpacity(alpha);
            dc.PushTransform(new TranslateTransform(position.X, position.Y));
            dc.PushTransform(new ScaleTransform(size / ZGlyphSize, size / ZGlyphSize));
            dc.DrawGeometry(null, ZOutline, ZGlyph);
            dc.DrawGeometry(ZBrush, null, ZGlyph);
            dc.Pop();
            dc.Pop();
            dc.Pop();
        }
    }

    private static void DrawHeart(DrawingContext dc, Heart heart)
    {
        const double life = 1.3;
        var t = heart.Age / life;
        if (t is < 0 or > 1)
        {
            return;
        }

        var position = heart.Position + new Vector(Math.Sin(t * 7) * 3, -t * 46);
        var scale = 1.2 + Math.Min(t * 4, 1) * 0.6;
        dc.PushOpacity(t < 0.7 ? 1 : 1 - (t - 0.7) / 0.3);
        dc.PushTransform(new TranslateTransform(position.X, position.Y));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.DrawGeometry(HeartBrush, HeartOutline, HeartShape);
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static void DrawStroke(DrawingContext dc, Brush brush, double width, Point start, Point control, Point end)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(start, false, false);
            ctx.QuadraticBezierTo(control, end, true, true);
        }

        dc.DrawGeometry(null, RoundPen(brush, width), geometry);
    }

    private static Pen RoundPen(Brush brush, double width, bool freeze = false)
    {
        var pen = new Pen(brush, width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        return freeze ? CatPalette.Freeze(pen) : pen;
    }

    private static Brush Solid(Color color) => CatPalette.Freeze(new SolidColorBrush(color));

    /// <summary>The sleep "z", built once from the font; drawing text every frame was the costliest part of sleeping.</summary>
    private static Geometry BuildZ()
    {
        var face = new Typeface(new FontFamily("Segoe UI"), FontStyles.Italic, FontWeights.Black, FontStretches.Normal);
        var text = new FormattedText("z", CultureInfo.InvariantCulture, FlowDirection.LeftToRight, face, ZGlyphSize, Brushes.Black, 1);
        var glyph = text.BuildGeometry(new Point(0, 0));
        glyph.Freeze();
        return glyph;
    }

    /// <summary>A slightly wedge-shaped face with fluffy cheeks.</summary>
    private static Geometry BuildHead()
    {
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            c.BeginFigure(new Point(0, -30), true, true);
            c.BezierTo(new Point(21, -30), new Point(36, -21), new Point(36, -4), true, true);
            c.QuadraticBezierTo(new Point(37, 4), new Point(42, 9), true, false);
            c.QuadraticBezierTo(new Point(36, 12), new Point(33, 15), true, false);
            c.BezierTo(new Point(25, 24), new Point(13, 27), new Point(0, 27), true, true);
            c.BezierTo(new Point(-13, 27), new Point(-25, 24), new Point(-33, 15), true, true);
            c.QuadraticBezierTo(new Point(-36, 12), new Point(-42, 9), true, false);
            c.QuadraticBezierTo(new Point(-37, 4), new Point(-36, -4), true, false);
            c.BezierTo(new Point(-36, -21), new Point(-21, -30), new Point(0, -30), true, true);
        }

        geometry.Freeze();
        return geometry;
    }

    /// <summary>The right ear, large and upright like the reference cat's; the left one is drawn mirrored.</summary>
    private static Geometry BuildEar(bool inner)
    {
        var geometry = new StreamGeometry();
        using (var c = geometry.Open())
        {
            if (inner)
            {
                c.BeginFigure(new Point(14, -21), true, true);
                c.LineTo(new Point(27, -44), true, true);
                c.QuadraticBezierTo(new Point(29.5, -47), new Point(31, -43), true, true);
                c.LineTo(new Point(31, -15), true, true);
            }
            else
            {
                c.BeginFigure(new Point(6, -24), true, true);
                c.LineTo(new Point(26.5, -50), true, true);
                c.QuadraticBezierTo(new Point(31.5, -57), new Point(35, -49), true, true);
                c.LineTo(new Point(37, -9), true, true);
            }
        }

        geometry.Freeze();
        return geometry;
    }
}
