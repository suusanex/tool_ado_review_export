using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AdoReviewExport.Application.Exceptions;
using AdoReviewExport.Application.Models;
using AdoReviewExport.Application.Services;
using AdoReviewExport.UI.Helpers;

namespace AdoReviewExport.UI.ViewModels;

/// <summary>
/// メイン画面 ViewModel。
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly IExportService _exportService;

    private string _organization = string.Empty;
    private string _project = string.Empty;
    private string _repository = string.Empty;
    private string _personalAccessToken = string.Empty;
    private string _outputFilePath = string.Empty;
    private string _authors = string.Empty;

    private bool _isExporting;
    private double _progressValue;
    private double _progressMaximum = 1;
    private string _statusText = string.Empty;

    private CancellationTokenSource? _cts;

    public MainViewModel(IExportService exportService)
    {
        _exportService = exportService;

        Logs = new ObservableCollection<string>();

        StartExportCommand = new AsyncRelayCommand(StartExportAsync, () => !IsExporting);
        CancelCommand = new RelayCommand(CancelExport, () => IsExporting);

        OutputFilePath = GetDefaultOutputPath();
        StatusText = "待機中";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler<DialogRequest>? DialogRequested;

    public ObservableCollection<string> Logs { get; }

    public AsyncRelayCommand StartExportCommand { get; }

    public RelayCommand CancelCommand { get; }

    public string Organization
    {
        get => _organization;
        set => SetProperty(ref _organization, value);
    }

    public string Project
    {
        get => _project;
        set => SetProperty(ref _project, value);
    }

    public string Repository
    {
        get => _repository;
        set => SetProperty(ref _repository, value);
    }

    public string PersonalAccessToken
    {
        get => _personalAccessToken;
        set => SetProperty(ref _personalAccessToken, value);
    }

    public string OutputFilePath
    {
        get => _outputFilePath;
        set => SetProperty(ref _outputFilePath, value);
    }

    public string Authors
    {
        get => _authors;
        set => SetProperty(ref _authors, value);
    }

    public bool IsExporting
    {
        get => _isExporting;
        private set
        {
            if (SetProperty(ref _isExporting, value))
            {
                StartExportCommand.NotifyCanExecuteChanged();
                CancelCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public double ProgressValue
    {
        get => _progressValue;
        private set => SetProperty(ref _progressValue, value);
    }

    public double ProgressMaximum
    {
        get => _progressMaximum;
        private set => SetProperty(ref _progressMaximum, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public void AppendLog(string message)
    {
        Logs.Add($"[{DateTimeOffset.Now:HH:mm:ss}] {message}");
    }

    public void SetOutputPathFromPicker(string? path)
    {
        if (!string.IsNullOrWhiteSpace(path))
        {
            OutputFilePath = path;
        }
    }

    private async Task StartExportAsync()
    {
        if (IsExporting)
        {
            return;
        }

        IsExporting = true;
        ProgressValue = 0;
        ProgressMaximum = 1;
        StatusText = "開始中...";
        AppendLog("Starting export...");

        _cts = new CancellationTokenSource();

        try
        {
            var request = new ExportRequest(
                Organization: Organization,
                Project: Project,
                Repository: Repository,
                PersonalAccessToken: PersonalAccessToken,
                OutputFilePath: OutputFilePath,
                AuthorFilters: ParseAuthors(Authors));

            var progress = new Progress<ExportProgress>(p =>
            {
                ProgressMaximum = Math.Max(1, p.TotalPullRequests);
                ProgressValue = Math.Min(ProgressMaximum, p.ProcessedPullRequests);
                StatusText = p.CurrentStatus;

                if (p.CurrentStatus.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase))
                {
                    AppendLog(p.CurrentStatus);
                }
            });

            var result = await _exportService.ExportAsync(request, progress, _cts.Token).ConfigureAwait(true);

            AppendLog($"Export completed. Output={result.OutputFilePath}");
            StatusText = "完了";

            DialogRequested?.Invoke(this, new DialogRequest(
                Kind: DialogKind.Completion,
                Title: "完了",
                Message: $"エクスポートが完了しました。\nPR: {result.TotalPullRequests}, Threads: {result.TotalThreads}, Comments: {result.TotalComments}",
                OutputFilePath: result.OutputFilePath));
        }
        catch (OperationCanceledException)
        {
            AppendLog("Canceled.");
            StatusText = "キャンセル";
        }
        catch (AdoExportException ex)
        {
            AppendLog($"Error: {ex.GetType().Name}: {ex.Message}");
            StatusText = "エラー";

            DialogRequested?.Invoke(this, new DialogRequest(
                Kind: DialogKind.Error,
                Title: "エラー",
                Message: ToUserMessage(ex)));
        }
        catch (Exception ex)
        {
            AppendLog("Unexpected error.");
            StatusText = "エラー";

            DialogRequested?.Invoke(this, new DialogRequest(
                Kind: DialogKind.Error,
                Title: "エラー",
                Message: "予期しないエラーが発生しました。ログを確認してください。"));

            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
        finally
        {
            _cts?.Dispose();
            _cts = null;
            IsExporting = false;
        }
    }

    private void CancelExport()
    {
        try
        {
            _cts?.Cancel();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
    }

    private static string GetDefaultOutputPath()
    {
        var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var fileName = $"ado-review-export-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.json";
        return Path.Combine(baseDir, fileName);
    }

    private static string[]? ParseAuthors(string authors)
    {
        if (string.IsNullOrWhiteSpace(authors))
        {
            return null;
        }

        var parts = authors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return parts.Length == 0 ? null : parts;
    }

    private static string ToUserMessage(AdoExportException ex)
    {
        return ex switch
        {
            InputValidationException => "入力内容を確認してください（必須項目が不足しています）。",
            AuthenticationException => "認証に失敗しました。PAT の権限（vso.code Read）を確認してください。",
            OutputException => "出力に失敗しました。出力先パス・権限・ディスク空き容量を確認してください。",
            ApiException => "API 呼び出しに失敗しました。ネットワークまたは Azure DevOps の状態を確認してください。",
            _ => "エクスポートに失敗しました。ログを確認してください。",
        };
    }

    private bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
