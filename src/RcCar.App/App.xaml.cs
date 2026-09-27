using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using RcCar.App.Infrastructure;
using RcCar.App.Presentation;
using RcCar.Bluetooth.Windows;
using RcCar.Core.Models;
using RcCar.Core.Protocol;
using RcCar.Core.Services;

namespace RcCar.App;

public partial class App : Application
{
    private SessionLog? sessionLog;
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        if (e.Args.Any(argument => argument != "--smoke-test"))
        {
            MessageBox.Show("Usage: RcCar.App [--smoke-test]");
            Shutdown(2);
            return;
        }

        var smokeTest = e.Args.Contains("--smoke-test");
        try
        {
            sessionLog = new SessionLog(smokeTest ? Path.Combine(Path.GetTempPath(), "RcCar-smoke") : null);
            var radio = new WindowsBluetoothTransport(sessionLog);
            var protocol = new CarProtocol(new CarIdentity(0x61, 0x62, 0x63));
            var service = new CarService(radio, radio, protocol, sessionLog);
            var model = new MainViewModel(service, radio, sessionLog, new ManualControlInput());
            var window = new MainWindow(model, smokeTest);
            MainWindow = window;
            if (smokeTest)
            {
                window.ShowInTaskbar = false;
                window.Opacity = 0;
                window.Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                {
                    // Offline WPF render only. No adapter inspection, scan, or advertising.
                    var content = (FrameworkElement)window.Content;
                    var bitmap = new RenderTargetBitmap((int)content.ActualWidth, (int)content.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                    var drawing = new DrawingVisual();
                    using (var context = drawing.RenderOpen())
                    {
                        context.DrawRectangle(new VisualBrush(content), null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
                    }

                    bitmap.Render(drawing);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var output = File.Create(Path.Combine(Path.GetTempPath(), "RcCar-smoke.png")))
                    {
                        encoder.Save(output);
                    }

                    window.Close();
                }));
            }

            window.Show();
        }
        catch (Exception exception)
        {
            if (!smokeTest)
            {
                MessageBox.Show(exception.ToString(), "RC Car startup failed");
            }
            else
            {
                File.WriteAllText(Path.Combine(Path.GetTempPath(), "RcCar-smoke-error.txt"), exception.ToString());
            }

            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        sessionLog?.Dispose();
        base.OnExit(e);
    }
}
