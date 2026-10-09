using System.Windows;

namespace Nexora.Desktop;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var vm = new ViewModels.MainViewModel();
        var window = new MainWindow { DataContext = vm };
        MainWindow = window;
        window.Show();
        _ = vm.InitializeAsync();
    }
}