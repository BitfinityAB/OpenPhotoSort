using OpenPhotoSort.ViewModels;

namespace OpenPhotoSort;

public partial class MainPage : ContentPage
{
    public MainPage()
    {
        InitializeComponent();
        BindingContext = new MainViewModel();
    }
}
