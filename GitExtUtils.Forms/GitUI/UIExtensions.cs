using System.Diagnostics.CodeAnalysis;
using GitUI;

namespace GitUI
{
    public static class UIExtensions
    {
        public static bool? GetNullableChecked(this CheckBox chx)
        {
            if (chx.CheckState == CheckState.Indeterminate)
            {
                return null;
            }
            else
            {
                return chx.Checked;
            }
        }

        public static void SetNullableChecked(this CheckBox chx, bool? @checked)
        {
            if (@checked.HasValue)
            {
                chx.CheckState = @checked.Value ? CheckState.Checked : CheckState.Unchecked;
            }
            else
            {
                chx.CheckState = CheckState.Indeterminate;
            }
        }

        public static bool IsFixedWidth(this Font ft, Graphics g)
        {
            char[] charSizes = { 'i', 'a', 'Z', '%', '#', 'a', 'B', 'l', 'm', ',', '.' };
            float charWidth = g.MeasureString("I", ft).Width;

            bool fixedWidth = true;

            foreach (char c in charSizes)
            {
                if (Math.Abs(g.MeasureString(c.ToString(), ft).Width - charWidth) > float.Epsilon)
                {
                    fixedWidth = false;
                }
            }

            return fixedWidth;
        }
    }
}

namespace System.Windows.Forms
{
    public static class FormsUIExtensions
    {
        [return: NotNullIfNotNull("window")]
        public static UiWindow? GetUiWindow(this IWin32Window? window)
            => window != null ? new UiWindow(window) : null;

        [return: NotNullIfNotNull("window")]
        public static IWin32Window? GetFormsWindow(this UiWindow? window)
            => (IWin32Window)window?.NativeWindow;

        [return: NotNullIfNotNull("control")]
        public static UiControl? GetUiControl(this Control? control)
            => control != null ? new UiControl(control) : null;

        [return: NotNullIfNotNull("control")]
        public static Control? GetFormsControl(this UiControl? control)
            => (Control)control?.NativeControl;

        public static DialogResult ShowDialog(this Form form, UiWindow? owner)
            => form.ShowDialog(owner.GetFormsWindow());

        public static DialogResult ShowDialog(this CommonDialog dialog, UiWindow? owner)
            => dialog.ShowDialog(owner.GetFormsWindow());
    }
}
