using System.Windows;

namespace LazyCatCompanion;

internal enum CatMood
{
    Sleeping,
    Waking,
    Idle,
    Jumping,
    Dozing,
    Held
}

/// <summary>
/// The cat's behaviour: plays pose clips (wake, jump, doze…) and layers small life signs on top
/// (breathing, blinking, ear twitches, tail swish, following the cursor, purring when petted).
/// Positions are in screen DIPs; <see cref="Anchor"/> is the ground point under the cat.
/// </summary>
internal sealed class CatController
{
    private const double TurnDuration = 0.26, HeartInterval = 0.5, HeartLife = 1.3;
    private const double IdleBeforeLoaf = 60, IdleBeforeSleep = 90;
    private static readonly CatPose RestingLoaf = CatPoses.Loaf.With(p => p.EyeOpen = 0.75);

    private readonly Random _random;
    private readonly List<Heart> _hearts = [];
    private CatPose _raw = CatPoses.Sleep.Clone();
    private CatPose _rest = CatPoses.Sleep;
    private CatPose _clipFrom = CatPoses.Sleep;
    private Clip? _clip;
    private double _clipTime;
    private double _breathPhase, _blinkStart = -10, _blinkAt = 2, _twitchStart = -10, _twitchAt = 4;
    private bool _twitchNear;
    private double _idleSeconds, _idleActionAt = 12, _petSeconds, _heartAt, _happy, _hunt, _swing;
    private Vector _look;
    private double _facingTarget = 1, _turnFrom = 1, _turnStart = -10;
    private bool _sleepAfterLanding;
    private Point _holdCursor, _lastHoldCursor;
    private SitStyle? _pendingStyle;

    /// <summary>A <paramref name="seed"/> makes blinks, twitches and idle choices repeat exactly (the demo render uses one).</summary>
    public CatController(int? seed = null) => _random = seed is { } s ? new Random(s) : new Random();

    /// <summary>Off for scripted runs, so the cat never changes how it sits or yawns on its own.</summary>
    public bool IdleActions { get; init; } = true;

    public CatMood Mood { get; private set; } = CatMood.Sleeping;
    public CatPose Pose { get; private set; } = CatPoses.Sleep.Clone();
    public Point Anchor { get; set; }
    public double Facing { get; private set; } = 1;
    public double TailPhase { get; private set; }
    public double Time { get; private set; }
    public double Scale { get; set; } = 1;
    public IReadOnlyList<Heart> Hearts => _hearts;
    public bool IsAwake => Mood is CatMood.Waking or CatMood.Idle or CatMood.Jumping or CatMood.Held;

    public event Action<bool>? AwakeChanged;
    public event Action? Landed;

    public void ToggleSleep()
    {
        if (IsAwake)
        {
            Sleep();
        }
        else
        {
            Wake();
        }
    }

    public void Wake()
    {
        if (Mood is not (CatMood.Sleeping or CatMood.Dozing))
        {
            return;
        }

        SetMood(CatMood.Waking);
        _idleSeconds = 0;
        Play(new Clip()
            .To(0.55, CatPoses.Loaf.With(p => p.EyeOpen = 0.3))
            .To(0.8, CatPoses.Loaf)
            .To(1.15, CatPoses.Loaf)
            .To(1.9, CatPoses.Stretch)
            .To(2.6, CatPoses.Stretch.With(p =>
            {
                p.Hip += new Vector(-3, -3);
                p.MouthOpen = 0.35;
            }))
            .To(3.2, CatPoses.Sit)
            .Then(() =>
            {
                _rest = CatPoses.Sit;
                _blinkAt = Time + 0.8;
                _idleActionAt = Time + 6;
                SetMood(CatMood.Idle);
                if (_pendingStyle is { } style)
                {
                    _pendingStyle = null;
                    PerformSit(style);
                }
            }));
    }

    /// <summary>Takes up a way of sitting now, waking first if needed.</summary>
    public void StrikePose(SitStyle style)
    {
        _idleSeconds = 0;
        if (Mood == CatMood.Idle)
        {
            PerformSit(style);
        }
        else if (Mood is CatMood.Sleeping or CatMood.Dozing)
        {
            _pendingStyle = style;
            Wake();
        }
    }

    public void Sleep()
    {
        if (Mood == CatMood.Jumping)
        {
            _sleepAfterLanding = true;
            return;
        }

        if (Mood is CatMood.Sleeping or CatMood.Dozing or CatMood.Held)
        {
            return;
        }

        SetMood(CatMood.Dozing);
        Play(new Clip()
            .To(0.4, _raw.With(p =>
            {
                p.TailBase = 175;
                p.TailCurl = -15;
                p.EyeOpen = 0.7;
            }))
            .To(0.7, CatPoses.Loaf.With(p =>
            {
                // Sweeping the tail round to the front: it points at the viewer, so it looks short.
                p.TailBase = 120;
                p.TailCurl = -60;
                p.TailLength = 0.45;
                p.EyeOpen = 0.7;
            }))
            .To(1.0, CatPoses.Loaf)
            .To(1.7, Yawning(CatPoses.Loaf))
            .To(2.2, CatPoses.Loaf.With(p => p.EyeOpen = 0.25))
            .To(3.1, CatPoses.Sleep)
            .Then(() =>
            {
                _rest = CatPoses.Sleep;
                SetMood(CatMood.Sleeping);
            }));
    }

    /// <summary>Crouches, wiggles, and leaps so that its feet land on <paramref name="target"/>.</summary>
    public bool TryJump(Point target)
    {
        if (Mood != CatMood.Idle)
        {
            return false;
        }

        SetMood(CatMood.Jumping);
        _idleSeconds = 0;
        _petSeconds = 0;
        var start = Anchor;
        var dx = target.X - start.X;
        if (Math.Abs(dx) > 6 && Math.Sign(dx) != Math.Sign(_facingTarget))
        {
            TurnTo(Math.Sign(dx));
        }

        Play(new Clip()
            .To(0.24, CatPoses.Crouch)
            .To(0.6, CatPoses.Crouch)
            .OnTick((t, p) =>
            {
                if (t > 0.24)
                {
                    var wiggle = Math.Sin((t - 0.24) * 42) * Math.Min(1, (t - 0.24) * 6);
                    p.Hip.X += wiggle * 1.6;
                    p.Hip.Y += Math.Abs(wiggle) * -1.2;
                    p.TailWag = 14;
                }
            })
            .Then(() => Fly(start, target)));
        return true;
    }

    public void BeginHold(Point cursor)
    {
        _sleepAfterLanding = false;
        _holdCursor = _lastHoldCursor = cursor;
        _idleSeconds = 0;
        _hearts.Clear();
        SetMood(CatMood.Held);
        _rest = CatPoses.Held;
        Play(new Clip().To(0.22, CatPoses.Held));
    }

    public void MoveHold(Point cursor) => _holdCursor = cursor;

    public void EndHold()
    {
        if (Mood != CatMood.Held)
        {
            return;
        }

        SetMood(CatMood.Jumping);
        Land();
    }

    public void Update(double dt, Point? cursor, bool cursorOverCat)
    {
        dt = Math.Clamp(dt, 0, 0.1);
        Time += dt;

        if (_clip is { } clip)
        {
            _clipTime += dt;
            _raw = clip.Sample(_clipFrom, Math.Min(_clipTime, clip.Duration));
            clip.Tick?.Invoke(_clipTime, _raw);
            if (_clipTime >= clip.Duration && ReferenceEquals(_clip, clip))
            {
                _clip = null;
                clip.Done?.Invoke();
            }
        }
        else
        {
            _raw = _rest.Clone();
        }

        UpdateFacing();
        UpdateIdle(dt);
        if (Mood == CatMood.Held)
        {
            FollowHoldCursor(dt);
        }

        Pose = AddLife(_raw.Clone(), dt, cursor, cursorOverCat);
    }

    private void Fly(Point start, Point target)
    {
        var distance = (target - start).Length;
        var duration = Math.Clamp(0.34 + distance / 1500, 0.42, 0.95);
        var height = Math.Clamp(30 + distance * 0.22, 30, 150);
        Play(new Clip()
            .To(duration * 0.18, CatPoses.Leap)
            .To(duration * 0.5, CatPoses.Leap)
            .To(duration, CatPoses.Land)
            .OnTick((t, p) =>
            {
                var f = Math.Clamp(t / duration, 0, 1);
                Anchor = new Point(
                    start.X + (target.X - start.X) * f,
                    start.Y + (target.Y - start.Y) * f - height * 4 * f * (1 - f));
                var vx = Math.Abs(target.X - start.X) + 60;
                var vy = target.Y - start.Y - height * 4 * (1 - 2 * f);
                p.Rotation = Math.Clamp(Math.Atan2(vy, vx) * 180 / Math.PI * 0.55, -36, 36);
            })
            .Then(() =>
            {
                Anchor = target;
                Land();
            }));
    }

    private void Land() =>
        Play(new Clip()
            .To(0.1, CatPoses.Squash)
            .To(0.5, CatPoses.Sit)
            .Then(() =>
            {
                _rest = CatPoses.Sit;
                _idleSeconds = 0;
                SetMood(CatMood.Idle);
                Landed?.Invoke();
                if (_sleepAfterLanding)
                {
                    _sleepAfterLanding = false;
                    Sleep();
                }
            }));

    private void UpdateIdle(double dt)
    {
        if (Mood != CatMood.Idle)
        {
            return;
        }

        _idleSeconds += dt;
        if (_clip is not null)
        {
            return;
        }

        if (_idleSeconds > IdleBeforeSleep)
        {
            Sleep();
        }
        else if (_idleSeconds > IdleBeforeLoaf && !ReferenceEquals(_rest, RestingLoaf))
        {
            _rest = RestingLoaf;
            Play(new Clip().To(1.0, RestingLoaf));
        }
        else if (IdleActions && _idleSeconds < IdleBeforeLoaf && Time > _idleActionAt)
        {
            _idleActionAt = Time + 8 + _random.NextDouble() * 9;
            if (_random.NextDouble() < 0.15)
            {
                var yawn = Yawning(_rest);
                Play(new Clip().To(0.45, yawn).To(1.5, yawn).To(2.0, _rest));
            }
            else
            {
                PerformSit(CatPoses.Sitting[_random.Next(CatPoses.Sitting.Length)]);
            }
        }
    }

    /// <summary>Rest styles become the new resting pose; gestures (and grooming) play, then return to it.</summary>
    private void PerformSit(SitStyle style)
    {
        if (style.Rest)
        {
            _rest = style.Pose;
            Play(new Clip().To(1.0, style.Pose));
            return;
        }

        var hold = style.Grooming ? 4.5 : 2.4;
        Play(new Clip()
            .To(0.8, style.Pose)
            .To(0.8 + hold, style.Pose)
            .To(1.6 + hold, _rest)
            .OnTick((t, p) =>
            {
                if (!style.Grooming || t < 0.8 || t > 0.8 + hold)
                {
                    return;
                }

                // Licking: the head bobs and the tongue flicks in and out.
                var lick = Math.Sin((t - 0.8) * 9);
                p.Head.Y += lick * 2.2;
                p.HeadTilt += lick * 3;
                p.Tongue *= Math.Max(0, lick);
                if (p.FrontNear.Y < -40)
                {
                    p.FrontNear.Y += lick * 1.5;
                }
            }));
    }

    private void FollowHoldCursor(double dt)
    {
        var scruff = new Vector(CatPoses.Scruff.X * Facing, CatPoses.Scruff.Y) * Scale;
        var goal = _holdCursor - scruff;
        Anchor += (goal - Anchor) * Math.Min(1, dt * 18);
        var velocity = dt > 0 ? (_holdCursor.X - _lastHoldCursor.X) / dt : 0;
        _lastHoldCursor = _holdCursor;
        _swing += (Math.Clamp(-velocity * 0.03, -24, 24) - _swing) * Math.Min(1, dt * 6);
    }

    private CatPose AddLife(CatPose pose, double dt, Point? cursor, bool cursorOverCat)
    {
        var awake = IsAwake;
        var asleep = Mood is CatMood.Sleeping or CatMood.Dozing;

        _breathPhase += dt * Math.PI * 2 * (asleep ? 0.3 : 0.42);
        var breath = Math.Sin(_breathPhase) * pose.Breath;
        pose.ChestRadius += breath * 0.9;
        pose.HipRadius += breath * 0.6;
        pose.Chest.Y -= breath * 0.7;
        pose.Head.Y -= breath * 0.4;

        var huntGoal = 0.0;
        var lookGoal = new Vector();
        if (awake && Mood != CatMood.Held && cursor is { } c)
        {
            var head = Anchor + new Vector((pose.Chest.X + pose.Head.X) * Facing, pose.Chest.Y + pose.Head.Y) * Scale;
            var toCursor = c - head;
            lookGoal = new Vector(Math.Clamp(toCursor.X * Math.Sign(Facing) / 220, -1, 1), Math.Clamp(toCursor.Y / 220, -1, 1));
            huntGoal = Mood == CatMood.Idle && !cursorOverCat && toCursor.Length < 170 * Scale ? 1 : 0;
        }

        _look += (lookGoal - _look) * Math.Min(1, dt * 7);
        _hunt += (huntGoal - _hunt) * Math.Min(1, dt * 3);
        pose.Look = _look;
        pose.HeadTurn += _look.X * 0.45;
        pose.Head += _look * 2.5;
        pose.Pupil += (1 - pose.Pupil) * _hunt * 0.7;
        pose.TailWag += _hunt * (8 - 6 * pose.TailFront);

        _petSeconds = Mood == CatMood.Idle && cursorOverCat ? _petSeconds + dt : 0;
        var petted = _petSeconds > 0.6;
        _happy += ((petted ? 1 : 0) - _happy) * Math.Min(1, dt * 6);
        if (petted)
        {
            _idleSeconds = 0;
            if (Time >= _heartAt)
            {
                _heartAt = Time + HeartInterval;
                var head = pose.Chest + pose.Head;
                _hearts.Add(new Heart(new Point(head.X * Facing + (_random.NextDouble() - 0.5) * 30, head.Y - 36), 0));
            }
        }

        pose.EyeOpen += (0 - pose.EyeOpen) * _happy;
        pose.EyeHappy += (1 - pose.EyeHappy) * _happy;
        pose.EarAngle += 6 * _happy;
        pose.HeadTilt += 8 * _happy;
        pose.TailWag += (6 - 4 * pose.TailFront) * _happy;

        if (awake && Mood != CatMood.Held && Time >= _blinkAt)
        {
            _blinkStart = Time;
            _blinkAt = Time + (_random.NextDouble() < 0.2 ? 0.32 : 2.5 + _random.NextDouble() * 3.5);
        }

        var blink = (Time - _blinkStart) / 0.16;
        if (blink is >= 0 and < 1)
        {
            pose.EyeOpen *= 1 - Math.Sin(Math.PI * blink) * 0.95;
        }

        if (Time >= _twitchAt || (asleep && cursorOverCat && Time - _twitchStart > 1.5))
        {
            _twitchStart = Time;
            _twitchNear = _random.NextDouble() < 0.6;
            _twitchAt = Time + 5 + _random.NextDouble() * 8;
        }

        var twitch = (Time - _twitchStart) / 0.3;
        if (twitch is >= 0 and < 1)
        {
            var amount = Math.Sin(Math.PI * twitch) * 16;
            if (_twitchNear)
            {
                pose.EarNear += amount;
            }
            else
            {
                pose.EarFar += amount;
            }
        }

        if (Mood == CatMood.Held)
        {
            pose.Rotation += _swing;
        }

        TailPhase += dt * (asleep ? 0.9 : 2.1 + _hunt * 4.5);

        for (var i = _hearts.Count - 1; i >= 0; i--)
        {
            var heart = _hearts[i] with { Age = _hearts[i].Age + dt };
            if (heart.Age > HeartLife)
            {
                _hearts.RemoveAt(i);
            }
            else
            {
                _hearts[i] = heart;
            }
        }

        return pose;
    }

    /// <summary>Turns to face left (-1) or right (+1), with the quick turning animation.</summary>
    public void TurnToward(double facing)
    {
        var side = Math.Sign(facing) is 0 ? 1 : Math.Sign(facing);
        if (side != _facingTarget)
        {
            TurnTo(side);
        }
    }

    /// <summary>Faces left (-1) or right (+1) at once, without the turning animation.</summary>
    public void FaceInstantly(double facing)
    {
        _facingTarget = _turnFrom = Facing = Math.Sign(facing) is 0 ? 1 : Math.Sign(facing);
    }

    private void TurnTo(double facing)
    {
        _turnFrom = _facingTarget;
        _facingTarget = facing;
        _turnStart = Time;
    }

    /// <summary>A quick turn: the cat narrows, flips at the midpoint, and widens again.</summary>
    private void UpdateFacing()
    {
        var t = (Time - _turnStart) / TurnDuration;
        Facing = t is >= 0 and < 1
            ? (t < 0.5 ? _turnFrom : _facingTarget) * (1 - 0.5 * Math.Sin(Math.PI * t))
            : _facingTarget;
    }

    private static CatPose Yawning(CatPose from) => from.With(p =>
    {
        p.HeadTilt -= 10;
        p.EarAngle += 6;
        p.EyeOpen = 0;
        p.EyeHappy = 0.9;
        p.MouthOpen = 1;
    });

    private void Play(Clip clip)
    {
        _clipFrom = _raw.Clone();
        _clip = clip;
        _clipTime = 0;
    }

    private void SetMood(CatMood mood)
    {
        var wasAwake = IsAwake;
        Mood = mood;
        if (wasAwake != IsAwake)
        {
            AwakeChanged?.Invoke(IsAwake);
        }
    }

    /// <summary>A run of key poses, each reached with an ease-in-out from the one before.</summary>
    private sealed class Clip
    {
        private readonly List<(double At, CatPose Pose)> _keys = [];

        public double Duration => _keys.Count == 0 ? 0 : _keys[^1].At;
        public Action<double, CatPose>? Tick { get; private set; }
        public Action? Done { get; private set; }

        public Clip To(double at, CatPose pose)
        {
            _keys.Add((at, pose));
            return this;
        }

        public Clip OnTick(Action<double, CatPose> tick)
        {
            Tick = tick;
            return this;
        }

        public Clip Then(Action done)
        {
            Done = done;
            return this;
        }

        public CatPose Sample(CatPose from, double t)
        {
            var previousAt = 0.0;
            var previous = from;
            foreach (var (at, pose) in _keys)
            {
                if (t < at)
                {
                    var f = (t - previousAt) / (at - previousAt);
                    var eased = 0.5 - 0.5 * Math.Cos(Math.PI * f);
                    var blended = CatPose.Lerp(previous, pose, eased);
                    if (previous.TailFront < 0.5 != pose.TailFront < 0.5)
                    {
                        // Swinging round between behind and in front, the tail points at the viewer: draw it short
                        // so it doesn't sweep through the floor on the way.
                        blended.TailLength *= 1 - 0.6 * Math.Sin(Math.PI * eased);
                    }

                    return blended;
                }

                previousAt = at;
                previous = pose;
            }

            return previous.Clone();
        }
    }
}
