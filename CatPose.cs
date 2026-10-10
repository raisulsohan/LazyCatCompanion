using System.Windows;

namespace LazyCatCompanion;

/// <summary>
/// Every adjustable part of the drawn cat. Poses blend field by field, so any two poses morph smoothly.
/// Cat space: the cat faces +x, y points down, and the origin is the ground point under the cat.
/// </summary>
internal sealed class CatPose
{
    public Vector Hip, Chest;
    public double HipRadius, ChestRadius, Arch, Haunch = 1, LegLength = 1;

    /// <summary>Head centre, relative to the chest centre.</summary>
    public Vector Head;
    public double HeadTilt, HeadTurn = 0.25, EarAngle, EarNear, EarFar;
    public double EyeOpen = 1, EyeHappy, Pupil = 0.5, MouthOpen, Tongue;

    /// <summary>Above 0.5 the near front paw is drawn over the face, as when it is held up to be licked.</summary>
    public double PawInFront;
    public Vector Look;

    /// <summary>Where each foot is placed; legs bend to reach them.</summary>
    public Vector FrontNear, FrontFar, BackNear, BackFar;

    /// <summary>Tail angles in degrees, clockwise from +x (90 = down, 180 = back, 270 = up).</summary>
    public double TailBase, TailCurl, TailWag, TailFront, TailEase = 1.5, TailLength = 1;

    public double Breath, Squash = 1, Rotation, Shadow = 1, Sleepy;

    public CatPose Clone() => (CatPose)MemberwiseClone();

    public CatPose With(Action<CatPose> change)
    {
        var copy = Clone();
        change(copy);
        return copy;
    }

    public static CatPose Lerp(CatPose a, CatPose b, double t) => new()
    {
        Hip = L(a.Hip, b.Hip, t),
        Chest = L(a.Chest, b.Chest, t),
        HipRadius = L(a.HipRadius, b.HipRadius, t),
        ChestRadius = L(a.ChestRadius, b.ChestRadius, t),
        Arch = L(a.Arch, b.Arch, t),
        Haunch = L(a.Haunch, b.Haunch, t),
        LegLength = L(a.LegLength, b.LegLength, t),
        Head = L(a.Head, b.Head, t),
        HeadTilt = L(a.HeadTilt, b.HeadTilt, t),
        HeadTurn = L(a.HeadTurn, b.HeadTurn, t),
        EarAngle = L(a.EarAngle, b.EarAngle, t),
        EarNear = L(a.EarNear, b.EarNear, t),
        EarFar = L(a.EarFar, b.EarFar, t),
        EyeOpen = L(a.EyeOpen, b.EyeOpen, t),
        EyeHappy = L(a.EyeHappy, b.EyeHappy, t),
        Pupil = L(a.Pupil, b.Pupil, t),
        MouthOpen = L(a.MouthOpen, b.MouthOpen, t),
        Tongue = L(a.Tongue, b.Tongue, t),
        PawInFront = L(a.PawInFront, b.PawInFront, t),
        Look = L(a.Look, b.Look, t),
        FrontNear = L(a.FrontNear, b.FrontNear, t),
        FrontFar = L(a.FrontFar, b.FrontFar, t),
        BackNear = L(a.BackNear, b.BackNear, t),
        BackFar = L(a.BackFar, b.BackFar, t),
        TailBase = L(a.TailBase, b.TailBase, t),
        TailCurl = L(a.TailCurl, b.TailCurl, t),
        TailWag = L(a.TailWag, b.TailWag, t),
        TailFront = L(a.TailFront, b.TailFront, t),
        TailEase = L(a.TailEase, b.TailEase, t),
        TailLength = L(a.TailLength, b.TailLength, t),
        Breath = L(a.Breath, b.Breath, t),
        Squash = L(a.Squash, b.Squash, t),
        Rotation = L(a.Rotation, b.Rotation, t),
        Shadow = L(a.Shadow, b.Shadow, t),
        Sleepy = L(a.Sleepy, b.Sleepy, t)
    };

    private static double L(double a, double b, double t) => a + (b - a) * t;
    private static Vector L(Vector a, Vector b, double t) => a + (b - a) * t;
}

/// <summary>A way of sitting. Rest styles are held until the next change; the others are gestures that return to the rest.</summary>
internal sealed record SitStyle(string Name, CatPose Pose, bool Rest, bool Grooming = false);

/// <summary>The key poses the animations travel between.</summary>
internal static class CatPoses
{
    private static Vector V(double x, double y) => new(x, y);

    public static readonly CatPose Sit = new()
    {
        Hip = V(-16, -27), HipRadius = 28, Chest = V(9, -64), ChestRadius = 22, Haunch = 1.1, LegLength = 0.9,
        Head = V(6, -42), Pupil = 0.45,
        FrontNear = V(20, 0), FrontFar = V(11, 0), BackNear = V(2, 0), BackFar = V(-8, 0),
        TailBase = 10, TailCurl = -160, TailEase = 5, TailFront = 1, TailWag = 2, Breath = 0.5
    };

    public static readonly CatPose Stand = new()
    {
        Hip = V(-24, -60), HipRadius = 24, Chest = V(22, -62), ChestRadius = 23, Haunch = 0.95,
        Head = V(16, -28),
        FrontNear = V(30, 0), FrontFar = V(20, 0), BackNear = V(-24, 0), BackFar = V(-34, 0),
        TailBase = 220, TailCurl = 70, TailWag = 10, Breath = 0.4
    };

    public static readonly CatPose Sleep = new()
    {
        Hip = V(-18, -25), HipRadius = 26, Chest = V(16, -21), ChestRadius = 21, Arch = 7, Haunch = 1.15, LegLength = 0.55,
        Head = V(20, -10), HeadTilt = 14, HeadTurn = 0.15, EarAngle = 8, EyeOpen = 0,
        FrontNear = V(46, -3), FrontFar = V(40, -2), BackNear = V(6, -2), BackFar = V(0, -2),
        TailBase = 100, TailCurl = -112, TailWag = 2, TailFront = 1, TailEase = 0.5, Breath = 1.2, Sleepy = 1
    };

    /// <summary>Sphinx: lying with the head up and the front paws stretched out.</summary>
    public static readonly CatPose Loaf = Sleep.With(p =>
    {
        p.Head = V(18, -28);
        p.HeadTilt = -2;
        p.HeadTurn = 0.3;
        p.EarAngle = 2;
        p.EyeOpen = 0.6;
        p.Breath = 0.8;
        p.Sleepy = 0;
    });

    public static readonly CatPose Stretch = new()
    {
        Hip = V(-26, -58), HipRadius = 25, Chest = V(24, -24), ChestRadius = 21, Arch = -8, Haunch = 0.95,
        Head = V(18, -22), HeadTilt = -14, EarAngle = -6, EyeOpen = 0, EyeHappy = 1, MouthOpen = 0.9,
        FrontNear = V(60, 0), FrontFar = V(50, 0), BackNear = V(-24, 0), BackFar = V(-34, 0),
        TailBase = 262, TailCurl = 30, TailWag = 4
    };

    public static readonly CatPose Crouch = new()
    {
        Hip = V(-24, -34), HipRadius = 25, Chest = V(18, -24), ChestRadius = 22, Arch = 3, LegLength = 0.8,
        Head = V(20, -14), EarAngle = -10, Pupil = 1,
        FrontNear = V(34, 0), FrontFar = V(26, 0), BackNear = V(-12, 0), BackFar = V(-20, 0),
        TailBase = 178, TailCurl = -10, TailWag = 6
    };

    public static readonly CatPose Leap = new()
    {
        Hip = V(-30, -52), HipRadius = 23, Chest = V(26, -58), ChestRadius = 21, Arch = -3,
        Head = V(18, -20), EarAngle = 10, Pupil = 0.9, MouthOpen = 0.45,
        FrontNear = V(80, -40), FrontFar = V(74, -34), BackNear = V(-80, -30), BackFar = V(-72, -38),
        TailBase = 188, TailCurl = -14, Shadow = 0
    };

    public static readonly CatPose Land = new()
    {
        Hip = V(-26, -70), HipRadius = 23, Chest = V(18, -50), ChestRadius = 22,
        Head = V(16, -24), EarAngle = 4, Pupil = 0.8,
        FrontNear = V(34, 0), FrontFar = V(26, -4), BackNear = V(-46, -32), BackFar = V(-52, -36),
        TailBase = 215, TailCurl = 50, Shadow = 0
    };

    public static readonly CatPose Squash = new()
    {
        Hip = V(-22, -30), HipRadius = 26, Chest = V(16, -30), ChestRadius = 23, LegLength = 0.75,
        Head = V(18, -18), Pupil = 0.8,
        FrontNear = V(32, 0), FrontFar = V(24, 0), BackNear = V(-14, 0), BackFar = V(-22, 0),
        TailBase = 230, TailCurl = 60, Squash = 0.88
    };

    /// <summary>Dangling by the scruff while dragged.</summary>
    public static readonly CatPose Held = new()
    {
        Hip = V(-2, -82), HipRadius = 23, Chest = V(0, -130), ChestRadius = 21, Haunch = 0.9, LegLength = 0.9,
        Head = V(4, -32), EarAngle = 12, Pupil = 0.8,
        FrontNear = V(10, -86), FrontFar = V(2, -88), BackNear = V(8, -38), BackFar = V(-6, -40),
        TailBase = 95, TailCurl = 25, TailWag = 6, Shadow = 0
    };

    public static readonly CatPose Yawn = Sit.With(p =>
    {
        p.Head = V(6, -44);
        p.HeadTilt = -10;
        p.EarAngle = 6;
        p.EyeOpen = 0;
        p.EyeHappy = 0.9;
        p.MouthOpen = 1;
    });

    public static readonly CatPose Curious = Sit.With(p =>
    {
        p.HeadTilt = 16;
        p.EarNear = -10;
        p.EarFar = 8;
        p.Pupil = 0.8;
    });

    // ----- Ways of sitting -----

    /// <summary>Sitting tall with the neck stretched and the ears forward.</summary>
    public static readonly CatPose SitAlert = Sit.With(p =>
    {
        p.Chest = V(12, -74);
        p.ChestRadius = 21;
        p.Head = V(8, -44);
        p.EarAngle = -9;
        p.Pupil = 0.75;
        p.TailWag = 3;
    });

    /// <summary>Hunkered down: shoulders hunched, head sunk, eyes half closed.</summary>
    public static readonly CatPose Hunched = new()
    {
        Hip = V(-14, -26), HipRadius = 28, Chest = V(8, -46), ChestRadius = 24, Arch = 6, Haunch = 1.15, LegLength = 0.72,
        Head = V(12, -26), HeadTilt = 6, EarAngle = 8, EyeOpen = 0.45, Pupil = 0.35,
        FrontNear = V(18, 0), FrontFar = V(12, 0), BackNear = V(2, 0), BackFar = V(-6, 0),
        TailBase = 15, TailCurl = -180, TailEase = 5, TailFront = 1, TailWag = 1, Breath = 0.7
    };

    /// <summary>The bread loaf: lying with every paw tucked out of sight.</summary>
    public static readonly CatPose LoafTucked = new()
    {
        Hip = V(-18, -25), HipRadius = 26, Chest = V(14, -25), ChestRadius = 23, Arch = 8, Haunch = 1.1, LegLength = 0.42,
        Head = V(18, -28), HeadTurn = 0.35, EarAngle = 3, EyeOpen = 0.7, Pupil = 0.4,
        FrontNear = V(24, -3), FrontFar = V(19, -3), BackNear = V(2, -3), BackFar = V(-4, -3),
        TailBase = 100, TailCurl = -112, TailEase = 0.5, TailFront = 1, TailWag = 1.5, Breath = 0.9
    };

    /// <summary>Sitting on its bottom like a person, hind legs stretched out in front (the photo).</summary>
    public static readonly CatPose Lounge = new()
    {
        Hip = V(-10, -24), HipRadius = 26, Chest = V(-4, -70), ChestRadius = 21, Arch = -2, Haunch = 1.05, LegLength = 0.85,
        Head = V(5, -40), HeadTurn = 0.6, EarAngle = 2, EyeOpen = 0.85, Pupil = 0.35,
        FrontNear = V(12, -8), FrontFar = V(6, -4), BackNear = V(46, -3), BackFar = V(36, -2),
        TailBase = 172, TailCurl = -8, TailWag = 3, Breath = 0.5
    };

    /// <summary>A lazy slouch, leaning back with one hind leg poking out.</summary>
    public static readonly CatPose Slouch = new()
    {
        Hip = V(-12, -22), HipRadius = 28, Chest = V(6, -56), ChestRadius = 22, Arch = -4, Haunch = 1.2, LegLength = 0.85,
        Head = V(6, -38), HeadTilt = 8, HeadTurn = 0.4, EarAngle = 5, EyeOpen = 0.7, Pupil = 0.4,
        FrontNear = V(20, 0), FrontFar = V(10, 0), BackNear = V(28, -3), BackFar = V(-14, 0),
        TailBase = 170, TailCurl = -20, TailWag = 4, Breath = 0.6
    };

    /// <summary>Sitting up on the haunches, front paws held in front of the chest.</summary>
    public static readonly CatPose Meerkat = new()
    {
        Hip = V(-6, -26), HipRadius = 26, Chest = V(0, -82), ChestRadius = 21, Haunch = 1.1, LegLength = 0.85,
        Head = V(4, -40), EarAngle = -8, Pupil = 0.8,
        FrontNear = V(14, -64), FrontFar = V(8, -62), BackNear = V(10, 0), BackFar = V(0, 0),
        TailBase = 175, TailCurl = -10, TailWag = 4, Breath = 0.5
    };

    public static readonly CatPose PawRaised = Sit.With(p =>
    {
        p.FrontNear = V(32, -26);
        p.HeadTilt = 12;
        p.Pupil = 0.8;
    });

    /// <summary>Sitting and licking a front paw.</summary>
    public static readonly CatPose PawLick = Sit.With(p =>
    {
        p.FrontNear = V(26, -72);
        p.PawInFront = 1;
        p.Head = V(6, -30);
        p.HeadTilt = 20;
        p.EyeOpen = 0.3;
        p.MouthOpen = 0.3;
        p.Tongue = 1;
    });

    /// <summary>The "cello" grooming pose: one hind leg straight up, head bowed to wash.</summary>
    public static readonly CatPose LegUp = new()
    {
        Hip = V(-12, -24), HipRadius = 27, Chest = V(6, -52), ChestRadius = 22, Arch = 6, Haunch = 1.05,
        Head = V(16, 4), HeadTilt = 26, EyeOpen = 0.3, MouthOpen = 0.25, Tongue = 1,
        FrontNear = V(16, 0), FrontFar = V(8, 0), BackNear = V(-22, -115), BackFar = V(-4, 0),
        TailBase = 175, TailCurl = -10, TailWag = 2, Breath = 0.5
    };

    public static readonly SitStyle[] Sitting =
    [
        new("Upright", Sit, true),
        new("Alert", SitAlert, false),
        new("Hunched", Hunched, true),
        new("Loaf (paws tucked)", LoafTucked, true),
        new("Sphinx (paws out)", Loaf, true),
        new("Lounging like a person", Lounge, true),
        new("Slouched", Slouch, true),
        new("Meerkat", Meerkat, false),
        new("Paw raised", PawRaised, false),
        new("Licking a paw", PawLick, false, Grooming: true),
        new("Leg-up grooming", LegUp, false, Grooming: true)
    ];

    /// <summary>The scruff, in Held-pose cat space: the point that stays under the cursor while dragging.</summary>
    public static readonly Vector Scruff = V(-6, -150);
}
