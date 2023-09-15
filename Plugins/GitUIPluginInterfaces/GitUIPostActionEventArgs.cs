using GitUI;

namespace GitUIPluginInterfaces
{
    public class GitUIPostActionEventArgs : GitUIEventArgs
    {
        public bool ActionDone { get; }

        public GitUIPostActionEventArgs(UiWindow? ownerForm, IGitUICommands gitUICommands, bool actionDone)
            : base(ownerForm, gitUICommands)
        {
            ActionDone = actionDone;
        }
    }
}
