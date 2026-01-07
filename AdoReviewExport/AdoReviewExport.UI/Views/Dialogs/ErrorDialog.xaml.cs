using Microsoft.UI.Xaml;

namespace AdoReviewExport.UI.Views.Dialogs;

public sealed partial class ErrorDialog
{
    public ErrorDialog(string message)
    {
        InitializeComponent();
        MessageText.Text = message;
    }
}
