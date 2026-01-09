using System;
using System.Runtime.Serialization;

namespace AdoReviewExport.Application.Exceptions;

/// <summary>
/// 認証に失敗した場合に送出される例外。
/// </summary>
[Serializable]
public sealed class AuthenticationException : AdoExportException
{
    public AuthenticationException() { }

    public AuthenticationException(string message)
        : base(message)
    {
    }

    public AuthenticationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private AuthenticationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
