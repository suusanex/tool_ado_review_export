using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(options =>
{
	options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

var app = builder.Build();

app.MapGet("/", () => "StubApi is running");

app.MapGet("/pullrequests", (int? top, int? skip) =>
{
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

app.MapGet("/threads", (int? prId) =>
{
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
				}
			}
		}
	};

	return Results.Json(new { value = items, count = items.Length });
});

// ADO-like route aliases (so tests can call realistic endpoints)
app.MapGet("/api/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests", (int? top, int? skip) =>
	Results.Redirect($"/pullrequests?top={top}&skip={skip}", false));

app.MapGet("/api/{org}/{project}/_apis/git/repositories/{repoId}/pullrequests/{prId}/threads", (int prId) =>
	Results.Redirect($"/threads?prId={prId}", false));

app.Run();
