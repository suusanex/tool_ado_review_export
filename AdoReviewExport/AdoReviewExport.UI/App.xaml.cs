using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using NLog;
using NLog.Config;
using System;
using AdoReviewExport.Application.Services;
using AdoReviewExport.Infrastructure.AzureDevOps;
using AdoReviewExport.Infrastructure.Json;

namespace AdoReviewExport.UI;

public sealed partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private IServiceProvider? _services;

    public App()
    {
        ConfigureLogging();
        _services = ConfigureServices();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Views.MainWindow();
        _window.Activate();
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAdoApiClientFactory, AdoApiClientFactory>();
        services.AddSingleton<IJsonExporter, JsonExporter>();
        services.AddSingleton<IExportService, ExportService>();

        return services.BuildServiceProvider();
    }

    private static void ConfigureLogging()
    {
        try
        {
            LogManager.ThrowConfigExceptions = true;
            LogManager.Configuration = new XmlLoggingConfiguration("nlog.config");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
            throw;
        }
    }
}
