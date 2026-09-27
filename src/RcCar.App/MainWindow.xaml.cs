using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using RcCar.App.Presentation;
using RcCar.Core.Services;

namespace RcCar.App;

/// <summary>Window events and physical key sampling only; workflows live in the view model and Core.</summary>
public partial class MainWindow : Window
{
    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int key);
    private readonly MainViewModel model;
    private readonly DispatcherTimer timer = new()
    {
        Interval = TimeSpan.FromMilliseconds(20)
    };
    private bool closeRequested;
    private bool canClose;

    public MainWindow(MainViewModel model, bool smokeTest = false)
    {
        InitializeComponent();
        this.model = model;
        DataContext = model;
        Deactivated += (_, _) => model.LoseFocus();
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        timer.Tick += (_, _) =>
        {
            model.DrainEvents();
            if (!smokeTest)
            {
                model.PollInput(IsActive, new HeldControls(Held(Key.Up), Held(Key.Down), Held(Key.Left), Held(Key.Right), Held(Key.Space)));
            }
        };
        timer.Start();
    }

    private static bool Held(Key key) => (GetAsyncKeyState(KeyInterop.VirtualKeyFromKey(key)) & 0x8000) != 0;
    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            model.Stop();
            e.Handled = true;
        }

        // Avoid arrow focus navigation or Space activating a button while driving.
        if (model.IsDriving && e.Key is Key.Up or Key.Down or Key.Left or Key.Right or Key.Space)
        {
            e.Handled = true;
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (canClose)
        {
            return;
        }

        e.Cancel = true;
        if (closeRequested)
        {
            return;
        }

        closeRequested = true;
        await model.ShutdownAsync();
        timer.Stop();
        canClose = true;
        // Shutdown may complete synchronously when no session was started.
        // Schedule the second close after WPF finishes the current Closing event.
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }
}
