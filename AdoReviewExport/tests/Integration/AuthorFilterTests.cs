using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using AdoReviewExport.Application.Models;
using AdoReviewExport.Application.Services;
using AdoReviewExport.Infrastructure.Json;
using Integration.TestHelpers;
using Xunit;

namespace Integration;

public sealed class AuthorFilterTests
{
    [Fact]
    public async Task Export_filters_comments_by_author_displayName_or_uniqueName()
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
            AuthorFilters: new[] { "alice" });

        _ = await exportService.ExportAsync(request);

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(outFile));
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);

        foreach (var item in items.EnumerateArray())
        {
            var author = item.GetProperty("comment").GetProperty("author");
            var displayName = author.GetProperty("displayName").GetString() ?? string.Empty;
            var uniqueName = author.GetProperty("uniqueName").GetString() ?? string.Empty;
            Assert.True(
                displayName.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                uniqueName.Contains("alice", StringComparison.OrdinalIgnoreCase));
        }
    }
}
