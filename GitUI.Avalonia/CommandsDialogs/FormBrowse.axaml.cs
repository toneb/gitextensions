namespace GitUI.CommandsDialogs;

public partial class FormBrowse : GitModuleForm // TODO - avalonia - , IBrowseRepo
{
    public GitUICommands UICommands { get; }

    /// <summary>
    /// Open Browse - main GUI including dashboard.
    /// </summary>
    /// <param name="commands">The commands in the current form.</param>
    /// <param name="args">The start up arguments.</param>
    public FormBrowse(GitUICommands commands, FormBrowseArguments args)
    {
        UICommands = commands;

        DataContext = new FormBrowseViewModel();

        InitializeComponent();
    }
}
