using RcCar.Core.Abstractions;
using RcCar.Core.Models;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace RcCar.Bluetooth.Windows;

/// <summary>Windows-only radio operations. No vehicle identities, gears, or UI dependencies.</summary>
public sealed class WindowsBluetoothTransport(ISessionEvents events) : IAdapterInspector, IAdvertisementTransmitter, IAdvertisementReceiver
{
    private readonly SemaphoreSlim publisherGate = new(1, 1);
    private readonly SemaphoreSlim watcherGate = new(1, 1);
    private bool publisherStopUnconfirmed;
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan StopTimeout = TimeSpan.FromSeconds(2);

    public async Task<AdapterInformation> InspectAsync(CancellationToken cancellationToken)
    {
        var adapter = await BluetoothAdapter.GetDefaultAsync().AsTask(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("Windows did not return a Bluetooth adapter.");

        var information = await DeviceInformation.CreateFromIdAsync(adapter.DeviceId).AsTask(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken).ConfigureAwait(false);
        
        var radio = await adapter.GetRadioAsync().AsTask(cancellationToken)
            .WaitAsync(StartupTimeout, cancellationToken).ConfigureAwait(false);

        var result = new AdapterInformation(
            information.Name, adapter.BluetoothAddress.ToString("X12"),
            adapter.IsLowEnergySupported, adapter.IsPeripheralRoleSupported,
            adapter.IsAdvertisementOffloadSupported, radio?.State.ToString() ?? "Unknown");
        
        events.Write(SessionEventKind.Information, $"Adapter: {result}");
        
        return result;
    }

    public async Task AdvertiseAsync(ushort companyId, byte[] payload, TimeSpan duration, Action? onStarted, CancellationToken cancellationToken)
    {
        if (payload.Length == 0 || duration <= TimeSpan.Zero)
        {
            throw new ArgumentException("Payload and duration are required.");
        }

        if (!await publisherGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The Windows publisher is already in use.");
        }

        try
        {
            if (publisherStopUnconfirmed)
            {
                throw new InvalidOperationException("The previous publisher stop could not be confirmed. Turn car OFF and restart the application.");
            }

            await PublishWindowAsync(companyId, payload, duration, onStarted, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            publisherGate.Release();
        }
    }

    private async Task PublishWindowAsync(ushort companyId, byte[] payload, TimeSpan duration, Action? onStarted, CancellationToken token)
    {
        // Windows repeats this manufacturer-data advertisement until Stop().
        // No device connection or characteristic write is involved.
        var publisher = new BluetoothLEAdvertisementPublisher
        {
            UseExtendedAdvertisement = false
        };
        using (var writer = new DataWriter())
        {
            writer.WriteBytes(payload);
            publisher.Advertisement.ManufacturerData.Add(new BluetoothLEManufacturerData(companyId, writer.DetachBuffer()));
        }

        var started = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        BluetoothError lastError = BluetoothError.Success;
        void StatusChanged(BluetoothLEAdvertisementPublisher sender, BluetoothLEAdvertisementPublisherStatusChangedEventArgs args)
        {
            lastError = args.Error;
            events.Write(SessionEventKind.Information, $"Publisher: {args.Status}; error={args.Error}");
            if (args.Status == BluetoothLEAdvertisementPublisherStatus.Started)
            {
                started.TrySetResult(args.Error == BluetoothError.Success);
            }

            if (args.Status is BluetoothLEAdvertisementPublisherStatus.Stopped or BluetoothLEAdvertisementPublisherStatus.Aborted)
            {
                started.TrySetResult(false);
                stopped.TrySetResult();
            }
        }

        publisher.StatusChanged += StatusChanged;
        try
        {
            token.ThrowIfCancellationRequested();
            publisher.Start();
            if (!await started.Task.WaitAsync(StartupTimeout, token).ConfigureAwait(false))
            {
                throw new InvalidOperationException($"Publisher did not start: {lastError}.");
            }

            onStarted?.Invoke();
            var elapsed = Task.Delay(duration, token);
            if (await Task.WhenAny(elapsed, stopped.Task).ConfigureAwait(false) != elapsed)
            {
                throw new InvalidOperationException($"Publisher stopped unexpectedly: {lastError}.");
            }

            await elapsed.ConfigureAwait(false);
        }
        finally
        {
            try
            {
                // Cancellation ends the advertising window, but shutdown still
                // needs its own timeout. A new publisher must not overlap this one.
                publisher.Stop();
                if (publisher.Status is not (BluetoothLEAdvertisementPublisherStatus.Created or
                    BluetoothLEAdvertisementPublisherStatus.Stopped or
                    BluetoothLEAdvertisementPublisherStatus.Aborted))
                {
                    try
                    {
                        await stopped.Task.WaitAsync(StopTimeout).ConfigureAwait(false);
                    }
                    catch
                    {
                        publisherStopUnconfirmed = true;
                        throw;
                    }
                }

                if (lastError != BluetoothError.Success)
                {
                    throw new InvalidOperationException($"Publisher ended with error {lastError}.");
                }
            }
            catch
            {
                if (publisher.Status is not (BluetoothLEAdvertisementPublisherStatus.Created or
                    BluetoothLEAdvertisementPublisherStatus.Stopped or
                    BluetoothLEAdvertisementPublisherStatus.Aborted))
                {
                    publisherStopUnconfirmed = true;
                }

                throw;
            }
            finally
            {
                publisher.StatusChanged -= StatusChanged;
            }
        }
    }

    public async Task WatchAsync(Action<Advertisement> onReceived, CancellationToken cancellationToken)
    {
        if (!await watcherGate.WaitAsync(0, cancellationToken).ConfigureAwait(false))
        {
            throw new InvalidOperationException("The Windows watcher is already in use.");
        }

        try
        {
            await WatchWindowAsync(onReceived, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            watcherGate.Release();
        }
    }

    private async Task WatchWindowAsync(Action<Advertisement> onReceived, CancellationToken token)
    {
        var watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Passive
        };
        var ended = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Exception? failure = null;
        var receivedEvents = 0;
        var manufacturerSections = 0;
        void Received(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            try
            {
                if (Interlocked.Increment(ref receivedEvents) == 1)
                {
                    events.Write(SessionEventKind.Information, "Reception confirmed: Windows received a nearby advertisement.");
                }

                foreach (var section in args.Advertisement.ManufacturerData)
                {
                    Interlocked.Increment(ref manufacturerSections);
                    using var reader = DataReader.FromBuffer(section.Data);
                    var data = new byte[checked((int)section.Data.Length)];
                    reader.ReadBytes(data);
                    onReceived(new Advertisement(section.CompanyId, data, args.BluetoothAddress, args.RawSignalStrengthInDBm));
                }
            }
            catch (Exception exception)
            {
                failure = exception;
                ended.TrySetResult();
            }
        }

        void Stopped(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementWatcherStoppedEventArgs args)
        {
            events.Write(SessionEventKind.Information, $"Watcher stopped: {args.Error}");
            if (args.Error != BluetoothError.Success)
            {
                failure = new InvalidOperationException($"Watcher error: {args.Error}.");
            }

            stopped.TrySetResult();
            ended.TrySetResult();
        }

        watcher.Received += Received;
        watcher.Stopped += Stopped;
        try
        {
            token.ThrowIfCancellationRequested();
            watcher.Start();
            events.Write(SessionEventKind.Information, "Passive watcher started; no name or service filters.");
            await ended.Task.WaitAsync(token).ConfigureAwait(false);
            throw failure ?? new InvalidOperationException("Watcher stopped unexpectedly.");
        }
        finally
        {
            try
            {
                watcher.Stop();
                if (watcher.Status is not (BluetoothLEAdvertisementWatcherStatus.Created or
                    BluetoothLEAdvertisementWatcherStatus.Stopped or
                    BluetoothLEAdvertisementWatcherStatus.Aborted))
                {
                    await stopped.Task.WaitAsync(StopTimeout).ConfigureAwait(false);
                }

                if (failure is not null)
                {
                    System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
                }
            }
            finally
            {
                watcher.Received -= Received;
                watcher.Stopped -= Stopped;
                events.Write(SessionEventKind.Information, $"Receive summary: events={receivedEvents}, manufacturer sections={manufacturerSections}.");
            }
        }
    }
}
