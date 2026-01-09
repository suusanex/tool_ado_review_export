using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

/// <summary>
/// コメントスレッド DTO。
/// </summary>
public sealed record ThreadDto(
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("status")] string? Status,
    [property: JsonPropertyName("publishedDate")] DateTimeOffset PublishedDate,
    [property: JsonPropertyName("threadContext")] ThreadContextDto? ThreadContext,
    [property: JsonPropertyName("comments")] IReadOnlyList<CommentDto>? Comments);
