using System.Windows;

namespace LazyCatCompanion;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Development aid: "LazyCatCompanion.exe --render-preview <folder>" writes pose and animation sheets as PNGs.
        if (e.Args is ["--render-preview", var folder])
        {
            PreviewRenderer.Render(folder);
            Shutdown();
            return;
        }

        // "--render-demo <folder>" writes the 15-second README demo as 30 fps PNG frames plus cues.json for its sound.
        if (e.Args is ["--render-demo", var demoFolder])
        {
            DemoRenderer.Render(demoFolder);
            Shutdown();
            return;
        }

        new MainWindow().Show();
    }
}
