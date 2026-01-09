using AdoReviewExport.Infrastructure.Http;
using Polly;

namespace AdoReviewExport.Infrastructure.AzureDevOps;

/// <summary>
/// <see cref="IAdoApiClientFactory"/> の既定実装。
/// </summary>
public sealed class AdoApiClientFactory : IAdoApiClientFactory
{
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;

    public AdoApiClientFactory()
    {
        _pipeline = RetryPolicyFactory.CreateHttpRetryPipeline();
    }

    public IAdoApiClient Create(string personalAccessToken)
    {
        var httpClient = new HttpClient();
        return new AdoApiClient(httpClient, _pipeline, personalAccessToken);
    }
}
