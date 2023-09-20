using GitCommands;
using GitUI.Hotkey;
using ResourceManager;

namespace GitUI.CommandsDialogs.Menus
{
    internal abstract class ToolStripMenuItemEx : ToolStripMenuItem, ITranslate
    {
#if AVALONIA
        // Appear as MenuItem in visual tree
        protected override Type StyleKeyOverride => typeof(MenuItem);
#endif

        private Func<GitUICommands>? _getUICommands;

        /// <summary>
        ///  Gets the current instance of the UI commands.
        /// </summary>
        protected GitUICommands UICommands
            => (_getUICommands ?? throw new InvalidOperationException("The button is not initialized"))();

        /// <summary>
        ///  Gets the current instance of the git module.
        /// </summary>
        protected GitModule Module
            => UICommands.Module;

        /// <summary>
        ///  Gets the form that is displaying the menu item.
        /// </summary>
#if !AVALONIA
        protected static Form? OwnerForm
            => Form.ActiveForm ?? (Application.OpenForms.Count > 0 ? Application.OpenForms[0] : null);
#else
        protected Form? OwnerForm
            => VisualRoot as Form ?? ((Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime)Avalonia.Application.Current?.ApplicationLifetime)?.Windows[0];
#endif

        /// <summary>
        ///  Initializes the menu item.
        /// </summary>
        /// <param name="getUICommands">The method that returns the current instance of UI commands.</param>
        public void Initialize(Func<GitUICommands> getUICommands)
        {
            Translator.Translate(this, AppSettings.CurrentTranslation);

            _getUICommands = getUICommands;
        }

        /// <summary>
        ///  Returns the string representation of <paramref name="commandCode"/> if it exists in <paramref name="hotkeys"/> collection.
        /// </summary>
        /// <param name="hotkeys">The collection of configured shortcut keys.</param>
        /// <param name="commandCode">The required shortcut identifier.</param>
        /// <returns>The string representation of the shortcut, if exists; otherwise, the string representation of Keys.None.</returns>
        protected static string GetShortcutKey(IEnumerable<HotkeyCommand>? hotkeys, int commandCode)
        {
#if !AVALONIA
            return (hotkeys?.FirstOrDefault(h => h.CommandCode == commandCode)?.KeyData ?? Keys.None).ToShortcutKeyDisplayString();
#else
            return hotkeys?.FirstOrDefault(h => h.CommandCode == commandCode)?.KeyData.ToShortcutKeyDisplayString() ?? "";
#endif
        }

#if AVALONIA
        /// <summary>
        ///  Sets the shortcut key to given <paramref name="menuItem" /> from provided <paramref name="commandCode"/> if it exists in <paramref name="hotkeys"/> collection.
        /// </summary>
        /// <param name="menuItem">The menu item to assign shortcut key to.</param>
        /// <param name="hotkeys">The collection of configured shortcut keys.</param>
        /// <param name="commandCode">The required shortcut identifier.</param>
        protected static void SetShortcutKey(MenuItem menuItem, IEnumerable<HotkeyCommand>? hotkeys, int commandCode)
        {
            Avalonia.Input.KeyGesture inputGesture = Keys.Parse(GetShortcutKey(hotkeys, (int)FormBrowse.Command.OpenRepo));
            menuItem.InputGesture = inputGesture;
            menuItem.HotKey = inputGesture;
        }
#endif

        /// <summary>
        ///  Allows reloading/reassigning the configured shortcut key.
        /// </summary>
        //// <param name="hotkeys"></param>
        public virtual void RefreshShortcutKeys(IEnumerable<HotkeyCommand>? hotkeys)
        {
        }

        /// <summary>
        ///  Allows refreshing the state of the menu item depending on the state of the loaded git repository.
        /// </summary>
        /// <param name="bareRepository"><see lang="true"/> if the current git repository is bare; otherwise, <see lang="false"/>.</param>
        public virtual void RefreshState(bool bareRepository)
        {
        }

        void ITranslate.AddTranslationItems(ITranslation translation)
        {
            TranslationUtils.AddTranslationItemsFromFields("FormBrowse", this, translation);
        }

        void ITranslate.TranslateItems(ITranslation translation)
        {
            TranslationUtils.TranslateItemsFromFields("FormBrowse", this, translation);
        }
    }
}
