using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace MyMusicPlayer
{
    public partial class App : Application
    {
        public App()
        {
            // FORCE SOFTWARE RENDERING
            RenderOptions.ProcessRenderMode = RenderMode.SoftwareOnly;
        }
    }
}
