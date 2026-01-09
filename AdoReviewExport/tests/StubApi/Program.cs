using System.Text.Json.Serialization;
using System.Collections.Concurrent;
using System.Collections.Generic;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

var mode = (Environment.GetEnvironmentVariable("STUB_MODE") ?? "success").Trim().ToLowerInvariant();
var counters = new ConcurrentDictionary<string, int>();

Task MaybeDelayAsync(string key)
{
	// タイムアウト/キャンセル系の統合テスト用に、意図的に遅延させる。
	// 実際のタイムアウトを発生させるのではなく、処理時間を伸ばしてキャンセル機会を作る。
	if (mode == "timeout")
	{
		return Task.Delay(TimeSpan.FromSeconds(2));
	}

	return Task.CompletedTask;
}

IResult? MaybeReturnError(string key)
{
	// 共通的なエラーモード
	if (mode == "auth_error")
	{
		return Results.StatusCode(StatusCodes.Status401Unauthorized);
	}
	if (mode == "api_error")
	{
		return Results.StatusCode(StatusCodes.Status500InternalServerError);
	}

	// リトライ系（最初の数回だけ失敗）
	if (mode == "rate_limit")
	{
		var n = counters.AddOrUpdate(key, 1, (_, v) => v + 1);
		if (n <= 2)
		{
			return Results.StatusCode(StatusCodes.Status429TooManyRequests);
		}
	}
	if (mode == "transient_503")
	{
		var n = counters.AddOrUpdate(key, 1, (_, v) => v + 1);
		if (n <= 2)
		{
			return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
		}
	}

	return null;
}

app.MapGet("/", () => "StubApi is running");

// PAT 検証用（AdoApiClient が最初に呼び出す）
app.MapGet("/_apis/profile/profiles/me", async () =>
{
	await MaybeDelayAsync("profiles");

	var error = MaybeReturnError("profiles");
	if (error is not null)
	{
		return error;
	}

	return Results.Json(new { id = "user-me", displayName = "Stub User", emailAddress = "stub@example.com" });
});

app.MapGet("/pullrequests", async (int? top, int? skip) =>
{
	await MaybeDelayAsync("pullrequests");

	var error = MaybeReturnError("pullrequests");
	if (error is not null)
	{
		return error;
	}

	var pageSize = top ?? 50;
	var offset = skip ?? 0;
	var total = 3;

	var items = Enumerable.Range(1, total)
		.Skip(offset)
		.Take(pageSize)
		.Select(i => new
		{
			pullRequestId = i,
			title = $"PR {i}",
			description = (string?)null,
			status = "completed",
			url = $"https://dev.azure.com/contoso/project/_git/repo/pullrequest/{i}",
			createdBy = new { displayName = "Alice", uniqueName = "alice@example.com", id = "user-alice" },
			creationDate = DateTimeOffset.UtcNow.AddDays(-i),
			closedDate = DateTimeOffset.UtcNow.AddDays(-i + 1),
			sourceRefName = "refs/heads/feature/test",
			targetRefName = "refs/heads/main",
		})
		.ToArray();

	return Results.Json(new { value = items, count = items.Length });
});

app.MapGet("/threads", async (int? prId) =>
{
	await MaybeDelayAsync("threads");

	var error = MaybeReturnError("threads");
	if (error is not null)
	{
		return error;
	}

	var pullRequestId = prId ?? 1;

	var items = new[]
	{
		new
		{
			id = 1000 + pullRequestId,
			status = "active",
			publishedDate = DateTimeOffset.UtcNow.AddDays(-1),
			threadContext = new
			{
				filePath = "/src/Example.cs",
				rightFileStart = new { line = 10, offset = 0 },
				rightFileEnd = new { line = 12, offset = 0 },
			},
			comments = new[]
			{
				new
				{
					id = 2000 + pullRequestId,
					parentCommentId = 0,
					author = new { displayName = "Bob", uniqueName = "bob@example.com", id = "user-bob" },
					publishedDate = DateTimeOffset.UtcNow.AddDays(-1),
					lastUpdatedDate = DateTimeOffset.UtcNow.AddDays(-1),
					commentType = "text",
					content = "Please add null checks.\nThanks!",
					isDeleted = false,
				},
				new
				{
					id = 3000 + pullRequestId,
					parentCommentId = 0,
					author = new { displayName = "Alice", uniqueName = "alice@example.com", id = "user-alice" },
					publishedDate = DateTimeOffset.UtcNow.AddDays(-1),
					lastUpdatedDate = DateTimeOffset.UtcNow.AddDays(-1),
					commentType = "text",
					content = "Looks good overall.",
					isDeleted = false,
				},
				new
				{
					id = 4000 + pullRequestId,
					parentCommentId = 0,
					author = new { displayName = "Charlie", uniqueName = "charlie@example.com", id = "user-charlie" },
					publishedDate = DateTimeOffset.UtcNow.AddDays(-1),
					lastUpdatedDate = DateTimeOffset.UtcNow.AddDays(-1),
					commentType = "text",
					content = "Consider adding unit tests.",
					isDeleted = false,
				}
			}
		}
	};

	return Results.Json(new { value = items, count = items.Length });
});

static string BuildPullRequestsQuery(HttpRequest request)
{
	static int? ReadInt(HttpRequest req, params string[] keys)
	{
		foreach (var key in keys)
		{
			if (!req.Query.TryGetValue(key, out var values))
			{
				continue;
			}

			var raw = values.ToString();
			if (string.IsNullOrWhiteSpace(raw))
			{
				continue;
			}

			if (int.TryParse(raw, out var value))
			{
				return value;
			}
		}

		return null;
	}

	var top = ReadInt(request, "top", "$top");
	var skip = ReadInt(request, "skip", "$skip");

	var parts = new List<string>();
	if (top is not null)
	{
		parts.Add($"top={top.Value}");
	}
	if (skip is not null)
	{
		parts.Add($"skip={skip.Value}");
	}

	return parts.Count == 0 ? string.Empty : "?" + string.Join("&", parts);
}

// ADO-like route aliases (so tests can call realistic endpoints)
app.MapGet("/api/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests", (HttpRequest request) =>
	Results.Redirect($"/pullrequests{BuildPullRequestsQuery(request)}", false));

app.MapGet("/api/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests/{prId}/threads", (int prId) =>
	Results.Redirect($"/threads?prId={prId}", false));

// AdoApiClient builds URLs like:
// - https://dev.azure.com/{org}/{project}/_apis/...
// - http://localhost:{port}/{project}/_apis/...
// Support both by providing aliases without the "/api" prefix.
app.MapGet("/{project}/_apis/git/repositories/{repoId}/pullrequests", (HttpRequest request) =>
	Results.Redirect($"/pullrequests{BuildPullRequestsQuery(request)}", false));

app.MapGet("/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests", (HttpRequest request) =>
	Results.Redirect($"/pullrequests{BuildPullRequestsQuery(request)}", false));

app.MapGet("/{project}/_apis/git/repositories/{repoId}/pullrequests/{prId}/threads", (int prId) =>
	Results.Redirect($"/threads?prId={prId}", false));

app.MapGet("/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests/{prId}/threads", (int prId) =>
	Results.Redirect($"/threads?prId={prId}", false));

app.Run();

public partial class Program { }
