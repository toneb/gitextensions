namespace GitUI;

/// <summary>
/// The UI control wrapper.
/// </summary>
public class UiControl
{
    public object NativeControl { get; }

    public UiControl(object nativeControl)
        => NativeControl = nativeControl;
}
