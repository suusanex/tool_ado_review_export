using System;
using System.Text.Json.Serialization;

namespace AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

/// <summary>
/// レビューコメント DTO。
/// </summary>
public sealed record CommentDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("parentCommentId")] int ParentCommentId,
    [property: JsonPropertyName("author")] IdentityDto? Author,
    [property: JsonPropertyName("publishedDate")] DateTimeOffset PublishedDate,
    [property: JsonPropertyName("lastUpdatedDate")] DateTimeOffset LastUpdatedDate,
    [property: JsonPropertyName("commentType")] string? CommentType,
    [property: JsonPropertyName("content")] string? Content,
    [property: JsonPropertyName("isDeleted")] bool IsDeleted);
