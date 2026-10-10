using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace LazyCatCompanion;

internal sealed class CompanionTrayIcon : IDisposable
{
    private static readonly Color TabbyAndWhite = Color.FromRgb(146, 131, 115);

    private static readonly (string Name, Color Color)[] Coats =
    [
        ("Ginger", Color.FromRgb(217, 138, 82)),
        ("Cream", Color.FromRgb(235, 215, 174)),
        ("Gray", Color.FromRgb(139, 148, 158)),
        ("Charcoal", Color.FromRgb(64, 66, 76)),
        ("Chocolate", Color.FromRgb(112, 76, 64)),
        ("White", Color.FromRgb(239, 239, 231)),
        ("Lavender", Color.FromRgb(164, 133, 193)),
        ("Blue", Color.FromRgb(94, 143, 204)),
        ("Pink", Color.FromRgb(218, 137, 164)),
        ("Mint", Color.FromRgb(111, 177, 153))
    ];

    private static readonly (string Name, double Scale)[] Sizes = [("Small", 0.75), ("Medium", 1), ("Large", 1.3)];

    private static readonly (string Name, string Value)[] Facings =
        [("Toward the screen (auto)", "Auto"), ("Left", "Left"), ("Right", "Right")];

    private readonly MainWindow _window;
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _moodItem;
    private readonly Forms.ToolStripMenuItem _anyClickItem;
    private readonly List<Forms.ToolStripMenuItem> _sizeItems = [];
    private readonly List<Forms.ToolStripMenuItem> _facingItems = [];

    public CompanionTrayIcon(MainWindow window, CatSettings settings)
    {
        _window = window;
        var menu = new Forms.ContextMenuStrip();
        _moodItem = new Forms.ToolStripMenuItem("Wake up");
        _moodItem.Click += (_, _) => _window.ToggleSleep();
        menu.Items.Add(_moodItem);
        menu.Items.Add(new Forms.ToolStripSeparator());

        var poseMenu = new Forms.ToolStripMenuItem("Sit like…");
        foreach (var style in CatPoses.Sitting)
        {
            poseMenu.DropDownItems.Add(new Forms.ToolStripMenuItem(style.Name, null, (_, _) => _window.StrikePose(style)));
        }

        menu.Items.Add(poseMenu);

        var coatMenu = new Forms.ToolStripMenuItem("Cat color");
        coatMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("Tabby & white (the photo cat)", null, (_, _) => _window.SetCoat(TabbyAndWhite, true)));
        coatMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        foreach (var (name, color) in Coats)
        {
            coatMenu.DropDownItems.Add(new Forms.ToolStripMenuItem(name, null, (_, _) => _window.SetCoat(color, false)));
        }

        coatMenu.DropDownItems.Add(new Forms.ToolStripSeparator());
        coatMenu.DropDownItems.Add(new Forms.ToolStripMenuItem("Choose a custom color…", null, (_, _) => ChooseCustomColor()));
        menu.Items.Add(coatMenu);

        var sizeMenu = new Forms.ToolStripMenuItem("Size");
        foreach (var (name, scale) in Sizes)
        {
            var item = new Forms.ToolStripMenuItem(name, null, (_, _) => _window.SetCatScale(scale))
            {
                Tag = scale,
                Checked = Math.Abs(settings.Scale - scale) < 0.01
            };
            _sizeItems.Add(item);
            sizeMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(sizeMenu);

        var facingMenu = new Forms.ToolStripMenuItem("Face");
        foreach (var (name, value) in Facings)
        {
            var item = new Forms.ToolStripMenuItem(name)
            {
                Tag = value,
                Checked = settings.Facing == value
            };
            item.Click += (_, _) =>
            {
                _window.SetFacing(value);
                foreach (var other in _facingItems)
                {
                    other.Checked = ReferenceEquals(other, item);
                }
            };
            _facingItems.Add(item);
            facingMenu.DropDownItems.Add(item);
        }

        menu.Items.Add(facingMenu);

        _anyClickItem = new Forms.ToolStripMenuItem("Jump to clicks on any window")
        {
            CheckOnClick = true,
            Checked = settings.JumpOnAnyClick
        };
        _anyClickItem.CheckedChanged += (_, _) => _window.SetJumpOnAnyClick(_anyClickItem.Checked);
        menu.Items.Add(_anyClickItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(new Forms.ToolStripMenuItem("Exit", null, (_, _) => System.Windows.Application.Current.Shutdown()));

        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = BuildIcon(new CatPalette(settings.Fur, settings.Markings)),
            Text = "Lazy Cat — double-click to wake",
            ContextMenuStrip = menu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (_, _) => _window.ToggleSleep();
    }

    public void UpdateMood(bool awake)
    {
        _moodItem.Text = awake ? "Let the cat sleep" : "Wake up";
        _notifyIcon.Text = awake
            ? "Lazy Cat — click the desktop to call it"
            : "Lazy Cat — double-click to wake";
    }

    public void UpdateCoat(CatPalette palette)
    {
        var old = _notifyIcon.Icon;
        _notifyIcon.Icon = BuildIcon(palette);
        old?.Dispose();
    }

    public void UpdateScale(double scale)
    {
        foreach (var item in _sizeItems)
        {
            item.Checked = Math.Abs((double)item.Tag! - scale) < 0.01;
        }
    }

    public void ShowMenu() => _notifyIcon.ContextMenuStrip?.Show(Forms.Cursor.Position);

    private void ChooseCustomColor()
    {
        using var dialog = new Forms.ColorDialog { FullOpen = true, AnyColor = true };
        if (dialog.ShowDialog() == Forms.DialogResult.OK)
        {
            _window.SetCoat(Color.FromRgb(dialog.Color.R, dialog.Color.G, dialog.Color.B), _window.Markings);
        }
    }

    /// <summary>Renders the cat's own face as the tray icon, in the current coat.</summary>
    private static Drawing.Icon BuildIcon(CatPalette palette)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.PushTransform(new TranslateTransform(32, 38));
            dc.PushTransform(new ScaleTransform(0.66, 0.66));
            CatRenderer.DrawHeadOnly(dc, CatPoses.Sit, palette);
            dc.Pop();
            dc.Pop();
        }

        var bitmap = new RenderTargetBitmap(64, 64, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = new MemoryStream();
        encoder.Save(stream);
        stream.Position = 0;
        using var image = new Drawing.Bitmap(stream);
        var handle = image.GetHicon();
        try
        {
            using var borrowed = Drawing.Icon.FromHandle(handle);
            return (Drawing.Icon)borrowed.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        _notifyIcon.Visible = false;
        _notifyIcon.Icon?.Dispose();
        _notifyIcon.ContextMenuStrip?.Dispose();
        _notifyIcon.Dispose();
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr icon);
}
