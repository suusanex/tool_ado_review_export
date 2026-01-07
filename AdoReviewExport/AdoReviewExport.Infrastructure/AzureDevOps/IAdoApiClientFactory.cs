namespace AdoReviewExport.Infrastructure.AzureDevOps;

/// <summary>
/// 実行時に与えられる PAT を用いて API クライアントを生成するファクトリ。
/// </summary>
public interface IAdoApiClientFactory
{
    /// <summary>
    /// 指定 PAT でクライアントを生成する。
    /// </summary>
    IAdoApiClient Create(string personalAccessToken);
}
