using System.Net.Http.Headers;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Styling;
using Avalonia.VisualTree;

namespace Sewarage.Views.Controls;

public partial class IconButton : UserControl
{
    private static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<IconButton, string>(nameof(IconPath));
    
    private static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<IconButton, string>(nameof(Text));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }
    
    public string IconPath
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }
    
    public IconButton()
    {
        InitializeComponent();
    }
}