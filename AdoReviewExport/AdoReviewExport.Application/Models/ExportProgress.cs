namespace AdoReviewExport.Application.Models;

/// <summary>
/// エクスポート進捗情報。
/// </summary>
public sealed record ExportProgress(
    int TotalPullRequests,
    int ProcessedPullRequests,
    int TotalComments,
    string CurrentStatus);
