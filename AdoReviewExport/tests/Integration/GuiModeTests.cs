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

public sealed class GuiModeTests
{
    [GuiFact(Timeout = 180_000)]
    public async Task Gui_can_start_and_exit_in_test_mode()
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");

        TestHelpers.DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var psi = new ProcessStartInfo("dotnet", $"run -c Release --project \"{uiProject}\" --no-build")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["ADO_REVIEW_EXPORT_GUI_TEST_EXIT"] = "1";

        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start GUI");

        if (!p.WaitForExit(120_000))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("GUI did not exit in time.");
        }

        var stdout = await p.StandardOutput.ReadToEndAsync();
        var stderr = await p.StandardError.ReadToEndAsync();

        Assert.True(p.ExitCode == 0, $"ExitCode={p.ExitCode}\nstdout={stdout}\nstderr={stderr}");
    }

    [GuiFact(Timeout = 180_000)]
    public async Task Gui_export_success_creates_output_file()
    {
        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "success");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-gui-{Guid.NewGuid():N}.json");
        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["ADO_REVIEW_EXPORT_GUI_TEST_AUTORUN"] = "1",
            ["ADO_REVIEW_EXPORT_GUI_TEST_ORG"] = $"http://127.0.0.1:{port}",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PROJECT"] = "project",
            ["ADO_REVIEW_EXPORT_GUI_TEST_REPO"] = "repo",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PAT"] = "TEST_PAT",
            ["ADO_REVIEW_EXPORT_GUI_TEST_OUTPUT"] = outFile,
        };

        var (code, stdout, stderr) = await RunGuiAsync(timeoutMs: 120_000, extraEnv: env);
        Assert.True(code == 0, $"ExitCode={code}\nstdout={stdout}\nstderr={stderr}");
        Assert.True(File.Exists(outFile));
    }

    [GuiFact(Timeout = 180_000)]
    public async Task Gui_auth_error_exits_with_nonzero_code_in_test_autorun_mode()
    {
        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "auth_error");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-gui-{Guid.NewGuid():N}.json");
        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["ADO_REVIEW_EXPORT_GUI_TEST_AUTORUN"] = "1",
            ["ADO_REVIEW_EXPORT_GUI_TEST_ORG"] = $"http://127.0.0.1:{port}",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PROJECT"] = "project",
            ["ADO_REVIEW_EXPORT_GUI_TEST_REPO"] = "repo",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PAT"] = "TEST_PAT",
            ["ADO_REVIEW_EXPORT_GUI_TEST_OUTPUT"] = outFile,
        };

        var (code, _, _) = await RunGuiAsync(timeoutMs: 120_000, extraEnv: env);
        Assert.True(code != 0);
        Assert.False(File.Exists(outFile));
    }

    [GuiFact(Timeout = 180_000)]
    public async Task Gui_transient_503_is_retried_and_eventually_succeeds()
    {
        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "transient_503");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-gui-{Guid.NewGuid():N}.json");
        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["ADO_REVIEW_EXPORT_GUI_TEST_AUTORUN"] = "1",
            ["ADO_REVIEW_EXPORT_GUI_TEST_ORG"] = $"http://127.0.0.1:{port}",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PROJECT"] = "project",
            ["ADO_REVIEW_EXPORT_GUI_TEST_REPO"] = "repo",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PAT"] = "TEST_PAT",
            ["ADO_REVIEW_EXPORT_GUI_TEST_OUTPUT"] = outFile,
        };

        var (code, stdout, stderr) = await RunGuiAsync(timeoutMs: 120_000, extraEnv: env);
        Assert.True(code == 0, $"ExitCode={code}\nstdout={stdout}\nstderr={stderr}");
        Assert.True(File.Exists(outFile));
    }

    [GuiFact(Timeout = 180_000)]
    public async Task Gui_cancel_does_not_leave_partial_file()
    {
        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "timeout");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-gui-cancel-{Guid.NewGuid():N}.json");
        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["ADO_REVIEW_EXPORT_GUI_TEST_AUTORUN"] = "1",
            ["ADO_REVIEW_EXPORT_GUI_TEST_ORG"] = $"http://127.0.0.1:{port}",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PROJECT"] = "project",
            ["ADO_REVIEW_EXPORT_GUI_TEST_REPO"] = "repo",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PAT"] = "TEST_PAT",
            ["ADO_REVIEW_EXPORT_GUI_TEST_OUTPUT"] = outFile,
            ["ADO_REVIEW_EXPORT_GUI_TEST_CANCEL_AFTER_MS"] = "200",
        };

        var (code, stdout, stderr) = await RunGuiAsync(timeoutMs: 120_000, extraEnv: env);
        Assert.True(code == 130, $"ExitCode={code}\nstdout={stdout}\nstderr={stderr}");
        Assert.False(File.Exists(outFile));
    }

    [GuiFact(Timeout = 180_000)]
    public async Task Gui_author_filter_only_outputs_matching_authors()
    {
        var port = GetFreePort();
        using var stub = StartStubApi(port, mode: "success");
        await WaitForStubAsync(stub, port, timeoutMs: 30_000);

        var outFile = Path.Combine(Path.GetTempPath(), $"ado-review-export-gui-authors-{Guid.NewGuid():N}.json");
        var env = new System.Collections.Generic.Dictionary<string, string>
        {
            ["ADO_REVIEW_EXPORT_GUI_TEST_AUTORUN"] = "1",
            ["ADO_REVIEW_EXPORT_GUI_TEST_ORG"] = $"http://127.0.0.1:{port}",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PROJECT"] = "project",
            ["ADO_REVIEW_EXPORT_GUI_TEST_REPO"] = "repo",
            ["ADO_REVIEW_EXPORT_GUI_TEST_PAT"] = "TEST_PAT",
            ["ADO_REVIEW_EXPORT_GUI_TEST_OUTPUT"] = outFile,
            ["ADO_REVIEW_EXPORT_GUI_TEST_AUTHORS"] = "alice,bob",
        };

        var (code, stdout, stderr) = await RunGuiAsync(timeoutMs: 120_000, extraEnv: env);
        Assert.True(code == 0, $"ExitCode={code}\nstdout={stdout}\nstderr={stderr}");

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

    private static async Task<(int ExitCode, string StdOut, string StdErr)> RunGuiAsync(
        int timeoutMs,
        System.Collections.Generic.IReadOnlyDictionary<string, string>? extraEnv)
    {
        var repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".."));
        var uiProject = Path.Combine(repoRoot, "AdoReviewExport", "AdoReviewExport.UI", "AdoReviewExport.UI.csproj");

        DotnetBuildOnce.BuildProjectReleaseOnce(uiProject, timeoutMs: 180_000);

        var psi = new ProcessStartInfo("dotnet", $"run -c Release --project \"{uiProject}\" --no-build")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        if (extraEnv is not null)
        {
            foreach (var kv in extraEnv)
            {
                psi.Environment[kv.Key] = kv.Value;
            }
        }

        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start GUI");

        var stdoutTask = p.StandardOutput.ReadToEndAsync();
        var stderrTask = p.StandardError.ReadToEndAsync();

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch { }
            throw new TimeoutException("GUI did not exit in time.");
        }

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        return (p.ExitCode, stdout, stderr);
    }
}
