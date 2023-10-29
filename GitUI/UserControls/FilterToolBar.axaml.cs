using Avalonia.Interactivity;
using Avalonia.Platform;
using GitCommands;
using GitExtUtils;
using GitUI.UserControls.RevisionGrid;
using GitUIPluginInterfaces;

namespace GitUI.UserControls
{
    internal partial class FilterToolBar : Avalonia.Controls.WrapPanel
    {
        internal const string ReflogButtonName = nameof(tsbShowReflog);
        private static readonly string[] _noResultsFound = { TranslatedStrings.NoResultsFound };
        private Func<IGitModule>? _getModule;
        private IRevisionGridFilter? _revisionGridFilter;
        private bool _isApplyingFilter;
        private bool _filterBeingChanged;
        private Func<RefsFilter, IReadOnlyList<IGitRef>> _getRefs;

        public FilterToolBar()
        {
            InitializeComponent();
            Avalonia.Controls.ToolTip.SetTip(tsbShowReflog, TranslatedStrings.ShowReflogTooltip);
            Avalonia.Controls.ToolTip.SetTip(tsmiShowOnlyFirstParent, TranslatedStrings.ShowOnlyFirstParent);

            // Select an option until we get a filter bound.
            SelectShowBranchesFilterOption(selectedIndex: 0);

#if false // TODO - Avalonia - not sure if this can be set this way, maybe through style?
            tscboBranchFilter.ComboBox.ResizeDropDownWidth(AppSettings.BranchDropDownMinWidth, AppSettings.BranchDropDownMaxWidth);
            tstxtRevisionFilter.ComboBox.ResizeDropDownWidth(AppSettings.BranchDropDownMinWidth, AppSettings.BranchDropDownMaxWidth);
#endif

            foreach (string revisionFilter in AppSettings.RevisionFilterDropdowns)
            {
                tstxtRevisionFilter.Items.Add(revisionFilter);
            }
        }

        /// <inheritdoc />
        protected override Type StyleKeyOverride => typeof(Avalonia.Controls.WrapPanel);

        private IRevisionGridFilter RevisionGridFilter
        {
            get => _revisionGridFilter ?? throw new InvalidOperationException($"{nameof(Bind)} is not called.");
        }

        /// <summary>
        ///  Applies the preset branch filters, such as "show all", "show current", and "show filtered".
        /// </summary>
        private void ApplyPresetBranchesFilter(Action filterAction)
        {
            _filterBeingChanged = true;

            // Action the filter
            filterAction();

            _filterBeingChanged = false;
        }

        /// <summary>
        ///  Applies custom branch filters supplied via the filter textbox.
        /// </summary>
        private void ApplyCustomBranchFilter(bool checkBranch = true)
        {
            if (_isApplyingFilter)
            {
                return;
            }

            _isApplyingFilter = true;

#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
            // The user has accepted the filter
            _filterBeingChanged = false;

            // Apply the textbox contents, no check if the (multiple) options is in tscboBranchFilter.Items (or that the list is generated)
            string filter = tscboBranchFilter.Text == TranslatedStrings.NoResultsFound ? string.Empty : tscboBranchFilter.Text;
            if (checkBranch && !string.IsNullOrWhiteSpace(filter))
            {
                List<string> newFilter = new();
                IReadOnlyList<IGitRef> refs = _getRefs(RefsFilter.NoFilter);

                // Split at whitespace (char[])null is default) but with split options.
                // Ignore quoting, Git revisions do not allow spaces.
                foreach (string branch in filter.Split((char[])null, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    bool wildcardBranchFilter = branch.IndexOfAny(new[] { '?', '*', '[' }) >= 0;
                    if (branch.StartsWith("--") || refs.Any(r => r.LocalName == branch))
                    {
                        // Added as git-log option or revision filter
                    }
                    else if (wildcardBranchFilter)
                    {
                        // Added as --branches= option
                    }
                    else
                    {
                        ObjectId oid = GetModule().RevParse(branch);
                        if (oid is null)
                        {
                            TaskDialogPage page = new()
                            {
                                Heading = string.Format(TranslatedStrings.IgnoringReference, branch),
                                Caption = TranslatedStrings.NonexistingGitRevision,
                                Buttons = { TaskDialogButton.OK },
                                Icon = TaskDialogIcon.Warning,
                                SizeToContent = true
                            };

                            TaskDialog.ShowDialog(this, page);
                            continue;
                        }
                    }

                    newFilter.Add(branch);
                }

                filter = string.Join(" ", newFilter);
            }

            RevisionGridFilter.SetAndApplyBranchFilter(filter);

#endif
            _isApplyingFilter = false;
        }

        private void ApplyRevisionFilter()
        {
#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
            if (_isApplyingFilter)
            {
                return;
            }

            _isApplyingFilter = true;
            RevisionGridFilter.SetAndApplyRevisionFilter(new RevisionFilter(tstxtRevisionFilter.Text.Trim(),
                                                                            tsmiCommitFilter.Checked,
                                                                            tsmiCommitterFilter.Checked,
                                                                            tsmiAuthorFilter.Checked,
                                                                            tsmiDiffContainsFilter.Checked));
            _isApplyingFilter = false;
#endif
        }

        public void Bind(Func<IGitModule> getModule, IRevisionGridFilter revisionGridFilter)
        {
            _getModule = getModule ?? throw new ArgumentNullException(nameof(getModule));

            DebugHelpers.Assert(_revisionGridFilter is null, $"{nameof(Bind)} must be invoked only once.");
            _revisionGridFilter = revisionGridFilter ?? throw new ArgumentNullException(nameof(revisionGridFilter));
            _revisionGridFilter.FilterChanged += revisionGridFilter_FilterChanged;
        }

        public void ClearQuickFilters()
        {
#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
            tscboBranchFilter.Text =
                tstxtRevisionFilter.Text = string.Empty;
#endif
        }

        private IGitModule GetModule()
        {
            if (_getModule is null)
            {
                throw new InvalidOperationException($"{nameof(Bind)} is not called.");
            }

            IGitModule module = _getModule();
            if (module is null)
            {
                throw new ArgumentException($"Require a valid instance of {nameof(GitModule)}");
            }

            return module;
        }

        private void InitBranchSelectionFilter(FilterChangedEventArgs e)
        {
            // Note: it is a weird combination, and it is mimicking the implementations in RevisionGridControl.
            // Refer to it for more details.

            Avalonia.Controls.MenuItem selectedItem = tsmiShowBranchesAll;

            if (e.ShowAllBranches)
            {
                // Show all branches
                selectedItem = tsmiShowBranchesAll;
            }

            if (e.ShowFilteredBranches)
            {
                // Show filtered branches
                selectedItem = tsmiShowBranchesFiltered;

#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
                // Keep value if other filter
                tscboBranchFilter.Text = e.BranchFilter;
#endif
            }

            if (e.ShowCurrentBranchOnly)
            {
                // Show current branch only
                selectedItem = tsmiShowBranchesCurrent;
            }

            int selectedIndex = ((Avalonia.Controls.MenuFlyout)tssbtnShowBranches.Flyout!).Items.IndexOf(selectedItem);
            SelectShowBranchesFilterOption(selectedIndex);
        }

        public void InitToolStripStyles(Color toolForeColor, Color toolBackColor)
        {
#if false // TODO - Avalonia
            tsddbtnRevisionFilter.BackColor = toolBackColor;
            tsddbtnRevisionFilter.ForeColor = toolForeColor;

            Color toolTextBoxBackColor = SystemColors.Window;
            tscboBranchFilter.BackColor = toolTextBoxBackColor;
            tscboBranchFilter.ForeColor = toolForeColor;
            tstxtRevisionFilter.BackColor = toolTextBoxBackColor;
            tstxtRevisionFilter.ForeColor = toolForeColor;
#endif
        }

        private void SelectShowBranchesFilterOption(int selectedIndex)
        {
            if (selectedIndex >= ((Avalonia.Controls.MenuFlyout)tssbtnShowBranches.Flyout!).Items.Count)
            {
                selectedIndex = 0;
            }

            Avalonia.Controls.MenuItem selectedMenuItem = (Avalonia.Controls.MenuItem)((Avalonia.Controls.MenuFlyout)tssbtnShowBranches.Flyout!).Items[selectedIndex]!;
            tssbtnShowBranchesImage.Source = ((Avalonia.Controls.Image)selectedMenuItem.Icon).Source;
            tssbtnShowBranchesText.Text = ((string)selectedMenuItem.Header!).Replace("_", string.Empty);
            Avalonia.Controls.ToolTip.SetTip(tssbtnShowBranches, Avalonia.Controls.ToolTip.GetTip(selectedMenuItem));
        }

        /// <summary>
        ///  Sets the branches filter.
        ///  No check that the branches exist (must be checked already, expected to be called from left panel).
        /// </summary>
        /// <param name="filter">The branches to filter separated by whitespace.</param>
        public void SetBranchFilter(string? filter)
        {
#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
            tscboBranchFilter.Text = filter;
#endif
            ApplyCustomBranchFilter(checkBranch: false);
        }

        /// <summary>
        /// If focus on branch filter, focus revision filter otherwise branch filter.
        /// </summary>
        public void SetFocus()
        {
            Avalonia.Controls.ComboBox filterToFocus = tstxtRevisionFilter.IsFocused
                ? tscboBranchFilter
                : tstxtRevisionFilter;
            filterToFocus.Focus();
        }

        /// <summary>
        ///  Sets the revision filter.
        /// </summary>
        /// <param name="filter">The filter to apply.</param>
        public void SetRevisionFilter(string? filter)
        {
#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
            if (string.IsNullOrEmpty(tstxtRevisionFilter.Text) && string.IsNullOrEmpty(filter))
            {
                // The current filter is empty and the new filter is empty. No-op
                return;
            }

            tstxtRevisionFilter.Text = filter;
            ApplyRevisionFilter();
#endif
        }

        /// <summary>
        /// Update the function to get refs for branch dropdown filter
        /// </summary>
        /// <param name="getRefs">Function to get refs, expected to be cached</param>
        public void RefreshRevisionFunction(Func<RefsFilter, IReadOnlyList<IGitRef>> getRefs)
        {
            _getRefs = getRefs;
            tscboBranchFilter.Items.Clear();
        }

        /// <summary>
        /// Update the tscboBranchFilter dropdown items matching the current filter.
        /// This is called when dropdown clicked or text is manually changed
        /// (so tscboBranchFilter.Items is not necessarily available when set externally
        /// from the left panel or FormBrowse).
        /// </summary>
        private void UpdateBranchFilterItems()
        {
            IGitModule module = GetModule();
            if (!module.IsValidGitWorkingDir())
            {
                IsEnabled = false;
                return;
            }

            IsEnabled = true;
            ThreadHelper.FileAndForget(async () =>
            {
                if (_getRefs is null)
                {
                    DebugHelpers.Fail("getRefs is unexpectedly null");
                    return;
                }

                RefsFilter branchesFilter = BranchesFilter();
                IReadOnlyList<IGitRef> refs = _getRefs(branchesFilter);
                string[] branches = refs.Select(branch => branch.Name).ToArray();

                await this.SwitchToMainThreadAsync();
                BindBranches(branches);
            });

            return;

            RefsFilter BranchesFilter()
            {
                // Options are interpreted as the refs the search should be limited too
                // If neither option is selected all refs will be queried also including stash and notes
                RefsFilter refs = (tsmiBranchLocal.IsChecked == true ? RefsFilter.Heads : RefsFilter.NoFilter)
                    | (tsmiBranchTag.IsChecked == true ? RefsFilter.Tags : RefsFilter.NoFilter)
                    | (tsmiBranchRemote.IsChecked == true ? RefsFilter.Remotes : RefsFilter.NoFilter);
                return refs;
            }

            void BindBranches(string[] branches)
            {
                IEnumerable<string> autoCompleteList = tscboBranchFilter.Items.Cast<string>();
                if (!autoCompleteList.SequenceEqual(branches))
                {
                    tscboBranchFilter.Items.Clear();

                    foreach (string branch in branches)
                    {
                        tscboBranchFilter.Items.Add(branch);
                    }
                }

#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
                string filter = tscboBranchFilter.Items.Count > 0 ? tscboBranchFilter.Text : string.Empty;
                string[] matches = branches.Where(branch => branch.IndexOf(filter, StringComparison.InvariantCultureIgnoreCase) >= 0).ToArray();

                if (matches.Length == 0)
                {
                    matches = _noResultsFound;
                }

                int index = tscboBranchFilter.SelectionStart;
                tscboBranchFilter.Items.Clear();
                tscboBranchFilter.Items.AddRange(matches);
                tscboBranchFilter.SelectionStart = index;
#endif
            }
        }

        public void SetShortcutKeys(Action<Avalonia.Controls.MenuItem, RevisionGridControl.Command> setShortcutString)
        {
            setShortcutString(tsmiResetPathFilters, RevisionGridControl.Command.ResetRevisionPathFilter);
            setShortcutString(tsmiResetAllFilters, RevisionGridControl.Command.ResetRevisionFilter);
            setShortcutString(tsmiAdvancedFilter, RevisionGridControl.Command.RevisionFilter);
        }

        private void revisionGridFilter_FilterChanged(object? sender, FilterChangedEventArgs e)
        {
            tsmiShowOnlyFirstParent.IsChecked = e.ShowOnlyFirstParent;
            tsbShowReflog.IsChecked = e.ShowReflogReferences;
            InitBranchSelectionFilter(e);

            List<(string filter, Avalonia.Controls.CheckBox menuItem)> revFilters = new()
            {
                (e.MessageFilter, tsmiCommitFilter),
                (e.CommitterFilter, tsmiCommitterFilter),
                (e.AuthorFilter, tsmiAuthorFilter),
                (e.DiffContentFilter, tsmiDiffContainsFilter),
            };

#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
            // If there is no filter in filterInfo, clear text but retain checks
            tstxtRevisionFilter.Text = "";
            if (revFilters.Any(item => !string.IsNullOrWhiteSpace(item.filter)))
            {
                foreach ((string filter, Avalonia.Controls.CheckBox menuItem) item in revFilters)
                {
                    // Check the first menuitem that matches and following identical filters
                    if (!string.IsNullOrWhiteSpace(item.filter)
                        && (string.IsNullOrWhiteSpace(tstxtRevisionFilter.Text)
                            || item.filter == tstxtRevisionFilter.Text))
                    {
                        tstxtRevisionFilter.Text = item.filter;
                        item.menuItem.IsChecked = true;
                    }
                    else
                    {
                        item.menuItem.IsChecked = false;
                    }
                }
            }

            // Add to dropdown and settings, unless already included
            string filter = tstxtRevisionFilter.Text.Trim();
            if (!string.IsNullOrWhiteSpace(filter) && (tstxtRevisionFilter.Items.Count == 0 || filter != (string)tstxtRevisionFilter.Items[0]))
            {
                if (tstxtRevisionFilter.Items.Contains(filter))
                {
                    tstxtRevisionFilter.Items.Remove(filter);
                }

                tstxtRevisionFilter.Items.Insert(0, filter);
                tstxtRevisionFilter.Text = filter;
                const int maxFilterItems = 30;
                AppSettings.RevisionFilterDropdowns = tstxtRevisionFilter.Items.Cast<object>()
                    .Select(item => item.ToString()).Take(maxFilterItems).ToArray();
            }
#endif

            Avalonia.Controls.ToolTip.SetTip(tsbtnAdvancedFilter, e.FilterSummary);
            tsbtnAdvancedFilterImage.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri(e.HasFilter ? "..\\Resources\\Icons\\FunnelExclamation.png" : "..\\Resources\\Icons\\FunnelPencil.png")));
            tsmiResetPathFilters.IsEnabled = !string.IsNullOrEmpty(e.PathFilter);
            tsmiResetAllFilters.IsEnabled = e.HasFilter;
        }

        private void revisionFilterBox_CheckedChanged(object sender, RoutedEventArgs e)
        {
#if false // TODO - avalonia - IsEditable not supported by ComboBox - https://github.com/AvaloniaUI/Avalonia/issues/205
            if (!string.IsNullOrWhiteSpace(tstxtRevisionFilter.Text))
#endif
            {
                ApplyRevisionFilter();
            }
        }

        private void tsbtnAdvancedFilter_ButtonClick(object sender, RoutedEventArgs e)
        {
            if (!tsmiResetAllFilters.IsEnabled)
            {
                RevisionGridFilter.ShowRevisionFilterDialog();
            }
            else
            {
                tsbtnAdvancedFilter.Flyout!.ShowAt(tsbtnAdvancedFilter);
            }
        }

        private void tstxtRevisionFilter_KeyUp(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                ApplyRevisionFilter();
            }
        }

        private void tscboBranchFilter_Click(object sender, RoutedEventArgs e)
        {
            if (!tscboBranchFilter.IsDropDownOpen)
            {
                tscboBranchFilter.IsDropDownOpen = true;
            }
        }

        private void tscboBranchFilter_DropDown(object sender, EventArgs e)
        {
            UpdateBranchFilterItems();
        }

        private void tscboBranchFilter_KeyUp(object sender, Avalonia.Input.KeyEventArgs e)
        {
            if (e.Key == Avalonia.Input.Key.Enter)
            {
                ApplyCustomBranchFilter(checkBranch: true);
            }
        }

        private void tscboBranchFilter_TextChanged(object sender, EventArgs e)
        {
            _filterBeingChanged = true;
        }

        private void tscboBranchFilter_TextUpdate(object sender, Avalonia.Input.TextInputEventArgs e)
        {
            _filterBeingChanged = true;
            UpdateBranchFilterItems();
        }

        private void tsmiDisablePathFilters_Click(object sender, RoutedEventArgs e) => RevisionGridFilter.SetAndApplyPathFilter("");

        private void tsmiDisableAllFilters_Click(object sender, RoutedEventArgs e) => RevisionGridFilter.ResetAllFiltersAndRefresh();

        private void tsmiAdvancedFilter_Click(object sender, RoutedEventArgs e) => RevisionGridFilter.ShowRevisionFilterDialog();

        private void tsmiShowReflogBranches_Click(object sender, RoutedEventArgs e) => ApplyPresetBranchesFilter(RevisionGridFilter.ShowReflog);

        private void tsmiShowBranchesAll_Click(object sender, RoutedEventArgs e) => ApplyPresetBranchesFilter(RevisionGridFilter.ShowAllBranches);

        private void tsmiShowBranchesCurrent_Click(object sender, RoutedEventArgs e) => ApplyPresetBranchesFilter(RevisionGridFilter.ShowCurrentBranchOnly);

        private void tsmiShowBranchesFiltered_Click(object sender, RoutedEventArgs e) => ApplyPresetBranchesFilter(RevisionGridFilter.ShowFilteredBranches);

        private void tsmiShowOnlyFirstParent_Click(object sender, RoutedEventArgs e) => RevisionGridFilter.ToggleShowOnlyFirstParent();

        private void tsmiShowReflog_Click(object sender, RoutedEventArgs e) => RevisionGridFilter.ToggleShowReflogReferences();

        private void tssbtnShowBranches_Click(object sender, RoutedEventArgs e)
            => tssbtnShowBranches.Flyout!.ShowAt(tssbtnShowBranches);

        private void tsmiBranchLocal_OnClick(object? sender, RoutedEventArgs e)
            => tsmiBranchLocal.IsChecked = !tsmiBranchLocal.IsChecked;

        private void tsmiBranchRemote_OnClick(object? sender, RoutedEventArgs e)
            => tsmiBranchRemote.IsChecked = !tsmiBranchRemote.IsChecked;

        private void tsmiBranchTag_OnClick(object? sender, RoutedEventArgs e)
            => tsmiBranchTag.IsChecked = !tsmiBranchTag.IsChecked;

        private void tsmiCommitFilter_OnClick(object? sender, RoutedEventArgs e)
            => tsmiCommitFilter.IsChecked = !tsmiCommitFilter.IsChecked;

        private void tsmiCommitterFilter_OnClick(object? sender, RoutedEventArgs e)
            => tsmiCommitterFilter.IsChecked = !tsmiCommitterFilter.IsChecked;

        private void tsmiAuthorFilter_OnClick(object? sender, RoutedEventArgs e)
            => tsmiAuthorFilter.IsChecked = !tsmiAuthorFilter.IsChecked;

        private void tsmiDiffContainsFilter_OnClick(object? sender, RoutedEventArgs e)
            => tsmiDiffContainsFilter.IsChecked = !tsmiDiffContainsFilter.IsChecked;

        internal TestAccessor GetTestAccessor()
            => new(this);

        internal readonly struct TestAccessor
        {
            private readonly FilterToolBar _control;

            public TestAccessor(FilterToolBar control)
            {
                _control = control;
            }

            public Avalonia.Controls.CheckBox tsmiBranchLocal => _control.tsmiBranchLocal;
            public Avalonia.Controls.CheckBox tsmiBranchRemote => _control.tsmiBranchRemote;
            public Avalonia.Controls.CheckBox tsmiBranchTag => _control.tsmiBranchTag;
            public Avalonia.Controls.CheckBox tsmiCommitFilter => _control.tsmiCommitFilter;
            public Avalonia.Controls.CheckBox tsmiCommitterFilter => _control.tsmiCommitterFilter;
            public Avalonia.Controls.CheckBox tsmiAuthorFilter => _control.tsmiAuthorFilter;
            public Avalonia.Controls.CheckBox tsmiDiffContainsFilter => _control.tsmiDiffContainsFilter;
            public Avalonia.Controls.Primitives.ToggleButton tsmiShowOnlyFirstParent => _control.tsmiShowOnlyFirstParent;
            public Avalonia.Controls.Button tsbShowReflog => _control.tsbShowReflog;
            public Avalonia.Controls.ComboBox tstxtRevisionFilter => _control.tstxtRevisionFilter;
            public Avalonia.Controls.Label tslblRevisionFilter => _control.tslblRevisionFilter;
            public Avalonia.Controls.SplitButton tsbtnAdvancedFilter => _control.tsbtnAdvancedFilter;
            public Avalonia.Controls.SplitButton tssbtnShowBranches => _control.tssbtnShowBranches;
            public Avalonia.Controls.MenuItem tsmiShowBranchesAll => _control.tsmiShowBranchesAll;
            public Avalonia.Controls.MenuItem tsmiShowBranchesCurrent => _control.tsmiShowBranchesCurrent;
            public Avalonia.Controls.MenuItem tsmiShowBranchesFiltered => _control.tsmiShowBranchesFiltered;
            public Avalonia.Controls.ComboBox tscboBranchFilter => _control.tscboBranchFilter;
            public void RefreshRevisionFunction(Func<RefsFilter, IReadOnlyList<IGitRef>> getRefs) => _control.RefreshRevisionFunction(getRefs);
            public Avalonia.Controls.Button tsddbtnBranchFilter => _control.tsddbtnBranchFilter;
            public Avalonia.Controls.Button tsddbtnRevisionFilter => _control.tsddbtnRevisionFilter;
            public bool _isApplyingFilter => _control._isApplyingFilter;
            public bool _filterBeingChanged => _control._filterBeingChanged;

            public IRevisionGridFilter RevisionGridFilter => _control.RevisionGridFilter;

            public void ApplyCustomBranchFilter(bool checkBranch) => _control.ApplyCustomBranchFilter(checkBranch);

            public void ApplyRevisionFilter() => _control.ApplyRevisionFilter();

            public IGitModule GetModule() => _control.GetModule();
        }
    }
}
