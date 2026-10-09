using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using Nexora.Desktop.ViewModels;

namespace Nexora.Desktop.Views;

public partial class ChatView : UserControl
{
    public ChatView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChatViewModel vm)
        {
            vm.ScrollRequested -= ScrollDown;
            vm.ScrollRequested += ScrollDown;
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded, ScrollDown);
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (DataContext is ChatViewModel vm)
            vm.ScrollRequested -= ScrollDown;
    }

    private void ScrollDown()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Background, () =>
        {
            MessageScroller.ScrollToEnd();
            InputBox.Focus();
        });
    }

    private void InputBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        if ((Keyboard.Modifiers & ModifierKeys.Shift) != 0) return; // Zeilenumbruch

        if (DataContext is ChatViewModel { SendCommand: not null } vm)
        {
            vm.SendCommand.Execute(null);
            e.Handled = true;
        }
    }
}