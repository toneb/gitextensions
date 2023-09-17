using GitCommands;
using GitCommands.Git;
using GitCommands.UserRepositoryHistory;
using GitUI.CommandsDialogs;
using Microsoft.VisualStudio.Threading;
#if AVALONIA
using Avalonia.Controls;
using ToolStripDropDownItem = Avalonia.Controls.MenuItem;
#endif

namespace GitUI
{
    internal class RepositoryHistoryUIService
    {
        private readonly IRepositoryCurrentBranchNameProvider _repositoryCurrentBranchNameProvider;
        private readonly IInvalidRepositoryRemover _invalidRepositoryRemover;

        public event EventHandler<GitModuleEventArgs> GitModuleChanged;

        internal RepositoryHistoryUIService(IRepositoryCurrentBranchNameProvider repositoryCurrentBranchNameProvider, IInvalidRepositoryRemover invalidRepositoryRemover)
        {
            _repositoryCurrentBranchNameProvider = repositoryCurrentBranchNameProvider;
            _invalidRepositoryRemover = invalidRepositoryRemover;
        }

        public RepositoryHistoryUIService()
            : this(new RepositoryCurrentBranchNameProvider(), new InvalidRepositoryRemover())
        {
        }

#if !AVALONIA
        private static Form? OwnerForm
            => Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
#endif

        private void AddRecentRepositories(ToolStripDropDownItem menuItemContainer, Repository repo, string? caption)
        {
#if !AVALONIA
            ToolStripMenuItem item = new(caption)
            {
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText
            };

            menuItemContainer.DropDownItems.Add(item);
#else
            TextBlock itemCaption = new() { Text = caption };
            TextBlock itemBranch = new();
            DockPanel.SetDock(itemBranch, Dock.Right);
            MenuItem item = new() { Header = new DockPanel { Children = { itemCaption, itemBranch } } };
            menuItemContainer.Items.Add(item);
#endif

            item.Click += (obj, args) =>
            {
                OpenRepo(repo.Path);
            };

            if (repo.Path != caption)
            {
#if !AVALONIA
                item.ToolTipText = repo.Path;
#else
                ToolTip.SetTip(item, repo.Path);
#endif
            }

            ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await TaskScheduler.Default;
                string branchName = _repositoryCurrentBranchNameProvider.GetCurrentBranchName(repo.Path);
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
#if !AVALONIA
                item.ShortcutKeyDisplayString = branchName;
#else
                itemBranch.Text = branchName;
#endif
            }).FileAndForget();
        }

        private void ChangeWorkingDir(string path)
        {
            GitModule module = new(path);
            if (module.IsValidGitWorkingDir())
            {
                GitModuleChanged?.Invoke(this, new GitModuleEventArgs(module));
                return;
            }

            _invalidRepositoryRemover.ShowDeleteInvalidRepositoryDialog(path);
        }

        private void OpenRepo(string repoPath)
        {
#if !AVALONIA // -- TODO: can we check for Ctrl key by registering to keyDown+keyUp events? On the window?
            if (Control.ModifierKeys != Keys.Control)
            {
                ChangeWorkingDir(repoPath);
                return;
            }
#endif

            GitUICommands.LaunchBrowse(repoPath);
        }

        public void PopulateFavouriteRepositoriesMenu(ToolStripDropDownItem container)
        {
            IList<Repository> repositoryHistory = ThreadHelper.JoinableTaskFactory.Run(RepositoryHistoryManager.Locals.LoadFavouriteHistoryAsync);
            if (repositoryHistory.Count < 1)
            {
                return;
            }

            PopulateFavouriteRepositoriesMenu(container, repositoryHistory);
        }

        private void PopulateFavouriteRepositoriesMenu(ToolStripDropDownItem container, in IList<Repository> repositoryHistory)
        {
#if !AVALONIA
            List<RecentRepoInfo> pinnedRepos = new();
            List<RecentRepoInfo> allRecentRepos = new();

            using (var graphics = OwnerForm.CreateGraphics())
            {
                RecentRepoSplitter splitter = new()
                {
                    MeasureFont = container.Font,
                    Graphics = graphics
                };

                splitter.SplitRecentRepos(repositoryHistory, pinnedRepos, allRecentRepos);
            }

            foreach (var repo in pinnedRepos.Union(allRecentRepos).GroupBy(k => k.Repo.Category).OrderBy(k => k.Key))
            {
                AddFavouriteRepositories(repo.Key, repo.ToList());
            }

            void AddFavouriteRepositories(string? category, IList<RecentRepoInfo> repos)
            {
                ToolStripMenuItem menuItemCategory;
                if (!container.DropDownItems.ContainsKey(category))
                {
                    menuItemCategory = new ToolStripMenuItem(category);
                    container.DropDownItems.Add(menuItemCategory);
                }
                else
                {
                    menuItemCategory = (ToolStripMenuItem)container.DropDownItems[category];
                }

                menuItemCategory.DropDown.SuspendLayout();
                foreach (var r in repos)
                {
                    AddRecentRepositories(menuItemCategory, r.Repo, r.Caption);
                }

                menuItemCategory.DropDown.ResumeLayout();
            }
#else
            throw new NotImplementedException("TODO");
#endif
        }

        public void PopulateRecentRepositoriesMenu(ToolStripDropDownItem container)
        {
#if !AVALONIA
            List<RecentRepoInfo> pinnedRepos = new();
            List<RecentRepoInfo> allRecentRepos = new();

            IList<Repository> repositoryHistory = ThreadHelper.JoinableTaskFactory.Run(RepositoryHistoryManager.Locals.LoadRecentHistoryAsync);
            if (repositoryHistory.Count < 1)
            {
                return;
            }

            using (var graphics = OwnerForm.CreateGraphics())
            {
                RecentRepoSplitter splitter = new()
                {
                    MeasureFont = container.Font,
                    Graphics = graphics
                };

                splitter.SplitRecentRepos(repositoryHistory, pinnedRepos, allRecentRepos);
            }

            foreach (var repo in pinnedRepos)
            {
                AddRecentRepositories(container, repo.Repo, repo.Caption);
            }

            if (allRecentRepos.Count > 0)
            {
                if (pinnedRepos.Count > 0)
                {
                    container.DropDownItems.Add(new ToolStripSeparator());
                }

                foreach (var repo in allRecentRepos)
                {
                    AddRecentRepositories(container, repo.Repo, repo.Caption);
                }
            }
#else
            throw new NotImplementedException("TODO");
#endif
        }

        internal TestAccessor GetTestAccessor()
            => new(this);

        internal readonly struct TestAccessor
        {
            private readonly RepositoryHistoryUIService _service;

            public TestAccessor(RepositoryHistoryUIService service)
            {
                _service = service;
            }

            internal void AddRecentRepositories(ToolStripDropDownItem menuItemContainer, Repository repo, string? caption)
                => _service.AddRecentRepositories(menuItemContainer, repo, caption);

            internal void PopulateFavouriteRepositoriesMenu(ToolStripDropDownItem container, in IList<Repository> repositoryHistory)
                => _service.PopulateFavouriteRepositoriesMenu(container, repositoryHistory);
}
    }
}
