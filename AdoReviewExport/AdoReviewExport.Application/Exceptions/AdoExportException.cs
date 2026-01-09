using System;
using System.Runtime.Serialization;

namespace AdoReviewExport.Application.Exceptions;

/// <summary>
/// 本アプリケーションのドメイン例外の基底クラス。
/// </summary>
[Serializable]
public abstract class AdoExportException : Exception
{
    protected AdoExportException() { }

    protected AdoExportException(string message)
        : base(message)
    {
    }

    protected AdoExportException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    protected AdoExportException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }
}
