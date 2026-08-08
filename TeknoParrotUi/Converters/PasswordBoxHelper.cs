using System.Windows;
using System.Windows.Controls;

namespace TeknoParrotUi.Converters
{
    /// <summary>
    /// WPF deliberately does not allow binding PasswordBox.Password directly (it's not a
    /// dependency property, so the masked value can't accidentally end up retained somewhere
    /// via data binding infrastructure). This attached property bridges it to an ordinary
    /// bindable string, so FieldType.Password fields can bind FieldValue the same declarative
    /// way every other field type does.
    /// </summary>
    public static class PasswordBoxHelper
    {
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                // Default is null, not string.Empty, deliberately: a new/blank password field's
                // bound value starts as "" too, and WPF only invokes the change callback below
                // when the value actually differs from the current one. With "" as the default,
                // an initial "" -> "" binding application looks like no change at all, so the
                // callback (the only place PasswordChanged ever gets subscribed) would never
                // fire - meaning every keystroke into a brand-new password field would silently
                // go nowhere, and Save would still read back the original empty value. null can
                // never equal a real bound string (including ""), so the callback is guaranteed
                // to fire at least once on initial binding regardless of the starting value.
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnBoundPasswordChanged));

        private static readonly DependencyProperty UpdatingProperty =
            DependencyProperty.RegisterAttached("Updating", typeof(bool), typeof(PasswordBoxHelper), new PropertyMetadata(false));

        public static string GetBoundPassword(DependencyObject dp) => (string)dp.GetValue(BoundPasswordProperty);
        public static void SetBoundPassword(DependencyObject dp, string value) => dp.SetValue(BoundPasswordProperty, value);

        private static bool GetUpdating(DependencyObject dp) => (bool)dp.GetValue(UpdatingProperty);
        private static void SetUpdating(DependencyObject dp, bool value) => dp.SetValue(UpdatingProperty, value);

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(d is PasswordBox box))
                return;

            box.PasswordChanged -= HandlePasswordChanged;

            // Only push the new value in if it didn't originate from the user typing (avoids an
            // infinite loop: PasswordChanged -> updates BoundPassword -> triggers this handler
            // -> would reset the caret/selection mid-typing otherwise).
            if (!GetUpdating(box))
            {
                box.Password = e.NewValue as string ?? string.Empty;
            }

            box.PasswordChanged += HandlePasswordChanged;
        }

        private static void HandlePasswordChanged(object sender, RoutedEventArgs e)
        {
            var box = (PasswordBox)sender;
            SetUpdating(box, true);
            SetBoundPassword(box, box.Password);
            SetUpdating(box, false);
        }
    }
}
