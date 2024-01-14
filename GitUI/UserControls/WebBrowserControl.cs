#if WINDOWS // TODO - mono
namespace GitUI.UserControls
{
    internal class WebBrowserControl : WebBrowser
    {
        public WebBrowserControl()
        {
            ScriptErrorsSuppressed = true;
        }
    }
}
#endif
