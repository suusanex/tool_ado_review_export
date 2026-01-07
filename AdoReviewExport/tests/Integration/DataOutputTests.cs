using System;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AdoReviewExport.Application.Models;
using AdoReviewExport.Application.Services;
using AdoReviewExport.Infrastructure.Json;
using Integration.TestHelpers;
using Xunit;

namespace Integration;

public sealed class DataOutputTests
{
    [Fact]
    public async Task Export_writes_valid_json_with_meta_and_items()
    {
        await using var factory = new StubApiFactory("success");
        var httpClient = factory.CreateClient();

        var adoFactory = new TestAdoApiClientFactory(httpClient);
        var exporter = new JsonExporter();
        var exportService = new ExportService(adoFactory, exporter);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-test-{Guid.NewGuid():N}.json");

        var request = new ExportRequest(
            Organization: "http://localhost",
            Project: "project",
            Repository: "repo",
            PersonalAccessToken: "TEST_PAT",
            OutputFilePath: outFile);

        var result = await exportService.ExportAsync(request);
        Assert.True(result.Success);
        Assert.True(File.Exists(outFile));

        var json = await File.ReadAllTextAsync(outFile, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);

        Assert.True(doc.RootElement.TryGetProperty("items", out var items));
        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(items.GetArrayLength() > 0);

        Assert.True(doc.RootElement.TryGetProperty("meta", out var meta));
        Assert.Equal("project", meta.GetProperty("project").GetString());
        Assert.Equal("repo", meta.GetProperty("repository").GetString());
        Assert.True(meta.TryGetProperty("summary", out _));
    }

    [Fact]
    public async Task Export_preserves_utf8_and_escapes_special_characters()
    {
        await using var factory = new StubApiFactory("success");
        var httpClient = factory.CreateClient();

        var adoFactory = new TestAdoApiClientFactory(httpClient);
        var exporter = new JsonExporter();
        var exportService = new ExportService(adoFactory, exporter);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-test-{Guid.NewGuid():N}.json");

        var request = new ExportRequest(
            Organization: "http://localhost",
            Project: "project",
            Repository: "repo",
            PersonalAccessToken: "TEST_PAT",
            OutputFilePath: outFile);

        _ = await exportService.ExportAsync(request);
        var json = await File.ReadAllTextAsync(outFile, Encoding.UTF8);

        // StubApi includes a newline in content; JSON must be valid and parseable.
        using var doc = JsonDocument.Parse(json);
        var firstItem = doc.RootElement.GetProperty("items")[0];
        var content = firstItem.GetProperty("comment").GetProperty("content").GetString();
        Assert.Contains("\n", content);
    }

    [Fact]
    public async Task Export_author_filter_with_no_matches_writes_zero_items_and_reports_warning()
    {
        await using var factory = new StubApiFactory("success");
        var httpClient = factory.CreateClient();

        var adoFactory = new TestAdoApiClientFactory(httpClient);
        var exporter = new JsonExporter();
        var exportService = new ExportService(adoFactory, exporter);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-test-{Guid.NewGuid():N}.json");

        var request = new ExportRequest(
            Organization: "http://localhost",
            Project: "project",
            Repository: "repo",
            PersonalAccessToken: "TEST_PAT",
            OutputFilePath: outFile,
            AuthorFilters: new[] { "no-such-author" });

        var statuses = new System.Collections.Generic.List<string>();
        var progress = new Progress<ExportProgress>(p => statuses.Add(p.CurrentStatus));

        _ = await exportService.ExportAsync(request, progress);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(outFile, Encoding.UTF8));
        var items = doc.RootElement.GetProperty("items");
        Assert.Equal(0, items.GetArrayLength());

        Assert.Contains(statuses, s => s.StartsWith("Warning:", StringComparison.OrdinalIgnoreCase));
    }
}
