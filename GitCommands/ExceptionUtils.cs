using System.Collections;
using System.Text;
using GitUI;

namespace GitCommands
{
    public static class ExceptionUtils
    {
        public static void ShowException(Exception e, bool canIgnore = true)
        {
            ShowException(e, string.Empty, canIgnore);
        }

        public static void ShowException(Exception e, string info, bool canIgnore = true)
        {
            ShowException(null, e, info, canIgnore);
        }

        public static void ShowException(UiWindow? owner, Exception e, string info, bool canIgnore)
        {
            if (!(canIgnore && IsIgnorable(e)))
            {
                UiApplication.ShowMessageBoxError(owner, string.Join(Environment.NewLine + Environment.NewLine, info, e.ToStringWithData()), "Error");
            }
        }

        public static bool IsIgnorable(Exception e)
        {
            return e is ThreadAbortException;
        }

        public static string ToStringWithData(this Exception e)
        {
            StringBuilder sb = new();
            sb.AppendLine(e.ToString());
            sb.AppendLine();
            foreach (DictionaryEntry entry in e.Data)
            {
                sb.AppendLine(entry.Key + " = " + entry.Value);
            }

            return sb.ToString();
        }
    }
}
