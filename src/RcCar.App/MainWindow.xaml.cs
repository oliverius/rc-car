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
    private bool uiForward;
    private bool uiReverse;
    private bool uiLeft;
    private bool uiRight;

    public MainWindow(MainViewModel model, bool smokeTest = false)
    {
        InitializeComponent();
        this.model = model;
        DataContext = model;
        Deactivated += (_, _) =>
        {
            ClearDriveButtons();
            model.LoseFocus();
        };
        Closing += OnClosing;
        PreviewKeyDown += OnPreviewKeyDown;
        timer.Tick += (_, _) =>
        {
            model.DrainEvents();
            if (!smokeTest)
            {
                model.PollInput(
                    IsActive,
                    new HeldControls(
                        Held(Key.Up) || uiForward,
                        Held(Key.Down) || uiReverse,
                        Held(Key.Left) || uiLeft,
                        Held(Key.Right) || uiRight,
                        Held(Key.Space)));
            }
        };
        timer.Start();
    }

    private static bool Held(Key key) => (GetAsyncKeyState(KeyInterop.VirtualKeyFromKey(key)) & 0x8000) != 0;

    private void OnForwardMouseDown(object sender, MouseButtonEventArgs e)
    {
        uiForward = true;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void OnReverseMouseDown(object sender, MouseButtonEventArgs e)
    {
        uiReverse = true;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void OnLeftMouseDown(object sender, MouseButtonEventArgs e)
    {
        uiLeft = true;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void OnRightMouseDown(object sender, MouseButtonEventArgs e)
    {
        uiRight = true;
        ((UIElement)sender).CaptureMouse();
        e.Handled = true;
    }

    private void OnDriveButtonMouseUp(object sender, MouseButtonEventArgs e)
    {
        ClearDriveButtons();
        ((UIElement)sender).ReleaseMouseCapture();
        e.Handled = true;
    }

    private void OnDriveButtonLostMouseCapture(object sender, MouseEventArgs e)
    {
        ClearDriveButtons();
    }

    private void ClearDriveButtons()
    {
        uiForward = false;
        uiReverse = false;
        uiLeft = false;
        uiRight = false;
    }

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
