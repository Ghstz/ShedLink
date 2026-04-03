using System.Windows;

namespace Shed_Security_AP
{
    public partial class App : Application
    {
        public App()
        {
#pragma warning disable WPF0001 // ThemeMode is experimental in .NET 10
            ThemeMode = ThemeMode.None;
#pragma warning restore WPF0001
        }
    }
}
