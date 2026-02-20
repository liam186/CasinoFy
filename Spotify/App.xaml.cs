using System.Configuration;
using System.Data;
using System.Windows;

namespace Spotify
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            // Add the converter to application resources so XAML can reference it by key
            Resources.Add("NullToVisibleConverter", new NullToVisibilityConverter());
            base.OnStartup(e);
        }
    }

}
