using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32.TaskScheduler;
using Newtonsoft.Json;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Windows;

namespace UnifiVideoExporter
{
    using Trigger = Microsoft.Win32.TaskScheduler.Trigger;

    public enum TaskFrequency
    {
        Daily,
        Weekly,
        Monthly
    }

    public class ScheduledTaskInfo
    {
        public string TaskName { get; set; }
        public string CameraName { get; set; }
        public TaskFrequency TaskFrequency { get; set; }
        public string StartTime { get; set; }
        public string EndTime { get; set; }
        public bool WeekdaysOnly { get; set; }
        public string ControllerAddress { get; set; }
        public string EncryptedUsername { get; set; }
        public string EncryptedPassword { get; set; }
        public string FfmpegPath { get; set; }
        public double SnapshotInterval { get; set; }
        public double FramesPerSecond { get; set; }
        public string TaskSettingsFile { get; set; }
        public string OutputVideoLocation { get; set; }
    }

    public partial class ScheduleTaskViewModel : ObservableValidator
    {
        private readonly string s_TaskFolder = Path.Combine(SettingsHelper.s_WorkingDirectory, "tasks");
        private ObservableCollection<TaskFrequency> _taskFrequencies = new ObservableCollection<TaskFrequency>
        {
            TaskFrequency.Daily,
            TaskFrequency.Weekly,
            TaskFrequency.Monthly
        };
        private ObservableCollection<ScheduledTaskInfo> _existingTasks;

        [ObservableProperty]
        [Required(ErrorMessage = "Task name is required.")]
        private string taskName = "UnifiVideoExporterTimelapse";

        [ObservableProperty]
        [Required(ErrorMessage = "Camera is required")]
        private string cameraName;

        [ObservableProperty]
        [Required(ErrorMessage = "Frequency is required.")]
        private TaskFrequency taskFrequency = TaskFrequency.Daily;

        [ObservableProperty]
        [Required(ErrorMessage = "Start date is required.")]
        private DateTime startDate = DateTime.Now;

        [ObservableProperty]
        [Required(ErrorMessage = "Start time is required")]
        private string startTime = "0800";

        [ObservableProperty]
        [Required(ErrorMessage = "End time is required")]
        private string endTime = "1700";

        [ObservableProperty]
        private bool weekdaysOnly = false;

        [ObservableProperty]
        [Required(ErrorMessage = "Output video location is required")]
        private string outputVideoLocation = string.Empty;

        [JsonIgnore]
        public ObservableCollection<TaskFrequency> TaskFrequencies => _taskFrequencies;
        [JsonIgnore]
        public ObservableCollection<string> CameraList { get; set; }
        [JsonIgnore]
        public ObservableCollection<ScheduledTaskInfo> ExistingTasks
        {
            get => _existingTasks;
            set => SetProperty(ref _existingTasks, value);
        }
        [JsonIgnore]
        public RelayCommand CreateTaskCommand { get; }
        [JsonIgnore]
        public RelayCommand<ScheduledTaskInfo> RemoveTaskCommand { get; }

        private MainWindowViewModel _mainWindowViewModel;

        public ScheduleTaskViewModel(MainWindowViewModel Vm)
        {
            CreateTaskCommand = new RelayCommand(CreateTask, CanCreateTask);
            RemoveTaskCommand = new RelayCommand<ScheduledTaskInfo>(RemoveTask, _ => true);
            LoadExistingTasks();
            _mainWindowViewModel = Vm;
            CameraList = Vm.CameraList;
        }

        private void LoadExistingTasks()
        {
            try
            {
                string tasksPath = Path.Combine(SettingsHelper.s_WorkingDirectory, "tasks.json");
                if (File.Exists(tasksPath))
                {
                    string json = File.ReadAllText(tasksPath);
                    var tasks = JsonConvert.DeserializeObject<List<ScheduledTaskInfo>>(json);
                    ExistingTasks = new ObservableCollection<ScheduledTaskInfo>(tasks!);
                }
                else
                {
                    ExistingTasks = new ObservableCollection<ScheduledTaskInfo>();
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
            //
            // Validate this window's form
            //
            ValidateAllProperties();
            if (HasErrors)
            {
                return false;
            }
            //
            // Validate data required from main window forms
            //
            if (_mainWindowViewModel.PropertyHasErrors(nameof(MainWindowViewModel.ControllerAddress)) ||
                _mainWindowViewModel.PropertyHasErrors(nameof(MainWindowViewModel.Username)) ||
                _mainWindowViewModel.PropertyHasErrors(nameof(MainWindowViewModel.Password)) ||
                _mainWindowViewModel.PropertyHasErrors(nameof(MainWindowViewModel.SnapshotInterval)) ||
                _mainWindowViewModel.PropertyHasErrors(nameof(MainWindowViewModel.FramesPerSecond)))
            {
                return false;
            }
            return true;
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
                string settingsPath = Path.Combine(s_TaskFolder, $"{Guid.NewGuid()}.json");
                var task = new ScheduledTaskInfo()
                {
                    TaskName = TaskName,
                    CameraName = CameraName,
                    TaskFrequency = TaskFrequency,
                    StartTime = StartTime,
                    EndTime = EndTime,
                    WeekdaysOnly = WeekdaysOnly,
                    ControllerAddress = _mainWindowViewModel.ControllerAddress,
                    EncryptedUsername = _mainWindowViewModel._EncryptedUserName,
                    EncryptedPassword = _mainWindowViewModel._EncryptedPassword,
                    FfmpegPath = _mainWindowViewModel.FfmpegPath,
                    SnapshotInterval = _mainWindowViewModel.SnapshotInterval,
                    FramesPerSecond = _mainWindowViewModel.FramesPerSecond,
                    TaskSettingsFile = settingsPath,
                    OutputVideoLocation = OutputVideoLocation,
                };

                var json = JsonConvert.SerializeObject(task, Formatting.Indented);
                File.WriteAllText(settingsPath, json);

                //
                // Create the task service
                //
                using (TaskService ts = new TaskService())
                {
                    TaskDefinition td = ts.NewTask();
                    td.RegistrationInfo.Description = $"UnifiVideoExporter timelapse task: {TaskName}";
                    td.Principal.RunLevel = TaskRunLevel.Highest;
                    td.Settings.Enabled = true;
                    td.Settings.Hidden = false;

                    Trigger trigger;
                    switch (TaskFrequency)
                    {
                        case TaskFrequency.Daily:
                            trigger = new DailyTrigger
                            {
                                DaysInterval = 1
                            };
                            break;
                        case TaskFrequency.Weekly:
                            trigger = new WeeklyTrigger
                            {
                                WeeksInterval = 1,
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
                    trigger.StartBoundary = StartDate;
                    td.Triggers.Add(trigger);
                    string exePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                    td.Actions.Add(new ExecAction(exePath, $"--settings \"{settingsPath}\" --scheduledtask", null));
                    ts.RootFolder.RegisterTaskDefinition(TaskName, td);
                }

                //
                // Add to existing tasks
                //
                ExistingTasks.Add(task);
                SaveExistingTasks();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error creating scheduled task: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveTask(ScheduledTaskInfo? task)
        {
            if (task == null)
            {
                return;
            }

            try
            {
                using (TaskService ts = new TaskService())
                {
                    ts.RootFolder.DeleteTask(task.TaskName);
                }

                if (File.Exists(task.TaskSettingsFile))
                {
                    File.Delete(task.TaskSettingsFile);
                }

                ExistingTasks.Remove(task);
                SaveExistingTasks();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error removing task '{task.TaskName}': {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnPropertyChanged(PropertyChangedEventArgs e)
        {
            base.OnPropertyChanged(e);
            CreateTaskCommand.NotifyCanExecuteChanged();
        }
    }
}