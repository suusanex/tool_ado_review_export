using System;
using System.Threading.Tasks;
using System.Threading;
using System.IO;
using AdoReviewExport.Application.Exceptions;
using AdoReviewExport.Application.Models;
using AdoReviewExport.Application.Services;
using AdoReviewExport.Infrastructure.AzureDevOps;
using AdoReviewExport.Infrastructure.Json;
using AdoReviewExport.UI.Helpers;
using Microsoft.Extensions.DependencyInjection;
using NLog;
using NLog.Config;

namespace AdoReviewExport.UI;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (IsCliMode(args))
        {
            return RunCliMode(args);
        }

        Microsoft.UI.Xaml.Application.Start(_params =>
        {
            var context = new Microsoft.UI.Dispatching.DispatcherQueueSynchronizationContext(
                Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread());
            SynchronizationContext.SetSynchronizationContext(context);
            _ = new App();
        });

        return 0;
    }

    private static bool IsCliMode(string[] args)
    {
        return args.Length > 0 && args[0].StartsWith("--", StringComparison.Ordinal);
    }

    private static int RunCliMode(string[] args)
    {
        ConsoleHelper.EnsureConsole();
        ConfigureLogging();

        var parsed = CliArgumentParser.Parse(args);
        if (parsed.ShowHelp)
        {
            Console.WriteLine(CliArgumentParser.GetUsage());
            return 0;
        }

        if (string.IsNullOrWhiteSpace(parsed.Organization) ||
            string.IsNullOrWhiteSpace(parsed.Project) ||
            string.IsNullOrWhiteSpace(parsed.Repository) ||
            string.IsNullOrWhiteSpace(parsed.PersonalAccessToken) ||
            string.IsNullOrWhiteSpace(parsed.OutputFilePath))
        {
            ConsoleHelper.WriteErrorLine("Missing required arguments.");
            Console.WriteLine(CliArgumentParser.GetUsage());
            return 1;
        }

        using var cts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            cts.Cancel();
        };

        // 統合テスト用: Ctrl+C を直接送れない環境でもキャンセル経路を検証できるようにする。
        var cancelAfterMsRaw = Environment.GetEnvironmentVariable("ADO_REVIEW_EXPORT_CLI_TEST_CANCEL_AFTER_MS");
        if (!string.IsNullOrWhiteSpace(cancelAfterMsRaw) &&
            int.TryParse(cancelAfterMsRaw, out var cancelAfterMs) &&
            cancelAfterMs > 0)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(cancelAfterMs).ConfigureAwait(false);
                    cts.Cancel();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Trace.TraceError(ex.ToString());
                }
            });
        }

        try
        {
            var services = ConfigureServices();
            var exportService = services.GetRequiredService<IExportService>();

            var request = new ExportRequest(
                Organization: parsed.Organization,
                Project: parsed.Project,
                Repository: parsed.Repository,
                PersonalAccessToken: parsed.PersonalAccessToken,
                OutputFilePath: parsed.OutputFilePath,
                AuthorFilters: parsed.Authors is null || parsed.Authors.Count == 0 ? null : parsed.Authors);

            var progress = new Progress<ExportProgress>(p =>
            {
                Console.WriteLine($"{p.CurrentStatus} (PR {p.ProcessedPullRequests}/{p.TotalPullRequests}, Comments={p.TotalComments})");
            });

            var result = exportService.ExportAsync(request, progress, cts.Token).GetAwaiter().GetResult();
            Console.WriteLine($"Completed. Output={result.OutputFilePath}");
            return 0;
        }
        catch (OperationCanceledException)
        {
            TryDeletePartialFile(parsed.OutputFilePath);
            ConsoleHelper.WriteErrorLine("Canceled.");
            return 130;
        }
        catch (InputValidationException ex)
        {
            TryDeletePartialFile(parsed.OutputFilePath);
            ConsoleHelper.WriteErrorLine(ex.Message);
            return 1;
        }
        catch (AuthenticationException ex)
        {
            TryDeletePartialFile(parsed.OutputFilePath);
            ConsoleHelper.WriteErrorLine(ex.Message);
            return 2;
        }
        catch (ApiException ex)
        {
            TryDeletePartialFile(parsed.OutputFilePath);
            ConsoleHelper.WriteErrorLine(ex.Message);
            return 3;
        }
        catch (OutputException ex)
        {
            TryDeletePartialFile(parsed.OutputFilePath);
            ConsoleHelper.WriteErrorLine(ex.Message);
            return 4;
        }
        catch (Exception ex)
        {
            TryDeletePartialFile(parsed.OutputFilePath);
            ConsoleHelper.WriteErrorLine("Unexpected error.");
            System.Diagnostics.Trace.TraceError(ex.ToString());
            return 1;
        }
    }

    private static void TryDeletePartialFile(string? outputFilePath)
    {
        if (string.IsNullOrWhiteSpace(outputFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(outputFilePath))
            {
                File.Delete(outputFilePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
    }

    private static ServiceProvider ConfigureServices()
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
