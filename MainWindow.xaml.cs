using System.Diagnostics;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

namespace LazyCatCompanion;

public partial class MainWindow : Window
{
    private readonly CatSettings _settings = CatSettings.Load();
    private readonly CatController _cat = new();
    private readonly CatView _view;
    private readonly LowLevelMouseHook _mouseHook = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly DispatcherTimer _slowTicks = new(DispatcherPriority.Render) { Interval = TimeSpan.FromSeconds(1 / 30.0) };
    private bool _everyFrame;
    private CompanionTrayIcon? _trayIcon;
    private double _lastFrame;
    private bool _pressed;
    private bool _dragging;
    private Point _pressPoint;

    public MainWindow()
    {
        InitializeComponent();
        _view = new CatView(_cat) { Palette = new CatPalette(_settings.Fur, _settings.Markings) };
        Content = _view;
        ApplyScale(_settings.Scale);

        Loaded += Window_Loaded;
        Closing += Window_Closing;
        _view.MouseLeftButtonDown += View_MouseLeftButtonDown;
        _view.MouseMove += View_MouseMove;
        _view.MouseLeftButtonUp += View_MouseLeftButtonUp;
        _view.MouseRightButtonUp += (_, e) =>
        {
            _trayIcon?.ShowMenu();
            e.Handled = true;
        };
        _cat.AwakeChanged += Cat_AwakeChanged;
        _cat.Landed += () =>
        {
            SavePosition();
            ApplyFacing(animate: true);
        };
        _mouseHook.LeftButtonDown += MouseHook_LeftButtonDown;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var workArea = SystemParameters.WorkArea;
        var fallback = new Point(workArea.Right - 130 * _cat.Scale, workArea.Bottom - 4);
        _cat.Anchor = _settings.AnchorX is { } x && _settings.AnchorY is { } y && IsOnScreen(new Point(x, y))
            ? new Point(x, y)
            : fallback;

        ApplyFacing(animate: false);
        PlaceWindow();

        _trayIcon = new CompanionTrayIcon(this, _settings);
        _slowTicks.Tick += OnFrame;
        _slowTicks.Start();
    }

    private void Window_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _slowTicks.Stop();
        if (_everyFrame)
        {
            CompositionTarget.Rendering -= OnFrame;
        }

        _mouseHook.Dispose();
        _trayIcon?.Dispose();
        SavePosition();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        // Timed rather than counted, so a 144 Hz screen doesn't multiply the work.
        var now = _clock.Elapsed.TotalSeconds;
        var interval = _cat.Mood switch
        {
            CatMood.Jumping or CatMood.Held => 1 / 60.0,
            CatMood.Sleeping => 1 / 15.0,
            _ => 1 / 30.0
        };
        if (now - _lastFrame < interval * 0.9)
        {
            return;
        }

        var dt = now - _lastFrame;
        _lastFrame = now;

        var cursor = CursorPosition();
        if (_dragging)
        {
            _cat.MoveHold(cursor);
        }

        _cat.Update(dt, cursor, IsOverCat(cursor));
        PlaceWindow();
        _view.InvalidateVisual();
        SetPace();
    }

    /// <summary>
    /// Every screen refresh only while the cat leaps or is carried; otherwise a plain timer at 30 fps (15 asleep).
    /// Breathing and tail swishes look the same at those rates, and the app stops waking on every refresh.
    /// </summary>
    private void SetPace()
    {
        var fast = _cat.Mood is CatMood.Jumping or CatMood.Held;
        if (fast != _everyFrame)
        {
            _everyFrame = fast;
            if (fast)
            {
                _slowTicks.Stop();
                CompositionTarget.Rendering += OnFrame;
            }
            else
            {
                CompositionTarget.Rendering -= OnFrame;
                _slowTicks.Start();
            }
        }

        var interval = TimeSpan.FromSeconds(_cat.Mood == CatMood.Sleeping ? 1 / 15.0 : 1 / 30.0);
        if (_slowTicks.Interval != interval)
        {
            _slowTicks.Interval = interval;
        }
    }

    private void View_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;
        if (e.ClickCount == 2)
        {
            _pressed = false;
            _view.ReleaseMouseCapture();
            _cat.ToggleSleep();
            return;
        }

        _pressed = true;
        _pressPoint = CursorPosition();
        _view.CaptureMouse();
    }

    private void View_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_pressed || _dragging || (CursorPosition() - _pressPoint).Length < 6)
        {
            return;
        }

        _dragging = true;
        _cat.BeginHold(CursorPosition());
        SetPace();
    }

    private void View_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pressed)
        {
            return;
        }

        _pressed = false;
        _view.ReleaseMouseCapture();
        if (_dragging)
        {
            _dragging = false;
            _cat.EndHold();
        }
    }

    private void Cat_AwakeChanged(bool awake)
    {
        if (awake)
        {
            try
            {
                _mouseHook.Install();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                Forms.MessageBox.Show($"The cat can't hear desktop clicks, so it won't jump.\n\n{ex.Message}",
                    "Lazy Cat Companion", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            }
        }
        else
        {
            _mouseHook.Dispose();
        }

        _trayIcon?.UpdateMood(awake);
    }

    private void MouseHook_LeftButtonDown(Forms.MouseEventArgs e)
    {
        if (!Dispatcher.HasShutdownStarted)
        {
            _ = Dispatcher.BeginInvoke(() => HandleClick(e.X, e.Y));
        }
    }

    private void HandleClick(int deviceX, int deviceY)
    {
        if (_cat.Mood != CatMood.Idle || _pressed)
        {
            return;
        }

        var point = ToDip(new Point(deviceX, deviceY));
        if (IsOverCat(point) || !(_settings.JumpOnAnyClick || DesktopProbe.IsDesktopAt(deviceX, deviceY)))
        {
            return;
        }

        var left = SystemParameters.VirtualScreenLeft;
        var top = SystemParameters.VirtualScreenTop;
        var target = new Point(
            Math.Clamp(point.X, left + 60 * _cat.Scale, left + SystemParameters.VirtualScreenWidth - 60 * _cat.Scale),
            Math.Clamp(point.Y, top + 170 * _cat.Scale, top + SystemParameters.VirtualScreenHeight - 2));
        _cat.TryJump(target);
        SetPace();
    }

    internal void ToggleSleep() => _cat.ToggleSleep();

    internal bool Markings => _settings.Markings;

    internal void StrikePose(SitStyle style) => _cat.StrikePose(style);

    internal void SetCoat(Color color, bool markings)
    {
        _settings.Fur = color;
        _settings.Markings = markings;
        _settings.Save();
        _view.Palette = new CatPalette(color, markings);
        _trayIcon?.UpdateCoat(_view.Palette);
        _view.InvalidateVisual();
    }

    internal void SetCatScale(double scale)
    {
        _settings.Scale = scale;
        _settings.Save();
        ApplyScale(scale);
        _trayIcon?.UpdateScale(scale);
    }

    internal void SetFacing(string choice)
    {
        _settings.Facing = choice;
        _settings.Save();
        ApplyFacing(animate: true);
    }

    /// <summary>
    /// Points the cat the way the settings ask. "Auto" faces into the screen, so from its default spot
    /// in the bottom-right corner it looks left.
    /// </summary>
    private void ApplyFacing(bool animate)
    {
        var screenMiddle = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth / 2;
        var facing = _settings.Facing switch
        {
            "Left" => -1,
            "Right" => 1,
            _ => _cat.Anchor.X > screenMiddle ? -1 : 1
        };
        if (animate)
        {
            _cat.TurnToward(facing);
        }
        else
        {
            _cat.FaceInstantly(facing);
        }
    }

    internal void SetJumpOnAnyClick(bool enabled)
    {
        _settings.JumpOnAnyClick = enabled;
        _settings.Save();
    }

    private void ApplyScale(double scale)
    {
        _cat.Scale = scale;
        Width = CatView.BaseWidth * scale;
        Height = CatView.BaseHeight * scale;
        PlaceWindow();
    }

    private void PlaceWindow()
    {
        var left = _cat.Anchor.X - CatView.OriginX * _cat.Scale;
        var top = _cat.Anchor.Y - CatView.OriginY * _cat.Scale;
        if (Math.Abs(Left - left) > 0.01 || Math.Abs(Top - top) > 0.01)
        {
            Left = left;
            Top = top;
        }
    }

    private void SavePosition()
    {
        _settings.AnchorX = _cat.Anchor.X;
        _settings.AnchorY = _cat.Anchor.Y;
        _settings.Save();
    }

    private bool IsOverCat(Point screenPoint)
    {
        var local = new Point(screenPoint.X - Left, screenPoint.Y - Top);
        return local.X >= 0 && local.Y >= 0 && local.X < ActualWidth && local.Y < ActualHeight
            && VisualTreeHelper.HitTest(_view, local) is not null;
    }

    private static bool IsOnScreen(Point point) =>
        point.X >= SystemParameters.VirtualScreenLeft
        && point.X <= SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth
        && point.Y >= SystemParameters.VirtualScreenTop + 100
        && point.Y <= SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;

    private Point CursorPosition()
    {
        var position = Forms.Cursor.Position;
        return ToDip(new Point(position.X, position.Y));
    }

    private Point ToDip(Point devicePoint) =>
        PresentationSource.FromVisual(this)?.CompositionTarget is { } target
            ? target.TransformFromDevice.Transform(devicePoint)
            : devicePoint;
}
