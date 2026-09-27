using System.Collections.ObjectModel;
using System.Diagnostics;
using RcCar.App.Infrastructure;
using RcCar.Core.Abstractions;
using RcCar.Core.Models;
using RcCar.Core.Services;

namespace RcCar.App.Presentation;

public sealed class MainViewModel : ObservableObject
{
    private readonly ICarService cars;
    private readonly IAdapterInspector adapter;
    private readonly SessionLog log;
    private readonly ManualControlInput input;
    private CancellationTokenSource? cancellation;
    private Task running = Task.CompletedTask;
    private bool busy;
    private bool driving;
    private bool keyboardReady;
    private bool closing;
    private CarCandidate? selectedCar;
    private string status = "Start by checking the adapter, then discover your car.";
    private string adapterText = "Adapter has not been checked.";
    private string observation = "";
    private string lastTest = "No diagnostic completed yet";
    private string gearText = "Gear 1 · 40%";
    private string requestedState = "Neutral";
    private bool observationExpanded;
    private readonly List<RelayCommand> commands = [];

    public ObservableCollection<CarCandidate> Cars { get; } = [];
    public ObservableCollection<string> Events { get; } = [];
    public string LogFolder => log.Folder;
    public bool IsBusy => busy;
    public bool CanSelect => !busy && !closing;
    public bool IsDriving => driving;
    public bool ForwardPressed { get; private set; }
    public bool ReversePressed { get; private set; }
    public bool LeftPressed { get; private set; }
    public bool RightPressed { get; private set; }
    public string Status
    {
        get => status;
        private set => Set(ref status, value);
    }

    public string AdapterText
    {
        get => adapterText;
        private set => Set(ref adapterText, value);
    }

    public string GearText
    {
        get => gearText;
        private set => Set(ref gearText, value);
    }

    public string RequestedState
    {
        get => requestedState;
        private set => Set(ref requestedState, value);
    }

    public string LastTest
    {
        get => lastTest;
        private set => Set(ref lastTest, value);
    }

    public bool ObservationExpanded
    {
        get => observationExpanded;
        set => Set(ref observationExpanded, value);
    }

    public string Observation
    {
        get => observation;
        set
        {
            Set(ref observation, value);
            SaveObservationCommand.Refresh();
        }
    }

    public CarCandidate? SelectedCar
    {
        get => selectedCar;
        set
        {
            if (!Set(ref selectedCar, value))
            {
                return;
            }

            if (value is not null && Cars.Count(car => car.Identity == value.Identity) > 1)
            {
                Status = "This identity appeared at multiple addresses. Turn other cars OFF and discover again before moving.";
            }

            RefreshCommands();
        }
    }

    public RelayCommand InspectCommand { get; }
    public RelayCommand DiscoverCommand { get; }
    public RelayCommand ForwardCommand { get; }
    public RelayCommand SteeringCommand { get; }
    public RelayCommand ReverseCommand { get; }
    public RelayCommand LightsCommand { get; }
    public RelayCommand DriveCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand OpenLogsCommand { get; }
    public RelayCommand SaveObservationCommand { get; }

    public MainViewModel(ICarService cars, IAdapterInspector adapter, SessionLog log, ManualControlInput input)
    {
        this.cars = cars;
        this.adapter = adapter;
        this.log = log;
        this.input = input;
        InspectCommand = Command(() => StartOperation("Adapter check", InspectAsync), () => !busy && !closing);
        DiscoverCommand = Command(() => StartOperation("Discovery", DiscoverAsync), () => !busy && !closing);
        ForwardCommand = Command(() => StartTest(DiagnosticTest.Forward), CanOperateCar);
        SteeringCommand = Command(() => StartTest(DiagnosticTest.Steering), CanOperateCar);
        ReverseCommand = Command(() => StartTest(DiagnosticTest.Reverse), CanOperateCar);
        LightsCommand = Command(() => StartTest(DiagnosticTest.Lights), CanOperateCar);
        DriveCommand = Command(StartDriving, CanOperateCar);
        StopCommand = Command(Stop, () => busy && !closing);
        OpenLogsCommand = Command(OpenLogs);
        SaveObservationCommand = Command(SaveObservation, () => !string.IsNullOrWhiteSpace(observation) && !busy && !closing);
    }

    private RelayCommand Command(Action execute, Func<bool>? canExecute = null)
    {
        var command = new RelayCommand(execute, canExecute);
        commands.Add(command);
        return command;
    }

    private bool CanOperateCar() => !busy && !closing && SelectedCar is not null && Cars.Count(car => car.Identity == SelectedCar.Identity) == 1;
    private void RefreshCommands()
    {
        foreach (var command in commands)
        {
            command.Refresh();
        }

        Changed(nameof(IsBusy));
        Changed(nameof(CanSelect));
        Changed(nameof(IsDriving));
    }

    private async Task InspectAsync(CancellationToken token)
    {
        var info = await adapter.InspectAsync(token);
        AdapterText = $"{info.Name} · {info.Address} · Radio {info.RadioState}\nLE {info.LowEnergySupported} · Peripheral {info.PeripheralRoleSupported} · Offload {info.AdvertisementOffloadSupported}";
        Status = "Adapter inspected. Actual advertising support is checked during discovery.";
    }

    private async Task DiscoverAsync(CancellationToken token)
    {
        Cars.Clear();
        SelectedCar = null;
        var candidates = await cars.DiscoverAsync(token);
        foreach (var candidate in candidates)
        {
            Cars.Add(candidate);
        }

        // A single result is convenient; multiple candidates always require explicit selection.
        if (Cars.Count == 1)
        {
            SelectedCar = Cars[0];
        }

        Status = Cars.Count == 0 ? "No matching replies. Check power-on timing and review the log." : $"Found {Cars.Count} candidate(s). Select your car. Turn it OFF before starting a test or driving.";
    }

    private void StartTest(DiagnosticTest test)
    {
        var target = SelectedCar!.Identity;
        LastTest = $"{test} · vehicle {target}";
        StartOperation(LastTest, async token =>
        {
            await cars.RunTestAsync(target, test, token);
            ObservationExpanded = true;
            Status = test == DiagnosticTest.Lights
                ? "Light test transmissions completed. Turn the car OFF and record what you observed."
                : $"{test} transmissions completed. Turn the car OFF and record what you observed.";
        });
    }

    private void StartDriving()
    {
        driving = true;
        ObservationExpanded = false;
        var target = SelectedCar!.Identity;
        StartOperation($"Keyboard driving · vehicle {target}", token => cars.DriveAsync(target, input, token));
    }

    private void StartOperation(string name, Func<CancellationToken, Task> action)
    {
        if (busy || closing)
        {
            return;
        }

        busy = true;
        keyboardReady = false;
        input.Reset();
        cancellation = new CancellationTokenSource();
        Status = name == "Adapter check" ? "Checking adapter…" : "Starting. Keep the car OFF until PUBLISHER READY, then turn it ON.";
        log.Write(SessionEventKind.Information, $"BEGIN {name}");
        RefreshCommands();
        // Core work runs away from the UI; continuations in this method report back through WPF.
        running = ExecuteOperationAsync(name, action, cancellation.Token);
    }

    private async Task ExecuteOperationAsync(string name, Func<CancellationToken, Task> action, CancellationToken token)
    {
        try
        {
            await action(token);
            log.Write(SessionEventKind.Information, $"END {name}: transmission workflow completed");
        }
        catch (OperationCanceledException)
        {
            Status = "Stopped. Neutral cleanup finished where pairing had succeeded. Turn car OFF.";
            log.Write(SessionEventKind.Information, $"END {name}: cancelled");
        }
        catch (Exception exception)
        {
            Status = "Operation failed. Turn the car OFF. " + exception.GetBaseException().Message;
            log.Write(SessionEventKind.Error, $"END {name}: {exception}");
        }
        finally
        {
            keyboardReady = false;
            driving = false;
            input.Reset();
            busy = false;
            cancellation?.Dispose();
            cancellation = null;
            RefreshCommands();
        }
    }

    public void PollInput(bool windowActive, HeldControls held)
    {
        var enabled = windowActive && driving && keyboardReady && !closing;
        var lightsWereOn = input.Read().LightsOn;
        input.Update(enabled, held);
        var state = input.Read();
        if (lightsWereOn != state.LightsOn)
        {
            var reason = enabled && held.ToggleLights ? "Enter key" : "input reset";
            log.Write(
                SessionEventKind.Information,
                $"Light state changed to {(state.LightsOn ? "on" : "off")} ({reason}).");
        }

        ForwardPressed = enabled && held.Forward;
        ReversePressed = enabled && held.Reverse;
        LeftPressed = enabled && held.Left;
        RightPressed = enabled && held.Right;
        Changed(nameof(ForwardPressed));
        Changed(nameof(ReversePressed));
        Changed(nameof(LeftPressed));
        Changed(nameof(RightPressed));
        var selectedGear = input.SelectedGear;
        GearText = $"Gear {(int)selectedGear + 1} · {selectedGear.Speed()}%";
        RequestedState = state.ToString();
    }

    public void LoseFocus()
    {
        log.Write(SessionEventKind.Information, "Window focus lost; clearing movement and lights.");
        input.Reset();
    }

    public void DrainEvents()
    {
        for (var i = 0; i < 200 && log.TryRead(out var entry); i++)
        {
            if (entry is null)
            {
                continue;
            }

            Events.Add($"{entry.Timestamp:HH:mm:ss.fff} [{entry.Kind}] {entry.Message}");
            if (Events.Count > 500)
            {
                Events.RemoveAt(0);
            }

            if (entry.Kind == SessionEventKind.PublisherReady && busy && cancellation?.IsCancellationRequested == false)
            {
                Status = entry.Message;
            }

            if (entry.Kind == SessionEventKind.DrivingReady && driving && cancellation?.IsCancellationRequested == false)
            {
                input.Reset();
                keyboardReady = true;
                Status = entry.Message;
            }
        }
    }

    public void Stop()
    {
        keyboardReady = false;
        input.Reset();
        cancellation?.Cancel();
        if (busy)
        {
            Status = "Stopping. Please wait for final neutral transmissions.";
        }
    }

    public async Task ShutdownAsync()
    {
        closing = true;
        Stop();
        RefreshCommands();
        await running;
        DrainEvents();
    }

    private void SaveObservation()
    {
        log.Write(SessionEventKind.Observation, $"{LastTest}: {Observation.Trim()}");
        Observation = "";
        Status = "Physical observation saved with this session.";
    }

    private void OpenLogs()
    {
        try
        {
            Process.Start(new ProcessStartInfo(log.Folder) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Status = "Cannot open folder: " + exception.Message;
        }
    }
}
