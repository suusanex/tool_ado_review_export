using System;
using System.Runtime.Serialization;

namespace AdoReviewExport.Application.Exceptions;

/// <summary>
/// 出力（ファイル書き込み等）に失敗した場合に送出される例外。
/// </summary>
[Serializable]
public sealed class OutputException : AdoExportException
{
    public OutputException() { }

    public OutputException(string message)
        : base(message)
    {
    }

    public OutputException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    private OutputException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
