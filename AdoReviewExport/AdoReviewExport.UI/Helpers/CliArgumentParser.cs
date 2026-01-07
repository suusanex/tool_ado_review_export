using System;
using System.Collections.Generic;

namespace AdoReviewExport.UI.Helpers;

/// <summary>
/// CLI 引数を最小限パースする。
/// </summary>
public static class CliArgumentParser
{
    public sealed record ParsedArgs(
        string? Organization,
        string? Project,
        string? Repository,
        string? PersonalAccessToken,
        string? OutputFilePath,
        IReadOnlyList<string>? Authors,
        bool ShowHelp);

    public static ParsedArgs Parse(string[] args)
    {
        string? org = null;
        string? project = null;
        string? repo = null;
        string? pat = null;
        string? output = null;
        List<string>? authors = null;
        var help = false;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            if (string.Equals(a, "--help", StringComparison.OrdinalIgnoreCase) || string.Equals(a, "-h", StringComparison.OrdinalIgnoreCase))
            {
                help = true;
                continue;
            }

            if (!a.StartsWith("--", StringComparison.Ordinal))
            {
                continue;
            }

            var key = a;
            var value = i + 1 < args.Length ? args[i + 1] : null;

            // 形だけのバリデーション（値が無い場合は後段で必須チェック）
            switch (key)
            {
                case "--org":
                    org = value;
                    i++;
                    break;
                case "--project":
                    project = value;
                    i++;
                    break;
                case "--repo":
                    repo = value;
                    i++;
                    break;
                case "--pat":
                    pat = value;
                    i++;
                    break;
                case "--output":
                    output = value;
                    i++;
                    break;
                case "--authors":
                    authors = ParseAuthors(value);
                    i++;
                    break;
            }
        }

        // 環境変数サポート
        pat ??= Environment.GetEnvironmentVariable("AZDO_PAT");

        return new ParsedArgs(org, project, repo, pat, output, authors, help);
    }

    private static List<string> ParseAuthors(string? authors)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(authors))
        {
            return list;
        }

        var parts = authors.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var p in parts)
        {
            if (!string.IsNullOrWhiteSpace(p))
            {
                list.Add(p);
            }
        }
        return list;
    }

    public static string GetUsage()
    {
        return string.Join(Environment.NewLine, new[]
        {
            "USAGE:",
            "  AdoReviewExport.UI.exe --org <orgOrBaseUrl> --project <project> --repo <repoIdOrName> --pat <pat> --output <path> [--authors \"a,b\"]",
            "",
            "EXAMPLES:",
            "  AdoReviewExport.UI.exe --org contoso --project MyProject --repo my-repo --pat *** --output .\\export.json",
            "  AdoReviewExport.UI.exe --org http://localhost --project project --repo repo --output .\\export.json  (PAT via AZDO_PAT env)",
            "",
            "EXIT CODES:",
            "  0 success",
            "  1 invalid arguments / validation error",
            "  2 authentication error",
            "  3 api error",
            "  4 output error",
            "  130 canceled (Ctrl+C)",
        });
    }
}
