namespace HearEqualOrNah.UI.ViewModels;

using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Models;
using HearEqualOrNah.Core.Services;
using HearEqualOrNah.Core.Constants;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly IAudioDeviceService _deviceService;
    private readonly IMeasurementEngine _measurementEngine;
    private readonly IMicrophoneCaptureService _captureService;
    private readonly IWindowsBalanceService _balanceService;
    private readonly ICorrectionCalculator _correctionCalculator;
    private readonly ICalibrationService _calibrationService;
    private readonly ITestHistoryService _historyService;
    private readonly ISettingsService _settingsService;
    private readonly IBalanceClassifier _classifier;
    private readonly ILogger<MainViewModel> _logger;

    private ApplicationSettings _settings = new ApplicationSettings(
        MicrophoneGain: 1.0,
        PreferredSampleRate: TestParameters.SampleRate,
        AutoApplyCorrection: false,
        ShowDetailedResults: true,
        LastUsedMicrophoneId: null,
        LastUsedOutputDeviceId: null,
        LastUsedCalibrationId: null
    );
    private AudioDeviceInfo? _selectedMicrophone;
    private AudioDeviceInfo? _selectedOutputDevice;
    private BalanceTestResult? _lastResult;
    private WindowsChannelBalance? _balanceBefore;
    private CancellationTokenSource? _testCancellation;
    private bool _correctionApplied;
    private WindowsChannelBalance? _balanceBeforeCorrection;
    private readonly System.Windows.Threading.DispatcherTimer _balanceRefreshTimer;
    private bool _isRefreshingBalance;

    public MainViewModel(
        IAudioDeviceService deviceService,
        IMeasurementEngine measurementEngine,
        IMicrophoneCaptureService captureService,
        IWindowsBalanceService balanceService,
        ICorrectionCalculator correctionCalculator,
        ICalibrationService calibrationService,
        ITestHistoryService historyService,
        ISettingsService settingsService,
        IBalanceClassifier balanceClassifier,
        ILogger<MainViewModel> logger)
    {
        _deviceService = deviceService;
        _measurementEngine = measurementEngine;
        _captureService = captureService;
        _balanceService = balanceService;
        _correctionCalculator = correctionCalculator;
        _calibrationService = calibrationService;
        _historyService = historyService;
        _settingsService = settingsService;
        _classifier = balanceClassifier;
        _logger = logger;

        _measurementEngine.StateChanged += OnStateChanged;
        _measurementEngine.ChannelMeasured += OnChannelMeasured;
        _measurementEngine.LiveLevelChanged += OnLiveLevelChanged;
        _measurementEngine.ErrorOccurred += OnErrorOccurred;
        _balanceService.BalanceChanged += OnBalanceChanged;

        _captureService.LevelChanged += OnCaptureLevelChanged;

        _deviceService.DevicesChanged += OnDevicesChanged;

        StartTestCommand = new AsyncRelayCommand(StartTestAsync, CanStartTest);
        MeasureLeftCommand = new AsyncRelayCommand(MeasureLeftAsync, CanMeasureLeft);
        MeasureRightCommand = new AsyncRelayCommand(MeasureRightAsync, CanMeasureRight);
        StopTestCommand = new RelayCommand(StopTest);
        ApplyCorrectionCommand = new AsyncRelayCommand(ApplyCorrectionAsync, CanApplyCorrection);
        UndoCorrectionCommand = new AsyncRelayCommand(UndoCorrectionAsync, () => _balanceBeforeCorrection != null);
        ResetBalanceCommand = new AsyncRelayCommand(ResetBalanceAsync);
        RefreshDevicesCommand = new RelayCommand(LoadDevices);
        ToggleHistoryCommand = new RelayCommand(() => ShowHistorySection = !ShowHistorySection);
        ClearHistoryCommand = new AsyncRelayCommand(ClearHistoryAsync);

        // Poll the Windows balance (read-only) so the user can adjust it in Windows and watch the
        // offset reach the recommended target.
        _balanceRefreshTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _balanceRefreshTimer.Tick += (_, _) => LoadBalanceAsync();
        _balanceRefreshTimer.Start();

        LoadDevices();
        _ = LoadSettings();
        _ = LoadHistory();
    }

    protected override void OnPropertyChanged(PropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);

        switch (e.PropertyName)
        {
            case nameof(IsTesting):
            case nameof(SelectedMicrophone):
            case nameof(SelectedOutputDevice):
            case nameof(CurrentState):
                StartTestCommand.NotifyCanExecuteChanged();
                MeasureLeftCommand.NotifyCanExecuteChanged();
                MeasureRightCommand.NotifyCanExecuteChanged();
                break;
            case nameof(ProposedCorrection):
            case nameof(CurrentWindowsBalance):
                OnPropertyChanged(nameof(TargetOffsetText));
                OnPropertyChanged(nameof(CurrentOffsetText));
                OnPropertyChanged(nameof(RemainingAdjustmentText));
                ApplyCorrectionCommand.NotifyCanExecuteChanged();
                break;
            case nameof(ShowHistorySection):
                OnPropertyChanged(nameof(HistoryToggleText));
                break;
            case nameof(LeftLevelDb):
            case nameof(RightLevelDb):
                OnPropertyChanged(nameof(DifferenceDisplay));
                break;
            case nameof(LiveLevelDb):
                OnPropertyChanged(nameof(LiveMeterWidth));
                break;
            case nameof(MicMonitorDb):
                OnPropertyChanged(nameof(MicMonitorWidth));
                OnPropertyChanged(nameof(MicMonitorText));
                break;
            case nameof(ShowResultsSection):
            case nameof(DifferenceDb):
                OnPropertyChanged(nameof(ShowCausesSection));
                break;
        }
    }

    public ObservableCollection<AudioDeviceInfo> Microphones { get; } = new();
    public ObservableCollection<AudioDeviceInfo> OutputDevices { get; } = new();
    public ObservableCollection<TestHistoryEntry> History { get; } = new();

    public AudioDeviceInfo? SelectedMicrophone
    {
        get => _selectedMicrophone;
        set
        {
            if (SetProperty(ref _selectedMicrophone, value))
            {
                StartTestCommand.NotifyCanExecuteChanged();
                StartMicMonitoring();
                _ = SaveSettingsAsync();
            }
        }
    }

    public AudioDeviceInfo? SelectedOutputDevice
    {
        get => _selectedOutputDevice;
        set
        {
            if (SetProperty(ref _selectedOutputDevice, value))
            {
                StartTestCommand.NotifyCanExecuteChanged();
                LoadBalanceAsync();
                _ = SaveSettingsAsync();
            }
        }
    }

    [ObservableProperty]
    private TestState _currentState = TestState.Idle;

    [ObservableProperty]
    private string _statusText = "Ready to test";

    [ObservableProperty]
    private double _liveLevelDb = double.NegativeInfinity;

    [ObservableProperty]
    private double _leftLevelDb = double.NegativeInfinity;

    [ObservableProperty]
    private double _rightLevelDb = double.NegativeInfinity;

    [ObservableProperty]
    private double _differenceDb = 0;

    [ObservableProperty]
    private string _classification = "--";

    [ObservableProperty]
    private string _description = "Waiting for test...";

    [ObservableProperty]
    private MeasurementQuality _leftQuality = MeasurementQuality.Good;

    [ObservableProperty]
    private MeasurementQuality _rightQuality = MeasurementQuality.Good;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private double _testProgress;

    [ObservableProperty]
    private string _currentChannelInstruction = "Place the LEFT earphone's sound outlet a few millimetres from the microphone opening.";

    [ObservableProperty]
    private bool _showHistorySection;

    [ObservableProperty]
    private ProposedCorrection? _proposedCorrection;

    [ObservableProperty]
    private WindowsChannelBalance? _currentWindowsBalance;

    [ObservableProperty]
    private bool _showCorrectionSection;

    [ObservableProperty]
    private bool _showResultsSection;

    [ObservableProperty]
    private string _errorMessage = "";

    public string DifferenceDisplay =>
        (LeftLevelDb > -200 && RightLevelDb > -200)
            ? $"{DifferenceDb:F1} dB"
            : "--";

    public double LiveMeterWidth =>
        double.IsNegativeInfinity(LiveLevelDb) || LiveLevelDb < -80
            ? 0
            : Math.Clamp((LiveLevelDb + 80) / 80.0, 0.0, 1.0) * 400;

    public double MicMonitorWidth =>
        double.IsNegativeInfinity(MicMonitorDb) || MicMonitorDb < -80
            ? 0
            : Math.Clamp((MicMonitorDb + 80) / 80.0, 0.0, 1.0) * 400;

    public string MicMonitorText =>
        double.IsNegativeInfinity(MicMonitorDb)
            ? "-- dB (no signal)"
            : $"{MicMonitorDb:F0} dB";

    [ObservableProperty]
    private double _micMonitorDb = double.NegativeInfinity;

    public bool ShowCausesSection =>
        ShowResultsSection && DifferenceDb > BalanceClassification.SlightMaxDb;

    public string HistoryToggleText => ShowHistorySection ? "Hide" : "Show";

    public IReadOnlyList<string> PossibleCauses => _classifier.GetPossibleCauses();

    public string SelectedOutputName => SelectedOutputDevice?.Name ?? "No output device";

    public IAsyncRelayCommand StartTestCommand { get; }
    public IAsyncRelayCommand MeasureLeftCommand { get; }
    public IAsyncRelayCommand MeasureRightCommand { get; }
    public IRelayCommand StopTestCommand { get; }
    public IAsyncRelayCommand ApplyCorrectionCommand { get; }
    public IAsyncRelayCommand UndoCorrectionCommand { get; }
    public IAsyncRelayCommand ResetBalanceCommand { get; }
    public IRelayCommand RefreshDevicesCommand { get; }
    public IRelayCommand ToggleHistoryCommand { get; }
    public IAsyncRelayCommand ClearHistoryCommand { get; }

    private bool CanStartTest() =>
        !IsTesting
        && (CurrentState == TestState.Idle
            || CurrentState == TestState.Completed
            || CurrentState == TestState.Error)
        && SelectedMicrophone != null
        && SelectedOutputDevice != null;

    private bool CanMeasureLeft() =>
        !IsTesting
        && CurrentState == TestState.ReadyForLeft
        && _balanceBefore != null;

    private bool CanMeasureRight() =>
        !IsTesting
        && CurrentState == TestState.ReadyForRight;

    private bool CanApplyCorrection() => ProposedCorrection != null && CurrentWindowsBalance != null && CurrentWindowsBalance.IsSupported && !_correctionApplied;

    private static string DescribeOffset(double offsetDb) =>
        Math.Abs(offsetDb) < 0.05
            ? "equal"
            : offsetDb > 0
                ? $"right {offsetDb:F1} dB below left"
                : $"left {-offsetDb:F1} dB below right";

    /// <summary>Live Windows channel offset, for matching against the recommendation.</summary>
    public string CurrentOffsetText =>
        CurrentWindowsBalance is { IsSupported: true } balance ? DescribeOffset(balance.OffsetDb) : "--";

    public string TargetOffsetText =>
        ProposedCorrection == null ? "--" : DescribeOffset(ProposedCorrection.TargetOffsetDb);

    /// <summary>How far the live Windows balance still is from the recommended offset.</summary>
    public string RemainingAdjustmentText
    {
        get
        {
            if (ProposedCorrection == null || CurrentWindowsBalance is not { IsSupported: true } balance)
                return "";
            var remaining = ProposedCorrection.TargetOffsetDb - balance.OffsetDb;
            if (Math.Abs(remaining) < 0.25)
                return "Windows balance matches the recommendation. Run the test again to verify.";
            return remaining > 0
                ? $"Still to adjust: lower the right channel (or raise the left) by {remaining:F1} dB."
                : $"Still to adjust: lower the left channel (or raise the right) by {-remaining:F1} dB.";
        }
    }

    private async void LoadDevices()
    {
        try
        {
            var mics = _deviceService.GetInputDevices();
            var outputs = _deviceService.GetOutputDevices();

            Microphones.Clear();
            foreach (var mic in mics)
                Microphones.Add(mic);

            OutputDevices.Clear();
            foreach (var output in outputs)
                OutputDevices.Add(output);

            var settings = await _settingsService.LoadAsync();

            SelectedMicrophone = Microphones.FirstOrDefault(m => m.Id == settings.LastUsedMicrophoneId)
                ?? _deviceService.GetDefaultInputDevice()
                ?? Microphones.FirstOrDefault();

            SelectedOutputDevice = OutputDevices.FirstOrDefault(o => o.Id == settings.LastUsedOutputDeviceId)
                ?? _deviceService.GetDefaultOutputDevice()
                ?? OutputDevices.FirstOrDefault();

            if (Microphones.Count == 0)
            {
                ErrorMessage = "No microphone detected. Connect a microphone and try again.";
            }

            if (OutputDevices.Count == 0)
            {
                ErrorMessage = "No output device detected. Connect headphones or speakers and try again.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load devices");
            ErrorMessage = "Failed to load audio devices.";
        }
    }

    private void OnDevicesChanged()
    {
        System.Windows.Application.Current.Dispatcher.Invoke(LoadDevices);
    }

    private async Task LoadSettings()
    {
        _settings = await _settingsService.LoadAsync();
    }

    private async Task SaveSettingsAsync()
    {
        if (SelectedMicrophone == null || SelectedOutputDevice == null)
            return;

        _settings = _settings with
        {
            LastUsedMicrophoneId = SelectedMicrophone.Id,
            LastUsedOutputDeviceId = SelectedOutputDevice.Id
        };

        await _settingsService.SaveAsync(_settings);
    }

    private async Task LoadHistory()
    {
        var history = await _historyService.GetHistoryAsync(50);
        History.Clear();
        foreach (var entry in history)
            History.Add(entry);
    }

    private async Task StartTestAsync()
    {
        if (SelectedMicrophone == null || SelectedOutputDevice == null)
            return;

        if (SelectedOutputDevice.Channels < 2)
        {
            ErrorMessage = "The selected output device is mono, so LEFT and RIGHT cannot be tested separately. " +
                "Bluetooth headsets switch to mono when their own microphone is in use; select a different microphone.";
            return;
        }

        IsTesting = true;
        TestProgress = 0;
        _correctionApplied = false;
        ErrorMessage = "";
        ShowResultsSection = false;
        ShowCorrectionSection = false;
        _lastResult = null;
        _balanceBefore = null;
        ProposedCorrection = null;
        DifferenceDb = 0;
        Classification = "--";
        Description = "";
        LeftLevelDb = double.NegativeInfinity;
        RightLevelDb = double.NegativeInfinity;
        LeftQuality = MeasurementQuality.Good;
        RightQuality = MeasurementQuality.Good;

        _captureService.StopMonitoring();

        _testCancellation = new CancellationTokenSource();

        try
        {
            _balanceBefore = await _balanceService.GetBalanceAsync(SelectedOutputDevice.Id);
            CurrentWindowsBalance = _balanceBefore;

            if (_balanceBefore.HasOffset)
            {
                ErrorMessage = "Note: a Windows channel balance is currently set on this output device. The test measures the result with that balance in place. Use RESET TO EQUAL for a raw measurement.";
            }

            var prepared = await _measurementEngine.PrepareAsync(
                SelectedMicrophone,
                SelectedOutputDevice,
                _testCancellation.Token);

            if (prepared)
            {
                TestProgress = 0.33;
                StatusText = "Ready. Set up the LEFT earphone, then press MEASURE LEFT.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Test preparation failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsTesting = false;
            TestProgress = 0;
            _testCancellation?.Dispose();
            _testCancellation = null;
        }
    }

    private async Task MeasureLeftAsync()
    {
        if (CurrentState != TestState.ReadyForLeft)
            return;

        IsTesting = true;
        ErrorMessage = "";
        LeftLevelDb = double.NegativeInfinity;

        _testCancellation = new CancellationTokenSource();

        try
        {
            var leftResult = await _measurementEngine.MeasureLeftAsync(_testCancellation.Token);
            if (leftResult != null)
            {
                TestProgress = 0.66;
                StatusText = "LEFT captured. Now set up the RIGHT earphone, then press MEASURE RIGHT.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Left measurement failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsTesting = false;
            _testCancellation?.Dispose();
            _testCancellation = null;
        }
    }

    private async Task MeasureRightAsync()
    {
        if (CurrentState != TestState.ReadyForRight)
            return;

        IsTesting = true;
        ErrorMessage = "";
        RightLevelDb = double.NegativeInfinity;

        _testCancellation = new CancellationTokenSource();

        try
        {
            var result = await _measurementEngine.MeasureRightAsync(_testCancellation.Token);

            if (result != null)
            {
                TestProgress = 1.0;
                _lastResult = result;

                // A software correction is in effect during this test if the output device's
                // current channel balance is not equal, regardless of whether it was applied
                // in this session or a previous one.
                var correctionActive = _balanceBefore is { HasOffset: true };

                if (correctionActive)
                {
                    result = result with { WasCorrectionApplied = true };
                    _lastResult = result;
                }

                DifferenceDb = result.DifferenceDb;
                Classification = result.Classification;
                Description = result.Description;
                LeftLevelDb = result.LeftResult.DbLevel;
                RightLevelDb = result.RightResult.DbLevel;
                LeftQuality = result.LeftResult.Quality;
                RightQuality = result.RightResult.Quality;

                ShowResultsSection = true;

                if (_balanceBefore != null && _balanceBefore.IsSupported)
                {
                    ProposedCorrection = _correctionCalculator.Calculate(result, _balanceBefore);
                    ShowCorrectionSection = true;
                }

                var entry = new TestHistoryEntry(
                    Id: Guid.NewGuid().ToString(),
                    Timestamp: result.Timestamp,
                    Result: result,
                    CalibrationUsed: null,
                    BalanceBefore: _balanceBefore,
                    BalanceAfter: correctionActive ? CurrentWindowsBalance : null
                );

                await _historyService.SaveTestAsync(entry);
                History.Insert(0, entry);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Right measurement failed");
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsTesting = false;
            _testCancellation?.Dispose();
            _testCancellation = null;
        }
    }

    private void StopTest()
    {
        _testCancellation?.Cancel();
    }

    private async Task ApplyCorrectionAsync()
    {
        if (ProposedCorrection == null || SelectedOutputDevice == null)
            return;

        try
        {
            // Re-read the live balance: if the master volume changed since the test, apply the
            // measured channel offset relative to the current level instead of stale absolute values.
            var current = await _balanceService.GetBalanceAsync(SelectedOutputDevice.Id);
            if (!current.IsSupported)
            {
                ErrorMessage = current.UnsupportedReason ?? "This device does not support channel balance.";
                return;
            }

            var (leftDb, rightDb) = CorrectionCalculator.ApplyOffset(ProposedCorrection.TargetOffsetDb, current);
            var success = await _balanceService.SetBalanceAsync(SelectedOutputDevice.Id, leftDb, rightDb);

            if (success)
            {
                _balanceBeforeCorrection = current;
                CurrentWindowsBalance = await _balanceService.GetBalanceAsync(SelectedOutputDevice.Id);
                _correctionApplied = true;
                ApplyCorrectionCommand.NotifyCanExecuteChanged();
                UndoCorrectionCommand.NotifyCanExecuteChanged();
                StatusText = "Correction applied. Run the test again to verify.";
            }
            else
            {
                ErrorMessage = "Failed to apply correction. The device may not support channel volume control.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to apply correction");
            ErrorMessage = "Failed to apply correction.";
        }
    }

    private async Task UndoCorrectionAsync()
    {
        if (_balanceBeforeCorrection == null || SelectedOutputDevice == null)
            return;

        try
        {
            var success = await _balanceService.SetBalanceAsync(
                SelectedOutputDevice.Id,
                _balanceBeforeCorrection.LeftDb,
                _balanceBeforeCorrection.RightDb);

            if (success)
            {
                _balanceBeforeCorrection = null;
                _correctionApplied = false;
                CurrentWindowsBalance = await _balanceService.GetBalanceAsync(SelectedOutputDevice.Id);
                UndoCorrectionCommand.NotifyCanExecuteChanged();
                ApplyCorrectionCommand.NotifyCanExecuteChanged();
                StatusText = "Previous balance restored.";
            }
            else
            {
                ErrorMessage = "Failed to restore previous balance.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to undo correction");
            ErrorMessage = "Failed to undo correction.";
        }
    }

    private async Task ResetBalanceAsync()
    {
        if (SelectedOutputDevice == null)
            return;

        try
        {
            // Equalise at the louder channel's current level. Never jump to 0 dB (full volume).
            var current = await _balanceService.GetBalanceAsync(SelectedOutputDevice.Id);
            if (!current.IsSupported)
            {
                ErrorMessage = current.UnsupportedReason ?? "This device does not support channel balance.";
                return;
            }

            var level = Math.Max(current.LeftDb, current.RightDb);
            var success = await _balanceService.SetBalanceAsync(SelectedOutputDevice.Id, level, level);

            if (success)
            {
                if (current.HasOffset)
                {
                    _balanceBeforeCorrection = current;
                    UndoCorrectionCommand.NotifyCanExecuteChanged();
                }
                _correctionApplied = false;
                ApplyCorrectionCommand.NotifyCanExecuteChanged();
                CurrentWindowsBalance = await _balanceService.GetBalanceAsync(SelectedOutputDevice.Id);
                StatusText = "Balance reset to equal.";
            }
            else
            {
                ErrorMessage = "Failed to reset balance.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset balance");
            ErrorMessage = "Failed to reset balance.";
        }
    }

    private async Task ClearHistoryAsync()
    {
        await _historyService.ClearHistoryAsync();
        History.Clear();
    }

    private void OnStateChanged(TestState state)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            CurrentState = state;
            StatusText = state switch
            {
                TestState.Idle => "Ready to test",
                TestState.Preparing => "Preparing...",
                TestState.MeasuringNoise => "Measuring background noise...",
                TestState.ReadyForLeft => "Ready. Set up the LEFT earphone, then press MEASURE LEFT.",
                TestState.TestingLeft => "Testing LEFT channel...",
                TestState.ReadyForRight => "LEFT captured. Set up the RIGHT earphone, then press MEASURE RIGHT.",
                TestState.TestingRight => "Testing RIGHT channel...",
                TestState.Completed => "Test completed",
                TestState.Error => "Error occurred",
                _ => "Unknown"
            };

            CurrentChannelInstruction = state switch
            {
                TestState.ReadyForLeft => "STEP 1 - LEFT EAR: Point the LEFT earphone's sound outlet (IEM nozzle) straight at the microphone opening, a few millimetres away, then press MEASURE LEFT. Note the exact spot and angle.",
                TestState.TestingLeft => "Measuring the LEFT earphone. Keep it still and do not move it.",
                TestState.ReadyForRight => "STEP 2 - RIGHT EAR: Put the RIGHT earphone in exactly the same spot and angle as the left, then press MEASURE RIGHT.",
                TestState.TestingRight => "Measuring the RIGHT earphone. Keep it still and do not move it.",
                TestState.MeasuringNoise => "Measuring background noise... Keep the environment quiet.",
                _ => "Press START TEST to begin the two-step LEFT and RIGHT measurement."
            };

            if (state == TestState.Completed || state == TestState.Error)
            {
                StartMicMonitoring();
            }
        });
    }

    private void OnChannelMeasured(Channel channel, double dbLevel, MeasurementQuality quality)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            if (channel == Channel.Left)
            {
                LeftLevelDb = dbLevel;
                LeftQuality = quality;
            }
            else
            {
                RightLevelDb = dbLevel;
                RightQuality = quality;
            }
        });
    }

    // Level events arrive on the audio capture thread. BeginInvoke (not Invoke) so the capture
    // thread never blocks on the UI thread.
    private void OnLiveLevelChanged(double dbLevel)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            LiveLevelDb = dbLevel;
        });
    }

    private void OnCaptureLevelChanged(double dbLevel)
    {
        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_isMonitoringMic)
                MicMonitorDb = dbLevel;
        });
    }

    private bool _isMonitoringMic;

    private void StartMicMonitoring()
    {
        if (SelectedMicrophone == null)
        {
            _captureService.StopMonitoring();
            MicMonitorDb = double.NegativeInfinity;
            return;
        }

        _isMonitoringMic = true;
        try
        {
            _captureService.StartMonitoring(SelectedMicrophone, TestParameters.SampleRate);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start microphone monitoring");
            _isMonitoringMic = false;
        }
    }

    private void OnErrorOccurred(string message)
    {
        System.Windows.Application.Current.Dispatcher.Invoke(() =>
        {
            ErrorMessage = message;
        });
    }

    private void OnBalanceChanged(string deviceId, WindowsChannelBalance balance)
    {
        if (deviceId == SelectedOutputDevice?.Id)
        {
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                CurrentWindowsBalance = balance;
            });
        }
    }

    private async void LoadBalanceAsync()
    {
        if (SelectedOutputDevice == null || _isRefreshingBalance)
            return;

        _isRefreshingBalance = true;

        try
        {
            CurrentWindowsBalance = await _balanceService.GetBalanceAsync(SelectedOutputDevice.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load balance");
        }
        finally
        {
            _isRefreshingBalance = false;
        }
    }
}