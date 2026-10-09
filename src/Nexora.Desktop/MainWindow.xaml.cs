using System.Windows;

namespace Nexora.Desktop;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnNavChecked(object sender, RoutedEventArgs e)
    {
        // Verhindert, dass sich ein deaktivierter Bereich über die Navigation
        // im Seiteninhalt vermischt – Standardverhalten reicht aus.
    }
}