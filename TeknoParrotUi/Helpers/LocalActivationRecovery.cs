using System;
using System.Globalization;
using System.Windows;

namespace TeknoParrotUi.Helpers
{
    internal static class LocalActivationRecovery
    {
        // Returns true when this error is handled, including a declined confirmation.
        internal static bool TryHandle(Window owner, Exception error, out bool removed)
        {
            removed = false;
            if (!(error is BudgieDeactivationException deactivation) || !deactivation.CanRemoveLocalActivation)
                return false;

            try
            {
                removed = deactivation.TryRemoveLocalActivation(() =>
                    Show(owner,
                        string.Format(Properties.Resources.LocalActivationRemovalPrompt, error.Message),
                        MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes);

                if (removed)
                    Show(owner, Properties.Resources.LocalActivationRemoved, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception removalError)
            {
                Show(owner, removalError.Message, MessageBoxButton.OK, MessageBoxImage.Error);
            }
            return true;
        }

        private static MessageBoxResult Show(Window owner, string text, MessageBoxButton buttons,
            MessageBoxImage image, MessageBoxResult defaultResult = MessageBoxResult.OK)
        {
            var culture = Properties.Resources.Culture ?? CultureInfo.CurrentUICulture;
            var options = culture.TextInfo.IsRightToLeft
                ? MessageBoxOptions.RtlReading | MessageBoxOptions.RightAlign : MessageBoxOptions.None;
            var title = Properties.Resources.LocalActivationRemovalTitle;
            return owner == null
                ? MessageBox.Show(text, title, buttons, image, defaultResult, options)
                : MessageBox.Show(owner, text, title, buttons, image, defaultResult, options);
        }
    }
}
