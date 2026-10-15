using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using HearEqualOrNah.Core.Interfaces;
using HearEqualOrNah.Core.Services;
using HearEqualOrNah.Audio.Devices;
using HearEqualOrNah.Audio.Playback;
using HearEqualOrNah.Audio.Capture;
using HearEqualOrNah.WindowsAudio.Balance;
using HearEqualOrNah.WindowsAudio.EndpointVolume;
using HearEqualOrNah.UI.ViewModels;
using HearEqualOrNah.UI.Views;

namespace HearEqualOrNah.UI;

public partial class App : Application
{
    private readonly IServiceProvider _serviceProvider;

    public App()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        _serviceProvider = services.BuildServiceProvider();
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.AddProvider(new FileLoggerProvider(System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "hearequalornah",
                "app.log")));
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core Services
        services.AddSingleton<ISignalGenerator, SignalGenerator>();
        services.AddSingleton<ISignalAnalyzer, SignalAnalyzer>();
        services.AddSingleton<IBalanceClassifier, BalanceClassifier>();
        services.AddSingleton<ICorrectionCalculator, CorrectionCalculator>();
        services.AddSingleton<ICalibrationService, CalibrationService>();
        services.AddSingleton<ITestHistoryService, TestHistoryService>();
        services.AddSingleton<ISettingsService, SettingsService>();

        // Audio Services
        services.AddSingleton<IAudioDeviceService, AudioDeviceService>();
        services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();
        services.AddSingleton<IMicrophoneCaptureService, MicrophoneCaptureService>();

        // Windows Audio Services
        services.AddSingleton<IEndpointVolumeService, EndpointVolumeService>();
        services.AddSingleton<IWindowsBalanceService, WindowsBalanceService>();

        // Measurement Engine
        services.AddSingleton<IMeasurementEngine, MeasurementEngine>();

        // ViewModels
        services.AddTransient<MainViewModel>();

        // Views
        services.AddTransient<MainWindow>();
    }

    protected override void OnStartup(StartupEventArgs e)
    {
        try
        {
            base.OnStartup(e);

            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            var logPath = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "hearequalornah",
                "startup.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(logPath)!);
            System.IO.File.WriteAllText(logPath, ex.ToString());
            MessageBox.Show("Startup error:\n" + ex.Message + "\n\nSee " + logPath, "hearequalornah");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
        base.OnExit(e);
    }
}