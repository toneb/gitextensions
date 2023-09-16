#pragma warning disable VSTHRD108 // Assert thread affinity unconditionally

using System.Diagnostics;
using GitCommands;
using GitUI;

namespace ResourceManager
{
    internal sealed class GitExtensionsControlInitialiser
    {
        private static bool? _isDesignMode;
        private readonly ITranslate _translate;

        // Indicates whether the initialisation has been signalled as complete.
        private bool _initialiseCompleteCalled;

        public GitExtensionsControlInitialiser(GitExtensionsFormBase form)
        {
            if (IsDesignMode)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();
#if !AVALONIA
            form.Load += LoadHandler;
#else
            form.Loaded += LoadHandler;
#endif
            _translate = form;
        }

        public GitExtensionsControlInitialiser(GitExtensionsControl control)
        {
            if (IsDesignMode)
            {
                return;
            }

            ThreadHelper.ThrowIfNotOnUIThread();
#if !AVALONIA
            control.Load += LoadHandler;
#else
            control.Loaded += LoadHandler;
#endif
            _translate = control;
        }

        /// <summary>
        /// Indicates whether code is running as part of an IDE designer, such as the WinForms designer.
        /// </summary>
        public bool IsDesignMode
        {
            get
            {
                if (_isDesignMode is null)
                {
                    string processName = Process.GetCurrentProcess().ProcessName.ToLowerInvariant();
                    _isDesignMode = processName.Contains("devenv") || processName.Contains("designtoolsserver");
                }

                return _isDesignMode.Value;
            }
        }

        public void InitializeComplete()
        {
            if (IsDesignMode)
            {
                return;
            }

            if (_initialiseCompleteCalled)
            {
                throw new InvalidOperationException($"{nameof(InitializeComplete)} already called.");
            }

            _initialiseCompleteCalled = true;

#if !AVALONIA // TODO - avalonia
            ((Control)_translate).Font = AppSettings.Font;
#endif
            Translator.Translate(_translate, AppSettings.CurrentTranslation);
        }

        private void LoadHandler(object control, EventArgs e)
        {
            if (!_initialiseCompleteCalled)
            {
                throw new Exception($"{control.GetType().Name} must call {nameof(InitializeComplete)} in its constructor, ideally as the final statement.");
            }
        }
    }
}

#pragma warning restore VSTHRD108 // Assert thread affinity unconditionally
