using System.Collections.Generic;

namespace AdoReviewExport.Application.Models;

/// <summary>
/// エクスポート要求パラメータ。
/// </summary>
public sealed record ExportRequest(
    string Organization,
    string Project,
    string Repository,
    string PersonalAccessToken,
    string OutputFilePath,
    IReadOnlyList<string>? AuthorFilters = null);
