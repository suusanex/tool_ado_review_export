using System;
using System.Net;
using System.Runtime.Serialization;

namespace AdoReviewExport.Application.Exceptions;

/// <summary>
/// 外部 API（Azure DevOps 等）でエラーが発生した場合に送出される例外。
/// </summary>
[Serializable]
public sealed class ApiException : AdoExportException
{
    public ApiException() { }

    public ApiException(string message)
        : base(message)
    {
    }

    public ApiException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    public ApiException(string message, HttpStatusCode statusCode, Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
    }

    public HttpStatusCode? StatusCode { get; }

    private ApiException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
