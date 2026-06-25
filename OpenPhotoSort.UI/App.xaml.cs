#if WINDOWS
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Windows.Graphics;
using System.Runtime.InteropServices;
#endif

namespace OpenPhotoSort
{
    public partial class App : Application
    {
        // Logical (density-independent) pixel targets
        const int WindowWidth = 1050;
        const int WindowHeight = 750;

#if WINDOWS
        [DllImport("user32.dll")]
        private static extern int GetDpiForWindow(IntPtr hwnd);
#endif

        public App()
        {
            InitializeComponent();
            Microsoft.Maui.Handlers.WindowHandler.Mapper.AppendToMapping(nameof(IWindow), (handler, view) =>
            {
#if WINDOWS
                var nativeWindow = handler.PlatformView;
                nativeWindow.Activate();
                IntPtr windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(nativeWindow);
                WindowId windowId = Win32Interop.GetWindowIdFromWindow(windowHandle);
                AppWindow appWindow = AppWindow.GetFromWindowId(windowId);

                // Scale to physical pixels so the window is WindowWidth×WindowHeight
                // in logical (density-independent) units regardless of display DPI.
                double scale = GetDpiForWindow(windowHandle) / 96.0;
                appWindow.Resize(new SizeInt32(
                    (int)(WindowWidth * scale),
                    (int)(WindowHeight * scale)));
#endif
            });
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            return new Window(new AppShell());
        }
    }
}
