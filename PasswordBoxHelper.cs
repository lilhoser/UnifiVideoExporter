using System.Windows;
using System.Windows.Controls;

namespace UnifiVideoExporter
{
    public static class PasswordBoxHelper
    {
        // Attached property to bind the password
        public static readonly DependencyProperty BoundPasswordProperty =
            DependencyProperty.RegisterAttached(
                "BoundPassword",
                typeof(string),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(string.Empty, OnBoundPasswordChanged));

        public static string GetBoundPassword(DependencyObject obj)
        {
            return (string)obj.GetValue(BoundPasswordProperty);
        }

        public static void SetBoundPassword(DependencyObject obj, string value)
        {
            obj.SetValue(BoundPasswordProperty, value);
        }

        private static void OnBoundPasswordChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is PasswordBox passwordBox)
            {
                // Update PasswordBox.Password when the bound property changes
                string newPassword = e.NewValue as string;
                if (passwordBox.Password != newPassword)
                {
                    passwordBox.Password = newPassword;
                }

                // Subscribe to PasswordChanged only once
                if (!IsPasswordBoxSubscribed(passwordBox))
                {
                    passwordBox.PasswordChanged += PasswordBox_PasswordChanged;
                    SetPasswordBoxSubscribed(passwordBox, true);
                }
            }
        }

        // Handle PasswordBox.PasswordChanged to update the bound property
        private static void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                // Update the attached property when the user types in the PasswordBox
                SetBoundPassword(passwordBox, passwordBox.Password);
            }
        }

        // Helper to track subscription to PasswordChanged event
        private static readonly DependencyProperty IsPasswordBoxSubscribedProperty =
            DependencyProperty.RegisterAttached(
                "IsPasswordBoxSubscribed",
                typeof(bool),
                typeof(PasswordBoxHelper),
                new PropertyMetadata(false));

        private static bool IsPasswordBoxSubscribed(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsPasswordBoxSubscribedProperty);
        }

        private static void SetPasswordBoxSubscribed(DependencyObject obj, bool value)
        {
            obj.SetValue(IsPasswordBoxSubscribedProperty, value);
        }
    }
}
