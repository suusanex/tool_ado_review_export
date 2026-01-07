using System;
using System.Text.Json.Serialization;

namespace AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

/// <summary>
/// Pull Request DTO。
/// </summary>
public sealed record PullRequestDto(
    [property: JsonPropertyName("pullRequestId")] int PullRequestId,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("description")] string? Description,
    [property: JsonPropertyName("url")] string? Url,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("createdBy")] IdentityDto? CreatedBy,
    [property: JsonPropertyName("creationDate")] DateTimeOffset CreationDate,
    [property: JsonPropertyName("closedDate")] DateTimeOffset? ClosedDate,
    [property: JsonPropertyName("sourceRefName")] string? SourceRefName,
    [property: JsonPropertyName("targetRefName")] string? TargetRefName);
