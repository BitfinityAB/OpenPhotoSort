using AppKit;

namespace OpenPhotoSort.Helpers;

public partial class FolderPickerX
{
    public partial async Task<string> PickFolderAsync(CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<string>();

        var openPanel = new NSOpenPanel
        {
            CanChooseFiles = false,
            CanChooseDirectories = true,
            AllowsMultipleSelection = false
        };

        openPanel.BeginSheet(NSApplication.SharedApplication.KeyWindow, result =>
        {
            if (result == 1 && openPanel.Urls.Length > 0)
                tcs.SetResult(openPanel.Urls[0].Path ?? string.Empty);
            else
                tcs.SetResult(string.Empty);
        });

        return await tcs.Task;
    }
}
