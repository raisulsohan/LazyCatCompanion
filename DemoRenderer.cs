using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;

namespace LazyCatCompanion;

/// <summary>
/// Renders the README demo: the real cat (its own controller and renderer) acting out a scripted 15 seconds on a mock
/// Windows desktop, with a drawn mouse pointer. Writes frame-0000.png… at 30 fps and cues.json, the moments the
/// soundtrack hangs its sounds on (clicks, hearts, take-off, landing…).
/// </summary>
internal static class DemoRenderer
{
    public const int Width = 1280, Height = 720, Fps = 30;
    private const double Duration = 15, CatScale = 1.2, TaskbarHeight = 44;
    private const int SubSteps = 2;
    private const double Ground = Height - TaskbarHeight - 4;

    // The storyboard, in seconds.
    private const double FirstClick = 1.55, WakeClick = 1.75, PetFrom = 4.9, PetTo = 7.0, JumpClick = 7.55;
    private const double Press = 11.3, Grab = 11.45, Release = 13.25, TitleIn = 13.55, PointerOut = 13.9;

    private static readonly Point Corner = new(1110, Ground), Perch = new(250, 300), Drop = new(380, Ground);
    private static readonly Color Ink = Color.FromRgb(46, 36, 30);
    private static readonly Color Accent = Color.FromRgb(0, 120, 212);
    private static readonly FontFamily UiFont = new("Segoe UI Variable Text, Segoe UI");
    private static readonly FontFamily DisplayFont = new("Segoe UI Variable Display, Segoe UI");

    public static void Render(string folder)
    {
        Directory.CreateDirectory(folder);
        var palette = new CatPalette(Color.FromRgb(146, 131, 115), true);
        var desktop = PreviewRenderer.Bake(Desktop(palette), Width, Height);
        desktop.Freeze();

        var cat = new CatController(seed: 7) { IdleActions = false, Scale = CatScale, Anchor = Corner };
        cat.FaceInstantly(1);

        var sleepingHead = Corner + (Vector)(CatPoses.Sleep.Chest + CatPoses.Sleep.Head) * CatScale + new Vector(-6, -4);
        var petSpot = Corner + (Vector)(CatPoses.Sit.Chest + CatPoses.Sit.Head) * CatScale + new Vector(-4, -27);
        var scruff = Perch + new Vector(-4, -112) * CatScale;
        var release = Drop + new Vector(CatPoses.Scruff.X, CatPoses.Scruff.Y) * CatScale;
        var watching = new Point(985, 455);
        var aside = new Point(470, 225);
        var path = new PointerPath(new Point(690, 300))
            .Move(0.5, 1.35, sleepingHead, 50)
            .Move(1.95, 2.7, watching, -30)
            .Move(4.25, PetFrom, petSpot, 30)
            .Move(PetTo, JumpClick - 0.05, Perch, -120)
            .Move(JumpClick + 0.1, 8.3, aside, 20)
            .Move(10.7, 11.25, scruff, 20)
            .Curve(Grab, 13.05, new Point(360, 120), new Point(560, 330), release)
            .Move(13.4, 14.3, new Point(1000, 400), 40);

        Point PointerAt(double t)
        {
            var p = path.At(t);
            if (t is > PetFrom and < PetTo)
            {
                // Stroking: small sweeps back and forth over the head.
                var envelope = Math.Min(Smooth((t - PetFrom) / 0.3), Smooth((PetTo - t) / 0.3));
                var sweep = Math.Sin((t - PetFrom) * Math.PI * 2 / 0.8);
                p += new Vector(sweep * 6, -Math.Abs(sweep) * 2) * envelope;
            }

            return p;
        }

        var cues = new List<Cue>();
        var clicks = new List<(double At, Point Where)>();
        void Cue(double t, string kind, double x) => cues.Add(new Cue(Math.Round(t, 3), kind, Math.Round(x / Width, 3)));
        void Click(double t)
        {
            var where = PointerAt(t);
            clicks.Add((t, where));
            Cue(t, "click", where.X);
        }

        var landings = 0;
        var clock = 0.0;
        cat.Landed += () =>
        {
            Cue(clock, "settle", cat.Anchor.X);

            // Like the app with Face set to Right: after every landing the cat turns back to face right.
            cat.TurnToward(1);
            if (++landings == 1)
            {
                cat.StrikePose(CatPoses.Sitting.First(s => s.Name == "Licking a paw"));
            }
        };

        var script = new List<(double At, Action Do)>
        {
            (FirstClick, () => Click(FirstClick)),
            (WakeClick, () =>
            {
                Click(WakeClick);
                cat.Wake();
                Cue(WakeClick, "wake", cat.Anchor.X);
            }),
            (JumpClick, () =>
            {
                Click(JumpClick);
                cat.TryJump(Perch);
            }),
            (Press, () => Click(Press)),
            (Grab, () =>
            {
                cat.BeginHold(PointerAt(Grab));
                Cue(Grab, "grab", cat.Anchor.X);
            }),
            (Release, () =>
            {
                cat.EndHold();
                Cue(Release, "drop", cat.Anchor.X);
            }),
            (TitleIn, () => Cue(TitleIn, "title", 0.5 * Width))
        };

        var shadow = new DropShadowEffect { Color = Color.FromRgb(30, 20, 16), BlurRadius = 6, ShadowDepth = 1.5, Direction = 270, Opacity = 0.35 };
        var titleShadow = new DropShadowEffect { Color = Color.FromRgb(60, 40, 70), BlurRadius = 22, ShadowDepth = 4, Direction = 270, Opacity = 0.22 };
        shadow.Freeze();
        titleShadow.Freeze();

        var bitmap = new RenderTargetBitmap(Width, Height, 96, 96, PixelFormats.Pbgra32);
        DrawingVisual? lastCat = null;
        var next = 0;
        var lastAnchor = cat.Anchor;
        var airborne = false;
        var lastMouth = 0.0;
        var lastTongue = 0.0;
        var frames = (int)Math.Round(Duration * Fps);
        for (var frame = 0; frame < frames; frame++)
        {
            var steps = frame == 0 ? 1 : SubSteps;
            var dt = frame == 0 ? 0 : 1.0 / (Fps * SubSteps);
            for (var s = 0; s < steps; s++)
            {
                clock += dt;
                while (next < script.Count && script[next].At <= clock + 1e-9)
                {
                    script[next++].Do();
                }

                var pointer = PointerAt(clock);
                if (cat.Mood == CatMood.Held)
                {
                    cat.MoveHold(pointer);
                }

                var over = lastCat is not null && VisualTreeHelper.HitTest(lastCat, pointer) is not null;
                cat.Update(dt, pointer, over);

                if (dt > 0 && cat.Hearts.Any(h => h.Age <= dt + 1e-9))
                {
                    Cue(clock, "heart", cat.Anchor.X);
                }

                var moving = cat.Mood == CatMood.Jumping && (cat.Anchor - lastAnchor).Length > 0.01;
                if (moving != airborne)
                {
                    Cue(clock, moving ? "takeoff" : "touchdown", cat.Anchor.X);
                    airborne = moving;
                }

                if (cat.Pose.MouthOpen > 0.6 && lastMouth <= 0.6 && cat.Mood == CatMood.Waking)
                {
                    Cue(clock, "yawn", cat.Anchor.X);
                }

                if (cat.Pose.Tongue > 0.85 && lastTongue <= 0.85 && cat.Mood == CatMood.Idle)
                {
                    Cue(clock, "lick", cat.Anchor.X);
                }

                lastAnchor = cat.Anchor;
                lastMouth = cat.Pose.MouthOpen;
                lastTongue = cat.Pose.Tongue;
            }

            var t = frame / (double)Fps;
            var scene = new ContainerVisual();
            var back = new DrawingVisual();
            using (var dc = back.RenderOpen())
            {
                dc.DrawImage(desktop, new Rect(0, 0, Width, Height));
            }

            var catVisual = new DrawingVisual { Effect = shadow };
            using (var dc = catVisual.RenderOpen())
            {
                dc.PushTransform(new TranslateTransform(cat.Anchor.X, cat.Anchor.Y));
                dc.PushTransform(new ScaleTransform(CatScale, CatScale));
                CatRenderer.Draw(dc, cat.Pose, palette, cat.Facing, cat.TailPhase, cat.Time, cat.Hearts);
                dc.Pop();
                dc.Pop();
            }

            var title = new DrawingVisual { Effect = titleShadow };
            using (var dc = title.RenderOpen())
            {
                DrawTitle(dc, Smooth((t - TitleIn) / 0.6));
            }

            var front = new DrawingVisual();
            using (var dc = front.RenderOpen())
            {
                foreach (var (at, where) in clicks)
                {
                    DrawRipple(dc, where, t - at);
                }

                var held = t is >= Press and < Release;
                var pressed = held || clicks.Any(c => t - c.At is >= 0 and < 0.1);
                DrawPointer(dc, PointerAt(t), pressed, held, 1 - Smooth((t - PointerOut) / 0.5));
            }

            scene.Children.Add(back);
            scene.Children.Add(catVisual);
            scene.Children.Add(title);
            scene.Children.Add(front);
            bitmap.Clear();
            bitmap.Render(scene);
            PreviewRenderer.Save(bitmap, Path.Combine(folder, $"frame-{frame:0000}.png"));
            lastCat = catVisual;
        }

        var json = JsonSerializer.Serialize(new { fps = Fps, duration = Duration, width = Width, height = Height, cues },
            new JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        File.WriteAllText(Path.Combine(folder, "cues.json"), json);
    }

    // ----- The mock desktop -----

    private static Visual Desktop(CatPalette palette)
    {
        var visual = new DrawingVisual();
        using var dc = visual.RenderOpen();

        dc.DrawRectangle(new LinearGradientBrush(
            [
                new GradientStop(Color.FromRgb(150, 184, 238), 0), new GradientStop(Color.FromRgb(192, 196, 242), 0.36),
                new GradientStop(Color.FromRgb(246, 204, 200), 0.66), new GradientStop(Color.FromRgb(253, 228, 202), 1)
            ], new Point(0, 0), new Point(0, 1)), null, new Rect(0, 0, Width, Height));

        var sun = new Point(930, 300);
        dc.DrawEllipse(new RadialGradientBrush(Color.FromArgb(190, 255, 246, 226), Color.FromArgb(0, 255, 236, 214)), null, sun, 340, 340);
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(240, 255, 250, 238)), null, sun, 44, 44);

        Cloud(dc, new Point(720, 128), 0.85);
        Cloud(dc, new Point(1080, 214), 0.7);
        Cloud(dc, new Point(520, 250), 0.55);
        PerchCloud(dc);

        Hill(dc, 470, 22, 160, 0.4, Color.FromRgb(196, 196, 228), Color.FromRgb(214, 204, 226));
        Hill(dc, 520, 26, 210, 2.1, Color.FromRgb(160, 200, 176), Color.FromRgb(176, 208, 182));
        Hill(dc, 590, 20, 260, 4.0, Color.FromRgb(124, 178, 140), Color.FromRgb(108, 164, 128));
        Flowers(dc);

        DesktopIcon(dc, 0, "Recycle Bin", RecycleBin);
        DesktopIcon(dc, 1, "Documents", c => Folder(c, new Point(0, 0), 1.6));
        DesktopIcon(dc, 2, "Lazy Cat", c =>
        {
            c.PushTransform(new TranslateTransform(0, 4));
            c.PushTransform(new ScaleTransform(0.52, 0.52));
            CatRenderer.DrawHeadOnly(c, CatPoses.Sit, palette);
            c.Pop();
            c.Pop();
            ShortcutArrow(c, new Point(-15, 15));
        });

        Taskbar(dc, palette);
        return visual;
    }

    private static void Hill(DrawingContext dc, double baseY, double amplitude, double wave, double phase, Color top, Color bottom)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(0, Height), true, true);
            for (var x = 0; x <= Width; x += 8)
            {
                ctx.LineTo(new Point(x, HillY(x, baseY, amplitude, wave, phase)), true, true);
            }

            ctx.LineTo(new Point(Width, Height), true, true);
        }

        dc.DrawGeometry(new LinearGradientBrush(top, bottom, 90), null, geometry);
    }

    private static double HillY(double x, double baseY, double amplitude, double wave, double phase) =>
        baseY + amplitude * Math.Sin(x / wave + phase) + amplitude * 0.45 * Math.Sin(x / (wave * 0.43) + phase * 1.7);

    /// <summary>A scatter of tiny flowers on the nearest hill.</summary>
    private static void Flowers(DrawingContext dc)
    {
        var random = new Random(3);
        Brush[] petals =
        [
            new SolidColorBrush(Color.FromArgb(220, 255, 255, 255)), new SolidColorBrush(Color.FromArgb(220, 255, 196, 214)),
            new SolidColorBrush(Color.FromArgb(220, 255, 230, 150))
        ];
        for (var i = 0; i < 70; i++)
        {
            var x = random.NextDouble() * Width;
            var y = HillY(x, 590, 20, 260, 4.0) + 12 + random.NextDouble() * (Ground - HillY(x, 590, 20, 260, 4.0) - 10);
            var r = 1.6 + random.NextDouble() * 1.6;
            dc.DrawEllipse(petals[random.Next(petals.Length)], null, new Point(x, y), r, r);
        }
    }

    private static void Cloud(DrawingContext dc, Point c, double s)
    {
        var puffs = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var (x, y, rx, ry) in new[] { (0.0, 0.0, 62.0, 24.0), (-44, 4, 36, 20), (46, 5, 40, 19), (-14, -16, 34, 26), (20, -12, 30, 22) })
        {
            puffs.Children.Add(new EllipseGeometry(new Point(c.X + x * s, c.Y + y * s), rx * s, ry * s));
        }

        dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(235, 255, 255, 255), Color.FromArgb(225, 236, 230, 250), 90), null, puffs);
    }

    /// <summary>The cloud the cat jumps onto: flat on top, so its feet rest on it at <see cref="Perch"/>.</summary>
    private static void PerchCloud(DrawingContext dc)
    {
        var puffs = new GeometryGroup { FillRule = FillRule.Nonzero };
        foreach (var (x, y, rx, ry) in new[] { (0.0, 22.0, 84.0, 22.0), (-62, 26, 38, 20), (64, 28, 40, 18), (-30, 30, 50, 22), (30, 32, 52, 20) })
        {
            puffs.Children.Add(new EllipseGeometry(new Point(Perch.X + 6 + x, Perch.Y + y), rx, ry));
        }

        dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(240, 255, 255, 255), Color.FromArgb(230, 234, 228, 250), 90), null, puffs);
    }

    private static void DesktopIcon(DrawingContext dc, int slot, string label, Action<DrawingContext> glyph)
    {
        var centre = new Point(54, 50 + slot * 92);
        dc.PushTransform(new TranslateTransform(centre.X, centre.Y));
        glyph(dc);
        dc.Pop();
        var text = Text(label, 12.5, Brushes.White, UiFont, FontWeights.Normal);
        var origin = new Point(centre.X - text.Width / 2, centre.Y + 28);
        var shade = Text(label, 12.5, new SolidColorBrush(Color.FromArgb(150, 30, 30, 50)), UiFont, FontWeights.Normal);
        dc.DrawText(shade, origin + new Vector(0.8, 1.2));
        dc.DrawText(text, origin);
    }

    private static void RecycleBin(DrawingContext dc)
    {
        var bin = Geometry.Parse("M -15,-14 L 15,-14 L 12,20 Q 11.5,23 8,23 L -8,23 Q -11.5,23 -12,20 Z");
        dc.DrawGeometry(new LinearGradientBrush(Color.FromArgb(220, 232, 240, 250), Color.FromArgb(200, 180, 196, 214), 0), new Pen(new SolidColorBrush(Color.FromRgb(120, 136, 156)), 1.2), bin);
        var line = new Pen(new SolidColorBrush(Color.FromArgb(150, 110, 126, 146)), 1.4);
        foreach (var x in new[] { -6.0, 0, 6 })
        {
            dc.DrawLine(line, new Point(x, -8), new Point(x * 0.85, 18));
        }

        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(196, 208, 222)), new Pen(new SolidColorBrush(Color.FromRgb(120, 136, 156)), 1.2), new Rect(-18, -20, 36, 6), 3, 3);
    }

    private static void Folder(DrawingContext dc, Point c, double s)
    {
        dc.PushTransform(new TranslateTransform(c.X, c.Y));
        dc.PushTransform(new ScaleTransform(s, s));
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(232, 168, 40)), null, Geometry.Parse("M -12,-8 L -4,-8 L -1,-5 L 12,-5 L 12,9 L -12,9 Z"));
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(255, 214, 102), Color.FromRgb(250, 190, 60), 90), null, new Rect(-12, -3, 24, 12), 1.5, 1.5);
        dc.Pop();
        dc.Pop();
    }

    private static void ShortcutArrow(DrawingContext dc, Point at)
    {
        dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(150, 150, 160)), 0.8), new Rect(at.X - 6, at.Y - 6, 12, 12), 2, 2);
        var arrow = new Pen(new SolidColorBrush(Color.FromRgb(30, 90, 190)), 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        dc.DrawLine(arrow, at + new Vector(-3, 3), at + new Vector(3, -3));
        dc.DrawLine(arrow, at + new Vector(-0.5, -3), at + new Vector(3, -3));
        dc.DrawLine(arrow, at + new Vector(3, 0.5), at + new Vector(3, -3));
    }

    private static void Taskbar(DrawingContext dc, CatPalette palette)
    {
        var top = Height - TaskbarHeight;
        var mid = top + TaskbarHeight / 2;
        dc.DrawRectangle(new SolidColorBrush(Color.FromArgb(226, 240, 240, 246)), null, new Rect(0, top, Width, TaskbarHeight));
        dc.DrawLine(new Pen(new SolidColorBrush(Color.FromArgb(40, 0, 0, 30)), 1), new Point(0, top + 0.5), new Point(Width, top + 0.5));

        var dark = new SolidColorBrush(Color.FromRgb(50, 52, 62));
        var darkPen = new Pen(dark, 1.8) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };

        // Start, search, files, browser, notes and mail, centred like Windows 11.
        var x0 = Width / 2.0 - 2.5 * 42;
        var startBrush = new LinearGradientBrush(Color.FromRgb(0, 120, 212), Color.FromRgb(40, 170, 240), 45);
        foreach (var (dx, dy) in new[] { (-5.5, -5.5), (5.5, -5.5), (-5.5, 5.5), (5.5, 5.5) })
        {
            dc.DrawRoundedRectangle(startBrush, null, new Rect(x0 + dx - 4.8, mid + dy - 4.8, 9.6, 9.6), 1.4, 1.4);
        }

        var search = new Point(x0 + 42, mid);
        dc.DrawEllipse(null, darkPen, search + new Vector(-2, -2), 6, 6);
        dc.DrawLine(darkPen, search + new Vector(2.5, 2.5), search + new Vector(7, 7));

        Folder(dc, new Point(x0 + 84, mid), 0.95);
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromRgb(120, 124, 140)), null, new Rect(x0 + 84 - 5, top + TaskbarHeight - 5, 10, 3), 1.5, 1.5);

        var globe = new Point(x0 + 126, mid);
        dc.DrawEllipse(new LinearGradientBrush(Color.FromRgb(46, 196, 182), Color.FromRgb(28, 110, 214), 60), null, globe, 10.5, 10.5);
        var meridian = new Pen(new SolidColorBrush(Color.FromArgb(170, 255, 255, 255)), 1.2);
        dc.DrawEllipse(null, meridian, globe, 4.5, 10);
        dc.DrawLine(meridian, globe + new Vector(-10, 0), globe + new Vector(10, 0));

        var notes = new Rect(x0 + 168 - 8.5, mid - 10.5, 17, 21);
        dc.DrawRoundedRectangle(Brushes.White, new Pen(new SolidColorBrush(Color.FromRgb(170, 176, 190)), 1), notes, 2, 2);
        dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(120, 92, 214)), null, new Rect(notes.X, notes.Y, notes.Width, 5));
        var rule = new Pen(new SolidColorBrush(Color.FromRgb(190, 194, 206)), 1);
        foreach (var y in new[] { 9.0, 13, 17 })
        {
            dc.DrawLine(rule, new Point(notes.X + 3.5, notes.Y + y), new Point(notes.Right - 3.5, notes.Y + y));
        }

        var mail = new Rect(x0 + 210 - 11, mid - 7.5, 22, 15);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(16, 132, 230), Color.FromRgb(0, 90, 180), 90), null, mail, 2, 2);
        var flap = new Pen(Brushes.White, 1.4) { LineJoin = PenLineJoin.Round };
        dc.DrawGeometry(null, flap, new PathGeometry([new PathFigure(mail.TopLeft + new Vector(2, 2), [new PolyLineSegment([new Point(mail.X + 11, mid + 1), mail.TopRight + new Vector(-2, 2)], true)], false)]));

        // The tray: overflow chevron, the cat's own tray icon, Wi-Fi, volume and the clock.
        var chevron = new Point(1084, mid);
        dc.DrawLine(darkPen, chevron + new Vector(-4, 2), chevron + new Vector(0, -2));
        dc.DrawLine(darkPen, chevron + new Vector(0, -2), chevron + new Vector(4, 2));

        dc.PushTransform(new TranslateTransform(1112, mid + 2));
        dc.PushTransform(new ScaleTransform(0.25, 0.25));
        CatRenderer.DrawHeadOnly(dc, CatPoses.Sit, palette);
        dc.Pop();
        dc.Pop();

        var wifi = new Point(1142, mid + 6);
        var arcPen = new Pen(dark, 1.6) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        foreach (var r in new[] { 5.0, 9.5, 14 })
        {
            dc.DrawGeometry(null, arcPen, new PathGeometry([new PathFigure(wifi + new Vector(-r * 0.7, -r * 0.7),
                [new ArcSegment(wifi + new Vector(r * 0.7, -r * 0.7), new Size(r, r), 0, false, SweepDirection.Clockwise, true)], false)]));
        }

        dc.DrawEllipse(dark, null, wifi + new Vector(0, -1), 1.6, 1.6);

        var speaker = new Point(1170, mid);
        dc.DrawGeometry(dark, null, Geometry.Parse($"M {speaker.X - 7},{speaker.Y - 3} L {speaker.X - 3},{speaker.Y - 3} L {speaker.X + 2},{speaker.Y - 7.5} L {speaker.X + 2},{speaker.Y + 7.5} L {speaker.X - 3},{speaker.Y + 3} L {speaker.X - 7},{speaker.Y + 3} Z"));
        dc.DrawGeometry(null, arcPen, new PathGeometry([new PathFigure(speaker + new Vector(5, -4), [new ArcSegment(speaker + new Vector(5, 4), new Size(5, 5), 0, false, SweepDirection.Clockwise, true)], false)]));

        var time = Text("9:41 AM", 12, dark, UiFont, FontWeights.Normal);
        var date = Text("10/10/2026", 12, dark, UiFont, FontWeights.Normal);
        dc.DrawText(time, new Point(Width - 14 - time.Width, mid - 17));
        dc.DrawText(date, new Point(Width - 14 - date.Width, mid + 1));
    }

    // ----- Overlays: title card, click ripples and the pointer -----

    private static void DrawTitle(DrawingContext dc, double appear)
    {
        if (appear <= 0)
        {
            return;
        }

        var lift = Back(appear);
        var title = Text("Lazy Cat Companion", 50, new SolidColorBrush(Ink), DisplayFont, FontWeights.Bold);
        var badge = Text("Free for Windows", 19, Brushes.White, UiFont, FontWeights.SemiBold);
        var card = new Rect(482, 500, title.Width + 64, 146);
        dc.PushOpacity(Math.Min(1, appear * 1.6));
        dc.PushTransform(new TranslateTransform(0, (1 - lift) * 22));
        dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(232, 255, 252, 248)), null, card, 26, 26);
        dc.DrawText(title, new Point(card.X + 32, card.Y + 20));
        var pill = new Rect(card.X + 34, card.Y + 92, badge.Width + 34, 34);
        dc.DrawRoundedRectangle(new LinearGradientBrush(Color.FromRgb(126, 172, 84), Color.FromRgb(98, 146, 66), 90), null, pill, 17, 17);
        dc.DrawText(badge, new Point(pill.X + 17, pill.Y + (pill.Height - badge.Height) / 2));
        var heart = Geometry.Parse("M 0,4 C -7,-1 -5,-8 0,-4 C 5,-8 7,-1 0,4 Z");
        dc.PushTransform(new TranslateTransform(pill.Right + 22, pill.Y + 18));
        dc.PushTransform(new ScaleTransform(1.7, 1.7));
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(242, 92, 124)), null, heart);
        dc.Pop();
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    private static void DrawRipple(DrawingContext dc, Point at, double age)
    {
        const double life = 0.45;
        if (age is < 0 or > life)
        {
            return;
        }

        var f = age / life;
        var radius = 6 + 26 * (1 - Math.Pow(1 - f, 3));
        var alpha = (byte)(230 * (1 - f));
        dc.DrawEllipse(new SolidColorBrush(Color.FromArgb((byte)(alpha / 5), Accent.R, Accent.G, Accent.B)),
            new Pen(new SolidColorBrush(Color.FromArgb(alpha, Accent.R, Accent.G, Accent.B)), 3), at, radius, radius);
    }

    private static readonly Geometry Arrow = Geometry.Parse("M 0,0 L 0,17 L 4,13.2 L 6.8,19.6 L 9.6,18.5 L 6.9,12.2 L 12.2,12.2 Z");

    private static void DrawPointer(DrawingContext dc, Point at, bool pressed, bool held, double opacity)
    {
        if (opacity <= 0)
        {
            return;
        }

        dc.PushOpacity(opacity);
        if (held)
        {
            dc.DrawEllipse(new SolidColorBrush(Color.FromArgb(70, Accent.R, Accent.G, Accent.B)), null, at, 11, 11);
        }

        var scale = 1.3 * (pressed ? 0.88 : 1);
        dc.PushTransform(new TranslateTransform(at.X, at.Y));
        dc.PushTransform(new ScaleTransform(scale, scale));
        dc.PushTransform(new TranslateTransform(1.1, 1.6));
        dc.DrawGeometry(new SolidColorBrush(Color.FromArgb(70, 0, 0, 0)), null, Arrow);
        dc.Pop();
        dc.DrawGeometry(Brushes.White, new Pen(Brushes.Black, 1.05) { LineJoin = PenLineJoin.Round }, Arrow);
        dc.Pop();
        dc.Pop();
        dc.Pop();
    }

    // ----- Helpers -----

    private static FormattedText Text(string text, double size, Brush brush, FontFamily family, FontWeight weight) =>
        new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(family, FontStyles.Normal, weight, FontStretches.Normal), size, brush, 1);

    private static double Smooth(double t)
    {
        t = Math.Clamp(t, 0, 1);
        return t * t * (3 - 2 * t);
    }

    /// <summary>Ease-out with a little overshoot, for the title card settling into place.</summary>
    private static double Back(double t)
    {
        t = Math.Clamp(t, 0, 1) - 1;
        return 1 + 2.7 * t * t * t + 1.7 * t * t;
    }

    private sealed record Cue(double T, string Kind, double X);

    /// <summary>The pointer's route: eased moves along gentle arcs (as a hand moves a mouse), resting between them.</summary>
    private sealed class PointerPath(Point start)
    {
        private readonly List<(double From, double To, Func<double, Point> At)> _moves = [];
        private readonly Point _start = start;
        private Point _end = start;

        public PointerPath Move(double from, double to, Point target, double bend)
        {
            var a = _end;
            var normal = new Vector(-(target - a).Y, (target - a).X);
            if (normal.Length > 0)
            {
                normal.Normalize();
            }

            var control = a + (target - a) / 2 + normal * bend;
            _moves.Add((from, to, f => Bezier(a, control, control, target, f)));
            _end = target;
            return this;
        }

        public PointerPath Curve(double from, double to, Point c1, Point c2, Point target)
        {
            var a = _end;
            _moves.Add((from, to, f => Bezier(a, c1, c2, target, f)));
            _end = target;
            return this;
        }

        public Point At(double t)
        {
            var position = _start;
            foreach (var (from, to, at) in _moves)
            {
                if (t < from)
                {
                    return position;
                }

                if (t < to)
                {
                    var f = (t - from) / (to - from);
                    return at(f < 0.5 ? 4 * f * f * f : 1 - Math.Pow(-2 * f + 2, 3) / 2);
                }

                position = at(1);
            }

            return position;
        }

        private static Point Bezier(Point a, Point b, Point c, Point d, double f)
        {
            var u = 1 - f;
            return new Point(
                u * u * u * a.X + 3 * u * u * f * b.X + 3 * u * f * f * c.X + f * f * f * d.X,
                u * u * u * a.Y + 3 * u * u * f * b.Y + 3 * u * f * f * c.Y + f * f * f * d.Y);
        }
    }
}
