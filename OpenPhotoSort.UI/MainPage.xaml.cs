using OpenPhotoSort.Helpers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenPhotoSort;

public partial class MainPage : ContentPage
{
    private readonly MyViewModel _viewModel = new();

    public MainPage()
    {
        InitializeComponent();
        BindingContext = _viewModel;
    }

    private async void OnFilePickerClicked(object sender, EventArgs e)
    {
        _viewModel.BtnIsEnabled = false;
        await PickAndShowFileAsync();
    }

    private async Task PickAndShowFileAsync()
    {
        try
        {
            FolderPickerX picker = new();
            var folderPath = await picker.PickFolderAsync(CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(folderPath))
            {
                await Task.Run(() => ProcessFiles(folderPath));
            }
        }
        catch (Exception ex)
        {
            await DisplayAlertAsync("Error", ex.Message, "OK");
        }
        finally
        {
            _viewModel.BtnIsEnabled = true;
        }
    }

    private void ProcessFiles(string folderPath)
    {
        var imageFiles = Directory.GetFiles(folderPath, "*.*", SearchOption.TopDirectoryOnly)
                                    .Where(file => file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                                                    file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                                                    file.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ||
                                                    file.EndsWith(".bmp", StringComparison.OrdinalIgnoreCase) ||
                                                    file.EndsWith(".gif", StringComparison.OrdinalIgnoreCase) ||
                                                    file.EndsWith(".heic", StringComparison.OrdinalIgnoreCase))
                                    .ToList();
        var fileProps = new StringBuilder();
        foreach (var filePath in imageFiles)
        {
            var exifData = Core.ImageHelper.ReadExifData(filePath);
            if (exifData != null)
            {
                fileProps.AppendLine(filePath);
                exifData.Keys.ToList().ForEach(k => fileProps.AppendLine($"{k}: {exifData[k].Item2} (type: {exifData[k].Item1})"));
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            FileProps.Text = fileProps.ToString();
        });
    }
}

public class MyViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public void OnPropertyChanged([CallerMemberName] string name = "") =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private bool _btnIsEnabled = true;

    public bool BtnIsEnabled
    {
        get => _btnIsEnabled;
        set
        {
            _btnIsEnabled = value;
            OnPropertyChanged();
        }
    }
}
