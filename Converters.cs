using System.Diagnostics;
using System.Globalization;
using System.Windows.Data;

namespace UnifiVideoExporter
{
    public class BooleanToButtonTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            var button = parameter as string;
            if (!string.IsNullOrEmpty(button))
            {
                if (button == "DownloadVideoButton")
                {
                    return (bool)value ? "Cancel" : "Download";
                }
                else if (button == "CreateTimelapseButton")
                {
                    return (bool)value ? "Cancel" : "Create Timelapse";
                }
            }
            Debug.Assert(false);
            return string.Empty;
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class BooleanToColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isError && isError)
                return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Red);
            return new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.Green);
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ErrorMessageConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length != 2)
            {
                Debug.Assert(false);
                return Binding.DoNothing;
            }
            var vm = values[0] as NotifyPropertyAndErrorInfoBase;
            var fullPropertyPath = values[1] as string;
            if (vm == null || string.IsNullOrEmpty(fullPropertyPath))
            {
                Debug.Assert(false);
                return Binding.DoNothing;
            }
            if (!vm.PropertyHasErrors(fullPropertyPath))
            {
                return Binding.DoNothing;
            }
            var errors = vm.GetErrors(fullPropertyPath).Cast<string>().ToList();
            var truncated = $"{errors[0]}";
            if (errors.Count > 1)
            {
                truncated += $" (+{errors.Count - 1} more)";
            }
            return truncated;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
