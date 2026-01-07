using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AdoReviewExport.Infrastructure.AzureDevOps.Dtos;

namespace AdoReviewExport.Infrastructure.Json;

/// <summary>
/// JSON エクスポートの既定実装。
/// </summary>
public sealed class JsonExporter : IJsonExporter
{
    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    public async Task<IJsonExportSession> StartAsync(
        string outputFilePath,
        JsonExportMeta meta,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var directory = Path.GetDirectoryName(outputFilePath);
            if (!string.IsNullOrWhiteSpace(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var stream = new FileStream(outputFilePath, FileMode.Create, FileAccess.Write, FileShare.Read);
            var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
            {
                Indented = true,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            });

            // Root object
            writer.WriteStartObject();
            writer.WriteString("schemaVersion", "1.0");

            // Stream items first so we can write accurate summary in meta.
            writer.WritePropertyName("items");
            writer.WriteStartArray();
            await writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            return new Session(stream, writer, meta);
        }
        catch (Exception ex)
        {
            throw new IOException($"Failed to open output file: {outputFilePath}", ex);
        }
    }

    private sealed class Session : IJsonExportSession
    {
        private readonly FileStream _stream;
        private readonly Utf8JsonWriter _writer;
        private readonly JsonExportMeta _meta;
        private int _written;
        private bool _completed;

        public Session(FileStream stream, Utf8JsonWriter writer, JsonExportMeta meta)
        {
            _stream = stream;
            _writer = writer;
            _meta = meta;
        }

        public Task WriteItemAsync(PullRequestDto pr, ThreadDto thread, CommentDto comment, CancellationToken cancellationToken = default)
        {
            if (_completed)
            {
                throw new InvalidOperationException("Export session is already completed.");
            }

            var item = new
            {
                pr = new
                {
                    id = pr.PullRequestId,
                    title = pr.Title,
                    description = pr.Description,
                    url = pr.Url,
                    status = pr.Status,
                    createdBy = pr.CreatedBy,
                    creationDate = pr.CreationDate,
                    closedDate = pr.ClosedDate,
                    sourceRefName = pr.SourceRefName,
                    targetRefName = pr.TargetRefName,
                },
                thread = new
                {
                    id = thread.Id,
                    status = thread.Status,
                    publishedDate = thread.PublishedDate,
                    threadContext = thread.ThreadContext,
                    properties = new { },
                },
                comment = new
                {
                    id = comment.Id,
                    parentCommentId = comment.ParentCommentId,
                    author = comment.Author,
                    publishedDate = comment.PublishedDate,
                    lastUpdatedDate = comment.LastUpdatedDate,
                    commentType = comment.CommentType,
                    content = comment.Content,
                    isDeleted = comment.IsDeleted,
                },
            };

            JsonSerializer.Serialize(_writer, item, new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                PropertyNamingPolicy = null,
            });

            _written++;
            if (_written % 100 == 0)
            {
                return _writer.FlushAsync(cancellationToken);
            }

            return Task.CompletedTask;
        }

        public async Task CompleteAsync(JsonExportSummary summary, CancellationToken cancellationToken = default)
        {
            if (_completed)
            {
                return;
            }

            _writer.WriteEndArray();

            _writer.WritePropertyName("meta");
            _writer.WriteStartObject();
            _writer.WriteString("schemaVersion", "1.0");
            _writer.WriteString("exportedAt", _meta.ExportedAt.ToString("O"));
            _writer.WriteString("organization", _meta.Organization);
            _writer.WriteString("project", _meta.Project);
            _writer.WriteString("repository", _meta.Repository);

            _writer.WritePropertyName("filters");
            _writer.WriteStartObject();
            _writer.WritePropertyName("authors");
            _writer.WriteStartArray();
            if (_meta.AuthorFilters is not null)
            {
                foreach (var author in _meta.AuthorFilters)
                {
                    _writer.WriteStringValue(author);
                }
            }
            _writer.WriteEndArray();
            _writer.WriteEndObject();

            _writer.WritePropertyName("summary");
            _writer.WriteStartObject();
            _writer.WriteNumber("totalPullRequests", summary.TotalPullRequests);
            _writer.WriteNumber("totalThreads", summary.TotalThreads);
            _writer.WriteNumber("totalComments", summary.TotalComments);
            _writer.WriteEndObject();

            _writer.WriteString("appVersion", _meta.AppVersion);
            _writer.WriteEndObject();

            _writer.WriteEndObject();
            await _writer.FlushAsync(cancellationToken).ConfigureAwait(false);

            _completed = true;
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                _writer.Dispose();
                await _stream.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Logger.Error(ex, ex.ToString());
            }
        }
    }
}
