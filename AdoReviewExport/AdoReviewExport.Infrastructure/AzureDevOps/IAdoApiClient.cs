using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

namespace AdoReviewExport.Infrastructure.AzureDevOps;

/// <summary>
/// Azure DevOps REST API クライアントのインターフェース。
/// </summary>
public interface IAdoApiClient
{
    /// <summary>
    /// 指定リポジトリの Pull Request 一覧を取得する。
    /// </summary>
    Task<IReadOnlyList<PullRequestDto>> GetPullRequestsAsync(
        string organization,
        string project,
        string repositoryId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 指定 Pull Request のスレッド一覧を取得する。
    /// </summary>
    Task<IReadOnlyList<ThreadDto>> GetThreadsAsync(
        string organization,
        string project,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default);
}
