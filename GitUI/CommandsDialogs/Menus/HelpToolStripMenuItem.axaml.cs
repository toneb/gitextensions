using Avalonia.Interactivity;
using GitCommands;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs.BrowseDialog;

namespace GitUI.CommandsDialogs.Menus
{
    internal partial class HelpToolStripMenuItem : ToolStripMenuItemExAvalonia
    {
        public HelpToolStripMenuItem()
        {
            InitializeComponent();

            // TODO - remove, left here to include as PR comment - not needed in Avalonia, since we're showing checkbox which has corresponding dark theme
            // translateToolStripMenuItem.AdaptImageLightness();
        }

        private void this_DropDownOpening(object sender, RoutedEventArgs e)
        {
            tsmiTelemetryEnabledCheckBox.IsChecked = AppSettings.TelemetryEnabled ?? false;
        }

        private void AboutToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            using FormAbout frm = new();
            frm.ShowDialog(OwnerForm);
        }

        private void ChangelogToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            using FormChangeLog frm = new();
            frm.ShowDialog(OwnerForm);
        }

        private void checkForUpdatesToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            FormUpdates updateForm = new(AppSettings.AppVersion);
            updateForm.SearchForUpdatesAndShow(OwnerForm, true);
        }

        private void DonateToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            using FormDonate frm = new();
            frm.ShowDialog(OwnerForm);
        }

        private void reportAnIssueToolStripMenuItem_Click(object sender, RoutedEventArgs e)
        {
            UserEnvironmentInformation.CopyInformation();
            OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/gitextensions/gitextensions/issues");
        }

        private void TranslateToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/gitextensions/gitextensions/wiki/Translations");
        }

        private void TsmiTelemetryEnabled_Click(object sender, RoutedEventArgs e)
        {
            UICommands.StartGeneralSettingsDialog(OwnerForm);
        }

        private void UserManualToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            // Point to the default documentation, will work also if the old doc version is removed
            OsShellUtil.OpenUrlInDefaultBrowser(AppSettings.DocumentationBaseUrl);
        }
    }
}
