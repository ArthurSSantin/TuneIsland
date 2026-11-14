using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using TuneIsland.Core;
using TuneIsland.Services;
using TuneIsland.Views;

namespace TuneIsland;

public partial class App : Application
{
    private const string SingleInstanceName = @"Local\TuneIsland.SingleInstance";

    private Mutex? _singleInstance;
    private AppSettings _settings = new();
    private MediaSessionService? _media;
    private IslandWindow? _island;
    private TrayIcon? _tray;
    private SettingsWindow? _settingsWindow;

    private void OnStartup(object sender, StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, SingleInstanceName, out bool createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        RegisterExceptionHandlers();
        Logger.Info("TuneIsland iniciado.");

        _settings = SettingsStore.Load();

        _media = new MediaSessionService();
        _island = new IslandWindow(_settings, _media);
        _island.Show();

        _tray = new TrayIcon();
        _tray.OpenSettings += (_, _) => ShowSettings();
        _tray.ExitRequested += (_, _) => Shutdown();
        _tray.AutostartChanged += (_, enabled) => OnAutostartChanged(enabled);

        _ = StartMediaAsync();
    }

    private void OnExit(object sender, ExitEventArgs e)
    {
        try
        {
            _media?.Dispose();
            _tray?.Dispose();
            _island?.Close();
            _singleInstance?.ReleaseMutex();
            _singleInstance?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Erro ao encerrar.");
        }

        Logger.Info("TuneIsland encerrado.");
    }

    private async Task StartMediaAsync()
    {
        try
        {
            if (_media is not null)
            {
                await _media.StartAsync();
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao iniciar a leitura de mídia (SMTC).");
        }
    }

    private void ShowSettings()
    {
        if (_settingsWindow is not null)
        {
            _settingsWindow.Activate();
            return;
        }

        _settingsWindow = new SettingsWindow(_settings, ApplySettings);
        _settingsWindow.Closed += (_, _) => _settingsWindow = null;
        _settingsWindow.Show();
    }

    private void ApplySettings(AppSettings updated)
    {
        _settings = updated;
        SettingsStore.Save(updated);
        Autostart.SetEnabled(updated.StartWithWindows);
        _island?.ApplySettings(updated);
    }

    private void OnAutostartChanged(bool enabled)
    {
        try
        {
            Autostart.SetEnabled(enabled);
            _settings.StartWithWindows = enabled;
            SettingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Falha ao alterar início automático.");
        }
    }

    private void RegisterExceptionHandlers()
    {
        DispatcherUnhandledException += (_, args) =>
        {
            Logger.Error(args.Exception, "Exceção não tratada na UI.");
            args.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, args) =>
        {
            Logger.Error(args.ExceptionObject as Exception, "Exceção não tratada no domínio.");
        };

        TaskScheduler.UnobservedTaskException += (_, args) =>
        {
            Logger.Error(args.Exception, "Task com exceção não observada.");
            args.SetObserved();
        };

        SystemEvents.DisplaySettingsChanged += (_, _) =>
            Dispatcher.BeginInvoke(new Action(() => _island?.Reposition()));
    }
}
