using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;

namespace UnifiVideoExporter
{
    using MessageBox = System.Windows.MessageBox;
    using Trigger = Microsoft.Win32.TaskScheduler.Trigger;

    public enum TaskFrequency
    {
        Daily,
        Weekly,
        Monthly
    }

    public partial class ScheduleTaskViewModel : ObservableValidator
    {
        private readonly string s_TaskFolder = Path.Combine(SettingsHelper.s_WorkingDirectory, "tasks");

        #region observable properties

        public ObservableCollection<TaskFrequency> TaskFrequencies { get; set; } = new ObservableCollection<TaskFrequency>
        {
            TaskFrequency.Daily,
            TaskFrequency.Weekly,
            TaskFrequency.Monthly
        };
        public ObservableCollection<string> CameraList { get; set; }
        public ObservableCollection<ConsoleSettings> ExistingTasks { get; set; }

        [ObservableProperty]
        private ConsoleSettings currentTaskSettings;
        #endregion

        #region commands

        public RelayCommand CreateTaskCommand { get; }
        public RelayCommand<ConsoleSettings> RemoveTaskCommand { get; }

        #endregion

        private ConsoleSettings _defaultSettings;

        public ScheduleTaskViewModel(MainWindowViewModel Vm)
        {
            CreateTaskCommand = new RelayCommand(CreateTask, CanCreateTask);
            RemoveTaskCommand = new RelayCommand<ConsoleSettings>(RemoveTask, _ => true);
            LoadExistingTasks();

            _defaultSettings = new ConsoleSettings();
            //
            // Initialize relevant properties from MainWindowViewModel
            //
            CameraList = Vm.CameraList;
            _defaultSettings.ControllerAddress = Vm.Settings.ControllerAddress;
            _defaultSettings.UserName = Vm.Settings.UserName;
            _defaultSettings.Password = Vm.Settings.Password;
            _defaultSettings.EndTime = Vm.Settings.EndTime;
            _defaultSettings.StartTime = Vm.Settings.StartTime;
            _defaultSettings.FfmpegPath = Vm.Settings.FfmpegPath;
            _defaultSettings.FramesPerSecond = Vm.Settings.FramesPerSecond;
            _defaultSettings.SnapshotInterval = Vm.Settings.SnapshotInterval;
            _defaultSettings.SelectedCamera = Vm.Settings.SelectedCamera;
            _defaultSettings.TaskSettingsFile = Path.Combine(s_TaskFolder, $"{Guid.NewGuid()}.json");
            ResetTask();
        }

        private void ResetTask()
        {
            CurrentTaskSettings = new ConsoleSettings();
            CurrentTaskSettings.PropertyChanged += CurrentTaskSettings_PropertyChanged;
            CurrentTaskSettings.ControllerAddress = _defaultSettings.ControllerAddress;
            CurrentTaskSettings.UserName = _defaultSettings.UserName;
            CurrentTaskSettings.Password = _defaultSettings.Password;
            CurrentTaskSettings.EndTime = _defaultSettings.EndTime;
            CurrentTaskSettings.StartTime = _defaultSettings.StartTime;
            CurrentTaskSettings.FfmpegPath = _defaultSettings.FfmpegPath;
            CurrentTaskSettings.FramesPerSecond = _defaultSettings.FramesPerSecond;
            CurrentTaskSettings.SnapshotInterval = _defaultSettings.SnapshotInterval;
            CurrentTaskSettings.SelectedCamera = _defaultSettings.SelectedCamera;
            CurrentTaskSettings.TaskSettingsFile = Path.Combine(s_TaskFolder, $"{Guid.NewGuid()}.json");
            CurrentTaskSettings.ValidateAll();
        }

        private void CurrentTaskSettings_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            CreateTaskCommand.NotifyCanExecuteChanged();
            RemoveTaskCommand.NotifyCanExecuteChanged();
        }

        public void SwitchCurrentTask(ConsoleSettings Task)
        {
            CurrentTaskSettings = Task;
            CurrentTaskSettings.PropertyChanged -= CurrentTaskSettings_PropertyChanged;
            CurrentTaskSettings.PropertyChanged += CurrentTaskSettings_PropertyChanged;
            CurrentTaskSettings.ValidateAll();
        }

        public static bool TaskExists(string taskName)
        {
            using (TaskService ts = new TaskService())
            {
                return ts.GetTask(taskName) != null;
            }
        }

        private void LoadExistingTasks()
        {
            try
            {
                string tasksPath = Path.Combine(SettingsHelper.s_WorkingDirectory, "tasks.json");
                if (File.Exists(tasksPath))
                {
                    string json = File.ReadAllText(tasksPath);
                    var tasks = JsonConvert.DeserializeObject<List<ConsoleSettings>>(json);
                    ExistingTasks = new ObservableCollection<ConsoleSettings>(tasks!);
                }
                else
                {
                    ExistingTasks = new ObservableCollection<ConsoleSettings>();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading existing tasks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveExistingTasks()
        {
            try
            {
                string settingsDir = SettingsHelper.s_WorkingDirectory;
                Directory.CreateDirectory(settingsDir);
                string tasksPath = Path.Combine(settingsDir, "tasks.json");
                string json = JsonConvert.SerializeObject(ExistingTasks, Formatting.Indented);
                File.WriteAllText(tasksPath, json);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving tasks: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanCreateTask()
        {
            CurrentTaskSettings.ValidateAll();
            ValidateAllProperties();
            return !HasErrors && !CurrentTaskSettings.HasErrors;
        }

        private void CreateTask()
        {
            try
            {
                Directory.CreateDirectory(s_TaskFolder);

                //
                // Generate a json settings file to be consumed by the task, which will launch
                // UnifiVideoExporter.exe as a command line app.
                //
                var json = JsonConvert.SerializeObject(CurrentTaskSettings, Formatting.Indented);
                File.WriteAllText(CurrentTaskSettings.TaskSettingsFile, json);

                //
                // Create the task service
                //
                using (TaskService ts = new TaskService())
                {
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = $"UnifiVideoExporter timelapse task: {CurrentTaskSettings.TaskName}";
                    td.Principal.RunLevel = TaskRunLevel.Highest;
                    td.Settings.Enabled = true;
                    td.Settings.Hidden = false;

                    Trigger trigger;
                    switch (CurrentTaskSettings.TaskFrequency)
                    {
                        case TaskFrequency.Daily:
                            trigger = new WeeklyTrigger
                            {
                                WeeksInterval = 1,
                            };
                            if (CurrentTaskSettings.WeekdaysOnly)
                            {
                                ((WeeklyTrigger)trigger).DaysOfWeek = DaysOfTheWeek.Tuesday
                                    | DaysOfTheWeek.Wednesday | DaysOfTheWeek.Thursday | DaysOfTheWeek.Friday | DaysOfTheWeek.Saturday;
                            }
                            else
                            {
                                ((WeeklyTrigger)trigger).DaysOfWeek = DaysOfTheWeek.AllDays;
                            }
                            break;
                        case TaskFrequency.Weekly:
                            trigger = new WeeklyTrigger
                            {
                                WeeksInterval = 1,
                                DaysOfWeek = DaysOfTheWeek.Sunday
                            };
                            break;
                        case TaskFrequency.Monthly:
                            trigger = new MonthlyTrigger
                            {
                                MonthsOfYear = MonthsOfTheYear.AllMonths,
                                DaysOfMonth = new int[] { 1 }
                            };
                            break;
                        default:
                            throw new ArgumentException("Invalid task frequency.");
                    }
                    trigger.StartBoundary = CurrentTaskSettings.StartDate.Add(
                        TimeSpan.Parse(CurrentTaskSettings.TaskStartTime));
                    td.Triggers.Add(trigger);
                    string exePath = Path.Combine(
                        Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location),
                        "UnifiVideoExporter.exe");
                    td.Actions.Add(new ExecAction(exePath, $"--settings \"{CurrentTaskSettings.TaskSettingsFile}\" --scheduledtask", null));
                    ts.RootFolder.RegisterTaskDefinition(CurrentTaskSettings.TaskName, td);
                }

                //
                // Add to existing tasks
                //
                ExistingTasks.Add(CurrentTaskSettings);
                SaveExistingTasks();
                ResetTask();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating scheduled task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveTask(ConsoleSettings? task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                if (File.Exists(task.TaskSettingsFile))
                {
                    File.Delete(task.TaskSettingsFile);
                }

                ExistingTasks.Remove(task);
                SaveExistingTasks();

                if (TaskExists(task.TaskName))
                {
                    using (TaskService ts = new TaskService())
                    {
                        ts.RootFolder.DeleteTask(task.TaskName);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing task '{task.TaskName}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}