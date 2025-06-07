using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace TeknoParrotUi.UserControls
{
    public class KeyCaptureTextBox : TextBox
    {
        public static readonly DependencyProperty VirtualKeyProperty = DependencyProperty.Register(
            nameof(VirtualKey), typeof(int), typeof(KeyCaptureTextBox), 
            new PropertyMetadata(0, OnVirtualKeyChanged));

        public int VirtualKey
        {
            get => (int)GetValue(VirtualKeyProperty);
            set => SetValue(VirtualKeyProperty, value);
        }

        private bool _isCapturing;
        
        public KeyCaptureTextBox()
        {
            IsReadOnly = true;
            GotFocus += OnGotFocus;
            LostFocus += OnLostFocus;
            PreviewKeyDown += OnPreviewKeyDown;
            
            UpdateText();
        }

        private static void OnVirtualKeyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is KeyCaptureTextBox textBox)
            {
                textBox.UpdateText();
            }
        }
        
        private void OnGotFocus(object sender, RoutedEventArgs e)
        {
            _isCapturing = true;
            Text = Properties.Resources.KeyCapturePressAnyKey;
        }

        private void OnLostFocus(object sender, RoutedEventArgs e)
        {
            _isCapturing = false;
            UpdateText();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (!_isCapturing) return;
            // Need to make sure to check for SystemKeys, like Score Submissions default F10 key
            Key key = e.Key == Key.System ? e.SystemKey : e.Key;
            int vkey = KeyInterop.VirtualKeyFromKey(key);
            bool sameKey = VirtualKey == vkey;
            VirtualKey = vkey;
            
            if (sameKey)
            {
                UpdateText();
            }
            
            Keyboard.ClearFocus();
            e.Handled = true;
        }

        private void UpdateText()
        {
            if (VirtualKey == 0)
            {
                Text = Properties.Resources.KeyCaptureNone;
                return;
            }

            var key = KeyInterop.KeyFromVirtualKey(VirtualKey);
            Text = $"{key} (0x{VirtualKey:X2})";
        }
    }
}