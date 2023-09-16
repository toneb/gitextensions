#if AVALONIA
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Controls;

namespace AvaloniaMonkeyPatch;

public static class Application
{
    public static void OnThreadException(Exception ex)
    {
        throw new NotImplementedException("TODO - avalonia");
    }

    public static string ExecutablePath => Path.ChangeExtension(Assembly.GetEntryAssembly().Location, "exe");

    public static string ProductVersion => Assembly.GetCallingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;

    public static string UserAppDataPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "GitExtensions", "GitExtensions");

    public static string ProductName => "Git Extensions";
}

public static class MessageBox
{
    public static DialogResult Show(string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        throw new NotImplementedException("TODO - avalonia");
    }

    public static DialogResult Show(Window? owner, string message, string caption, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        throw new NotImplementedException("TODO - avalonia");
    }
}

public static class SystemDrawingExtensions
{
    [return: NotNullIfNotNull("bitmap")]
    public static Avalonia.Media.Imaging.Bitmap? ToAvaloniaBitmap(this Bitmap? bitmap)
    {
        if (bitmap == null)
        {
            return null;
        }

        System.Drawing.Imaging.BitmapData bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), System.Drawing.Imaging.ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        Avalonia.Media.Imaging.Bitmap bitmap1 = new(Avalonia.Platform.PixelFormat.Bgra8888, Avalonia.Platform.AlphaFormat.Unpremul, bitmapData.Scan0, new Avalonia.PixelSize(bitmapData.Width, bitmapData.Height), new Avalonia.Vector(96, 96), bitmapData.Stride);
        bitmap.UnlockBits(bitmapData);
        return bitmap1;
    }
}

public enum MessageBoxButtons
{
    OK,
    OKCancel,
    YesNo,
    YesNoCancel,
}

public enum MessageBoxIcon
{
    None,
    Error,
    Warning,
    Information,
    Question
}

public enum DialogResult
{
    OK,
    Cancel,
    Yes,
    No,
}
#endif
