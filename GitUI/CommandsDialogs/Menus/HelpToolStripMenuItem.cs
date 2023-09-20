using GitCommands;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs.BrowseDialog;
#if AVALONIA
using EventArgs = Avalonia.Interactivity.RoutedEventArgs;
#endif

namespace GitUI.CommandsDialogs.Menus
{
    internal partial class HelpToolStripMenuItem : ToolStripMenuItemEx
    {
        public HelpToolStripMenuItem()
        {
            InitializeComponent();

#if !AVALONIA // NOTE: not needed in Avalonia, since we're showing checkbox which has corresponding dark theme
            translateToolStripMenuItem.AdaptImageLightness();
#endif
        }

        private void this_DropDownOpening(object sender, EventArgs e)
        {
#if !AVALONIA
            tsmiTelemetryEnabled.Checked = AppSettings.TelemetryEnabled ?? false;
#else
            tsmiTelemetryEnabledCheckBox.IsChecked = AppSettings.TelemetryEnabled ?? false;
#endif
        }

        private void AboutToolStripMenuItemClick(object sender, EventArgs e)
        {
#if !AVALONIA
            using FormAbout frm = new();
            frm.ShowDialog(OwnerForm);
#else
            throw new NotImplementedException("TODO - avalonia");
#endif
        }

        private void ChangelogToolStripMenuItemClick(object sender, EventArgs e)
        {
#if !AVALONIA
            using FormChangeLog frm = new();
            frm.ShowDialog(OwnerForm);
#else
            throw new NotImplementedException("TODO - avalonia");
#endif
        }

        private void checkForUpdatesToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if !AVALONIA
            FormUpdates updateForm = new(AppSettings.AppVersion);
            updateForm.SearchForUpdatesAndShow(Owner, true);
#else
            throw new NotImplementedException("TODO - avalonia");
#endif
        }

        private void DonateToolStripMenuItemClick(object sender, EventArgs e)
        {
#if !AVALONIA
            using FormDonate frm = new();
            frm.ShowDialog(OwnerForm);
#else
            throw new NotImplementedException("TODO - avalonia");
#endif
        }

        private void reportAnIssueToolStripMenuItem_Click(object sender, EventArgs e)
        {
#if !AVALONIA
            UserEnvironmentInformation.CopyInformation();
            OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/gitextensions/gitextensions/issues");
#else
            throw new NotImplementedException("TODO - avalonia");
#endif
        }

        private void TranslateToolStripMenuItemClick(object sender, EventArgs e)
        {
            OsShellUtil.OpenUrlInDefaultBrowser(@"https://github.com/gitextensions/gitextensions/wiki/Translations");
        }

        private void TsmiTelemetryEnabled_Click(object sender, EventArgs e)
        {
            UICommands.StartGeneralSettingsDialog(OwnerForm);
        }

        private void UserManualToolStripMenuItemClick(object sender, EventArgs e)
        {
            // Point to the default documentation, will work also if the old doc version is removed
            OsShellUtil.OpenUrlInDefaultBrowser(AppSettings.DocumentationBaseUrl);
        }
    }
}
