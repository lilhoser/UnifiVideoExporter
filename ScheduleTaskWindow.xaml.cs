using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace UnifiVideoExporter
{
    using OpenFileDialog = Microsoft.Win32.OpenFileDialog;

    public partial class ScheduleTaskWindow : Window
    {
        public ScheduleTaskViewModel ViewModel { get; }

        public ScheduleTaskWindow(MainWindowViewModel Vm)
        {
            InitializeComponent();
            ViewModel = new ScheduleTaskViewModel(Vm);
            DataContext = ViewModel;
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
            ViewModel.CurrentTaskSettings.OutputVideoLocation = browser.FolderName;
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.CurrentTaskSettings.Password = passwordBox.Password;
            }
        }

        private void BrowseFffmpegButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "FFmpeg Executable|ffmpeg.exe",
                Title = "Select FFmpeg Executable"
            };
            if (dialog.ShowDialog() == true)
            {
                ViewModel.CurrentTaskSettings.FfmpegPath = dialog.FileName;
            }
        }

        private void ExistingTasksListview_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selectedTask = ExistingTasksListview.SelectedItem as ConsoleSettings;
            if (selectedTask == null)
            {
                return;
            }
            ViewModel.SwitchCurrentTask(selectedTask);
        }
    }
}