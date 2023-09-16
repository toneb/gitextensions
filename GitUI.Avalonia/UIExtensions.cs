using System.Diagnostics.CodeAnalysis;
using GitUI;

namespace Avalonia.Controls
{
    public static class AvaloniaUIExtensions
    {
        [return: NotNullIfNotNull("window")]
        public static UiWindow? GetUiWindow(this Window? window)
            => window != null ? new UiWindow(window) : null;

        [return: NotNullIfNotNull("window")]
        public static Window? GetAvaloniaWindow(this UiWindow? window)
            => (Window?)window?.NativeWindow;

        // [return: NotNullIfNotNull("control")]
        // public static UiControl? GetUiControl(this Control? control)
        //     => control != null ? new UiControl(control) : null;
        //
        // [return: NotNullIfNotNull("control")]
        // public static Control? GetFormsControl(this UiControl? control)
        //     => (Control)control?.NativeControl;
        //
        // public static DialogResult ShowDialog(this Form form, UiWindow? owner)
        //     => form.ShowDialog(owner.GetFormsWindow());
        //
        // public static DialogResult ShowDialog(this CommonDialog dialog, UiWindow? owner)
        //     => dialog.ShowDialog(owner.GetFormsWindow());
    }
}
