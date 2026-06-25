# .NET 10 Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Upgrade OpenPhotoSort from .NET 8 to .NET 10, updating all package references and fixing pre-existing nullable/MVVM bugs revealed during the upgrade.

**Architecture:** Two-project MAUI solution — `OpenPhotoSort.Core` (class library) and `OpenPhotoSort.UI` (MAUI app targeting Windows and macOS via MacCatalyst). Upgrade proceeds project by project, Core first, then UI, then verification build.

**Tech Stack:** .NET 10, .NET MAUI, Magick.NET, CommunityToolkit.Maui, CommunityToolkit.Mvvm, Microsoft.Extensions.Logging

## Global Constraints

- Target: `net10.0` for Core, `net10.0-windows10.0.19041.0` and `net10.0-maccatalyst` for UI.
- Windows minimum supported version stays `10.0.17763.0`.
- macOS (MacCatalyst) minimum supported version stays `13.1`.
- Nullable reference types remain enabled (`<Nullable>enable</Nullable>`).
- Implicit usings remain enabled.
- All package references must be explicitly versioned (no floating `*`).
- Do not add Android or iOS as build targets — only Windows and macOS are in scope.
- `Uno.UI` must be removed (it is an accidental dependency; no Uno namespaces are imported anywhere in source).

---

## File Map

| File | Action | What changes |
|------|--------|--------------|
| `OpenPhotoSort.Core/OpenPhotoSort.Core.csproj` | Modify | TFM `net8.0` → `net10.0`; update Magick.NET versions; remove empty `<Folder>` |
| `OpenPhotoSort.UI/OpenPhotoSort.UI.csproj` | Modify | TFMs `net8.0-*` → `net10.0-*`; update all package versions; remove `Uno.UI` |
| `OpenPhotoSort.UI/MainPage.xaml.cs` | Modify | Fix `MyViewModel.BtnIsEnabled` setter (missing `OnPropertyChanged` call); fix nullable warning on `PropertyChangedEventHandler` |
| `OpenPhotoSort.UI/Platforms/MacCatalyst/FolderPicker.cs` | Modify | Fix `tcs.SetResult(null)` → `tcs.SetResult(string.Empty)` (nullable-safe) |
| `OpenPhotoSort.Core/Interfaces/IFolderPicker.cs` | Delete | Unused interface — `FolderPickerX` partial class is used instead |

---

## Task 1: Install Prerequisites

**Files:** none modified

**Interfaces:**
- Produces: verified .NET 10 SDK + MAUI workload on the developer machine

- [ ] **Step 1: Check .NET 10 SDK is installed**

```powershell
dotnet --list-sdks
```

Expected: at least one line containing `10.0.` (e.g., `10.0.100 [C:\Program Files\dotnet\sdk]`). If absent, download and install the .NET 10 SDK from https://dotnet.microsoft.com/download/dotnet/10.0.

- [ ] **Step 2: Install the MAUI workload for .NET 10**

```powershell
dotnet workload install maui
```

Expected output includes lines like `Successfully installed workload(s) maui`. This installs the `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst`, and `net10.0-windows` targets.

- [ ] **Step 3: Verify workload is present**

```powershell
dotnet workload list
```

Expected: `maui` appears in the list.

- [ ] **Step 4: Check Visual Studio version (if using VS)**

Visual Studio 2022 17.14 or later is required for .NET 10 MAUI. Go to `Help → About Microsoft Visual Studio` and verify the version. If older, update via `Help → Check for Updates`.

---

## Task 2: Upgrade Core Project to .NET 10

**Files:**
- Modify: `OpenPhotoSort.Core/OpenPhotoSort.Core.csproj`
- Delete: `OpenPhotoSort.Core/Interfaces/IFolderPicker.cs`

**Interfaces:**
- Consumes: nothing from earlier tasks
- Produces: `OpenPhotoSort.Core` compiling on `net10.0`

- [ ] **Step 1: Find the latest Magick.NET version compatible with .NET 10**

```powershell
dotnet package search Magick.NET-Q16-AnyCPU --take 5
```

The package targets `netstandard2.0`/`netstandard2.1`, so any version ≥ 14.0.0 works with .NET 10. Note the latest stable version number.

- [ ] **Step 2: Update Core csproj**

Replace the entire content of `OpenPhotoSort.Core/OpenPhotoSort.Core.csproj` with (substituting the actual latest version from Step 1 in place of `14.x.x`):

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Magick.NET-Q16-AnyCPU" Version="14.x.x" />
    <PackageReference Include="Magick.NET.Core" Version="14.x.x" />
  </ItemGroup>

</Project>
```

> **Note:** Replace `14.x.x` with the exact latest stable version found in Step 1. Both packages must use the same version number.

- [ ] **Step 3: Delete the unused interface**

Delete the file `OpenPhotoSort.Core/Interfaces/IFolderPicker.cs`. The `FolderPickerX` partial class pattern is used for folder picking throughout the app; this interface is never implemented or consumed.

```powershell
Remove-Item "C:\Project\OpenPhotoSort\OpenPhotoSort.Core\Interfaces\IFolderPicker.cs"
```

- [ ] **Step 4: Build Core only to verify**

```powershell
dotnet build OpenPhotoSort.Core/OpenPhotoSort.Core.csproj
```

Expected: `Build succeeded.` with 0 errors. Fix any errors before proceeding.

- [ ] **Step 5: Commit**

```powershell
git add OpenPhotoSort.Core/OpenPhotoSort.Core.csproj
git rm OpenPhotoSort.Core/Interfaces/IFolderPicker.cs
git commit -m "chore: upgrade Core to net10.0, remove unused IFolderPicker interface"
```

---

## Task 3: Upgrade UI Project Package References

**Files:**
- Modify: `OpenPhotoSort.UI/OpenPhotoSort.UI.csproj`

**Interfaces:**
- Consumes: upgraded Core from Task 2
- Produces: `OpenPhotoSort.UI` csproj with all packages pointing to .NET 10-compatible versions

- [ ] **Step 1: Find current .NET 10-compatible package versions**

Run each search and note the latest stable version:

```powershell
dotnet package search CommunityToolkit.Maui --take 3
dotnet package search CommunityToolkit.Mvvm --take 3
dotnet package search Microsoft.Extensions.Logging.Debug --take 3
```

For `CommunityToolkit.Maui`: the version must explicitly support `net10.0-windows` and `net10.0-maccatalyst` targets. If the latest stable version doesn't (unlikely by mid-2026 but verify), use `--prerelease` flag. The package version for .NET 10 MAUI is typically 11.x or later.

For `Microsoft.Extensions.Logging.Debug`: use `10.0.x` (matches the .NET 10 release train).

- [ ] **Step 2: Update the UI csproj**

Replace the entire content of `OpenPhotoSort.UI/OpenPhotoSort.UI.csproj` with the following, substituting the actual package versions discovered in Step 1:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>net10.0-maccatalyst</TargetFrameworks>
    <TargetFrameworks Condition="$([MSBuild]::IsOSPlatform('windows'))">$(TargetFrameworks);net10.0-windows10.0.19041.0</TargetFrameworks>

    <OutputType>Exe</OutputType>
    <RootNamespace>OpenPhotoSort</RootNamespace>
    <UseMaui>true</UseMaui>
    <SingleProject>true</SingleProject>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>

    <ApplicationTitle>OpenPhotoSort</ApplicationTitle>
    <ApplicationId>com.companyname.openphotosort</ApplicationId>
    <ApplicationDisplayVersion>1.0</ApplicationDisplayVersion>
    <ApplicationVersion>1</ApplicationVersion>

    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'ios'">11.0</SupportedOSPlatformVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'maccatalyst'">13.1</SupportedOSPlatformVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'android'">21.0</SupportedOSPlatformVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</SupportedOSPlatformVersion>
    <TargetPlatformMinVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'windows'">10.0.17763.0</TargetPlatformMinVersion>
    <SupportedOSPlatformVersion Condition="$([MSBuild]::GetTargetPlatformIdentifier('$(TargetFramework)')) == 'tizen'">6.5</SupportedOSPlatformVersion>
  </PropertyGroup>

  <ItemGroup>
    <MauiIcon Include="Resources\AppIcon\appicon.svg" ForegroundFile="Resources\AppIcon\appiconfg.svg" Color="#512BD4" />
    <MauiSplashScreen Include="Resources\Splash\splash.svg" Color="#512BD4" BaseSize="128,128" />
    <MauiImage Include="Resources\Images\*" />
    <MauiImage Update="Resources\Images\dotnet_bot.png" Resize="True" BaseSize="300,185" />
    <MauiFont Include="Resources\Fonts\*" />
    <MauiAsset Include="Resources\Raw\**" LogicalName="%(RecursiveDir)%(Filename)%(Extension)" />
  </ItemGroup>

  <ItemGroup>
    <Compile Remove="Helpers\MacCatalyst\FolderPickerXBase.cs" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Maui" Version="REPLACE_WITH_LATEST" />
    <PackageReference Include="CommunityToolkit.Mvvm" Version="REPLACE_WITH_LATEST" />
    <PackageReference Include="Microsoft.Maui.Controls" Version="$(MauiVersion)" />
    <PackageReference Include="Microsoft.Maui.Controls.Compatibility" Version="$(MauiVersion)" />
    <PackageReference Include="Microsoft.Extensions.Logging.Debug" Version="10.0.0" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\OpenPhotoSort.Core\OpenPhotoSort.Core.csproj" />
  </ItemGroup>

  <!-- Both iOS and Mac Catalyst -->
  <ItemGroup Condition="$(TargetFramework.StartsWith('net10.0-ios')) != true AND $(TargetFramework.StartsWith('net10.0-maccatalyst')) != true">
    <Compile Remove="**\MaciOS\**\*.cs" />
    <None Include="**\MaciOS\**\*.cs" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />
  </ItemGroup>

  <!-- Mac Catalyst -->
  <ItemGroup Condition="$(TargetFramework.StartsWith('net10.0-maccatalyst')) != true">
    <Compile Remove="**\MacCatalyst\**\*.cs" />
    <None Include="**\MacCatalyst\**\*.cs" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />
  </ItemGroup>

  <!-- Windows -->
  <ItemGroup Condition="$(TargetFramework.Contains('-windows')) != true">
    <Compile Remove="**\Windows\**\*.cs" />
    <None Include="**\Windows\**\*.cs" Exclude="$(DefaultItemExcludes);$(DefaultExcludesInProjectFolder)" />
  </ItemGroup>

</Project>
```

> **Important:** Replace both `REPLACE_WITH_LATEST` placeholders with the actual versions found in Step 1. The conditions in the platform ItemGroups have been updated from `net8.0-*` to `net10.0-*` to match the new TFMs.

- [ ] **Step 3: Restore packages**

```powershell
dotnet restore OpenPhotoSort.UI/OpenPhotoSort.UI.csproj
```

Expected: `Restore succeeded.` If package not found errors appear, verify the version numbers from Step 1 are correct.

- [ ] **Step 4: Commit the csproj change**

```powershell
git add OpenPhotoSort.UI/OpenPhotoSort.UI.csproj
git commit -m "chore: upgrade UI to net10.0-*, update MAUI packages, remove Uno.UI"
```

---

## Task 4: Fix Pre-existing Code Bugs

**Files:**
- Modify: `OpenPhotoSort.UI/MainPage.xaml.cs`
- Modify: `OpenPhotoSort.UI/Platforms/MacCatalyst/FolderPicker.cs`

**Interfaces:**
- Consumes: nothing from prior tasks (these are standalone bug fixes)
- Produces: `MyViewModel` with working property change notification; `FolderPickerX` MacCatalyst implementation with no nullable violation

These bugs exist in the .NET 8 version but become errors or warnings under .NET 10's stricter nullable analysis.

- [ ] **Step 1: Fix MyViewModel in MainPage.xaml.cs**

The `BtnIsEnabled` setter does not call `OnPropertyChanged()`, so the button never re-enables after the operation completes. Also, the `PropertyChangedEventHandler` field must be nullable.

Open `OpenPhotoSort.UI/MainPage.xaml.cs` and replace the `MyViewModel` class with:

```csharp
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
```

- [ ] **Step 2: Fix MainPage to reuse the existing ViewModel instance**

While fixing the ViewModel, also fix `MainPage` so it reuses the same `MyViewModel` instance instead of creating a new one on each click (the current code discards binding and creates a new ViewModel on every button press, losing any state):

Replace `OnFilePickerClicked` and `PickAndShowFileAsync` in `MainPage.xaml.cs` with:

```csharp
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
        var picker = new FolderPickerX();
        var folderPath = await picker.PickFolderAsync(CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(folderPath))
        {
            await Task.Run(() => ProcessFiles(folderPath));
        }
    }
    catch (Exception ex)
    {
        await DisplayAlert("Error", ex.Message, "OK");
    }
    finally
    {
        _viewModel.BtnIsEnabled = true;
    }
}
```

> Remove the old `BindingContext = new MyViewModel { BtnIsEnabled = false };` and `BindingContext = new MyViewModel { BtnIsEnabled = true };` lines — these were resetting the binding context on every click, which breaks two-way binding.

- [ ] **Step 3: Fix MacCatalyst FolderPicker nullable issue**

Open `OpenPhotoSort.UI/Platforms/MacCatalyst/FolderPicker.cs`. The call `tcs.SetResult(null)` violates the non-nullable `Task<string>` return type when `Nullable` is enabled.

Replace the entire file content with:

```csharp
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
```

> The unused `using System.Net.NetworkInformation`, `using Foundation`, `using Microsoft.Maui.Controls`, `using OpenPhotoSort`, and `using System.Threading.Tasks` are removed (covered by implicit usings and not needed). `null` return replaced with `string.Empty`. `tcs.Task` is now properly awaited.

- [ ] **Step 4: Commit the bug fixes**

```powershell
git add OpenPhotoSort.UI/MainPage.xaml.cs
git add "OpenPhotoSort.UI/Platforms/MacCatalyst/FolderPicker.cs"
git commit -m "fix: correct ViewModel property notification, fix nullable in MacCatalyst picker"
```

---

## Task 5: Build Verification and Breaking Change Resolution

**Files:**
- Modify: any file where the compiler reports an error (discovered during this task)

**Interfaces:**
- Consumes: all prior tasks completed
- Produces: clean build on Windows; no errors, warnings-as-errors passing

- [ ] **Step 1: Full restore and build on Windows**

```powershell
dotnet restore OpenPhotoSort.sln
dotnet build OpenPhotoSort.sln -f net10.0-windows10.0.19041.0
```

Expected: `Build succeeded.` with 0 errors. If there are errors, read them carefully — each one is a breaking change or missing package version.

- [ ] **Step 2: Treat each build error as a task**

Common .NET 10 MAUI breaking changes to watch for:

| Error pattern | Fix |
|---|---|
| `'FolderPicker' does not contain a definition for 'Default'` | CommunityToolkit.Maui API change — check release notes for the new API; typically `FolderPicker.PickAsync()` or similar |
| `The type or namespace 'MauiWinUIApplication' could not be found` | WinUI namespace changed — check the MAUI migration guide |
| `Error NETSDK1138: The target framework 'net10.0-windows...'` | Wrong Windows SDK version installed — install Windows 10 SDK 19041 via VS Installer |
| `Package 'X' was restored using '.NETFramework'` | Package doesn't support net10.0 — find a newer version or a replacement |

For each error, search the official MAUI .NET 10 migration guide at https://learn.microsoft.com/dotnet/maui/migration/dotnet10 and apply the documented fix. Commit each fix separately.

- [ ] **Step 3: Build with warnings-as-errors to catch nullable issues**

```powershell
dotnet build OpenPhotoSort.sln -f net10.0-windows10.0.19041.0 -p:TreatWarningsAsErrors=true
```

Fix all warnings before marking this task done.

- [ ] **Step 4: Update MauiProgram.cs if CommunityToolkit.Maui API changed**

If the build fails in `MauiProgram.cs`, the most likely cause is a breaking change in `UseMauiCommunityToolkit()`. Check the CommunityToolkit.Maui release notes and update the call accordingly.

Current content for reference:

```csharp
builder
    .UseMauiApp<App>()
    .UseMauiCommunityToolkit()
    .ConfigureFonts(fonts =>
    {
        fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
        fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
    });
```

- [ ] **Step 5: Commit build fixes**

After all errors are resolved:

```powershell
git add -A
git commit -m "chore: resolve .NET 10 MAUI build errors and breaking changes"
```

---

## Task 6: Runtime Smoke Test on Windows

**Files:** none modified

**Interfaces:**
- Consumes: clean build from Task 5

- [ ] **Step 1: Run the app on Windows**

From Visual Studio: press F5, or from command line:

```powershell
dotnet run --project OpenPhotoSort.UI/OpenPhotoSort.UI.csproj -f net10.0-windows10.0.19041.0
```

- [ ] **Step 2: Verify golden path**

1. App launches and shows a 600×500 window with a "Select Folder" button.
2. Click "Select Folder" — a folder picker dialog opens.
3. Navigate to a folder that contains JPG images with EXIF data (e.g., photos from a camera).
4. Select the folder. The button should be disabled during processing.
5. After processing completes, the text area displays file paths and EXIF tags (e.g., `DateTimeOriginal`, `Make`, `Model`).
6. The "Select Folder" button re-enables after processing completes.

- [ ] **Step 3: Verify the button re-enables (the MVVM fix)**

Click "Select Folder" with a folder that has many images. While processing, confirm the button is disabled. After completion, confirm it is enabled again. This verifies the `OnPropertyChanged()` fix in Task 4.

- [ ] **Step 4: Test with a folder containing no images**

Select an empty folder or a folder with no supported image types. The text area should remain empty (no crash).

- [ ] **Step 5: Test with an image that has no EXIF data**

If you have a PNG or a stripped JPEG, include it. The app should skip it silently (the `if (profile is null)` branch in `ImageHelper.cs` logs to console and returns `null`; the UI skips null results).

---

## Task 7: Clean Up Artifacts and Final Commit

**Files:**
- Delete: `OpenPhotoSort.UI/bin` and `OpenPhotoSort.UI/obj` (or `.gitignore` them)
- Create: `.gitignore` if not present

**Interfaces:**
- Consumes: verified runtime from Task 6
- Produces: clean repo state, no binary artifacts tracked in git

- [ ] **Step 1: Check if .gitignore exists**

```powershell
Test-Path "C:\Project\OpenPhotoSort\.gitignore"
```

- [ ] **Step 2: Create or update .gitignore**

If `.gitignore` is absent, create it:

```
bin/
obj/
*.user
.vs/
```

If it exists, verify those patterns are present and add any that are missing.

- [ ] **Step 3: Remove tracked build artifacts from git (if any)**

```powershell
git rm -r --cached OpenPhotoSort.UI/bin OpenPhotoSort.UI/obj OpenPhotoSort.Core/bin OpenPhotoSort.Core/obj 2>$null
```

This removes them from git tracking without deleting the files on disk.

- [ ] **Step 4: Final commit**

```powershell
git add .gitignore
git commit -m "chore: add .gitignore, untrack build artifacts"
```

---

## Self-Review Checklist

- [x] TFM changed in Core csproj: `net8.0` → `net10.0`
- [x] TFM changed in UI csproj: `net8.0-*` → `net10.0-*`
- [x] `Uno.UI` removed (no source file imports it)
- [x] Platform ItemGroup conditions updated to `net10.0-*`
- [x] `Microsoft.Extensions.Logging.Debug` updated to `10.0.0`
- [x] CommunityToolkit.Maui and Mvvm versions updated to .NET 10-compatible releases
- [x] `IFolderPicker.cs` (unused dead code) deleted
- [x] `MyViewModel.BtnIsEnabled` setter now calls `OnPropertyChanged()`
- [x] `PropertyChangedEventHandler` field is now nullable (`?`)
- [x] MacCatalyst `FolderPicker.cs` no longer returns `null` for a `Task<string>`
- [x] `MainPage` no longer discards and recreates the ViewModel on every click
- [x] Windows platform smoke test steps provided
- [x] `.gitignore` cleanup included
