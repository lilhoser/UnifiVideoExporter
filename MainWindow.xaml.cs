using Microsoft.Win32;
using System.Windows;
using System.Windows.Controls;

namespace UnifiVideoExporter
{
    public partial class MainWindow : Window
    {
        public MainWindowViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            ViewModel = new MainWindowViewModel();
            DataContext = ViewModel;
            ViewModel.LoadSettings();
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
                ViewModel.FfmpegPath = dialog.FileName;
            }
        }

        private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (sender is PasswordBox passwordBox)
            {
                ViewModel.Password = passwordBox.Password;
            }
        }

        private void ClearLogButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Log = "";
        }

        private void CopyLogButton_Click(object sender, RoutedEventArgs e)
        {
            Clipboard.SetText(ViewModel.Log);
        }

        private void BrowseLocalVideoPathButton_Click(object sender, RoutedEventArgs e)
        {
            var browser = new OpenFolderDialog();
            browser.Title = "Select the video directory";
            browser.InitialDirectory = Environment.SpecialFolder.MyComputer.ToString();
            var result = browser.ShowDialog();
            if (!result.HasValue || !result.Value)
            {
                return;
            }
            ViewModel.LocalVideoPath = browser.FolderName;
        }

        private void LogTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            LogTextBox.ScrollToEnd();
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (!ViewModel.CanShutdown())
            {
                _ = ViewModel.Shutdown(); // no need to await; caller doesn't honor it.
                e.Cancel = true;
                return;
            }
            ViewModel.SaveSettings();
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ViewModel.SetWindowLoaded();
            if (ViewModel.AutoConnect && ViewModel.ConnectCommand.CanExecute(null))
            {
                await ViewModel.ConnectCommand.ExecuteAsync(null);
            }
        }
    }
}