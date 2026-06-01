using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace LenovoTray;

/// <summary>
/// Invisible host window.  WinUI 3 exits when all windows are closed, so this
/// 1×1 off-screen window keeps the application alive without appearing on-screen
/// or in the taskbar / Alt-Tab switcher.
/// </summary>
public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        // Exclude from taskbar and Alt-Tab.
        AppWindow.IsShownInSwitchers = false;

        // Remove chrome so nothing is visible even if the window flickers on-screen.
        var presenter = OverlappedPresenter.Create();
        presenter.SetBorderAndTitleBar(hasBorder: false, hasTitleBar: false);
        presenter.IsResizable   = false;
        presenter.IsMaximizable = false;
        presenter.IsMinimizable = false;
        AppWindow.SetPresenter(presenter);

        // Park it far off-screen as a belt-and-suspenders measure.
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1, 1));
        AppWindow.Move(new Windows.Graphics.PointInt32(-32000, -32000));
    }
}
