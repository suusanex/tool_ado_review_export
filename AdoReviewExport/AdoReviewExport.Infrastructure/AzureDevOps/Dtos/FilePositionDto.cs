using System.Text.Json.Serialization;

namespace AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

/// <summary>
/// ファイル位置（行・オフセット）DTO。
/// </summary>
public sealed record FilePositionDto(
    [property: JsonPropertyName("line")] int Line,
    [property: JsonPropertyName("offset")] int Offset);
