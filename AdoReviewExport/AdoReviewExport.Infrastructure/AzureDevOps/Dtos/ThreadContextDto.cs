using System.Text.Json.Serialization;

namespace AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

/// <summary>
/// スレッドコンテキスト（ファイルパス・行番号）DTO。
/// </summary>
public sealed record ThreadContextDto(
    [property: JsonPropertyName("filePath")] string? FilePath,
    [property: JsonPropertyName("rightFileStart")] FilePositionDto? RightFileStart,
    [property: JsonPropertyName("rightFileEnd")] FilePositionDto? RightFileEnd);
