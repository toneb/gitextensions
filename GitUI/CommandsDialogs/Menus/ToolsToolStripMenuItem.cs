using GitCommands;
using GitCommands.Utils;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.Infrastructure;
using GitUI.Shells;
using Microsoft;
using ResourceManager;
#if AVALONIA
using EventArgs = Avalonia.Interactivity.RoutedEventArgs;
#endif

namespace GitUI.CommandsDialogs.Menus
{
    internal partial class ToolsToolStripMenuItem : ToolStripMenuItemEx
    {
        public event EventHandler<SettingsChangedEventArgs> SettingsChanged;

        public ToolsToolStripMenuItem()
        {
            InitializeComponent();

            gitBashToolStripMenuItem.Tag = new ShellProvider().GetShell(BashShell.ShellName);

            if (!EnvUtils.RunningOnWindows())
            {
#if !AVALONIA
                toolStripSeparator6.Visible = false;
                PuTTYToolStripMenuItem.Visible = false;
#else
                toolStripSeparator6.IsVisible = false;
                PuTTYToolStripMenuItem.IsVisible = false;
#endif
            }
        }

        public override void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
        {
#if !AVALONIA
            gitBashToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKey(hotkeys, (int)FormBrowse.Command.GitBash);
            gitGUIToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKey(hotkeys, (int)FormBrowse.Command.GitGui);
            kGitToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKey(hotkeys, (int)FormBrowse.Command.GitGitK);
            settingsToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKey(hotkeys, (int)FormBrowse.Command.OpenSettings);
#else
            SetShortcutKey(gitBashToolStripMenuItem, hotkeys, (int)FormBrowse.Command.GitBash);
            SetShortcutKey(gitGUIToolStripMenuItem, hotkeys, (int)FormBrowse.Command.GitGui);
            SetShortcutKey(kGitToolStripMenuItem, hotkeys, (int)FormBrowse.Command.GitGitK);
            SetShortcutKey(settingsToolStripMenuItem, hotkeys, (int)FormBrowse.Command.OpenSettings);
#endif

            base.RefreshShortcutKeys(hotkeys);
        }

        public override void RefreshState(bool bareRepository)
        {
#if !AVALONIA
            gitGUIToolStripMenuItem.Enabled = !bareRepository;
#else
            gitGUIToolStripMenuItem.IsEnabled = !bareRepository;
#endif

            base.RefreshState(bareRepository);
        }

        private void GitcommandLogToolStripMenuItemClick(object sender, EventArgs e)
        {
#if !AVALONIA
            FormGitCommandLog.ShowOrActivate(OwnerForm);
#else
            throw new NotImplementedException("TODO - avalonia");
#endif
        }

        private void GitGuiToolStripMenuItemClick(object sender, EventArgs e)
        {
            UICommands.Module.RunGui();
        }

        private void KGitToolStripMenuItemClick(object sender, EventArgs e)
        {
            UICommands.Module.RunGitK();
        }

        private void StartAuthenticationAgentToolStripMenuItemClick(object sender, EventArgs e)
        {
            PuttyHelpers.StartPageant(UICommands.Module.WorkingDir);
        }

        private void GenerateOrImportKeyToolStripMenuItemClick(object sender, EventArgs e)
        {
            PuttyHelpers.StartPuttygen(UICommands.Module.WorkingDir);
        }

        private void OnShowSettingsClick(object sender, EventArgs e)
        {
            string translation = AppSettings.Translation;
            CommitInfoPosition commitInfoPosition = AppSettings.CommitInfoPosition;

            UICommands.StartSettingsDialog(OwnerForm);

            SettingsChanged?.Invoke(sender, new(translation, commitInfoPosition));
        }

        private void gitBashToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (gitBashToolStripMenuItem.Tag is not IShellDescriptor shell)
            {
                return;
            }

            try
            {
                Validates.NotNull(shell.ExecutablePath);

                Executable executable = new(shell.ExecutablePath, UICommands.Module.WorkingDir);
                executable.Start(createWindow: true, throwOnErrorExit: false); // throwOnErrorExit would redirect the output
            }
            catch (Exception exception)
            {
                MessageBoxes.FailedToRunShell(OwnerForm, shell.Name, exception);
            }
        }
    }
}
