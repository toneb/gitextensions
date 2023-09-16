using Avalonia;
using Avalonia.ReactiveUI;
using GitCommands;

namespace GitUI.Avalonia;

internal static class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args) => BuildAvaloniaApp(args)
        .StartWithClassicDesktopLifetime(args);

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp(string[] args)
        => AppBuilder.Configure(() => new App(args, GetWorkingDir(args)))
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace()
            .UseReactiveUI();

    private static string? GetWorkingDir(string[] args)
    {
        string? workingDir = null;

        if (args.Length >= 3)
        {
            // there is bug in .net
            // while parsing command line arguments, it unescapes " incorrectly
            // https://github.com/gitextensions/gitextensions/issues/3489
            string? dirArg = args[2].TrimEnd('"');
            if (!string.IsNullOrWhiteSpace(dirArg))
            {
                if (!Directory.Exists(dirArg))
                {
                    dirArg = Path.GetDirectoryName(dirArg);
                }

                workingDir = GitModule.TryFindGitWorkingDir(dirArg);

                if (Directory.Exists(workingDir))
                {
                    workingDir = Path.GetFullPath(workingDir);
                }

                // Do not add this working directory to the recent repositories. It is a nice feature, but it
                // also increases the startup time
                ////if (Module.ValidWorkingDir())
                ////   Repositories.RepositoryHistory.AddMostRecentRepository(Module.WorkingDir);
            }
        }

        if (args.Length <= 1 && workingDir is null && AppSettings.StartWithRecentWorkingDir)
        {
            if (GitModule.IsValidGitWorkingDir(AppSettings.RecentWorkingDir))
            {
                workingDir = AppSettings.RecentWorkingDir;
            }
        }

        if (args.Length > 1 && workingDir is null)
        {
            // If no working dir is yet found, try to find one relative to the current working directory.
            // This allows the `fileeditor` command to discover repository configuration which is
            // required for core.commentChar support.
            workingDir = GitModule.TryFindGitWorkingDir(Environment.CurrentDirectory);
        }

        return workingDir;
    }
}
