using CommunityToolkit.Maui.Storage;

namespace OpenPhotoSort.Helpers;

public partial class FolderPickerX
{
    public partial async Task<string> PickFolderAsync(CancellationToken cancellationToken)
    {
        var result = await FolderPicker.Default.PickAsync(cancellationToken);
        if (result.IsSuccessful)
        {
            return result.Folder.Path;
        }

        return string.Empty;
    }
}
