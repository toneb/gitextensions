using Avalonia.Markup.Xaml;

namespace GitExtensions;

public partial class App : Avalonia.Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
