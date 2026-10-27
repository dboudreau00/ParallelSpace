using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ParallelSpace.Views;

public partial class ConfirmActionWindow : Window
{
    public ConfirmActionWindow(string title, string message)
    {
        InitializeComponent();
        Title = title;
        TitleText.Text = title;
        MessageText.Text = message;
    }

    private void OnAccept(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(false);
}

