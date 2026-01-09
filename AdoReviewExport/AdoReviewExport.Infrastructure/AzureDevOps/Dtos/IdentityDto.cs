using System.Text.Json.Serialization;

namespace AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

/// <summary>
/// Azure DevOps のユーザー情報 DTO。
/// </summary>
public sealed record IdentityDto(
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("uniqueName")] string? UniqueName,
    [property: JsonPropertyName("id")] string? Id);
