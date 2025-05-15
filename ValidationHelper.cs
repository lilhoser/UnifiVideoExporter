using System.Windows.Controls;
using System.Windows.Data;
using System.Windows;
using Microsoft.Xaml.Behaviors;

namespace UnifiVideoExporter
{
    using TextBox = System.Windows.Controls.TextBox;
    using CheckBox = System.Windows.Controls.CheckBox;
    using ListBox = System.Windows.Controls.ListBox;
    using ComboBox = System.Windows.Controls.ComboBox;

    //
    // This BindingHelper is used by CustomErrorTemplate defined in App.xaml during WPF form validation.
    // The template utilizes BindingHelper to extract the full path of the bound property on a control
    // that is participating in validation. The full path is needed because some viewmodels have properties that are themselves
    // classes with their own error tracking, and the full path provides a reliable way to disambiguate
    // if an error occurred on a property in the viewmodel or a property in the sub-class.
    //
    public static class BindingHelper
    {
        public static readonly DependencyProperty BindingPathProperty =
            DependencyProperty.RegisterAttached("BindingPath", typeof(string), typeof(BindingHelper));
        public static string GetBindingPath(DependencyObject obj) => (string)obj.GetValue(BindingPathProperty);
        public static void SetBindingPath(DependencyObject obj, string value) => obj.SetValue(BindingPathProperty, value);
    }

    public class BindingPathBehavior : Behavior<AdornedElementPlaceholder>
    {
        private static readonly Dictionary<Type, DependencyProperty> PrimaryProperties = new()
    {
        { typeof(TextBox), TextBox.TextProperty },
        { typeof(CheckBox), CheckBox.IsCheckedProperty },
        { typeof(ListBox), ListBox.SelectedItemProperty },
        { typeof(ComboBox), ComboBox.SelectedItemProperty },
        { typeof(Slider), Slider.ValueProperty },
        { typeof(DatePicker), DatePicker.SelectedDateProperty },
    };

        protected override void OnAttached()
        {
            base.OnAttached();
            UpdateBindingPath();
        }

        private void UpdateBindingPath()
        {
            if (AssociatedObject.AdornedElement is FrameworkElement element)
            {
                var controlType = element.GetType();
                var primaryProperty = PrimaryProperties.FirstOrDefault(kvp => kvp.Key.IsAssignableFrom(controlType)).Value;
                if (primaryProperty != null)
                {
                    var binding = BindingOperations.GetBindingExpression(element, primaryProperty);
                    if (binding != null)
                    {
                        BindingHelper.SetBindingPath(element, binding.ParentBinding.Path.Path);
                    }
                }
            }
        }
    }
}
