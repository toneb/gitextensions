namespace ResourceManager
{
    /// <summary>Provides translation capabilities.</summary>
    public interface ITranslate
#if !AVALONIA
        : IDisposable
#endif
    {
        void AddTranslationItems(ITranslation translation);

        /// <summary>Translates all (translatable) items.</summary>
        void TranslateItems(ITranslation translation);
    }
}
