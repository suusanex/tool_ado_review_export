using System;
using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using AdoReviewExport.UI.ViewModels;
using AdoReviewExport.UI.Views.Dialogs;
using Windows.Storage;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace AdoReviewExport.UI.Views;

public sealed partial class MainWindow : Window
{
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    public void Initialize(MainViewModel viewModel, bool enableDialogs = true)
    {
        _viewModel = viewModel;
        Root.DataContext = viewModel;
        if (enableDialogs)
        {
            viewModel.DialogRequested += OnDialogRequested;
        }
    }

    private async void OnBrowseClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
        {
            return;
        }

        try
        {
            var picker = new FileSavePicker();
            picker.FileTypeChoices.Add("JSON", new[] { ".json" });
            picker.SuggestedFileName = "ado-review-export";

            var hwnd = WindowNative.GetWindowHandle(this);
            InitializeWithWindow.Initialize(picker, hwnd);

            StorageFile? file = await picker.PickSaveFileAsync();
            _viewModel.SetOutputPathFromPicker(file?.Path);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
    }

    private async void OnDialogRequested(object? sender, DialogRequest request)
    {
        try
        {
            if (Content is not FrameworkElement fe)
            {
                return;
            }

            if (request.Kind == DialogKind.Completion && request.OutputFilePath is not null)
            {
                var dialog = new CompletionDialog(request.Message, request.OutputFilePath)
                {
                    XamlRoot = fe.XamlRoot,
                };
                await dialog.ShowAsync();
                return;
            }

            if (request.Kind == DialogKind.Error)
            {
                var dialog = new ErrorDialog(request.Message)
                {
                    XamlRoot = fe.XamlRoot,
                };
                await dialog.ShowAsync();
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Trace.TraceError(ex.ToString());
        }
    }
}
