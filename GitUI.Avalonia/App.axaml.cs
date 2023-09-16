using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using GitUI.CommandsDialogs;

namespace GitUI.Avalonia;

public partial class App : Application
{
    private readonly string[] _args;
    private readonly string? _workingDir;

    public App(string[] args, string? workingDir)
    {
        _args = args;
        _workingDir = workingDir;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            GitUICommands commands = new(_workingDir);

            if (_args.Length <= 1)
            {
                commands.StartBrowseDialog(owner: null);
            }
            else
            {
                // if we are here args.Length > 1

                // Avoid replacing the ExitCode eventually set while parsing arguments,
                // i.e. assume -1 and afterwards, only set it to 0 if no error is indicated.
                Environment.ExitCode = -1;
                if (commands.RunCommand(_args))
                {
                    Environment.ExitCode = 0;
                }
            }
        }

        base.OnFrameworkInitializationCompleted();
    }
}
