namespace TeknoParrotUi.Common.Proton
{
    /// <summary>
    /// Plain (Avalonia-free) monitor identity snapshot the UI layer supplies
    /// via <see cref="LinuxDisplayResolver.AvaloniaScreenIdentityProvider"/> -
    /// deliberately contains no Avalonia types (Screen/PixelRect/etc.) so
    /// Common stays UI-framework-agnostic. Captured on the UI thread by the
    /// Avalonia layer, same as the existing width/height provider.
    /// </summary>
    public sealed class AvaloniaScreenIdentity
    {
        /// <summary>Best available distinguishing identifier for the screen (e.g. Avalonia's DisplayName), or null if unavailable.</summary>
        public string Identifier { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public double Scaling { get; init; } = 1.0;
        public DisplaySelectionReason SelectionReason { get; init; } = DisplaySelectionReason.Unknown;
    }
}
