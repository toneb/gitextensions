using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.RegularExpressions;
using GitCommands;
using GitCommands.Patches;
using GitCommands.Settings;
using GitCommands.Utils;
using GitExtUtils;
using GitExtUtils.GitUI;
using GitExtUtils.GitUI.Theming;
using GitUI.CommandsDialogs;
using GitUI.CommandsDialogs.SettingsDialog.Pages;
using GitUI.Hotkey;
using GitUI.Properties;
using GitUI.UserControls;
using GitUIPluginInterfaces;
using Microsoft;
using ResourceManager;

namespace GitUI.Editor
{
    [DefaultEvent("SelectedLineChanged")]
    public partial class FileViewer : GitModuleControl
    {
        /// <summary>
        /// Raised when the Escape key is pressed (and only when no selection exists, as the default behaviour of escape is to clear the selection).
        /// </summary>
        public event Action? EscapePressed;

        /// <summary>
        /// The type of information currently shown in the file viewer.
        /// </summary>
        private enum ViewMode
        {
            // Plain text
            Text,

            // Diff or patch
            Diff,

            // Diffs that will not be affected by diff arguments like white space etc (limited options)
            FixedDiff,

            // range-diff output
            RangeDiff,

            // Image viewer
            Image
        }

        private readonly TranslationString _largeFileSizeWarning = new("This file is {0:N1} MB. Showing large files can be slow. Click to show anyway.");
        private readonly TranslationString _cannotViewImage = new("Cannot view image {0}");

        public event EventHandler<SelectedLineEventArgs>? SelectedLineChanged;
        public event EventHandler? HScrollPositionChanged;
        public event EventHandler? VScrollPositionChanged;
        public event EventHandler? BottomScrollReached;
        public event EventHandler? TopScrollReached;
        public event EventHandler? RequestDiffView;
        public new event EventHandler? TextChanged;
        public event EventHandler? TextLoaded;
        public event CancelEventHandler? ContextMenuOpening;
        public event EventHandler<EventArgs>? ExtraDiffArgumentsChanged;
        public event EventHandler<EventArgs>? PatchApplied;

        private readonly AsyncLoader _async;
        private readonly IFullPathResolver _fullPathResolver;
        private ViewMode _viewMode;
        private Encoding? _encoding;
        private Func<Task>? _deferShowFunc;
        private readonly ContinuousScrollEventManager _continuousScrollEventManager;
        private FileStatusItem? _viewItem;
#if WINDOWS // TODO - mono
        private readonly TaskDialogPage _NO_TRANSLATE_resetSelectedLinesConfirmationDialog;
#endif

        [GeneratedRegex("warning: .*has type .* expected .*")]
        private static partial Regex FileModeWarningRegex();

        public FileViewer()
        {
            TreatAllFilesAsText = false;
            ShowEntireFile = false;
            NumberOfContextLines = AppSettings.NumberOfContextLines;
            InitializeComponent();
            InitializeComplete();

            UICommandsSourceSet += OnUICommandsSourceSet;

            internalFileViewer.MouseEnter += (_, e) => OnMouseEnter(e);
            internalFileViewer.MouseLeave += (_, e) => OnMouseLeave(e);
            internalFileViewer.MouseMove += (_, e) => OnMouseMove(e);
            internalFileViewer.KeyUp += (_, e) => OnKeyUp(e);
            internalFileViewer.EscapePressed += () => EscapePressed?.Invoke();

            _continuousScrollEventManager = new ContinuousScrollEventManager();
            _continuousScrollEventManager.BottomScrollReached += _continuousScrollEventManager_BottomScrollReached;
            _continuousScrollEventManager.TopScrollReached += _continuousScrollEventManager_TopScrollReached;

            PictureBox.MouseWheel += PictureBox_MouseWheel;
            internalFileViewer.SetContinuousScrollManager(_continuousScrollEventManager);

            TextLoaded += (sender, args) => AllowLinePatching = SupportLinePatching;
            _async = new AsyncLoader();
            _async.LoadingError +=
                (_, e) =>
                {
                    if (!IsDisposed)
                    {
                        ResetView(ViewMode.Text, null);
                        internalFileViewer.SetText("Unsupported file: \n\n" + e.Exception.ToString(), openWithDifftool: null /* not applicable */);
                        TextLoaded?.Invoke(this, null);
                    }
                };

            IgnoreWhitespace = AppSettings.IgnoreWhitespaceKind.GetValue(reload: !AppSettings.RememberIgnoreWhiteSpacePreference);
            OnIgnoreWhitespaceChanged();

            ignoreWhitespaceAtEol.Image = Images.WhitespaceIgnoreEol.AdaptLightness();
            ignoreWhitespaceAtEolToolStripMenuItem.Image = ignoreWhitespaceAtEol.Image;

            ignoreWhiteSpaces.Image = Images.WhitespaceIgnore.AdaptLightness();
            ignoreWhitespaceChangesToolStripMenuItem.Image = ignoreWhiteSpaces.Image;

            ignoreAllWhitespaces.Image = Images.WhitespaceIgnoreAll.AdaptLightness();
            ignoreAllWhitespaceChangesToolStripMenuItem.Image = ignoreAllWhitespaces.Image;

            ShowEntireFile = AppSettings.ShowEntireFile.GetValue(reload: !AppSettings.RememberShowEntireFilePreference);
            showEntireFileButton.Checked = ShowEntireFile;
            showEntireFileToolStripMenuItem.Checked = ShowEntireFile;
            SetStateOfContextLinesButtons();

            automaticContinuousScrollToolStripMenuItem.Image = Images.UiScrollBar.AdaptLightness();
            automaticContinuousScrollToolStripMenuItem.Checked = AppSettings.AutomaticContinuousScroll;

            showNonPrintChars.Image = Images.ShowWhitespace.AdaptLightness();
            showNonprintableCharactersToolStripMenuItem.Image = showNonPrintChars.Image;
            bool showNonPrintingChars = AppSettings.ShowNonPrintingChars.GetValue(reload: !AppSettings.RememberShowNonPrintingCharsPreference);
            showNonPrintChars.Checked = showNonPrintingChars;
            showNonprintableCharactersToolStripMenuItem.Checked = showNonPrintingChars;
            ToggleNonPrintingChars(showNonPrintingChars);

            ShowSyntaxHighlightingInDiff = AppSettings.ShowSyntaxHighlightingInDiff.GetValue(reload: !AppSettings.RememberShowSyntaxHighlightingInDiff);
            showSyntaxHighlighting.Image = Resources.SyntaxHighlighting.AdaptLightness();
            showSyntaxHighlighting.Checked = ShowSyntaxHighlightingInDiff;
            automaticContinuousScrollToolStripMenuItem.Text = TranslatedStrings.ContScrollToNextFileOnlyWithAlt;

            IsReadOnly = true;

            internalFileViewer.MouseMove += (_, e) =>
            {
                if (IsDiffView(_viewMode) && !fileviewerToolbar.Visible)
                {
                    fileviewerToolbar.Visible = true;
                    fileviewerToolbar.Location = new Point(Width - fileviewerToolbar.Width - 40, 0);
                    fileviewerToolbar.BringToFront();
                }
            };
            internalFileViewer.MouseLeave += (_, e) =>
            {
                if (GetChildAtPoint(PointToClient(MousePosition)) != fileviewerToolbar &&
                    fileviewerToolbar is not null)
                {
                    fileviewerToolbar.Visible = false;
                }
            };
            internalFileViewer.TextChanged += (sender, e) =>
            {
                internalFileViewer.AddTextHighlighting();

                TextChanged?.Invoke(sender, e);
            };
            internalFileViewer.HScrollPositionChanged += (sender, e) => HScrollPositionChanged?.Invoke(sender, e);
            internalFileViewer.VScrollPositionChanged += (sender, e) => VScrollPositionChanged?.Invoke(sender, e);
            internalFileViewer.SelectedLineChanged += (sender, e) => SelectedLineChanged?.Invoke(sender, e);
            internalFileViewer.DoubleClick += (_, args) => RequestDiffView?.Invoke(this, EventArgs.Empty);

            HotkeysEnabled = true;

            if (!IsDesignMode && ContextMenuStrip is null)
            {
                ContextMenuStrip = contextMenu;
            }

            contextMenu.Opening += (sender, e) =>
            {
                copyToolStripMenuItem.Enabled = internalFileViewer.GetSelectionLength() > 0;
                ContextMenuOpening?.Invoke(sender, e);
            };

            _fullPathResolver = new FullPathResolver(() => Module.WorkingDir);
            SupportLinePatching = false;

#if WINDOWS // TODO - mono
            _NO_TRANSLATE_resetSelectedLinesConfirmationDialog = new()
            {
                Text = TranslatedStrings.ResetSelectedLinesConfirmation,
                Caption = TranslatedStrings.ResetChangesCaption,
                Icon = TaskDialogIcon.Warning,
                Buttons = { TaskDialogButton.Yes, TaskDialogButton.No },
                DefaultButton = TaskDialogButton.Yes,
                SizeToContent = true,
            };
#endif
        }

        // Public properties

        [Browsable(false)]
        public byte[]? FilePreamble { get; private set; }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        public new Font Font
        {
            get => internalFileViewer.Font;
            set => internalFileViewer.Font = value;
        }

        [DefaultValue(true)]
        [Category("Behavior")]
        public bool IsReadOnly
        {
            get => internalFileViewer.IsReadOnly;
            set
            {
                internalFileViewer.IsReadOnly = value;
                replaceToolStripMenuItem.Visible = !value;
            }
        }

        [DefaultValue(true)]
        [Category("Behavior")]
        public bool EnableAutomaticContinuousScroll
        {
            get => automaticContinuousScrollToolStripMenuItem.Visible;
            set => automaticContinuousScrollToolStripMenuItem.Visible = value;
        }

        [DefaultValue(null)]
        [Description("If true line numbers are shown in the textarea")]
        [Category("Appearance")]
        public bool? ShowLineNumbers
        {
            get => internalFileViewer.ShowLineNumbers;
            set => internalFileViewer.ShowLineNumbers = value;
        }

        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        [Browsable(false)]
        [NotNull]
        public Encoding? Encoding
        {
            get => _encoding ??= Module.FilesEncoding;
            set
            {
                _encoding = value;

                this.InvokeAndForget(() =>
                {
                    if (_encoding is not null)
                    {
                        encodingToolStripComboBox.Text = _encoding.EncodingName;
                    }
                    else
                    {
                        encodingToolStripComboBox.SelectedIndex = -1;
                    }
                });
            }
        }

        public void ScrollToTop()
        {
            internalFileViewer.ScrollToTop();
        }

        public void ScrollToBottom()
        {
            internalFileViewer.ScrollToBottom();
        }

        [DefaultValue(0)]
        [Browsable(false)]
        public int HScrollPosition
        {
            get => internalFileViewer.HScrollPosition;
            set => internalFileViewer.HScrollPosition = value;
        }

        [DefaultValue(0)]
        [Browsable(false)]
        public int VScrollPosition
        {
            get => internalFileViewer.VScrollPosition;
            set => internalFileViewer.VScrollPosition = value;
        }

        // Private properties

        [Description("Sets what kind of whitespace changes shall be ignored in diffs")]
        [DefaultValue(IgnoreWhitespaceKind.None)]
        private IgnoreWhitespaceKind IgnoreWhitespace { get; set; }

        [Description("Show diffs with <n> lines of context.")]
        [DefaultValue(3)]
        private int NumberOfContextLines { get; set; }

        [Description("Show diffs with entire file.")]
        [DefaultValue(false)]
        private bool ShowEntireFile { get; set; }

        [Description("Treat all files as text.")]
        [DefaultValue(false)]
        private bool TreatAllFilesAsText { get; set; }

        [Description("Show syntax highlighting in diffs.")]
        [DefaultValue(true)]
        private bool ShowSyntaxHighlightingInDiff { get; set; }

        // Public methods

        public void SetGitBlameGutter(IEnumerable<GitBlameEntry> gitBlameEntries)
        {
            internalFileViewer.ShowGutterAvatars = AppSettings.BlameShowAuthorAvatar;

            if (AppSettings.BlameShowAuthorAvatar)
            {
                internalFileViewer.SetGitBlameGutter(gitBlameEntries);
            }
        }

        public void ClearBlameGutter()
        {
            internalFileViewer.ShowGutterAvatars = false;
        }

        public void ReloadHotkeys()
        {
            LoadHotkeys(HotkeySettingsName);
            findToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.Find);
            replaceToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.Replace);
            stageSelectedLinesToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.StageLines);
            unstageSelectedLinesToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.UnstageLines);
            resetSelectedLinesToolStripMenuItem.ShortcutKeyDisplayString = GetShortcutKeyDisplayString(Command.ResetLines);
        }

        public ToolStripSeparator AddContextMenuSeparator()
        {
            ToolStripSeparator separator = new();
            contextMenu.Items.Add(separator);
            return separator;
        }

        public ToolStripMenuItem AddContextMenuEntry(string text, EventHandler toolStripItem_Click)
        {
            ToolStripMenuItem toolStripItem = new(text);
            contextMenu.Items.Add(toolStripItem);
            toolStripItem.Click += toolStripItem_Click;
            return toolStripItem;
        }

        public void EnableScrollBars(bool enable)
        {
            internalFileViewer.EnableScrollBars(enable);
        }

        public ArgumentString GetExtraDiffArguments(bool isRangeDiff = false)
        {
            return new ArgumentBuilder
            {
                { IgnoreWhitespace == IgnoreWhitespaceKind.AllSpace, "--ignore-all-space" },
                { IgnoreWhitespace == IgnoreWhitespaceKind.Change, "--ignore-space-change" },
                { IgnoreWhitespace == IgnoreWhitespaceKind.Eol, "--ignore-space-at-eol" },
                { ShowEntireFile, "--inter-hunk-context=9000 --unified=9000", $"--unified={NumberOfContextLines}" },

                // Handle zero context as showing no file changes, to get the summary only
                { isRangeDiff && NumberOfContextLines == 0, "--no-patch " },
                { TreatAllFilesAsText, "--text" }
            };
        }

        public string GetSelectedText()
        {
            return internalFileViewer.GetSelectedText();
        }

        public int GetSelectionPosition()
        {
            return internalFileViewer.GetSelectionPosition();
        }

        public int GetSelectionLength()
        {
            return internalFileViewer.GetSelectionLength();
        }

        public void GoToLine(int line)
        {
            internalFileViewer.GoToLine(line);
        }

        public int GetLineFromVisualPosY(int visualPosY)
        {
            return internalFileViewer.GetLineFromVisualPosY(visualPosY);
        }

        public int CurrentFileLine => internalFileViewer.CurrentFileLine();

        public void HighlightLines(int startLine, int endLine, Color color)
        {
            internalFileViewer.HighlightLines(startLine, endLine, color);
        }

        public void ClearHighlighting()
        {
            internalFileViewer.ClearHighlighting();
        }

        public string GetText() => internalFileViewer.GetText();

        /// <summary>
        /// Present the text as a patch in the file viewer.
        /// </summary>
        /// <param name="item">The gitItem to present.</param>
        /// <param name="text">The patch text.</param>
        /// <param name="line">The line to display.</param>
        /// <param name="openWithDifftool">The action to open the difftool.</param>
        public Task ViewPatchAsync(FileStatusItem item, string text, int? line, Action? openWithDifftool)
            => ViewPrivateAsync(item, item?.Item?.Name, text, line, openWithDifftool, ViewMode.Diff);

        /// <summary>
        /// Present the text as a patch in the file viewer, for GitHub.
        /// </summary>
        /// <param name="fileName">The fileName to present.</param>
        /// <param name="text">The patch text.</param>
        /// <param name="openWithDifftool">The action to open the difftool.</param>
        public Task ViewFixedPatchAsync(string fileName, string text, Action? openWithDifftool = null)
            => ViewPrivateAsync(item: null, fileName, text, line: null, openWithDifftool, ViewMode.FixedDiff);

        public void ViewFixedPatch(string? fileName,
            string text,
            Action? openWithDifftool = null)
        {
            ThreadHelper.JoinableTaskFactory.Run(
                () => ViewFixedPatchAsync(fileName, text, openWithDifftool));
        }

        public Task ViewRangeDiffAsync(string fileName, string text)
            => ViewPrivateAsync(item: null, fileName, text, line: null, openWithDifftool: null, ViewMode.RangeDiff);

        public void ViewText(string? fileName,
            string text,
            Action? openWithDifftool = null)
        {
            ThreadHelper.JoinableTaskFactory.Run(
                () => ViewTextAsync(fileName, text, openWithDifftool: openWithDifftool));
        }

        /// <summary>
        /// Present the text in the file viewer, for GitHub.
        /// </summary>
        /// <param name="fileName">The fileName to present.</param>
        /// <param name="text">The patch text.</param>
        /// <param name="line">The line to display.</param>
        /// <param name="openWithDifftool">The action to open the difftool.</param>
        /// <param name="checkGitAttributes">Check Git attributes to check for binary files.</param>
        public Task ViewTextAsync(string? fileName,
            string text,
            FileStatusItem? item = null,
            int? line = null,
            Action? openWithDifftool = null,
            bool checkGitAttributes = false)
        {
            return ShowOrDeferAsync(
                text.Length,
                () =>
                {
                    ResetView(ViewMode.Text, fileName, item: item);

                    // Check for binary file. Using gitattributes could be misleading for a changed file,
                    // but not much other can be done
                    bool isBinary = (checkGitAttributes && FileHelper.IsBinaryFileName(Module, fileName))
                                    || FileHelper.IsBinaryFileAccordingToContent(text);

                    if (isBinary)
                    {
                        try
                        {
                            StringBuilder summary = new StringBuilder()
                                .AppendLine("Binary file:")
                                .AppendLine()
                                .AppendLine(fileName)
                                .AppendLine()
                                .AppendLine($"{text.Length:N0} bytes:")
                                .AppendLine();
                            internalFileViewer.SetText(summary.ToString(), openWithDifftool);

                            ToHexDump(Encoding.ASCII.GetBytes(text), summary);
                            internalFileViewer.SetText(summary.ToString(), openWithDifftool);
                        }
                        catch
                        {
                            internalFileViewer.SetText($"Binary file: {fileName} (Detected)", openWithDifftool);
                        }
                    }
                    else
                    {
                        internalFileViewer.SetText(text, openWithDifftool);
                        if (line is not null)
                        {
                            GoToLine(line.Value);
                        }
                    }

                    TextLoaded?.Invoke(this, null);
                    return Task.CompletedTask;
                });
        }

        public Task ViewGitItemAsync(FileStatusItem item, int? line, Action? openWithDifftool)
        {
            return ViewGitItemAsync(item.Item, item.SecondRevision.ObjectId, item, line, openWithDifftool);
        }

        public Task ViewGitItemAsync(GitItemStatus file, ObjectId objectId, int? line = null, Action? openWithDifftool = null)
        {
            return ViewGitItemAsync(file, objectId, item: null, line, openWithDifftool);
        }

        /// <summary>
        /// View the git item with the TreeGuid.
        /// </summary>
        /// <param name="file">GitItem file, with TreeGuid.</param>
        /// <param name="objectId">Revision to present. Can be null if file.TreeGuid is set.</param>
        /// <param name="item">Metadata for line patching and presentation.</param>
        /// <param name="line">The line to display.</param>
        /// <param name="openWithDifftool">difftool command</param>
        /// <returns>Task to view the item</returns>
        private Task ViewGitItemAsync(GitItemStatus file, ObjectId? objectId, FileStatusItem? item, int? line, Action? openWithDifftool)
        {
            if (objectId == ObjectId.WorkTreeId || file.Staged == StagedStatus.WorkTree)
            {
                // No blob exists for worktree, present contents from file system
                return ViewFileAsync(file.Name, file.IsSubmodule, item, line, openWithDifftool);
            }

            file.TreeGuid ??= Module.GetFileBlobHash(file.Name, objectId);

            if (file.TreeGuid is null)
            {
                return ViewTextAsync(file.Name, $"Cannot get treeId from Git for {file.Name} for commit {objectId}.");
            }

            string sha = file.TreeGuid.ToString();
            bool isSubmodule = file.IsSubmodule;

            if (!isSubmodule && file.IsNew && file.Staged == StagedStatus.Index)
            {
                // File system access for other than Worktree,
                // to handle that git-status does not detect details for untracked (git-diff --no-index will not give info)
                string fullPath = Path.Combine(Module.WorkingDir, file.Name);
                if (Directory.Exists(fullPath) && GitModule.IsValidGitWorkingDir(fullPath))
                {
                    isSubmodule = true;
                }
            }

            return ViewItemAsync(
                file.Name,
                isSubmodule,
                getImage: GetImage,
                getFileText: GetFileTextIfBlobExists,
                getSubmoduleText: () => LocalizationHelpers.GetSubmoduleText(Module, file.Name.TrimEnd('/'), sha, cache: true),
                item: item,
                line: line,
                openWithDifftool: openWithDifftool);

            string GetFileTextIfBlobExists()
            {
                FilePreamble = new byte[] { };
                return file.TreeGuid is not null ? Module.GetFileText(file.TreeGuid, Encoding) : string.Empty;
            }

            Image? GetImage()
            {
                try
                {
                    using MemoryStream stream = Module.GetFileStream(sha);
                    if (stream is not null)
                    {
                        return CreateImage(file.Name, stream);
                    }
                }
                catch
                {
                }

                return null;
            }
        }

        /// <summary>
        /// Get contents in the file system async if not too big, otherwise ask user.
        /// </summary>
        /// <param name="fileName">The file/submodule path.</param>
        /// <param name="isSubmodule">If submodule.</param>
        /// <param name="item">Metadata for line patching and presentation.</param>
        /// <param name="line">The line to display.</param>
        /// <param name="openWithDifftool">Diff action.</param>
        /// <returns>Task.</returns>
        public Task ViewFileAsync(string fileName, bool isSubmodule = false, FileStatusItem? item = null, int? line = null, Action? openWithDifftool = null)
        {
            string? fullPath = _fullPathResolver.Resolve(fileName);
            Validates.NotNull(fullPath);
            DebugHelpers.Assert(Path.IsPathFullyQualified(fullPath), "Path must be resolved and fully qualified");

            if (!isSubmodule)
            {
                if (fileName.EndsWith("/") || Directory.Exists(fullPath))
                {
                    if (!GitModule.IsValidGitWorkingDir(fullPath))
                    {
                        return ViewTextAsync(fileName, "Directory: " + fileName);
                    }

                    isSubmodule = true;
                }
                else if (!File.Exists(fullPath))
                {
                    return ViewTextAsync(fileName, $"File {fullPath} does not exist");
                }
            }
            else if (!GitModule.IsValidGitWorkingDir(fullPath))
            {
                return ViewTextAsync(fileName, $"Invalid submodule: {fileName}");
            }

            return ShowOrDeferAsync(
                fullPath,
                () => ViewItemAsync(
                    fileName,
                    isSubmodule,
                    getImage: GetImage,
                    getFileText: GetFileText,
                    getSubmoduleText: () => LocalizationHelpers.GetSubmoduleText(Module, fileName.TrimEnd('/'), "", cache: false),
                    item: item,
                    line: line,
                    openWithDifftool));

            Image? GetImage()
            {
                try
                {
                    using FileStream stream = File.OpenRead(fullPath);
                    return CreateImage(fileName, stream);
                }
                catch
                {
                    return null;
                }
            }

            string GetFileText()
            {
                using FileStream stream = File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                using StreamReader reader = new(stream, Module.FilesEncoding);
#pragma warning disable VSTHRD103 // Call async methods when in an async method
                string content = reader.ReadToEnd();
#pragma warning restore VSTHRD103 // Call async methods when in an async method
                FilePreamble = reader.CurrentEncoding.GetPreamble();
                return content;
            }
        }

        public Task ClearAsync() => ViewTextAsync("", "");

        public void Clear()
        {
            ThreadHelper.JoinableTaskFactory.Run(ClearAsync);
        }

        /// <summary>
        /// If the file viewer contents support line patches.
        /// </summary>
        public bool SupportLinePatching { get; private set; }

        /// <summary>
        /// Configuration to require that the form using the viewer reloads contents before allowing next line patch
        /// by clearing <see cref="AllowLinePatching" />
        /// Used for index/worktree where line patches modifies the diff.
        /// </summary>
        public bool LinePatchingBlocksUntilReload { private get; set; }

        /// <summary>
        /// Current state for line patching allowed for worktree/index
        /// Cleared when the file is reloaded.
        /// </summary>
        private bool AllowLinePatching { get; set; }

        public void SetFileLoader(GetNextFileFnc fileLoader)
        {
            internalFileViewer.SetFileLoader(fileLoader);
        }

        public void CherryPickAllChanges()
        {
            if (!SupportLinePatching)
            {
                // Hotkey executed when menu is disabled
                return;
            }

            ApplySelectedLines(allFile: true, reverse: false);
        }

        // Protected

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                UICommandsSourceSet -= OnUICommandsSourceSet;
                _async.Dispose();
                components?.Dispose();

                if (TryGetUICommandsDirect(out GitUICommands uiCommands))
                {
                    uiCommands.PostSettings -= UICommands_PostSettings;
                }
            }

            base.Dispose(disposing);
        }

        protected override void DisposeUICommandsSource()
        {
            UICommandsSource.UICommandsChanged -= OnUICommandsChanged;
            base.DisposeUICommandsSource();
        }

        protected override void OnRuntimeLoad()
        {
            base.OnRuntimeLoad();

            ReloadHotkeys();

            Font = AppSettings.FixedWidthFont;

            string[] encodings = AppSettings.AvailableEncodings.Values.Select(e => e.EncodingName).ToArray();
            encodingToolStripComboBox.Items.AddRange(encodings);
            encodingToolStripComboBox.ResizeDropDownWidth(50, 250);
        }

        // Private methods

        private static bool IsDiffView(ViewMode viewMode)
            => viewMode is (ViewMode.Diff or ViewMode.FixedDiff or ViewMode.RangeDiff);

        private Task ViewPrivateAsync(FileStatusItem? item, string? fileName, string text, int? line, Action? openWithDifftool, ViewMode viewMode)
        {
            return ShowOrDeferAsync(
                text.Length,
                () =>
                {
                    ResetView(viewMode, fileName, item: item, text: text);
                    internalFileViewer.SetText(text, openWithDifftool, isDiff: IsDiffView(_viewMode), isRangeDiff: _viewMode == ViewMode.RangeDiff);
                    if (line is not null)
                    {
                        GoToLine(line.Value);
                    }

                    TextLoaded?.Invoke(this, null);
                    return Task.CompletedTask;
                });
        }

        private StagedStatus ViewItemStagedStatus()
        {
            StagedStatus stagedStatus = _viewItem?.Item?.Staged ?? StagedStatus.Unknown;
            if (stagedStatus == StagedStatus.Unknown)
            {
                stagedStatus = GitModule.GetStagedStatus(_viewItem?.FirstRevision?.ObjectId,
                    _viewItem?.SecondRevision?.ObjectId,
                    _viewItem?.SecondRevision?.FirstParentId);
                if (_viewItem?.Item is not null)
                {
                    _viewItem.Item.Staged = stagedStatus;
                }
            }

            return stagedStatus;
        }

        private void SetVisibilityDiffContextMenu(ViewMode viewMode)
        {
            // stage and reset has different implementation depending on the viewItem
            // For the user it looks the same and they expect the same menu item (and hotkey)
            bool isIndex = ViewItemStagedStatus() == StagedStatus.Index;
            stageSelectedLinesToolStripMenuItem.Visible = SupportLinePatching && !isIndex;
            unstageSelectedLinesToolStripMenuItem.Visible = SupportLinePatching && isIndex;
            resetSelectedLinesToolStripMenuItem.Visible = SupportLinePatching;

            // RangeDiff patch is undefined, could be new/old commit or to parents
            copyPatchToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.FixedDiff);
            copyNewVersionToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.FixedDiff);
            copyOldVersionToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.FixedDiff);

            ignoreWhitespaceAtEolToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            ignoreWhitespaceChangesToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            ignoreAllWhitespaceChangesToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            increaseNumberOfLinesToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            decreaseNumberOfLinesToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            showEntireFileToolStripMenuItem.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            toolStripSeparator2.Visible = IsDiffView(viewMode);
            treatAllFilesAsTextToolStripMenuItem.Visible = IsDiffView(viewMode);

            // toolbar
            nextChangeButton.Visible = IsDiffView(viewMode);
            previousChangeButton.Visible = IsDiffView(viewMode);
            increaseNumberOfLines.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            decreaseNumberOfLines.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            showEntireFileButton.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            showSyntaxHighlighting.Visible = IsDiffView(viewMode);
            ignoreWhitespaceAtEol.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            ignoreWhiteSpaces.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
            ignoreAllWhitespaces.Visible = viewMode is (ViewMode.Diff or ViewMode.RangeDiff);
        }

        private void OnExtraDiffArgumentsChanged()
        {
            ExtraDiffArgumentsChanged?.Invoke(this, EventArgs.Empty);
        }

        private Task ShowOrDeferAsync(string fullPath, Func<Task> showFunc)
        {
            return ShowOrDeferAsync(GetFileLength(), showFunc);

            long GetFileLength()
            {
                try
                {
                    DebugHelpers.Assert(Path.IsPathFullyQualified(fullPath), "Path must be resolved and fully qualified");
                    if (File.Exists(fullPath))
                    {
                        return new FileInfo(fullPath).Length;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, $"{ex.Message}{Environment.NewLine}{fullPath}", TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                // If the file does not exist, it doesn't matter what size we
                // return as nothing will be shown anyway.
                return 0;
            }
        }

        private async Task ShowOrDeferAsync(long contentLength, Func<Task> showFunc)
        {
            const long maxLength = 5 * 1024 * 1024;

            if (contentLength > maxLength)
            {
                await ClearAsync();
                Refresh();
                _NO_TRANSLATE_lblShowPreview.Text = string.Format(_largeFileSizeWarning.Text, contentLength / (1024d * 1024));
                _NO_TRANSLATE_lblShowPreview.Show();
                _deferShowFunc = showFunc;
                return;
            }
            else
            {
                _NO_TRANSLATE_lblShowPreview.Hide();
                _deferShowFunc = null;
                await showFunc();
                return;
            }
        }

        private static Image CreateImage(string fileName, Stream stream)
        {
            if (IsIcon())
            {
                using Icon icon = new(stream);
                return icon.ToBitmap();
            }

            return new Bitmap(CopyStream());

            bool IsIcon()
            {
                return fileName.EndsWith(".ico", StringComparison.CurrentCultureIgnoreCase);
            }

            MemoryStream CopyStream()
            {
                MemoryStream copy = new();
                stream.CopyTo(copy);
                return copy;
            }
        }

        private void OnIgnoreWhitespaceChanged()
        {
            switch (IgnoreWhitespace)
            {
                case IgnoreWhitespaceKind.None:
                    ignoreWhitespaceAtEol.Checked = false;
                    ignoreWhitespaceAtEolToolStripMenuItem.Checked = ignoreWhitespaceAtEol.Checked;
                    ignoreWhiteSpaces.Checked = false;
                    ignoreWhitespaceChangesToolStripMenuItem.Checked = ignoreWhiteSpaces.Checked;
                    ignoreAllWhitespaces.Checked = false;
                    ignoreAllWhitespaceChangesToolStripMenuItem.Checked = ignoreAllWhitespaces.Checked;
                    break;
                case IgnoreWhitespaceKind.Eol:
                    ignoreWhitespaceAtEol.Checked = true;
                    ignoreWhitespaceAtEolToolStripMenuItem.Checked = ignoreWhitespaceAtEol.Checked;
                    ignoreWhiteSpaces.Checked = false;
                    ignoreWhitespaceChangesToolStripMenuItem.Checked = ignoreWhiteSpaces.Checked;
                    ignoreAllWhitespaces.Checked = false;
                    ignoreAllWhitespaceChangesToolStripMenuItem.Checked = ignoreAllWhitespaces.Checked;
                    break;
                case IgnoreWhitespaceKind.Change:
                    ignoreWhitespaceAtEol.Checked = true;
                    ignoreWhitespaceAtEolToolStripMenuItem.Checked = ignoreWhitespaceAtEol.Checked;
                    ignoreWhiteSpaces.Checked = true;
                    ignoreWhitespaceChangesToolStripMenuItem.Checked = ignoreWhiteSpaces.Checked;
                    ignoreAllWhitespaces.Checked = false;
                    ignoreAllWhitespaceChangesToolStripMenuItem.Checked = ignoreAllWhitespaces.Checked;
                    break;
                case IgnoreWhitespaceKind.AllSpace:
                    ignoreWhitespaceAtEol.Checked = true;
                    ignoreWhitespaceAtEolToolStripMenuItem.Checked = ignoreWhitespaceAtEol.Checked;
                    ignoreWhiteSpaces.Checked = true;
                    ignoreWhitespaceChangesToolStripMenuItem.Checked = ignoreWhiteSpaces.Checked;
                    ignoreAllWhitespaces.Checked = true;
                    ignoreAllWhitespaceChangesToolStripMenuItem.Checked = ignoreAllWhitespaces.Checked;
                    break;
                default:
                    throw new NotSupportedException("Unsupported value for IgnoreWhitespaceKind: " + IgnoreWhitespace);
            }

            AppSettings.IgnoreWhitespaceKind.Value = IgnoreWhitespace;
        }

        /// <summary>
        /// Reset internal status for the new file.
        /// </summary>
        /// <param name="viewMode">Requested mode to view.</param>
        /// <param name="fileName">Filename to present, for highlighting etc.</param>
        /// <param name="item">Metadata for linepatching.</param>
        /// <param name="text">Metadata for linepatching.</param>
        private void ResetView(ViewMode viewMode, string? fileName, FileStatusItem? item = null, string? text = null)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            _viewMode = viewMode;
            _viewItem = item;
            if (_viewMode == ViewMode.Text
                && !string.IsNullOrEmpty(fileName)
                && (fileName.EndsWith(".diff", StringComparison.OrdinalIgnoreCase)
                    || fileName.EndsWith(".patch", StringComparison.OrdinalIgnoreCase)))
            {
                // Override the set view mode
                _viewMode = ViewMode.FixedDiff;
            }

            SupportLinePatching =

                // Diffs, currently requires that the file to update exists
                ((IsDiffView(_viewMode) && (text?.Contains("@@") ?? false)
                        && File.Exists(_fullPathResolver.Resolve(fileName)))

                // New files, patches only applies for artificial or if the file does not exist
                    || ((item?.Item.IsNew ?? false)
                        && ((item.Item.Staged is StagedStatus.WorkTree or StagedStatus.Index)
                            || !File.Exists(_fullPathResolver.Resolve(fileName)))))

                // No patching allowed if no worktree
                && !Module.IsBareRepository();

            SetVisibilityDiffContextMenu(_viewMode);
            ClearImage();
            PictureBox.Visible = _viewMode == ViewMode.Image;
            internalFileViewer.Visible = _viewMode != ViewMode.Image;

            if (((ShowSyntaxHighlightingInDiff && IsDiffView(_viewMode)) || _viewMode == ViewMode.Text) && fileName is not null)
            {
                internalFileViewer.SetHighlightingForFile(fileName);
            }
            else
            {
                internalFileViewer.SetHighlighting("");
            }

            return;

            void ClearImage()
            {
                PictureBox.ImageLocation = "";

                if (PictureBox.Image is null)
                {
                    return;
                }

                PictureBox.Image.Dispose();
                PictureBox.Image = null;
            }
        }

        private Task ViewItemAsync(string fileName, bool isSubmodule, Func<Image?> getImage, Func<string> getFileText, Func<string> getSubmoduleText, FileStatusItem? item, int? line, Action? openWithDifftool)
        {
            FilePreamble = null;

            if (isSubmodule)
            {
                return _async.LoadAsync(
                    getSubmoduleText,
                    text => ThreadHelper.JoinableTaskFactory.Run(() => ViewTextAsync(fileName, text, item, line: null, openWithDifftool)));
            }
            else if (FileHelper.IsImage(fileName))
            {
                return _async.LoadAsync(getImage,
                            image =>
                            {
                                if (image is null)
                                {
                                    ResetView(ViewMode.Text, null);

                                    string text = getFileText();
                                    StringBuilder summary = new StringBuilder()
                                        .AppendLine(string.Format(_cannotViewImage.Text, fileName))
                                        .AppendLine()
                                        .AppendLine($"{text.Length:N0} bytes:")
                                        .AppendLine();

                                    ToHexDump(Encoding.ASCII.GetBytes(text), summary);
                                    internalFileViewer.SetText(summary.ToString(), openWithDifftool);
                                    return;
                                }

                                ResetView(ViewMode.Image, fileName, item);
                                Size size = DpiUtil.Scale(image.Size);
                                if (size.Height > PictureBox.Size.Height || size.Width > PictureBox.Size.Width)
                                {
                                    PictureBox.SizeMode = PictureBoxSizeMode.Zoom;
                                }
                                else
                                {
                                    PictureBox.SizeMode = PictureBoxSizeMode.CenterImage;
                                }

                                PictureBox.Image = DpiUtil.Scale(image);
                                internalFileViewer.SetText("", openWithDifftool);
                            });
            }
            else
            {
                return _async.LoadAsync(
                    getFileText,
                    text => ThreadHelper.JoinableTaskFactory.Run(() => ViewTextAsync(fileName, text, item, line, openWithDifftool, checkGitAttributes: true)));
            }
        }

        private static string ToHexDump(byte[] bytes, StringBuilder str, int columnWidth = 8, int columnCount = 2)
        {
            if (bytes.Length == 0)
            {
                return "";
            }

            // Do not freeze GE when selecting large binary files
            // Show only the header of the binary file to indicate contents and files incorrectly handled
            // Use a dedicated editor to view the complete file
            int limit = Math.Min(bytes.Length, columnWidth * columnCount * 256);
            int i = 0;

            while (i < limit)
            {
                int baseIndex = i;

                if (i != 0)
                {
                    str.AppendLine();
                }

                // OFFSET
                str.Append($"{baseIndex:X4}    ");

                // BYTES
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    // space between columns
                    if (columnIndex != 0)
                    {
                        str.Append("  ");
                    }

                    for (int j = 0; j < columnWidth; j++)
                    {
                        if (j != 0)
                        {
                            str.Append(' ');
                        }

                        str.Append(i < bytes.Length
                            ? bytes[i].ToString("X2")
                            : "  ");
                        i++;
                    }
                }

                str.Append("    ");

                // ASCII
                i = baseIndex;
                for (int columnIndex = 0; columnIndex < columnCount; columnIndex++)
                {
                    // space between columns
                    if (columnIndex != 0)
                    {
                        str.Append(' ');
                    }

                    for (int j = 0; j < columnWidth; j++)
                    {
                        if (i < bytes.Length)
                        {
                            char c = (char)bytes[i];
                            str.Append(char.IsControl(c) ? '.' : c);
                        }
                        else
                        {
                            str.Append(' ');
                        }

                        i++;
                    }
                }
            }

            if (bytes.Length > limit)
            {
                str.AppendLine();
                str.Append("[Truncated]");
            }

            return str.ToString();
        }

        private void SetStateOfContextLinesButtons()
        {
            increaseNumberOfLines.Enabled = !ShowEntireFile;
            decreaseNumberOfLines.Enabled = !ShowEntireFile;
            increaseNumberOfLinesToolStripMenuItem.Enabled = !ShowEntireFile;
            decreaseNumberOfLinesToolStripMenuItem.Enabled = !ShowEntireFile;
        }

        private void ToggleNonPrintingChars(bool show)
        {
            internalFileViewer.EolMarkerStyle = show
                ? AppSettings.ShowEolMarkerAsGlyph
                    ? ICSharpCode.TextEditor.Document.EolMarkerStyle.Glyph
                    : ICSharpCode.TextEditor.Document.EolMarkerStyle.Text
                : ICSharpCode.TextEditor.Document.EolMarkerStyle.None;
            internalFileViewer.ShowSpaces = show;
            internalFileViewer.ShowTabs = show;
        }

        // Event handlers

        private void OnUICommandsChanged(object sender, GitUICommandsChangedEventArgs? e)
        {
            if (e?.OldCommands is not null)
            {
                e.OldCommands.PostSettings -= UICommands_PostSettings;
            }

            IGitUICommandsSource commandSource = sender as IGitUICommandsSource;
            if (commandSource?.UICommands is not null)
            {
                commandSource.UICommands.PostSettings += UICommands_PostSettings;
                UICommands_PostSettings(commandSource.UICommands, null);
            }

            Encoding = null;
        }

        private void UICommands_PostSettings(object sender, GitUIPostActionEventArgs? e)
        {
            internalFileViewer.InvokeAndForget(() => internalFileViewer.VRulerPosition = AppSettings.DiffVerticalRulerPosition);
        }

        private void IgnoreWhitespaceAtEolToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IgnoreWhitespace == IgnoreWhitespaceKind.Eol)
            {
                IgnoreWhitespace = IgnoreWhitespaceKind.None;
            }
            else
            {
                IgnoreWhitespace = IgnoreWhitespaceKind.Eol;
            }

            OnIgnoreWhitespaceChanged();
            OnExtraDiffArgumentsChanged();
        }

        private void IgnoreWhitespaceChangesToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (IgnoreWhitespace == IgnoreWhitespaceKind.Change)
            {
                IgnoreWhitespace = IgnoreWhitespaceKind.None;
            }
            else
            {
                IgnoreWhitespace = IgnoreWhitespaceKind.Change;
            }

            OnIgnoreWhitespaceChanged();
            OnExtraDiffArgumentsChanged();
        }

        private void IncreaseNumberOfLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            NumberOfContextLines++;
            AppSettings.NumberOfContextLines = NumberOfContextLines;
            OnExtraDiffArgumentsChanged();
        }

        private void DecreaseNumberOfLinesToolStripMenuItemClick(object sender, EventArgs e)
        {
            if (NumberOfContextLines > 0)
            {
                NumberOfContextLines--;
            }
            else
            {
                NumberOfContextLines = 0;
            }

            AppSettings.NumberOfContextLines = NumberOfContextLines;
            OnExtraDiffArgumentsChanged();
        }

        private void ShowSyntaxHighlighting_Click(object sender, EventArgs e)
        {
            ShowSyntaxHighlightingInDiff = !ShowSyntaxHighlightingInDiff;
            showSyntaxHighlighting.Checked = ShowSyntaxHighlightingInDiff;
            AppSettings.ShowSyntaxHighlightingInDiff.Value = ShowSyntaxHighlightingInDiff;
            OnExtraDiffArgumentsChanged();
        }

        private void ShowEntireFileToolStripMenuItemClick(object sender, EventArgs e)
        {
            ShowEntireFile = !ShowEntireFile;
            showEntireFileButton.Checked = ShowEntireFile;
            showEntireFileToolStripMenuItem.Checked = ShowEntireFile;
            SetStateOfContextLinesButtons();
            AppSettings.ShowEntireFile.Value = ShowEntireFile;
            OnExtraDiffArgumentsChanged();
        }

        private void _continuousScrollEventManager_BottomScrollReached(object sender, EventArgs e)
            => BottomScrollReached?.Invoke(sender, e);

        private void _continuousScrollEventManager_TopScrollReached(object sender, EventArgs e)
            => TopScrollReached?.Invoke(sender, e);

        private void llShowPreview_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            _NO_TRANSLATE_lblShowPreview.Hide();
            ThreadHelper.JoinableTaskFactory.Run(() => _deferShowFunc?.Invoke() ?? Task.CompletedTask);
        }

        private void PictureBox_MouseWheel(object sender, MouseEventArgs e)
        {
            bool isScrollingTowardTop = e.Delta > 0;
            bool isScrollingTowardBottom = e.Delta < 0;

            if (isScrollingTowardTop)
            {
                _continuousScrollEventManager.RaiseTopScrollReached(sender, e);
            }

            if (isScrollingTowardBottom)
            {
                _continuousScrollEventManager.RaiseBottomScrollReached(sender, e);
            }
        }

        private void OnUICommandsSourceSet(object sender, GitUICommandsSourceEventArgs e)
        {
            UICommandsSource.UICommandsChanged += OnUICommandsChanged;
            OnUICommandsChanged(UICommandsSource, null);
        }

        private void TreatAllFilesAsTextToolStripMenuItemClick(object sender, EventArgs e)
        {
            treatAllFilesAsTextToolStripMenuItem.Checked = !treatAllFilesAsTextToolStripMenuItem.Checked;
            TreatAllFilesAsText = treatAllFilesAsTextToolStripMenuItem.Checked;
            OnExtraDiffArgumentsChanged();
        }

        private void settingsButton_Click(object sender, EventArgs e)
        {
            UICommands.StartSettingsDialog(ParentForm, DiffViewerSettingsPage.GetPageReference());
        }

        private void IgnoreAllWhitespaceChangesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (IgnoreWhitespace == IgnoreWhitespaceKind.AllSpace)
            {
                IgnoreWhitespace = IgnoreWhitespaceKind.None;
            }
            else
            {
                IgnoreWhitespace = IgnoreWhitespaceKind.AllSpace;
            }

            OnIgnoreWhitespaceChanged();
            OnExtraDiffArgumentsChanged();
        }

        private void stageSelectedLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            StageSelectedLines();
        }

        private void unstageSelectedLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            UnstageSelectedLines();
        }

        private void resetSelectedLinesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ResetSelectedLines();
        }

        /// <summary>
        /// Use implementation matching the current viewItem.
        /// </summary>
        private bool StageSelectedLines()
        {
            if (!SupportLinePatching)
            {
                // Hotkey executed when menu is disabled
                return false;
            }

            if (ViewItemStagedStatus() == StagedStatus.WorkTree)
            {
                StageSelectedLines(stage: true);
                return true;
            }

            ApplySelectedLines(allFile: false, reverse: false);
            return true;
        }

        private bool UnstageSelectedLines()
        {
            if (!SupportLinePatching || ViewItemStagedStatus() != StagedStatus.Index)
            {
                // Hotkey executed when menu is disabled
                return false;
            }

            StageSelectedLines(stage: false);
            return true;
        }

        private bool ResetSelectedLines()
        {
            if (!SupportLinePatching)
            {
                // Hotkey executed when menu is disabled
                return false;
            }

            if (ViewItemStagedStatus() is StagedStatus.WorkTree or StagedStatus.Index)
            {
                ResetNoncommittedSelectedLines();
                return true;
            }

            ApplySelectedLines(allFile: false, reverse: true);
            return true;
        }

        /// <summary>
        /// Stage lines in WorkTree or Unstage lines in Index.
        /// </summary>
        public void StageSelectedLines(bool stage)
        {
            if (!AllowLinePatching || _viewItem is null)
            {
                // reload not completed
                return;
            }

            byte[]? patch;
            if (_viewItem.Item.IsNew)
            {
                Validates.NotNull(FilePreamble);

                string treeGuid = !stage ? _viewItem.Item.TreeGuid?.ToString() : null;
                patch = PatchManager.GetSelectedLinesAsNewPatch(
                    Module,
                    _viewItem.Item.Name,
                    GetText(),
                    GetSelectionPosition(),
                    GetSelectionLength(),
                    Encoding,
                    reset: false,
                    FilePreamble,
                    treeGuid);
            }
            else
            {
                patch = PatchManager.GetSelectedLinesAsPatch(
                    GetText(),
                    GetSelectionPosition(),
                    GetSelectionLength(),
                    isIndex: !stage,
                    Encoding,
                    reset: false,
                    _viewItem.Item.IsNew,
                    _viewItem.Item.IsRenamed);
            }

            if (patch?.Length is not > 0)
            {
                return;
            }

            GitArgumentBuilder args = new("apply")
            {
                "--cached",
                "--index",
                "--whitespace=nowarn",
                { !stage, "--reverse" }
            };

            ProcessApplyOutput(args, patch, patchUpdateDiff: true);
        }

        /// <summary>
        /// Reset lines in Index or Worktree.
        /// </summary>
        public void ResetNoncommittedSelectedLines()
        {
            if (!AllowLinePatching || _viewItem is null)
            {
                // reload not completed
                return;
            }

#if WINDOWS // TODO - mono
            if (TaskDialog.ShowDialog(Handle, _NO_TRANSLATE_resetSelectedLinesConfirmationDialog) == TaskDialogButton.No)
            {
                return;
            }
#endif

            byte[]? patch;
            bool currentItemStaged = _viewItem.SecondRevision.ObjectId == ObjectId.IndexId;
            if (_viewItem.Item.IsNew)
            {
                Validates.NotNull(FilePreamble);

                string treeGuid = currentItemStaged ? _viewItem.Item.TreeGuid?.ToString() : null;
                patch = PatchManager.GetSelectedLinesAsNewPatch(
                    Module,
                    _viewItem.Item.Name,
                    GetText(),
                    GetSelectionPosition(),
                    GetSelectionLength(),
                    Encoding,
                    reset: true,
                    FilePreamble,
                    treeGuid);
            }
            else if (currentItemStaged)
            {
                patch = PatchManager.GetSelectedLinesAsPatch(
                    GetText(),
                    GetSelectionPosition(),
                    GetSelectionLength(),
                    isIndex: true,
                    Encoding,
                    reset: true,
                    _viewItem.Item.IsNew,
                    _viewItem.Item.IsRenamed);
            }
            else
            {
                patch = PatchManager.GetResetWorkTreeLinesAsPatch(
                    GetText(),
                    GetSelectionPosition(),
                    GetSelectionLength(),
                    Encoding);
            }

            if (patch?.Length is not > 0)
            {
                return;
            }

            GitArgumentBuilder args = new("apply")
            {
                "--whitespace=nowarn",
                { currentItemStaged, "--reverse --index" }
            };

            ProcessApplyOutput(args, patch, patchUpdateDiff: true);
        }

        /// <summary>
        /// Cherry-pick/revert patches (not worktree or index).
        /// </summary>
        /// <param name="reverse"><see langword="true"/> if patches is to be reversed; otherwise <see langword="false"/>.</param>.
        private void ApplySelectedLines(bool allFile, bool reverse)
        {
            if (!AllowLinePatching || _viewItem is null)
            {
                // reload not completed
                return;
            }

            int selectionStart = allFile ? 0 : GetSelectionPosition();
            int selectionLength = allFile ? GetText().Length : GetSelectionLength();
            if (allFile && selectionLength == 0)
            {
                return;
            }

            byte[]? patch;

            if (_viewItem.Item.IsNew)
            {
                Validates.NotNull(FilePreamble);

                string treeGuid = reverse ? _viewItem.Item.TreeGuid?.ToString() : null;
                patch = PatchManager.GetSelectedLinesAsNewPatch(
                    Module,
                    _viewItem.Item.Name,
                    GetText(),
                    selectionStart,
                    selectionLength,
                    Encoding,
                    reset: reverse,
                    FilePreamble,
                    treeGuid);
            }
            else if (!reverse)
            {
                patch = PatchManager.GetSelectedLinesAsPatch(
                    GetText(),
                    selectionStart,
                    selectionLength,
                    isIndex: false,
                    Encoding,
                    reset: false,
                    _viewItem.Item.IsNew,
                    _viewItem.Item.IsRenamed);
            }
            else
            {
                patch = PatchManager.GetResetWorkTreeLinesAsPatch(
                    GetText(),
                    selectionStart,
                    selectionLength,
                    Encoding);
            }

            if (patch?.Length is not > 0)
            {
                return;
            }

            GitArgumentBuilder args = new("apply")
            {
                "--3way",
                "--index",
                "--whitespace=nowarn"
            };

            ProcessApplyOutput(args, patch);
        }

        private void ProcessApplyOutput(GitArgumentBuilder args, byte[] patch, bool patchUpdateDiff = false)
        {
            // TODO Cleanup the handling and separate AllOutput to StandardOutput/StandardError
            ExecutionResult result = Module.GitExecutable.Execute(args, inputWriter => inputWriter.BaseStream.Write(patch), throwOnErrorExit: false);
            string output = result.AllOutput.Trim();
            if (EnvUtils.RunningOnWindows())
            {
                // remove file mode warnings
                output = output.RemoveLines(FileModeWarningRegex().IsMatch);
            }

            if (!result.ExitedSuccessfully && (patchUpdateDiff || !MergeConflictHandler.HandleMergeConflicts(UICommands, this, false, false)))
            {
                MessageBox.Show(this, $"{output}\n\n{Encoding.GetString(patch)}", TranslatedStrings.Error, MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (!result.ExitedSuccessfully || output.StartsWith("error: ") || output.StartsWith("warning: "))
            {
                // git-apply may fail on first attempt but succeed in subsequent attempts
                // Trace such occurrences that may be interesting, some of these should maybe be presented to the user
                Trace.WriteLineIf(!string.IsNullOrWhiteSpace(output), $"Patch output: {result.ExitCode}:{output} for: git {args}");
            }
            else if (!string.IsNullOrWhiteSpace(output))
            {
                Debug.WriteLine($"Patch output: {result.ExitCode}:{output} for: git {args}");
            }

            if (patchUpdateDiff && LinePatchingBlocksUntilReload)
            {
                AllowLinePatching = false;
            }

            PatchApplied?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Copy selected text, excluding diff added/deleted information.
        /// </summary>
        /// <param name="sender">sender object.</param>
        /// <param name="e">event args.</param>
        private void CopyToolStripMenuItemClick(object sender, EventArgs e)
        {
            string code = internalFileViewer.GetSelectedText();

            if (string.IsNullOrEmpty(code))
            {
                return;
            }

            if (IsDiffView(_viewMode))
            {
                int pos = internalFileViewer.GetSelectionPosition();
                string fileText = internalFileViewer.GetText();
                int hpos = fileText.IndexOf("\n@@");

                // if header is selected then don't remove diff extra chars
                // for range-diff, copy all info (hpos will never match)
                if (hpos <= pos)
                {
                    if (pos > 0)
                    {
                        // add artificial space if selected text is not starting from line beginning, it will be removed later
                        if (fileText[pos - 1] != '\n')
                        {
                            code = " " + code;
                        }
                    }

                    code = string.Join("\n", code.LazySplit('\n').Select(RemovePrefix));
                }
            }

            ClipboardUtil.TrySetText(code.AdjustLineEndings(Module.GetEffectiveSettingsByPath("core").GetNullableEnum<AutoCRLFType>("autocrlf")));

            return;

            string RemovePrefix(string line)
            {
                string[] specials = internalFileViewer.GetFullDiffPrefixes();

                foreach (string special in specials.Where(line.StartsWith))
                {
                    return line[special.Length..];
                }

                return line;
            }
        }

        /// <summary>
        /// Copy selected text as a patch.
        /// </summary>
        /// <param name="sender">sender object.</param>
        /// <param name="e">event args.</param>
        private void CopyPatchToolStripMenuItemClick(object sender, EventArgs e)
        {
            string selectedText = internalFileViewer.GetSelectedText();

            if (!string.IsNullOrEmpty(selectedText))
            {
                ClipboardUtil.TrySetText(selectedText);
                return;
            }

            string text = internalFileViewer.GetText();

            if (!string.IsNullOrEmpty(text))
            {
                ClipboardUtil.TrySetText(text);
            }
        }

        private void copyNewVersionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            internalFileViewer.CopyNotStartingWith('-');
        }

        private void copyOldVersionToolStripMenuItem_Click(object sender, EventArgs e)
        {
            internalFileViewer.CopyNotStartingWith('+');
        }

        /// <summary>
        /// Go to next change
        /// For normal diffs, this is the next block of lines with a difference.
        /// For range-diff, it is the next commit summary header.
        /// </summary>
        private void NextChangeButtonClick(object sender, EventArgs e)
        {
            Focus();

            internalFileViewer.GoToNextChange(NumberOfContextLines);
        }

        private void PreviousChangeButtonClick(object sender, EventArgs e)
        {
            Focus();

            internalFileViewer.GoToPreviousChange(NumberOfContextLines);
         }

        private void ContinuousScrollToolStripMenuItemClick(object sender, EventArgs e)
        {
            automaticContinuousScrollToolStripMenuItem.Checked = !automaticContinuousScrollToolStripMenuItem.Checked;
            AppSettings.AutomaticContinuousScroll = automaticContinuousScrollToolStripMenuItem.Checked;
        }

        private void ShowNonprintableCharactersToolStripMenuItemClick(object sender, EventArgs e)
        {
            showNonprintableCharactersToolStripMenuItem.Checked = !showNonprintableCharactersToolStripMenuItem.Checked;
            showNonPrintChars.Checked = showNonprintableCharactersToolStripMenuItem.Checked;

            ToggleNonPrintingChars(show: showNonprintableCharactersToolStripMenuItem.Checked);
            AppSettings.ShowNonPrintingChars.Value = showNonPrintChars.Checked;
        }

        private void FindToolStripMenuItemClick(object sender, EventArgs e)
        {
            internalFileViewer.Find(sender == replaceToolStripMenuItem && !IsReadOnly);
        }

        private void encodingToolStripComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            Encoding encod;
            if (string.IsNullOrEmpty(encodingToolStripComboBox.Text))
            {
                encod = Module.FilesEncoding;
            }
            else
            {
                encod = AppSettings.AvailableEncodings.Values
                    .FirstOrDefault(en => en.EncodingName == encodingToolStripComboBox.Text)
                        ?? Module.FilesEncoding;
            }

            if (!encod.Equals(Encoding))
            {
                Encoding = encod;
                OnExtraDiffArgumentsChanged();
            }
        }

        private void fileviewerToolbar_VisibleChanged(object sender, EventArgs e)
        {
            if (fileviewerToolbar.Visible)
            {
                if (Encoding is not null)
                {
                    encodingToolStripComboBox.Text = Encoding.EncodingName;
                }
            }
        }

        private void goToLineToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using FormGoToLine formGoToLine = new();
            formGoToLine.SetMaxLineNumber(internalFileViewer.MaxLineNumber);
            if (formGoToLine.ShowDialog(this) == DialogResult.OK)
            {
                GoToLine(formGoToLine.GetLineNumber());
            }
        }

        #region Hotkey commands

        public static readonly string HotkeySettingsName = "FileViewer";

        internal enum Command
        {
            Find = 0,
            Replace = 16,
            FindNextOrOpenWithDifftool = 8,
            FindPrevious = 9,
            GoToLine = 1,
            IncreaseNumberOfVisibleLines = 2,
            DecreaseNumberOfVisibleLines = 3,
            ShowEntireFile = 4,
            TreatFileAsText = 5,
            NextChange = 6,
            PreviousChange = 7,
            NextOccurrence = 10,
            PreviousOccurrence = 11,
            StageLines = 12,
            UnstageLines = 13,
            ResetLines = 14,
            IgnoreAllWhitespace = 15,
        }

        protected override bool ExecuteCommand(int cmd)
        {
            Command command = (Command)cmd;

            switch (command)
            {
                case Command.Find: internalFileViewer.Find(replace: false); break;
                case Command.Replace:
                    if (!IsReadOnly)
                    {
                        internalFileViewer.Find(replace: true);
                    }

                    break;
                case Command.FindNextOrOpenWithDifftool: internalFileViewer.InvokeAndForget(() => internalFileViewer.FindNextAsync(searchForwardOrOpenWithDifftool: true)); break;
                case Command.FindPrevious: internalFileViewer.InvokeAndForget(() => internalFileViewer.FindNextAsync(searchForwardOrOpenWithDifftool: false)); break;
                case Command.GoToLine: return PerformClickIfAvailable(goToLineToolStripMenuItem);
                case Command.IncreaseNumberOfVisibleLines: return PerformClickIfAvailable(increaseNumberOfLinesToolStripMenuItem);
                case Command.DecreaseNumberOfVisibleLines: return PerformClickIfAvailable(decreaseNumberOfLinesToolStripMenuItem);
                case Command.ShowEntireFile: return PerformClickIfAvailable(showEntireFileToolStripMenuItem);
                case Command.TreatFileAsText: return PerformClickIfAvailable(treatAllFilesAsTextToolStripMenuItem);
                case Command.NextChange: return PerformClickIfAvailable(nextChangeButton);
                case Command.PreviousChange: return PerformClickIfAvailable(previousChangeButton);
                case Command.NextOccurrence: internalFileViewer.GoToNextOccurrence(); break;
                case Command.PreviousOccurrence: internalFileViewer.GoToPreviousOccurrence(); break;
                case Command.StageLines: return StageSelectedLines();
                case Command.UnstageLines: return UnstageSelectedLines();
                case Command.ResetLines: return ResetSelectedLines();
                case Command.IgnoreAllWhitespace: return PerformClickIfAvailable(ignoreAllWhitespaceChangesToolStripMenuItem);
                default: return base.ExecuteCommand(cmd);
            }

            return true;

            bool PerformClickIfAvailable(ToolStripItem item)
            {
                if (item.Enabled && item.Available)
                {
                    item.PerformClick();
                    return true;
                }

                // Don't handle the hotkey to let the control handle it if an action is bound to it
                return false;
            }
        }

        private string GetShortcutKeyDisplayString(Command cmd)
        {
            return GetShortcutKeys((int)cmd).ToShortcutKeyDisplayString();
        }

        #endregion

        internal TestAccessor GetTestAccessor() => new(this);

        internal readonly struct TestAccessor
        {
            private readonly FileViewer _fileViewer;

            public TestAccessor(FileViewer fileViewer)
            {
                _fileViewer = fileViewer;
            }

            public ToolStripMenuItem CopyToolStripMenuItem => _fileViewer.copyToolStripMenuItem;

            public FileViewerInternal FileViewerInternal => _fileViewer.internalFileViewer;

            public IgnoreWhitespaceKind IgnoreWhitespace
            {
                get => _fileViewer.IgnoreWhitespace;
                set => _fileViewer.IgnoreWhitespace = value;
            }

            public bool ShowSyntaxHighlightingInDiff
            {
                get => _fileViewer.ShowSyntaxHighlightingInDiff;
                set => _fileViewer.ShowSyntaxHighlightingInDiff = value;
            }

#if WINDOWS // TODO - mono
            public TaskDialogPage ResetSelectedLinesConfirmationDialog => _fileViewer._NO_TRANSLATE_resetSelectedLinesConfirmationDialog;
#endif
            public ToolStripButton IgnoreWhitespaceAtEolButton => _fileViewer.ignoreWhitespaceAtEol;
            public ToolStripMenuItem IgnoreWhitespaceAtEolMenuItem => _fileViewer.ignoreWhitespaceAtEolToolStripMenuItem;

            public ToolStripButton IgnoreWhiteSpacesButton => _fileViewer.ignoreWhiteSpaces;
            public ToolStripMenuItem IgnoreWhiteSpacesMenuItem => _fileViewer.ignoreWhitespaceChangesToolStripMenuItem;

            public ToolStripButton IgnoreAllWhitespacesButton => _fileViewer.ignoreAllWhitespaces;
            public ToolStripMenuItem IgnoreAllWhitespacesMenuItem => _fileViewer.ignoreAllWhitespaceChangesToolStripMenuItem;

            internal bool ExecuteCommand(Command command) => _fileViewer.ExecuteCommand((int)command);

            internal void IgnoreWhitespaceAtEolToolStripMenuItem_Click(object sender, EventArgs e) => _fileViewer.IgnoreWhitespaceAtEolToolStripMenuItem_Click(sender, e);
            internal void IgnoreWhitespaceChangesToolStripMenuItemClick(object sender, EventArgs e) => _fileViewer.IgnoreWhitespaceChangesToolStripMenuItemClick(sender, e);
            internal void IgnoreAllWhitespaceChangesToolStripMenuItem_Click(object sender, EventArgs e) => _fileViewer.IgnoreAllWhitespaceChangesToolStripMenuItem_Click(sender, e);

            public void ViewPatch(string? fileName,
                string text,
                Action? openWithDifftool = null)
            {
                FileStatusItem f = new(new GitRevision(ObjectId.Random()),
                    new GitRevision(ObjectId.Random()),
                    new GitItemStatus(name: fileName ?? ""));
                FileViewer fileViewer = _fileViewer;
                ThreadHelper.JoinableTaskFactory.Run(
                    () => fileViewer.ViewPatchAsync(f, text, line: null, openWithDifftool));
            }
        }
    }
}
