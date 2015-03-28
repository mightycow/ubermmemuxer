using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace Uber
{
    public class AppBase<ConfigType> where ConfigType : class, new()
    {
        protected delegate void VoidDelegate();

        protected string AppVersion = "";
        protected ConfigType Config = new ConfigType();
        protected Application Application;
        protected Thread JobThread;
        protected Window MainWindow;
        protected TabControl TabControl;
        protected DockPanel RootPanel;
        protected ProgressBar ProgressBar;
        protected Button CancelJobButton;
        protected GroupBox ProgressGroupBox;
        protected List<FrameworkElement> RootElements = new List<FrameworkElement>();
        protected AlternatingListBoxBackground AltListBoxBg;
        protected Log Log;
        protected int CancelJobValue = 0;
        protected System.Drawing.Icon AboutIcon;
        protected int TotalWorkLoad = 0;
        protected int ProcessedWorkLoad = 0; // Not counting the current job's progress.
        protected int CurrentJobWorkLoad = 0;
        protected double CurrentProgress = 0.0;
        protected readonly Stopwatch ProgressTimer = new Stopwatch();
        protected Label TimeElapsedLabel;
        protected Label TimeRemainingLabel;
        protected Label AverageSpeedLabel;
        protected Label CurrentSpeedLabel;
        protected Label FrameIndexLabel;
        protected TabItem ProgressTab;
        private Thread _progressUpdateThread;
        private bool _stopProgressUpdateThread;
        private int _previousTabIndex = -1;
        private int _currentFrameIndex = -1;
        private double _currentFrameRate = -1.0;

        public AppBase(string[] cmdLineArgs)
        {
            PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Critical;

            AltListBoxBg = new AlternatingListBoxBackground(Colors.White, Color.FromRgb(223, 223, 223));
        }

        public ConfigType GetConfig()
        {
            return Config;
        }

        protected void CreateWindow()
        {
            var window = new Window();
            MainWindow = window;
            window.Closing += (obj, args) => OnQuit();
            window.WindowStyle = WindowStyle.SingleBorderWindow;
            window.AllowsTransparency = false;
            window.Background = new SolidColorBrush(System.Windows.SystemColors.ControlColor);
            window.ShowInTaskbar = true;
            window.Title = "UMM";
            window.Content = RootPanel;
            window.Width = 1024;
            window.Height = 768;
            window.MinWidth = 1024;
            window.MinHeight = 768;
            TextOptions.SetTextRenderingMode(window, TextRenderingMode.ClearType);
            TextOptions.SetTextHintingMode(window, TextHintingMode.Fixed);
            TextOptions.SetTextFormattingMode(window, TextFormattingMode.Display);
        }

        protected void RunApplication()
        {
            LoadConfig();

            var app = new Application();
            Application = app;
            app.ShutdownMode = ShutdownMode.OnLastWindowClose;
            app.Run(MainWindow);
        }

        protected void AddRootElement(FrameworkElement element)
        {
            RootPanel.Children.Add(element);
            RootElements.Add(element);
        }

        protected void OnQuit()
        {
            if(JobThread != null)
            {
                JobThread.Join();
            }

            SaveConfig();
            Application.Shutdown();
        }

        protected void ViewHelp()
        {
            try
            {
                var exeDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var helpPath = Path.Combine(exeDir, "README.txt");
                Process.Start(helpPath);
            }
            catch(Exception exception)
            {
                Log.LogError("Couldn't open the help: " + exception.Message);
            }
        }

        protected void ShowAboutWindow()
        {
            var textPanelList = new List<Tuple<FrameworkElement, FrameworkElement>>();
            textPanelList.Add(WpfHelper.CreateTuple("Version", AppVersion));
            textPanelList.Add(WpfHelper.CreateTuple("Developer", "myT"));
            var textPanel = WpfHelper.CreateDualColumnPanel(textPanelList, 100, 1);

            var image = new System.Windows.Controls.Image();
            image.HorizontalAlignment = HorizontalAlignment.Right;
            image.VerticalAlignment = VerticalAlignment.Top;
            image.Margin = new Thickness(5);
            image.Stretch = Stretch.None;
            image.Source = AboutIcon.ToImageSource();

            var rootPanel = new StackPanel();
            rootPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            rootPanel.VerticalAlignment = VerticalAlignment.Stretch;
            rootPanel.Margin = new Thickness(5);
            rootPanel.Orientation = Orientation.Horizontal;
            rootPanel.Children.Add(textPanel);
            rootPanel.Children.Add(image);

            var window = new Window();
            window.WindowStyle = WindowStyle.ToolWindow;
            window.ResizeMode = ResizeMode.NoResize;
            window.Background = new SolidColorBrush(System.Windows.SystemColors.ControlColor);
            window.ShowInTaskbar = false;
            window.Title = "About UberMmeMuxer";
            window.Content = rootPanel;
            window.Width = 240;
            window.Height = 100;
            window.Left = MainWindow.Left + (MainWindow.Width - window.Width) / 2;
            window.Top = MainWindow.Top + (MainWindow.Height - window.Height) / 2;
            window.Icon = AboutIcon.ToImageSource();
            window.ShowDialog();
        }

        protected virtual void LoadConfig()
        {
            Serializer.FromXml("Config.xml", out Config);
        }

        protected virtual void SaveConfig()
        {
            Serializer.ToXml("Config.xml", Config);
        }

        // Called from the main window's thread.
        protected void DisableUiNonThreadSafe()
        {
            ProgressGroupBox.Visibility = Visibility.Visible;
            ProgressBar.Value = 0;
            foreach(var element in RootElements)
            {
                element.IsEnabled = false;
            }
        }

        // Called from the job thread.
        protected void EnableUiThreadSafe()
        {
            VoidDelegate guiResetter = delegate
            {
                ProgressGroupBox.Visibility = Visibility.Collapsed;
                ProgressBar.Value = 0;
                foreach(var element in RootElements)
                {
                    element.IsEnabled = true;
                }
            };

            CancelJobValue = 0;
            MainWindow.Dispatcher.Invoke(guiResetter);
        }

        protected void OnCancelJobClicked()
        {
            var oldValue = CancelJobValue;
            CancelJobValue = 1;
            if(oldValue == 0)
            {
                Log.LogWarning("Batch canceled! The batch will stop after the current job is done.");
            }
        }

        protected void SetProgressThreadSafe(double value)
        {
            CurrentProgress = value;

            VoidDelegate valueSetter = delegate { ProgressBar.Value = value; ProgressBar.InvalidateVisual(); };
            ProgressBar.Dispatcher.Invoke(valueSetter);
        }

        public void SetCurrentJobProgress(double value)
        {
            var total = TotalWorkLoad;
            var done = ProcessedWorkLoad + (int)(CurrentJobWorkLoad * (value / 100.0));

            SetProgressThreadSafe(100.0 * (done / (double)total));
        }

        public void SetCurrentSubJobFrameRate(double frameRate)
        {
            _currentFrameRate = frameRate;
        }

        public void SetCurrentSubJobFrameIndex(int index)
        {
            _currentFrameIndex = index;
        }

        public void SetJobAsStarted()
        {
            ProgressTimer.Restart();
        }

        protected FrameworkElement CreateProgressTab()
        {
            var timeElapsedLabel = new Label();
            TimeElapsedLabel = timeElapsedLabel;

            var timeRemaining = new Label();
            TimeRemainingLabel = timeRemaining;

            var averageSpeed = new Label();
            AverageSpeedLabel = averageSpeed;

            var currentSpeed = new Label();
            CurrentSpeedLabel = currentSpeed;

            var frameIndex = new Label();
            FrameIndexLabel = frameIndex;

            var progressPanelList = new List<Tuple<FrameworkElement, FrameworkElement>>();
            progressPanelList.Add(Tuple.Create<FrameworkElement, FrameworkElement>(new Label { Content = "Time Elapsed" }, timeElapsedLabel));
            progressPanelList.Add(Tuple.Create<FrameworkElement, FrameworkElement>(new Label { Content = "Time Remaining" }, timeRemaining));
            progressPanelList.Add(Tuple.Create<FrameworkElement, FrameworkElement>(new Label { Content = "Average Speed" }, averageSpeed));
            progressPanelList.Add(Tuple.Create<FrameworkElement, FrameworkElement>(new Label { Content = "Current Speed" }, currentSpeed));
            progressPanelList.Add(Tuple.Create<FrameworkElement, FrameworkElement>(new Label { Content = "Frame Index" }, frameIndex));
            var progressPanel = WpfHelper.CreateDualColumnPanel(progressPanelList, 100, 1, 5);

            var groupBox = new GroupBox();
            groupBox.HorizontalAlignment = HorizontalAlignment.Left;
            groupBox.VerticalAlignment = VerticalAlignment.Top;
            groupBox.Margin = new Thickness(5);
            groupBox.Header = "Progress";
            groupBox.Content = progressPanel;

            return groupBox;
        }

        protected void HideProgressThreadSafe()
        {
            ProgressTimer.Stop();
            ProgressTimer.Reset();

            VoidDelegate progressTabRemover = delegate 
            { 
                TabControl.Items.Remove(ProgressTab);
                TabControl.SelectedIndex = _previousTabIndex == -1 ? 0 : _previousTabIndex;
            };
            TabControl.Dispatcher.Invoke(progressTabRemover);

            _stopProgressUpdateThread = true;
            if(_progressUpdateThread != null)
            {
                _progressUpdateThread.Join();
            }
        }

        protected void ShowProgressNonThreadSafe()
        {
            ProgressTimer.Reset();
            _currentFrameIndex = 0;

            _previousTabIndex = TabControl.SelectedIndex;
            TabControl.Items.Add(ProgressTab);
            TabControl.SelectedIndex = TabControl.Items.Count - 1;

            TimeElapsedLabel.Content = "N/A";
            TimeRemainingLabel.Content = "N/A";
            AverageSpeedLabel.Content = "N/A";
            CurrentSpeedLabel.Content = "N/A";
            FrameIndexLabel.Content = "N/A";

            var thread = new Thread(UpdateProgressThread);
            _progressUpdateThread = thread;
            thread.Start();
        }

        protected void UpdateProgress()
        {
            if(!ProgressTimer.IsRunning || ProgressTimer.ElapsedMilliseconds < 100)
            {
                return;
            }

            var elapsedMs = ProgressTimer.ElapsedMilliseconds;
            var progress = CurrentProgress / 100.0;
            var totalMs = (long)(elapsedMs / progress);
            var remainingMs = totalMs - elapsedMs;

            var frameCount = ProcessedWorkLoad + (int)(CurrentJobWorkLoad * progress);
            var fps = frameCount / ((double)elapsedMs / 1000.0);
            var averageSpeed = (double.IsNaN(fps) || double.IsInfinity(fps) || fps < 0.1) ? "N/A" : (fps.ToString("F1") + " FPS");

            VoidDelegate progressGuiUpdater = delegate 
            {
                TimeElapsedLabel.Content = FormatMilliSeconds(elapsedMs);
                TimeRemainingLabel.Content = FormatMilliSeconds(remainingMs);
                AverageSpeedLabel.Content = averageSpeed;
                CurrentSpeedLabel.Content = _currentFrameRate >= 0.1 ? (_currentFrameRate.ToString("F1") + " FPS") : "N/A";
                FrameIndexLabel.Content = _currentFrameIndex >= 1 ? _currentFrameIndex.ToString() : "N/A";
            };
            ProgressTab.Dispatcher.Invoke(progressGuiUpdater);
        }

        protected void OnJobStart()
        {
            _currentFrameRate = -1.0;
            _currentFrameIndex = -1;
        }

        private void UpdateProgressThread()
        {
            if(Debugger.IsAttached)
            {
                UpdateProgressThreadImpl();
                return;
            }

            try
            {
                UpdateProgressThreadImpl();
            }
            catch(Exception exception)
            {
                EntryPoint.RaiseException(exception);
            }
        }

        private void UpdateProgressThreadImpl()
        {
            _stopProgressUpdateThread = false;
            while(!_stopProgressUpdateThread)
            {
                Thread.Sleep(1000);
                UpdateProgress();
            }
        }

        private const long MilliSecondsInADay = 1000 * 60 * 60 * 24;

        private string FormatMilliSeconds(long elapsedMs)
        {
            var elapsedMsDbl = (double)elapsedMs;
            if( double.IsNaN(elapsedMsDbl) || 
                double.IsInfinity(elapsedMsDbl) || 
                elapsedMsDbl <= 0.0 ||
                elapsedMsDbl >= 100 * MilliSecondsInADay)
            {
                return "Replacing the flux capacitor...";
            }

            var elapsed = TimeSpan.FromMilliseconds(elapsedMsDbl);
            if(elapsed.Days > 0)
            {
                return "Eternity :(";
            }

            if(elapsed.Hours > 0)
            {
                return string.Format("{0}h {1}m {2}s",
                    elapsed.Hours.ToString("00"),
                    elapsed.Minutes.ToString("00"),
                    elapsed.Seconds.ToString("00"));
            }
            else if(elapsed.Minutes > 0)
            {
                return string.Format("{0}m {1}s",
                    elapsed.Minutes.ToString("00"),
                    elapsed.Seconds.ToString("00"));
            }

            return string.Format("{0}s", elapsed.Seconds.ToString());
        }
    }
}