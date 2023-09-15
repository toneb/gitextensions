namespace GitExtUtils.GitUI.Theming
{
    public static class FormsColorHelper
    {
        public static void SetForeColorForBackColor(this Control control) =>
            control.ForeColor = ColorHelper.GetForeColorForBackColor(control.BackColor);

        public static void SetForeColorForBackColor(this ToolStripItem control) =>
            control.ForeColor = ColorHelper.GetForeColorForBackColor(control.BackColor);

        public static void AdaptImageLightness(this ToolStripItem item) =>
            item.Image = ((Bitmap)item.Image)?.AdaptLightness();

        public static void AdaptImageLightness(this ButtonBase button) =>
            button.Image = ((Bitmap)button.Image)?.AdaptLightness();
    }
}
