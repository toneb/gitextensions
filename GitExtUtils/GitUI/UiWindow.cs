namespace GitUI;

/// <summary>
/// The UI window wrapper.
/// </summary>
public class UiWindow
{
    public object NativeWindow { get; }

    public UiWindow(object nativeWindow)
        => NativeWindow = nativeWindow;
}
