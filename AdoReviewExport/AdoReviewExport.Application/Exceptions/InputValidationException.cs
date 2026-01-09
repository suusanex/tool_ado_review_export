using System;
using System.Runtime.Serialization;

namespace AdoReviewExport.Application.Exceptions;

/// <summary>
/// 入力パラメータが不正な場合に送出される例外。
/// </summary>
[Serializable]
public sealed class InputValidationException : AdoExportException
{
    public InputValidationException() { }

    public InputValidationException(string message)
        : base(message)
    {
    }

    public InputValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private InputValidationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
