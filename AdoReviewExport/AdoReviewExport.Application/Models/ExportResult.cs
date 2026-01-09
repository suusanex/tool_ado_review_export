using System;

namespace AdoReviewExport.Application.Models;

/// <summary>
/// エクスポート結果。
/// </summary>
public sealed record ExportResult(
    bool Success,
    string OutputFilePath,
    int TotalPullRequests,
    int TotalThreads,
    int TotalComments,
    TimeSpan ElapsedTime);
