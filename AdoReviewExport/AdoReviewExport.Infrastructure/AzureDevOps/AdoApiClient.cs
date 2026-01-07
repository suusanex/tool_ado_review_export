using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using AdoReviewExport.Infrastructure.AzureDevOps.Dtos;
using NLog;
using Polly;

namespace AdoReviewExport.Infrastructure.AzureDevOps;

/// <summary>
/// Azure DevOps REST API クライアント。
/// </summary>
public sealed class AdoApiClient : IAdoApiClient
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ResiliencePipeline<HttpResponseMessage> _pipeline;
    private readonly string _personalAccessToken;
    private bool _authenticationValidated;

    public AdoApiClient(HttpClient httpClient, ResiliencePipeline<HttpResponseMessage> pipeline, string personalAccessToken)
    {
        _httpClient = httpClient;
        _pipeline = pipeline;
        _personalAccessToken = personalAccessToken;
    }

    public async Task<IReadOnlyList<PullRequestDto>> GetPullRequestsAsync(
        string organization,
        string project,
        string repositoryId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationValidatedAsync(organization, cancellationToken).ConfigureAwait(false);

        var results = new List<PullRequestDto>();

        // Prefer continuation-token paging when present; fall back to $skip paging.
        string? continuationToken = null;
        var skip = 0;
        const int pageSize = 50;

        while (true)
        {
            var uri = BuildPullRequestsUri(organization, project, repositoryId, pageSize, skip, continuationToken);

            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            ApplyPatAuthentication(request);

            HttpResponseMessage response;
            try
            {
                response = await _pipeline.ExecuteAsync(
                    async ct => await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false),
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
                throw;
            }

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                await DrainAndDisposeAsync(response, cancellationToken).ConfigureAwait(false);
                throw new UnauthorizedAccessException("The provided PAT is invalid or does not have the required permissions. Required permissions: Code (Read)");
            }

            if (!response.IsSuccessStatusCode)
            {
                var body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
                Logger.Error("Azure DevOps API error. Status={0}, Body={1}", (int)response.StatusCode, body);
                throw new HttpRequestException(
                    $"Azure DevOps API returned an error: {(int)response.StatusCode} - {response.ReasonPhrase}",
                    null,
                    response.StatusCode);
            }

            var page = await DeserializeListAsync<PullRequestDto>(response, cancellationToken).ConfigureAwait(false);
            results.AddRange(page);

            continuationToken = TryGetContinuationToken(response);
            await DrainAndDisposeAsync(response, cancellationToken).ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(continuationToken))
            {
                continue;
            }

            if (page.Count == 0)
            {
                break;
            }

            skip += pageSize;
        }

        return results;
    }

    public async Task<IReadOnlyList<ThreadDto>> GetThreadsAsync(
        string organization,
        string project,
        string repositoryId,
        int pullRequestId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAuthenticationValidatedAsync(organization, cancellationToken).ConfigureAwait(false);

        var uri = BuildThreadsUri(organization, project, repositoryId, pullRequestId);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyPatAuthentication(request);

        HttpResponseMessage response;
        try
        {
            response = await _pipeline.ExecuteAsync(
                async ct => await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
            throw;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await DrainAndDisposeAsync(response, cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("The provided PAT is invalid or does not have the required permissions. Required permissions: Code (Read)");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            Logger.Error("Azure DevOps API error. Status={0}, Body={1}", (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"Azure DevOps API returned an error: {(int)response.StatusCode} - {response.ReasonPhrase}",
                null,
                response.StatusCode);
        }

        var items = await DeserializeListAsync<ThreadDto>(response, cancellationToken).ConfigureAwait(false);
        await DrainAndDisposeAsync(response, cancellationToken).ConfigureAwait(false);
        return items;
    }

    private async Task EnsureAuthenticationValidatedAsync(string organization, CancellationToken cancellationToken)
    {
        if (_authenticationValidated)
        {
            return;
        }

        // FR-004: Verify PAT on first API connection.
        // Note: Azure DevOps does not provide a direct “scope list” endpoint here; treat 401/403 as insufficient scope.
        var uri = BuildProfilesMeUri(organization);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        ApplyPatAuthentication(request);

        HttpResponseMessage response;
        try
        {
            response = await _pipeline.ExecuteAsync(
                async ct => await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct).ConfigureAwait(false),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, ex.ToString());
            throw;
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            await DrainAndDisposeAsync(response, cancellationToken).ConfigureAwait(false);
            throw new UnauthorizedAccessException("The provided PAT is invalid or does not have the required permissions. Required permissions: Code (Read)");
        }

        if (!response.IsSuccessStatusCode)
        {
            var body = await SafeReadBodyAsync(response, cancellationToken).ConfigureAwait(false);
            Logger.Error("Profile validation API error. Status={0}, Body={1}", (int)response.StatusCode, body);
            throw new HttpRequestException(
                $"Authentication validation API returned an error: {(int)response.StatusCode} - {response.ReasonPhrase}",
                null,
                response.StatusCode);
        }

        await DrainAndDisposeAsync(response, cancellationToken).ConfigureAwait(false);
        _authenticationValidated = true;
    }

    private void ApplyPatAuthentication(HttpRequestMessage request)
    {
        // Basic base64(":" + pat)
        var raw = ":" + _personalAccessToken;
        var bytes = Encoding.UTF8.GetBytes(raw);
        var b64 = Convert.ToBase64String(bytes);
        request.Headers.Authorization = new AuthenticationHeaderValue("Basic", b64);
    }

    private static Uri BuildPullRequestsUri(string organization, string project, string repositoryId, int top, int skip, string? continuationToken)
    {
        var baseUri = GetBaseHostUri(organization);
        var path = $"{TrimSlashes(baseUri.AbsolutePath)}/{project}/_apis/git/repositories/{repositoryId}/pullrequests";

        var query = new List<string>
        {
            "api-version=7.2",
            $"$top={top}",
            $"$skip={skip}",
        };

        if (!string.IsNullOrWhiteSpace(continuationToken))
        {
            query.Add($"continuationToken={Uri.EscapeDataString(continuationToken)}");
        }

        return new UriBuilder(baseUri)
        {
            Path = path,
            Query = string.Join("&", query),
        }.Uri;
    }

    private static Uri BuildThreadsUri(string organization, string project, string repositoryId, int pullRequestId)
    {
        var baseUri = GetBaseHostUri(organization);
        var path = $"{TrimSlashes(baseUri.AbsolutePath)}/{project}/_apis/git/repositories/{repositoryId}/pullrequests/{pullRequestId}/threads";

        return new UriBuilder(baseUri)
        {
            Path = path,
            Query = "api-version=7.2",
        }.Uri;
    }

    private static Uri BuildProfilesMeUri(string organization)
    {
        // Spec task requires calling _apis/profile/profiles/me on first connection.
        var baseUri = GetBaseHostUri(organization);
        return new UriBuilder(baseUri)
        {
            Path = $"{TrimSlashes(baseUri.AbsolutePath)}/_apis/profile/profiles/me",
            Query = "api-version=7.2",
        }.Uri;
    }

    private static Uri GetBaseHostUri(string organization)
    {
        // Accept real org name ("contoso") and local stub host ("localhost:5000")
        if (Uri.TryCreate(organization, UriKind.Absolute, out var absolute))
        {
            return absolute;
        }

        if (organization.Contains(':'))
        {
            return new Uri($"http://{organization}");
        }

        return new Uri($"https://dev.azure.com/{organization}");
    }

    private static string TrimSlashes(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path == "/")
        {
            return string.Empty;
        }

        return path.TrimEnd('/');
    }

    private static string? TryGetContinuationToken(HttpResponseMessage response)
    {
        // Common header used by Azure DevOps.
        if (response.Headers.TryGetValues("x-ms-continuationtoken", out var values))
        {
            return values.FirstOrDefault();
        }

        return null;
    }

    private static async Task<IReadOnlyList<T>> DeserializeListAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
        var wrapper = await JsonSerializer.DeserializeAsync<AdoListResponse<T>>(stream, JsonOptions, cancellationToken).ConfigureAwait(false);
        return wrapper?.Value ?? Array.Empty<T>();
    }

    private static async Task<string> SafeReadBodyAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static async Task DrainAndDisposeAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        try
        {
            _ = await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Intentionally ignore draining errors.
        }
        finally
        {
            response.Dispose();
        }
    }

    private sealed record AdoListResponse<T>(
        [property: JsonPropertyName("value")] IReadOnlyList<T>? Value,
        [property: JsonPropertyName("count")] int Count);
}
