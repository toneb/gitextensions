using GitUIPluginInterfaces;

namespace ResourceManager;

public interface IGitModuleForm
{
    /// <summary>
    ///  Gets the currently assigned <see cref="IGitUICommands"/> instance.
    /// </summary>
    IGitUICommands UICommands { get; }
}
