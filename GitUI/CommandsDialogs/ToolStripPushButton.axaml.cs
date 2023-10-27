using Avalonia.Platform;
using GitCommands;
using GitCommands.Git;
using ResourceManager;

namespace GitUI.CommandsDialogs
{
    public partial class ToolStripPushButton : Avalonia.Controls.Button
    {
        protected override Type StyleKeyOverride => typeof(Avalonia.Controls.Button);

        private readonly TranslationString _push = new("Push");

        private readonly TranslationString _aheadCommitsToPush =
            new("{0} new commit(s) will be pushed");

        private readonly TranslationString _behindCommitsTointegrateOrForcePush =
            new("{0} commit(s) should be integrated (or will be lost if force pushed)");

        private IAheadBehindDataProvider? _aheadBehindDataProvider;

        public ToolStripPushButton()
        {
            InitializeComponent();
        }

        public void Initialize(IAheadBehindDataProvider? aheadBehindDataProvider)
        {
            _aheadBehindDataProvider = aheadBehindDataProvider;
            ResetToDefaultState();
        }

        public void DisplayAheadBehindInformation(IDictionary<string, AheadBehindData>? aheadBehindData, string branchName)
        {
            ResetToDefaultState();

            if (string.IsNullOrWhiteSpace(branchName) || !AppSettings.ShowAheadBehindData)
            {
                return;
            }

            if (aheadBehindData?.ContainsKey(branchName) is not true)
            {
                return;
            }

            AheadBehindData data = aheadBehindData[branchName];
            Text.Text = data.ToDisplay();
            Text.IsVisible = true;
            Avalonia.Controls.ToolTip.SetTip(this, GetToolTipText(data));

            if (!string.IsNullOrEmpty(data.BehindCount))
            {
                Image.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("..\\Resources\\Icons\\Unstage.png")));
            }
        }

        private void ResetToDefaultState()
        {
            Text.IsVisible = false;
            Image.Source = new Avalonia.Media.Imaging.Bitmap(AssetLoader.Open(new Uri("..\\Resources\\Icons\\Push.png")));
            Avalonia.Controls.ToolTip.SetTip(this, _push.Text);
        }

        private string? GetToolTipText(AheadBehindData data)
        {
            string? tooltip = null;
            if (!string.IsNullOrEmpty(data.AheadCount))
            {
                tooltip = string.Format(_aheadCommitsToPush.Text, data.AheadCount);
            }

            if (!string.IsNullOrEmpty(data.BehindCount))
            {
                if (!string.IsNullOrEmpty(tooltip))
                {
                    tooltip += Environment.NewLine;
                }

                tooltip += string.Format(_behindCommitsTointegrateOrForcePush.Text, data.BehindCount);
            }

            return tooltip;
        }

        internal TestAccessor GetTestAccessor()
            => new(this);

        internal readonly struct TestAccessor
        {
            private readonly ToolStripPushButton _button;

            public TestAccessor(ToolStripPushButton button)
            {
                _button = button;
            }

            public string? GetToolTipText(AheadBehindData data) => _button.GetToolTipText(data);
        }
    }
}
