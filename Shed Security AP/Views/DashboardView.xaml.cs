using System.Windows.Controls;
using System.Windows.Input;
using Shed_Security_AP.ViewModels;

namespace Shed_Security_AP.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
    }

    private void ModalOverlay_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is DashboardViewModel vm)
            vm.IsInspectModalOpen = false;
    }
}
