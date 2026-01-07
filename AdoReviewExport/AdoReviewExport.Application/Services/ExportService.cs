using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using AdoReviewExport.Application.Exceptions;
using AdoReviewExport.Application.Models;
using AdoReviewExport.Infrastructure.AzureDevOps;
using AdoReviewExport.Infrastructure.Json;

namespace AdoReviewExport.Application.Services;

/// <summary>
/// エクスポート処理の既定実装。
/// </summary>
public sealed class ExportService : IExportService
{
    private readonly IAdoApiClientFactory _adoApiClientFactory;
    private readonly IJsonExporter _jsonExporter;

    public ExportService(IAdoApiClientFactory adoApiClientFactory, IJsonExporter jsonExporter)
    {
        _adoApiClientFactory = adoApiClientFactory;
        _jsonExporter = jsonExporter;
    }

    public async Task<ExportResult> ExportAsync(
        ExportRequest request,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        ValidateRequest(request);

        var stopwatch = Stopwatch.StartNew();
        var exportedAt = DateTimeOffset.UtcNow;
        var appVersion = typeof(ExportService).Assembly.GetName().Version?.ToString() ?? "0.0.0";

        var meta = new JsonExportMeta(
            ExportedAt: exportedAt,
            Organization: request.Organization,
            Project: request.Project,
            Repository: request.Repository,
            AppVersion: appVersion,
            AuthorFilters: request.AuthorFilters);

        IJsonExportSession? session = null;
        try
        {
            session = await _jsonExporter.StartAsync(request.OutputFilePath, meta, cancellationToken).ConfigureAwait(false);

            var ado = _adoApiClientFactory.Create(request.PersonalAccessToken);
            var prs = await ado.GetPullRequestsAsync(request.Organization, request.Project, request.Repository, cancellationToken).ConfigureAwait(false);

            var totalPullRequests = prs.Count;
            var processedPullRequests = 0;
            var totalThreads = 0;
            var totalComments = 0;
            var uniqueThreadIds = new HashSet<int>();

            progress?.Report(new ExportProgress(totalPullRequests, processedPullRequests, totalComments, "Fetching pull requests..."));

            foreach (var pr in prs)
            {
                cancellationToken.ThrowIfCancellationRequested();

                processedPullRequests++;
                progress?.Report(new ExportProgress(totalPullRequests, processedPullRequests - 1, totalComments, $"Processing PR {processedPullRequests} / {totalPullRequests}"));

                var threads = await ado.GetThreadsAsync(request.Organization, request.Project, request.Repository, pr.PullRequestId, cancellationToken).ConfigureAwait(false);
                foreach (var thread in threads)
                {
                    if (uniqueThreadIds.Add(thread.Id))
                    {
                        totalThreads++;
                    }

                    if (thread.Comments is null)
                    {
                        continue;
                    }

                    foreach (var comment in thread.Comments)
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        if (!ShouldIncludeComment(request.AuthorFilters, comment.Author))
                        {
                            continue;
                        }

                        await session.WriteItemAsync(pr, thread, comment, cancellationToken).ConfigureAwait(false);
                        totalComments++;
                    }
                }

                progress?.Report(new ExportProgress(totalPullRequests, processedPullRequests, totalComments, $"Processed PR {processedPullRequests} / {totalPullRequests}"));
            }

            await session.CompleteAsync(new JsonExportSummary(totalPullRequests, totalThreads, totalComments), cancellationToken).ConfigureAwait(false);

            if (request.AuthorFilters is not null && request.AuthorFilters.Count > 0 && totalComments == 0)
            {
                progress?.Report(new ExportProgress(totalPullRequests, processedPullRequests, totalComments, "Warning: author filter matched zero comments."));
            }
            stopwatch.Stop();

            return new ExportResult(
                Success: true,
                OutputFilePath: request.OutputFilePath,
                TotalPullRequests: totalPullRequests,
                TotalThreads: totalThreads,
                TotalComments: totalComments,
                ElapsedTime: stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            // Do not leave partial output on cancel.
            TryDeleteFile(request.OutputFilePath);
            throw;
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new AuthenticationException(ex.Message, ex);
        }
        catch (IOException ex)
        {
            throw new OutputException($"Failed to write to the output file: {request.OutputFilePath}", ex);
        }
        catch (HttpRequestException ex)
        {
            throw new ApiException(ex.Message, ex);
        }
        catch (Exception ex) when (ex is not AdoExportException)
        {
            // Unexpected: wrap as API error by default.
            throw new ApiException("An unexpected error occurred.", ex);
        }
        finally
        {
            if (session is not null)
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    private static void ValidateRequest(ExportRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Organization))
        {
            throw new InputValidationException("Missing required field: Organization");
        }
        if (string.IsNullOrWhiteSpace(request.Project))
        {
            throw new InputValidationException("Missing required field: Project");
        }
        if (string.IsNullOrWhiteSpace(request.Repository))
        {
            throw new InputValidationException("Missing required field: Repository");
        }
        if (string.IsNullOrWhiteSpace(request.PersonalAccessToken))
        {
            throw new InputValidationException("Missing required field: PersonalAccessToken");
        }
        if (string.IsNullOrWhiteSpace(request.OutputFilePath))
        {
            throw new InputValidationException("Missing required field: OutputFilePath");
        }
    }

    private static bool ShouldIncludeComment(IReadOnlyList<string>? authorFilters, AdoReviewExport.Infrastructure.AzureDevOps.Dtos.IdentityDto? author)
    {
        if (authorFilters is null || authorFilters.Count == 0)
        {
            return true;
        }

        var displayName = author?.DisplayName ?? string.Empty;
        var uniqueName = author?.UniqueName ?? string.Empty;

        foreach (var filter in authorFilters)
        {
            if (string.IsNullOrWhiteSpace(filter))
            {
                continue;
            }

            if (displayName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                uniqueName.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static void TryDeleteFile(string filePath)
    {
        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
    }
}
