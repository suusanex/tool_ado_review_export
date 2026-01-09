using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using NLog;
using NLog.Config;
using System;
using System.IO;
using System.Threading.Tasks;
using AdoReviewExport.Application.Services;
using AdoReviewExport.Infrastructure.AzureDevOps;
using AdoReviewExport.Infrastructure.Json;
using AdoReviewExport.UI.ViewModels;

namespace AdoReviewExport.UI;

public sealed partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private readonly IServiceProvider _services;

    public App()
    {
        ConfigureLogging();
        _services = ConfigureServices();
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        var mainWindow = new Views.MainWindow();
        var vm = _services.GetRequiredService<MainViewModel>();

        var isGuiTestAutoRun = string.Equals(
            Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_AUTORUN"),
            "1",
            StringComparison.OrdinalIgnoreCase);

        mainWindow.Initialize(vm, enableDialogs: !isGuiTestAutoRun);

        _window = mainWindow;
        _window.Activate();

        // 統合テスト用: GUI を起動できることだけを確認し、すぐ終了する。
        if (string.Equals(Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_EXIT"), "1", StringComparison.OrdinalIgnoreCase))
        {
            _window.Closed += (_, __) => Exit();
            _window.Close();
        }

        // 統合テスト用: UI 自動操作を避けるため、環境変数で入力値を受け取り自動実行→終了する。
        if (isGuiTestAutoRun)
        {
            _ = RunGuiAutoExportAsync(vm, _window);
        }
    }

    private static async Task RunGuiAutoExportAsync(MainViewModel vm, Window window)
    {
        try
        {
            vm.Organization = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_ORG") ?? vm.Organization;
            vm.Project = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_PROJECT") ?? vm.Project;
            vm.Repository = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_REPO") ?? vm.Repository;
            vm.PersonalAccessToken = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_PAT") ?? vm.PersonalAccessToken;
            vm.OutputFilePath = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_OUTPUT") ?? vm.OutputFilePath;
            vm.Authors = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_AUTHORS") ?? vm.Authors;

            var cancelAfterMsRaw = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_GUI_TEST_CANCEL_AFTER_MS");
            var cancelAfterMs = 0;
            if (!string.IsNullOrWhiteSpace(cancelAfterMsRaw) && int.TryParse(cancelAfterMsRaw, out var parsed) && parsed > 0)
            {
                cancelAfterMs = parsed;
            }

            // ダイアログを出さずに終了コードで結果を判断できるようにする。
            vm.DialogRequested += (_, req) =>
            {
                try
                {
                    Environment.ExitCode = req.Kind == DialogKind.Completion ? 0 : 1;
                    window.Close();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                    throw;
                }
            };

            vm.StartExportCommand.Execute(null);

            if (cancelAfterMs > 0)
            {
                await Task.Delay(cancelAfterMs).ConfigureAwait(true);
                vm.CancelCommand.Execute(null);
            }

            var deadline = DateTimeOffset.UtcNow.AddMinutes(2);
            while (vm.IsExporting && DateTimeOffset.UtcNow < deadline)
            {
                await Task.Delay(100).ConfigureAwait(true);
            }

            if (vm.IsExporting)
            {
                Environment.ExitCode = 124; // timeout
                window.Close();
                return;
            }

            if (string.Equals(vm.StatusText, "キャンセル", StringComparison.Ordinal))
            {
                Environment.ExitCode = 130;
                window.Close();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
            Environment.ExitCode = 1;
            try
            {
                window.Close();
            }
            catch (Exception closeEx)
            {
                System.Diagnostics.Trace.TraceError(closeEx.ToString());
                throw;
            }
        }
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton<IAdoApiClientFactory, AdoApiClientFactory>();
        services.AddSingleton<IJsonExporter, JsonExporter>();
        services.AddSingleton<IExportService, ExportService>();
        services.AddTransient<MainViewModel>();

        return services.BuildServiceProvider();
    }

    private static void ConfigureLogging()
    {
        try
        {
            LogManager.ThrowConfigExceptions = true;
            var configPath = Path.Combine(AppContext.BaseDirectory, "nlog.config");
            LogManager.Configuration = new XmlLoggingConfiguration(configPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
            throw;
        }
    }
}
