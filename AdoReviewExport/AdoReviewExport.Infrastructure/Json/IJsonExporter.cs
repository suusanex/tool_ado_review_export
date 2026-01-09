using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

namespace AdoReviewExport.Infrastructure.Json;

/// <summary>
/// JSON エクスポートの書き込みを担当するインターフェース。
/// </summary>
public interface IJsonExporter
{
    /// <summary>
    /// ストリーミング書き込みセッションを開始する。
    /// </summary>
    Task<IJsonExportSession> StartAsync(
        string outputFilePath,
        JsonExportMeta meta,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// JSON エクスポートのストリーミング書き込みセッション。
/// </summary>
public interface IJsonExportSession : IAsyncDisposable
{
    /// <summary>
    /// 1件分のコメントレコードを書き込む。
    /// </summary>
    Task WriteItemAsync(PullRequestDto pr, ThreadDto thread, CommentDto comment, CancellationToken cancellationToken = default);

    /// <summary>
    /// 書き込みを完了する。
    /// </summary>
    Task CompleteAsync(JsonExportSummary summary, CancellationToken cancellationToken = default);
}

/// <summary>
/// JSON エクスポートのメタ情報。
/// </summary>
public sealed record JsonExportMeta(
    DateTimeOffset ExportedAt,
    string Organization,
    string Project,
    string Repository,
    string AppVersion,
    IReadOnlyList<string>? AuthorFilters);

/// <summary>
/// JSON エクスポートの集計情報。
/// </summary>
public sealed record JsonExportSummary(
    int TotalPullRequests,
    int TotalThreads,
    int TotalComments);
