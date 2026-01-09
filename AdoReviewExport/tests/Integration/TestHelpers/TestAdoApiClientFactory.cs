using System.Net.Http;
using AdoReviewExport.Infrastructure.AzureDevOps;
using AdoReviewExport.Infrastructure.Http;

namespace Integration.TestHelpers;

public sealed class TestAdoApiClientFactory : IAdoApiClientFactory
{
    private readonly HttpClient _httpClient;

    public TestAdoApiClientFactory(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public IAdoApiClient Create(string personalAccessToken)
    {
        var pipeline = RetryPolicyFactory.CreateHttpRetryPipeline();
        return new AdoApiClient(_httpClient, pipeline, personalAccessToken);
    }
}
