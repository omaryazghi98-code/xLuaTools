using System.Windows.Controls;
using LuaToolsGui.ViewModels;

namespace LuaToolsGui.Views;

public partial class Ps5CompanionView : UserControl
{
    public Ps5CompanionView(Ps5CompanionViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.OnViewLoadedAsync();
    }
}
