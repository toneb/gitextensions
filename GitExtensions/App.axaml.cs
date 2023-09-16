using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace GitExtensions;

public partial class App : Avalonia.Application
{
    private readonly Action _onFrameworkInitialized;

    public App(Action onFrameworkInitialized)
        => _onFrameworkInitialized = onFrameworkInitialized;

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            _onFrameworkInitialized();
        }

        base.OnFrameworkInitializationCompleted();
    }
}
