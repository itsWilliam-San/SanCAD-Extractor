using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;

namespace Sewarage.Views.Controls;

public partial class SearchBox : UserControl
{
    private static readonly StyledProperty<string> PlaceholderProperty =
        AvaloniaProperty.Register<SearchBox, string>(nameof(Placeholder));

    public string Placeholder
    {
        get => GetValue(PlaceholderProperty);
        set => SetValue(PlaceholderProperty, value);
    }

    public SearchBox()
    {
        InitializeComponent();
    }
}