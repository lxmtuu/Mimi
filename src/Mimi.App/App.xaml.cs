using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Mimi.Services;
using Mimi.ViewModels;
using Serilog;

namespace Mimi;

/// <summary>
/// Application composition root: logging, global exception reporting, and
/// dependency-injection registrations live here so features depend on
/// abstractions instead of constructing their own infrastructure.
/// </summary>
public partial class App : Application
{
    private ServiceProvider? serviceProvider;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        ConfigureLogging();
        RegisterGlobalExceptionHandlers();
        Log.Information(
            "Mimi {Version} starting",
            Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "unknown");

        try
        {
            serviceProvider = BuildServices().BuildServiceProvider();
            var mainWindow = serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Mimi failed to start");
            MessageBox.Show(
                "Mimi couldn't start. Details were written to the log in " +
                "%LocalAppData%\\Mimi\\logs.",
                "Mimi",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("Mimi exiting with code {ExitCode}", e.ApplicationExitCode);
        serviceProvider?.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static ServiceCollection BuildServices()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IAudioPlayer, AudioService>();
        services.AddSingleton<IUserDataService, UserDataService>();
        services.AddSingleton<ITrackMetadataReader>(provider =>
            new TagLibTrackMetadataReader(
                provider.GetRequiredService<IUserDataService>()));
        services.AddSingleton<ILibraryService, LibraryService>();
        services.AddSingleton<ILibraryCatalog, SqliteLibraryCatalog>();
        services.AddSingleton<ICoverArtOptimizer, CoverArtOptimizer>();
        services.AddSingleton<ILibraryFolderWatcher, FileSystemLibraryFolderWatcher>();
        services.AddSingleton<IDialogService, WpfDialogService>();
        services.AddSingleton<MainWindowViewModel>();
        services.AddSingleton<MainWindow>();
        return services;
    }

    private static void ConfigureLogging()
    {
        try
        {
            var logDirectory = Path.Combine(
                Environment.GetFolderPath(
                    Environment.SpecialFolder.LocalApplicationData),
                "Mimi",
                "logs");
            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Information()
                .WriteTo.File(
                    Path.Combine(logDirectory, "mimi-.log"),
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 14,
                    shared: true)
                .CreateLogger();
        }
        catch (Exception ex) when (
            ex is IOException or UnauthorizedAccessException)
        {
            // Logging is diagnostic support; the player should still start
            // when its log directory is unavailable.
            System.Diagnostics.Debug.WriteLine(
                $"Mimi could not initialize file logging: {ex.Message}");
        }
    }

    private void RegisterGlobalExceptionHandlers()
    {
        DispatcherUnhandledException += App_DispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException +=
            CurrentDomain_UnhandledException;
        TaskScheduler.UnobservedTaskException +=
            TaskScheduler_UnobservedTaskException;
    }

    private void App_DispatcherUnhandledException(
        object sender,
        DispatcherUnhandledExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unhandled UI exception");
        e.Handled = true;
        MessageBox.Show(
            "Mimi hit an unexpected problem, but it will try to keep " +
            "running. Details were written to the log in " +
            "%LocalAppData%\\Mimi\\logs.",
            "Mimi",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private static void CurrentDomain_UnhandledException(
        object sender,
        UnhandledExceptionEventArgs e)
    {
        Log.Fatal(
            e.ExceptionObject as Exception,
            "Unhandled background exception; IsTerminating={IsTerminating}",
            e.IsTerminating);
        Log.CloseAndFlush();
    }

    private static void TaskScheduler_UnobservedTaskException(
        object? sender,
        UnobservedTaskExceptionEventArgs e)
    {
        Log.Error(e.Exception, "Unobserved task exception");
        e.SetObserved();
    }
}
