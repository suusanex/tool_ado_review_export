using System;
using System.Diagnostics;
using System.IO;
using Microsoft.UI.Xaml;

namespace AdoReviewExport.UI.Views.Dialogs;

public sealed partial class CompletionDialog
{
    private readonly string _outputFilePath;

    public CompletionDialog(string message, string outputFilePath)
    {
        _outputFilePath = outputFilePath;
        InitializeComponent();
        MessageText.Text = message;
        PathText.Text = outputFilePath;

        PrimaryButtonClick += (_, __) => OpenFile();
        SecondaryButtonClick += (_, __) => OpenFolder();
    }

    private void OpenFile()
    {
        try
        {
            if (File.Exists(_outputFilePath))
            {
                Process.Start(new ProcessStartInfo(_outputFilePath) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
    }

    private void OpenFolder()
    {
        try
        {
            var dir = Path.GetDirectoryName(_outputFilePath);
            if (!string.IsNullOrWhiteSpace(dir) && Directory.Exists(dir))
            {
                Process.Start(new ProcessStartInfo(dir) { UseShellExecute = true });
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
    }
}
