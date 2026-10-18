namespace ShadowVale.Core.Services
{
    /// <summary>
    /// Set while an overlay (the dev-build AI debug console) owns mouse and keyboard. Map code folds
    /// it into the player/camera/weapon input gates the same way it folds in its own menus.
    /// </summary>
    public static class InputFocus
    {
        public static bool OverlayOpen { get; set; }
    }
}
