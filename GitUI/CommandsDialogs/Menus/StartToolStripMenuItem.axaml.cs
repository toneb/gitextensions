using Avalonia.Controls;
using Avalonia.Interactivity;
using GitCommands;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using GitUI.CommandsDialogs.BrowseDialog;
using ResourceManager;

namespace GitUI.CommandsDialogs.Menus
{
    internal partial class StartToolStripMenuItem : ToolStripMenuItemExAvalonia
    {
        private readonly RepositoryHistoryUIService _repositoryHistoryUIService = new();

        public event EventHandler<GitModuleEventArgs> GitModuleChanged;
        public event EventHandler RecentRepositoriesCleared;

        public StartToolStripMenuItem()
        {
            InitializeComponent();

            AttachedToVisualTree += (_, _) => _repositoryHistoryUIService.GitModuleChanged += repositoryHistoryUIService_GitModuleChanged;
            DetachedFromVisualTree += (_, _) => _repositoryHistoryUIService.GitModuleChanged -= repositoryHistoryUIService_GitModuleChanged;
        }

        internal MenuItem OpenRepositoryMenuItem => openToolStripMenuItem;
        internal MenuItem FavouriteRepositoriesMenuItem => tsmiFavouriteRepositories;

        public override void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
        {
            SetShortcutKey(openToolStripMenuItem, hotkeys, (int)FormBrowse.Command.OpenRepo);

            base.RefreshShortcutKeys(hotkeys);
        }

        private void CloneToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            UICommands.StartCloneDialog(OwnerForm, string.Empty, false, GitModuleChanged);
        }

        private void ExitToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            OwnerForm?.Close();
        }

        private void InitNewRepositoryToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            UICommands.StartInitializeDialog(OwnerForm, gitModuleChanged: GitModuleChanged);
        }

        private void OpenToolStripMenuItemClick(object sender, RoutedEventArgs e)
        {
            GitModule? module = FormOpenDirectory.OpenModule(OwnerForm, UICommands.Module);
            if (module is not null)
            {
                GitModuleChanged?.Invoke(OwnerForm, new GitModuleEventArgs(module));
            }
        }

        private void repositoryHistoryUIService_GitModuleChanged(object? sender, GitModuleEventArgs e)
        {
            GitModuleChanged?.Invoke(this, e);
        }

        private void tsmiFavouriteRepositories_DropDownOpening(object sender, RoutedEventArgs e)
        {
#if false
            tsmiFavouriteRepositories.DropDown.SuspendLayout();
            tsmiFavouriteRepositories.DropDownItems.Clear();
            _repositoryHistoryUIService.PopulateFavouriteRepositoriesMenu(tsmiFavouriteRepositories);
            tsmiFavouriteRepositories.DropDown.ResumeLayout();
#endif

            throw new NotImplementedException("TODO - avalonia");
        }

        private void tsmiRecentRepositories_DropDownOpening(object sender, RoutedEventArgs e)
        {
#if false
            tsmiRecentRepositories.DropDown.SuspendLayout();
            tsmiRecentRepositories.DropDownItems.Clear();
            _repositoryHistoryUIService.PopulateRecentRepositoriesMenu(tsmiRecentRepositories);
            if (tsmiRecentRepositories.DropDownItems.Count < 1)
            {
                return;
            }

            tsmiRecentRepositories.DropDownItems.Add(clearRecentRepositoriesListToolStripMenuItem);
            ////TranslateItem(tsmiRecentRepositoriesClear.Name, tsmiRecentRepositoriesClear);
            tsmiRecentRepositories.DropDownItems.Add(tsmiRecentRepositoriesClear);
            tsmiRecentRepositories.DropDown.ResumeLayout();
#endif

            throw new NotImplementedException("TODO - avalonia");
        }

        private void tsmiRecentRepositoriesClear_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            ThreadHelper.JoinableTaskFactory.Run(() => RepositoryHistoryManager.Locals.SaveRecentHistoryAsync(Array.Empty<Repository>()));
            RecentRepositoriesCleared?.Invoke(sender, e);
        }
    }
}
