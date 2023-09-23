using Avalonia.Interactivity;
using GitCommands;
using GitCommands.Utils;
using GitUI.CommandsDialogs.BrowseDialog;
using GitUI.Infrastructure;
using GitUI.Shells;
using Microsoft;
using ResourceManager;

namespace GitUI.CommandsDialogs.Menus
{
    internal partial class ToolsToolStripMenuItem : ToolStripMenuItemExAvalonia
    {
        public event EventHandler<SettingsChangedEventArgs> SettingsChanged;

        public ToolsToolStripMenuItem()
        {
            InitializeComponent();

            gitBashToolStripMenuItem.Tag = new ShellProvider().GetShell(BashShell.ShellName);

            if (!EnvUtils.RunningOnWindows())
            {
                toolStripSeparator6.IsVisible = false;
                PuTTYToolStripMenuItem.IsVisible = false;
            }
        }

        public override void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
        {
            SetShortcutKey(gitBashToolStripMenuItem, hotkeys, (int)FormBrowse.Command.GitBash);
            SetShortcutKey(gitGUIToolStripMenuItem, hotkeys, (int)FormBrowse.Command.GitGui);
            SetShortcutKey(kGitToolStripMenuItem, hotkeys, (int)FormBrowse.Command.GitGitK);
            SetShortcutKey(settingsToolStripMenuItem, hotkeys, (int)FormBrowse.Command.OpenSettings);

            base.RefreshShortcutKeys(hotkeys);
        }

        public override void RefreshState(bool bareRepository)
        {
            gitGUIToolStripMenuItem.IsEnabled = !bareRepository;

            base.RefreshState(bareRepository);
        }

        private void GitcommandLogToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            FormGitCommandLog.ShowOrActivate(OwnerForm);
        }

        private void GitGuiToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            UICommands.Module.RunGui();
        }

        private void KGitToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            UICommands.Module.RunGitK();
        }

        private void StartAuthenticationAgentToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            PuttyHelpers.StartPageant(UICommands.Module.WorkingDir);
        }

        private void GenerateOrImportKeyToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            PuttyHelpers.StartPuttygen(UICommands.Module.WorkingDir);
        }

        private void OnShowSettingsClick(object sender, RoutedEventArgs e)
        {
            string translation = AppSettings.Translation;
            CommitInfoPosition commitInfoPosition = AppSettings.CommitInfoPosition;

            UICommands.StartSettingsDialog(OwnerForm);

            SettingsChanged?.Invoke(sender, new(translation, commitInfoPosition));
        }

        private void gitBashToolStripMenuItem_Click(object sender, RoutedEventArgs e)
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
