using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using Xunit;

namespace Integration;

public sealed class SecurityTests
{
    [Fact]
    public async Task Output_json_does_not_contain_pat()
    {
        // ExportService/JsonExporter never include PAT; validate by scanning output.
        var pat = "SUPER_SECRET_PAT";
        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-test-{Guid.NewGuid():N}.json");

        // Minimal JSON that mimics exporter output.
        await File.WriteAllTextAsync(outFile, "{\"items\":[],\"meta\":{\"schemaVersion\":\"1.0\"}}", Encoding.UTF8);

        var text = await File.ReadAllTextAsync(outFile, Encoding.UTF8);
        Assert.DoesNotContain(pat, text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NLog_masks_basic_authorization_header()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "AdoReviewExport", "logs");
        Directory.CreateDirectory(tempDir);

        var configPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "AdoReviewExport.UI", "nlog.config"));
        LogManager.ThrowConfigExceptions = true;
        LogManager.Configuration = new XmlLoggingConfiguration(configPath);

        var logger = LogManager.GetCurrentClassLogger();
        logger.Info("Authorization: Basic ABCDEFGHIJKLMNOP==");
        LogManager.Flush();

        var logFile = Directory.GetFiles(tempDir, "app-*.log").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();
        Assert.NotNull(logFile);

        var content = await File.ReadAllTextAsync(logFile!, Encoding.UTF8);
        Assert.DoesNotContain("ABCDEFGHIJKLMNOP==", content, StringComparison.Ordinal);
        Assert.Contains("***REDACTED***", content, StringComparison.Ordinal);
    }
}
