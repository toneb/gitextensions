﻿using System.ComponentModel;
using System.Net;
using GitCommands;
using GitCommands.Settings;
using GitUI.UserControls;
using GitUIPluginInterfaces;
using GitUIPluginInterfaces.BuildServerIntegration;
using Microsoft;

namespace GitUI.CommandsDialogs
{
    public class BuildReportTabPageExtension
    {
        private readonly TabControl _tabControl;
        private readonly string _caption;
        private readonly Func<IGitModule> _getModule;

        private TabPage? _buildReportTabPage;
#if WINDOWS // TODO - mono
        private WebBrowserControl? _buildReportWebBrowser;
#endif
        private GitRevision? _selectedGitRevision;
        private string? _url;
        private readonly LinkLabel _openReportLink = new() { AutoSize = false, Text = TranslatedStrings.OpenReport, TextAlign = ContentAlignment.MiddleCenter, Dock = DockStyle.Fill };

        public Control? Control { get; private set; } // for focusing

        public BuildReportTabPageExtension(Func<IGitModule> getModule, TabControl tabControl, string caption)
        {
            _getModule = getModule;
            _tabControl = tabControl;
            _caption = caption;

            _openReportLink.Click += (o, args) =>
            {
                if (!string.IsNullOrWhiteSpace(_url))
                {
                    OsShellUtil.OpenUrlInDefaultBrowser(_url);
                }
            };
            _openReportLink.Font = new Font(_openReportLink.Font.Name, 16F);
        }

        public void FillBuildReport(GitRevision? revision)
        {
            SetSelectedRevision(revision);

            _tabControl.SuspendLayout();

            try
            {
                bool buildResultPageEnabled = revision is not null && IsBuildResultPageEnabled();
                bool buildInfoIsAvailable = !string.IsNullOrEmpty(revision?.BuildStatus?.Url);

                if (buildResultPageEnabled && buildInfoIsAvailable)
                {
                    Validates.NotNull(revision);

                    if (_buildReportTabPage is null)
                    {
                        CreateBuildReportTabPage(_tabControl);
                        Validates.NotNull(_buildReportTabPage);
                    }

                    _buildReportTabPage.Controls.Clear();

                    SetTabPageContent(revision);

                    bool isFavIconMissing = _buildReportTabPage.ImageIndex < 0;

                    if (isFavIconMissing || _tabControl.SelectedTab == _buildReportTabPage)
                    {
                        LoadReportContent(revision, isFavIconMissing);
                    }

                    if (!_tabControl.Controls.Contains(_buildReportTabPage))
                    {
                        _tabControl.Controls.Add(_buildReportTabPage);
                    }
                }
                else
                {
#if WINDOWS // TODO - mono
                    if (_buildReportTabPage is not null && _buildReportWebBrowser is not null && _tabControl.Controls.Contains(_buildReportTabPage))
                    {
                        _buildReportWebBrowser.Stop();
                        _buildReportWebBrowser.Document.Write(string.Empty);
                        _tabControl.Controls.Remove(_buildReportTabPage);
                    }
#endif
                }
            }
            finally
            {
                _tabControl.ResumeLayout();
            }
        }

        private void LoadReportContent(GitRevision revision, bool isFavIconMissing)
        {
#if WINDOWS // TODO - mono
            Validates.NotNull(_buildReportWebBrowser);

            try
            {
                if (revision.BuildStatus?.ShowInBuildReportTab == true)
                {
                    _buildReportWebBrowser.Navigate(revision.BuildStatus.Url);
                }

                if (isFavIconMissing)
                {
                    _buildReportWebBrowser.Navigated += BuildReportWebBrowserOnNavigated;
                }
            }
            catch
            {
                // No propagation to the user if the report fails
            }
#endif
        }

        private void SetTabPageContent(GitRevision revision)
        {
            Validates.NotNull(_buildReportTabPage);

            if (revision.BuildStatus?.ShowInBuildReportTab == true)
            {
                _url = null;
#if WINDOWS // TODO - mono
                Control = _buildReportWebBrowser;
                _buildReportTabPage.Controls.Add(_buildReportWebBrowser);
#endif
            }
            else
            {
                _url = revision.BuildStatus?.Url;
                _buildReportTabPage.Cursor = Cursors.Hand;
                Control = _openReportLink;
                _buildReportTabPage.Controls.Add(_openReportLink);
            }
        }

        private void SetSelectedRevision(GitRevision? revision)
        {
            if (_selectedGitRevision is not null)
            {
                _selectedGitRevision.PropertyChanged -= RevisionPropertyChanged;
            }

            _selectedGitRevision = revision;

            if (_selectedGitRevision is not null)
            {
                _selectedGitRevision.PropertyChanged += RevisionPropertyChanged;
            }
        }

        private void RevisionPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(GitRevision.BuildStatus))
            {
                // Refresh the selected Git revision
                FillBuildReport(_selectedGitRevision);
            }
        }

        private void CreateBuildReportTabPage(TabControl tabControl)
        {
            _buildReportTabPage = new TabPage
            {
                Padding = new Padding(3),
                TabIndex = tabControl.Controls.Count,
                Text = _caption,
                UseVisualStyleBackColor = true
            };

#if WINDOWS // TODO - mono
            _buildReportWebBrowser = new WebBrowserControl
            {
                Dock = DockStyle.Fill
            };
#endif
        }

#if WINDOWS // TODO - mono
        private void BuildReportWebBrowserOnNavigated(object sender,
                                                      WebBrowserNavigatedEventArgs webBrowserNavigatedEventArgs)
        {
            Validates.NotNull(_buildReportWebBrowser);
            Validates.NotNull(_buildReportTabPage);

            _buildReportWebBrowser.Navigated -= BuildReportWebBrowserOnNavigated;

            string favIconUrl = DetermineFavIconUrl(_buildReportWebBrowser.Document);

            if (favIconUrl is not null)
            {
                ThreadHelper.FileAndForget(async () =>
                    {
                        using Stream imageStream = await DownloadRemoteImageFileAsync(favIconUrl);
                        if (imageStream is not null)
                        {
                            await _tabControl.SwitchToMainThreadAsync();

                            Image favIconImage = Image.FromStream(imageStream)
                                                    .GetThumbnailImage(16, 16, null, IntPtr.Zero);
                            ImageList.ImageCollection imageCollection = _tabControl.ImageList.Images;
                            int imageIndex = _buildReportTabPage.ImageIndex;

                            if (imageIndex < 0)
                            {
                                _buildReportTabPage.ImageIndex = imageCollection.Count;
                                imageCollection.Add(favIconImage);
                            }
                            else
                            {
                                imageCollection[imageIndex] = favIconImage;
                            }

                            _tabControl.Invalidate(false);
                        }
                    });
            }
        }
#endif

        private bool IsBuildResultPageEnabled()
        {
            IBuildServerSettings buildServerSettings = GetModule().GetEffectiveSettings().GetBuildServerSettings();
            return buildServerSettings.ShowBuildResultPageOrDefault;
        }

        private IGitModule GetModule()
        {
            IGitModule module = _getModule();

            if (module is null)
            {
                throw new ArgumentException($"Require a valid instance of {nameof(IGitModule)}");
            }

            return module;
        }

#if WINDOWS // TODO - mono
        private static string? DetermineFavIconUrl(HtmlDocument htmlDocument)
        {
            HtmlElementCollection links = htmlDocument.GetElementsByTagName("link");
            HtmlElement favIconLink =
                links.Cast<HtmlElement>()
                     .SingleOrDefault(x => x.GetAttribute("rel").ToLowerInvariant() == "shortcut icon");

            if (favIconLink is null || htmlDocument.Url is null)
            {
                return null;
            }

            string href = favIconLink.GetAttribute("href");

            if (htmlDocument.Url.PathAndQuery == "/")
            {
                // Scenario: http://test.test/teamcity/....
                return htmlDocument.Url.AbsoluteUri.Replace(htmlDocument.Url.PathAndQuery, href);
            }
            else
            {
                // Scenario: http://teamcity.domain.test/
                return new Uri(new Uri(htmlDocument.Url.AbsoluteUri), href).ToString();
            }
        }
#endif

        private static async Task<Stream?> DownloadRemoteImageFileAsync(string uri)
        {
#pragma warning disable SYSLIB0014 // 'WebRequest.Create(string)' is obsolete
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uri);
#pragma warning restore SYSLIB0014 // 'WebRequest.Create(string)' is obsolete

            HttpWebResponse response = await GetWebResponseAsync(request).ConfigureAwait(false);

            // Check that the remote file was found. The ContentType
            // check is performed since a request for a non-existent
            // image file might be redirected to a 404-page, which would
            // yield the StatusCode "OK", even though the image was not
            // found.
            if ((response.StatusCode == HttpStatusCode.OK ||
                    response.StatusCode == HttpStatusCode.Moved ||
                    response.StatusCode == HttpStatusCode.Redirect) &&
                response.ContentType.StartsWith("image", StringComparison.OrdinalIgnoreCase))
            {
                // if the remote file was found, download it
                return response.GetResponseStream();
            }

            return null;
        }

        private static Task<HttpWebResponse> GetWebResponseAsync(HttpWebRequest webRequest)
        {
            return Task<HttpWebResponse>.Factory.FromAsync(
                webRequest.BeginGetResponse,
                ar => (HttpWebResponse)webRequest.EndGetResponse(ar),
                null);
        }
    }
}
