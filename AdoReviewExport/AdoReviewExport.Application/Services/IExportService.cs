using System;
using System.Threading;
using System.Threading.Tasks;
using AdoReviewExport.Application.Exceptions;
using AdoReviewExport.Application.Models;

namespace AdoReviewExport.Application.Services;

/// <summary>
/// Azure DevOps のレビューコメントをエクスポートするサービスのインターフェース。
/// </summary>
public interface IExportService
{
    /// <summary>
    /// エクスポート処理を実行する。
    /// </summary>
    /// <exception cref="InputValidationException">入力パラメータが不正な場合</exception>
    /// <exception cref="AuthenticationException">認証に失敗した場合</exception>
    /// <exception cref="ApiException">Azure DevOps API でエラーが発生した場合</exception>
    /// <exception cref="OutputException">ファイル書き込みに失敗した場合</exception>
    Task<ExportResult> ExportAsync(
        ExportRequest request,
        IProgress<ExportProgress>? progress = null,
        CancellationToken cancellationToken = default);
}
