using System.Diagnostics;
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
}
