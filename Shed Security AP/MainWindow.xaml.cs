using System.Windows;
using System.Windows.Input;

namespace Shed_Security_AP
{
    /// <summary>
    /// Custom chrome window. Tracks focus state so the anti-cheat ViewModel knows
    /// whether to fire desktop notifications, and handles double-click maximize
    /// on the title bar since we're using a borderless window.
    /// </summary>
    public partial class MainWindow : Window
    {
        public bool IsAppFocused { get; private set; } = true;

        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnActivated(EventArgs e)
        {
            base.OnActivated(e);
            IsAppFocused = true;
        }

        protected override void OnDeactivated(EventArgs e)
        {
            base.OnDeactivated(e);
            IsAppFocused = false;
        }

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                WindowState = WindowState == WindowState.Maximized
                    ? WindowState.Normal
                    : WindowState.Maximized;
            }
            else
            {
                DragMove();
            }
        }
    }
}