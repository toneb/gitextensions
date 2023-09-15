namespace GitUI;

/// <summary>
/// Abstracts the UI application functionalities (WinForms, Avalonia).
/// </summary>
public interface IUiApplication
{
    void OnThreadException(Exception t);

    void ShowMessageBoxError(UiWindow? owner, string text, string caption);
}

/// <summary>
/// Abstracts the UI application (WinForms, Avalonia).
/// </summary>
public static class UiApplication
{
    public static IUiApplication Instance { get; set; }

    public static void OnThreadException(Exception t)
        => Instance.OnThreadException(t);

    public static void ShowMessageBoxError(UiWindow? owner, string text, string caption)
        => Instance.ShowMessageBoxError(owner, text, caption);
}
