using Microsoft.Win32;
using System.Windows;

namespace UnifiVideoExporter
{
    public partial class ScheduleTaskWindow : Window
    {
        public ScheduleTaskViewModel ViewModel { get; }

        public ScheduleTaskWindow(MainWindowViewModel Vm)
        {
            InitializeComponent();
            ViewModel = new ScheduleTaskViewModel(Vm);
            DataContext = ViewModel;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void BrowseOutputVideoLocationButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select the output directory";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            ViewModel.OutputVideoLocation = browser.FolderName;
        }
    }
}