namespace AdoReviewExport.UI.ViewModels;

public enum DialogKind
{
    Completion,
    Error,
}

public sealed record DialogRequest(
    DialogKind Kind,
    string Title,
    string Message,
    string? OutputFilePath = null);
