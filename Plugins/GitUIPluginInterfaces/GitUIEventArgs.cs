using System.ComponentModel;
using GitUI;

namespace GitUIPluginInterfaces
{
    public class GitUIEventArgs : CancelEventArgs
    {
        private readonly IFilteredGitRefsProvider _getRefs;

        public GitUIEventArgs(UiWindow? ownerForm, IGitUICommands gitUICommands, Lazy<IReadOnlyList<IGitRef>> getRefs = null)
            : base(cancel: false)
        {
            OwnerForm = ownerForm;
            GitUICommands = gitUICommands;
            if (getRefs is null)
            {
                _getRefs = new FilteredGitRefsProvider(GitModule);
            }
            else
            {
                _getRefs = new FilteredGitRefsProvider(getRefs);
            }
        }

        public IGitUICommands GitUICommands { get; }

        public UiWindow? OwnerForm { get; }

        public IGitModule GitModule => GitUICommands.GitModule;

        public IReadOnlyList<IGitRef> GetRefs(RefsFilter filter) => _getRefs.GetRefs(filter);
    }
}
