namespace GitUI;

/// <summary>
/// The forms application wrapper.
/// </summary>
public class FormsApplication : IUiApplication
{
    public void OnThreadException(Exception t)
    {
        Application.OnThreadException(t);
    }

    public void ShowMessageBoxError(UiWindow? owner, string text, string caption)
    {
        MessageBox.Show(owner.GetFormsWindow(), text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }
}
