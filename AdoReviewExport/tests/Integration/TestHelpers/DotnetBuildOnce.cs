using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;

namespace Integration.TestHelpers;

public static class DotnetBuildOnce
{
    private static readonly ConcurrentDictionary<string, bool> Built = new(StringComparer.OrdinalIgnoreCase);

    public static void BuildProjectReleaseOnce(string projectPath, int timeoutMs)
    {
        projectPath = Path.GetFullPath(projectPath);

        if (Built.ContainsKey(projectPath))
        {
            return;
        }

        var psi = new ProcessStartInfo("dotnet", $"build -c Release \"{projectPath}\"")
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        var p = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start dotnet build");

        if (!p.WaitForExit(timeoutMs))
        {
            try { p.Kill(entireProcessTree: true); } catch (Exception ex) { Trace.TraceError(ex.ToString()); }
            throw new TimeoutException($"dotnet build timed out for {projectPath}");
        }
        var stdout = p.StandardOutput.ReadToEnd();
        var stderr = p.StandardError.ReadToEnd();

        if (p.ExitCode != 0)
        {
            throw new InvalidOperationException($"dotnet build failed for {projectPath}.\nstdout:\n{stdout}\nstderr:\n{stderr}");
        }

        Built[projectPath] = true;
    }
}
