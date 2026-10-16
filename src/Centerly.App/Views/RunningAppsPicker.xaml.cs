using System.Windows.Controls;
using System.Windows.Input;
using Centerly.App.ViewModels;

namespace Centerly.App.Views;

internal sealed partial class RunningAppsPicker : UserControl
{
    public RunningAppsPicker(RunningAppsPickerViewModel viewModel)
    {
        DataContext = viewModel;
        InitializeComponent();
        Loaded += (_, _) => FilterBox.Focus();
    }

    /// <summary>Raised when the user double-clicks an application: pick it right away.</summary>
    public event EventHandler? ItemActivated;

    private void OnItemDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (((RunningAppsPickerViewModel)DataContext).Selected is not null)
            ItemActivated?.Invoke(this, EventArgs.Empty);
    }
}
