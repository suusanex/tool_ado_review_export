using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Integration.TestHelpers;
using Xunit;

namespace Integration;

public sealed class CliModeTests
{
    [Fact(Timeout = 180_000)]
    public async Task Cli_missing_args_returns_exit_code_1()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var (code, _, _) = await RunCliAsync("--help", timeoutMs: 60_000);
        Assert.Equal(0, code);

        (code, _, _) = await RunCliAsync("--org contoso", timeoutMs: 60_000);
        Assert.Equal(1, code);
    }

    [Fact(Timeout = 180_000)]
    public async Task Cli_success_returns_exit_code_0_and_writes_file()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "success");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-cli-{Guid.NewGuid():N}.json");
        var args = $"--org http://127.0.0.1:{port} --project project --repo repo --pat TEST_PAT --output \"{outFile}\"";

        var (code, stdout, stderr) = await RunCliAsync(args, timeoutMs: 60_000);
        Assert.Equal(0, code);
        Assert.True(File.Exists(outFile), $"Output not found. stdout={stdout} stderr={stderr}");
    }

    [Fact(Timeout = 180_000)]
    public async Task Cli_can_use_AZDO_PAT_environment_variable_when_pat_arg_is_omitted()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "success");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-cli-{Guid.NewGuid():N}.json");
        var args = $"--org http://127.0.0.1:{port} --project project --repo repo --output \"{outFile}\"";

        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["AZDO_PAT"] = "TEST_PAT",
        };

        var (code, stdout, stderr) = await RunCliAsync(args, timeoutMs: 60_000, extraEnv: env);
        Assert.Equal(0, code);
        Assert.True(File.Exists(outFile), $"Output not found. stdout={stdout} stderr={stderr}");
    }

    [Fact(Timeout = 180_000)]
    public async Task Cli_auth_error_returns_exit_code_2()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "auth_error");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-cli-{Guid.NewGuid():N}.json");
        var args = $"--org http://127.0.0.1:{port} --project project --repo repo --pat TEST_PAT --output \"{outFile}\"";

        var (code, _, _) = await RunCliAsync(args, timeoutMs: 60_000);
        Assert.Equal(2, code);
    }

    [Fact(Timeout = 180_000)]
    public async Task Cli_api_error_returns_exit_code_3()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "api_error");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-cli-{Guid.NewGuid():N}.json");
        var args = $"--org http://127.0.0.1:{port} --project project --repo repo --pat TEST_PAT --output \"{outFile}\"";

        var (code, _, _) = await RunCliAsync(args, timeoutMs: 60_000);
        Assert.Equal(3, code);
    }

    [Fact(Timeout = 180_000)]
    public async Task Cli_output_error_returns_exit_code_4()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "success");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        // Use an existing directory path as the "file" path to trigger a deterministic output error
        // without touching protected OS locations.
        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-output-dir-{Guid.NewGuid():N}");
        Directory.CreateDirectory(outFile);
        var args = $"--org http://127.0.0.1:{port} --project project --repo repo --pat TEST_PAT --output \"{outFile}\"";

        var (code, _, _) = await RunCliAsync(args, timeoutMs: 60_000);
        Assert.Equal(4, code);
    }

    [Fact(Timeout = 180_000)]
    public async Task Cli_author_filter_excludes_non_matching_authors_in_output_json()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "success");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-cli-{Guid.NewGuid():N}.json");
        var args = $"--org http://127.0.0.1:{port} --project project --repo repo --pat TEST_PAT --output \"{outFile}\" --authors \"alice,bob\"";

        var (code, stdout, stderr) = await RunCliAsync(args, timeoutMs: 60_000);
        Assert.Equal(0, code);
        Assert.True(File.Exists(outFile), $"Output not found. stdout={stdout} stderr={stderr}");

        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(outFile, Encoding.UTF8));
        var items = doc.RootElement.GetProperty("items");
        Assert.True(items.GetArrayLength() > 0);

        foreach (var item in items.EnumerateArray())
        {
            var author = item.GetProperty("comment").GetProperty("author");
            var displayName = author.GetProperty("displayName").GetString() ?? string.Empty;
            var uniqueName = author.GetProperty("uniqueName").GetString() ?? string.Empty;

            Assert.True(
                displayName.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                uniqueName.Contains("alice", StringComparison.OrdinalIgnoreCase) ||
                displayName.Contains("bob", StringComparison.OrdinalIgnoreCase) ||
                uniqueName.Contains("bob", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact(Timeout = 180_000)]
    public async Task Cli_cancellation_returns_exit_code_130_and_does_not_leave_partial_file()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");
        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "timeout");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-cli-cancel-{Guid.NewGuid():N}.json");
        var args = $"--org http://127.0.0.1:{port} --project project --repo repo --pat TEST_PAT --output \"{outFile}\"";

        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["ADO_REVIEW_EXPORT_CLI_TEST_CANCEL_AFTER_MS"] = "200",
        };

        var (code, _, _) = await RunCliAsync(args, timeoutMs: 60_000, extraEnv: env);
        Assert.Equal(130, code);
        Assert.False(File.Exists(outFile));
    }

    private static int GetFreePort()
    {
        var listener = new TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private sealed class StartedProcess : IDisposable
    {
        public StartedProcess(Process process)
        {
            Process = process;
        }

        public Process Process { get; }

        public void Dispose()
        {
            try
            {
                if (!Process.HasExited)
                {
                    Process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // ignore
            }
            finally
            {
                Process.Dispose();
            }
        }
    }

    private static StartedProcess StartStubApi(int port, string mode)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var stubProject = Path.Combine(repoRoot, "AdoReviewExport", "tests", "StubApi", "StubApi.csproj");

        DotnetBuildOnce.BuildProjectReleaseOnce(stubProject, timeoutMs: 180_000);

        var psi = new ProcessStartInfo("dotnet", $"run -c Release --project \"{stubProject}\" --no-build --no-launch-profile")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        psi.Environment["STUB_MODE"] = mode;

        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start StubApi");
        return new StartedProcess(p);
    }

    private static async Task WaitForStubAsync(StartedProcess stub, int port, int timeoutMs)
    {
        using var http = new HttpClient();
        var url = $"http://127.0.0.1:{port}/";

        var deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (stub.Process.HasExited)
            {
                var stdout = await stub.Process.StandardOutput.ReadToEndAsync();
                var stderr = await stub.Process.StandardError.ReadToEndAsync();
                throw new InvalidOperationException($"StubApi exited early. ExitCode={stub.Process.ExitCode}\nstdout={stdout}\nstderr={stderr}");
            }

            try
            {
                var s = await http.GetStringAsync(url);
                if (!string.IsNullOrWhiteSpace(s))
                {
                    return;
                }
            }
            catch
            {
                await Task.Delay(100);
            }
        }

        throw new TimeoutException("StubApi did not start in time.");
    }

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunCliAsync(
        string args,
        int timeoutMs,
        System.Collections.Generic.IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");

        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var psi = new ProcessStartInfo("dotnet", $"run -c Release --project \"{uiProject}\" --no-build -- {args}")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        // Ensure CLI gets a console (WinExe attach code is still executed but harmless here)
        psi.Environment["DOTNET_NOLOGO"] = "1";

        if (extraEnv is not null)
        {
            foreach (var kv in extraEnv)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }

        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start CLI");

        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("CLI did not exit in time.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (p.ExitCode, stdout, stderr);
    }
}
