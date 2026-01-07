using System;
using System.Runtime.InteropServices;

namespace AdoReviewExport.UI.Helpers;

/// <summary>
/// WinExe で起動された CLI モード用のコンソール補助。
/// </summary>
public static class ConsoleHelper
{
    private const int ATTACH_PARENT_PROCESS = -1;

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AttachConsole(int dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool AllocConsole();

    public static void EnsureConsole()
    {
        // 親プロセスのコンソールに接続できる場合はそれを使う。
        if (AttachConsole(ATTACH_PARENT_PROCESS))
        {
            return;
        }

        // それでも無理なら新規に作る。
        _ = AllocConsole();
    }

    public static void WriteErrorLine(string message)
    {
        var prev = Console.ForegroundColor;
        try
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
        }
        finally
        {
            Console.ForegroundColor = prev;
        }
    }
}
