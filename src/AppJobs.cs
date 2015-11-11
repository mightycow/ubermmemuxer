using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;


namespace Uber.MmeMuxer
{
    public partial class UmmApp
    {
        private FrameworkElement CreateJobsTab()
        {
            const int ButtonWidth = 100;
            const int ButtonHeight = 25;

            var addFilesButton = new Button();
            addFilesButton.HorizontalAlignment = HorizontalAlignment.Left;
            addFilesButton.VerticalAlignment = VerticalAlignment.Center;
            addFilesButton.Content = "Add Files...";
            addFilesButton.Width = ButtonWidth;
            addFilesButton.Height = ButtonHeight;
            addFilesButton.Margin = new Thickness(5);
            addFilesButton.Click += (obj, args) => OnAddFileJobs();

            var addFoldersButton = new Button();
            addFoldersButton.HorizontalAlignment = HorizontalAlignment.Left;
            addFoldersButton.VerticalAlignment = VerticalAlignment.Center;
            addFoldersButton.Content = "Add Folder...";
            addFoldersButton.Width = ButtonWidth;
            addFoldersButton.Height = ButtonHeight;
            addFoldersButton.Margin = new Thickness(5);
            addFoldersButton.Click += (obj, args) => OnAddFolderJob();

            var removeButton = new Button();
            removeButton.HorizontalAlignment = HorizontalAlignment.Left;
            removeButton.VerticalAlignment = VerticalAlignment.Center;
            removeButton.Content = "Remove";
            removeButton.Width = ButtonWidth;
            removeButton.Height = ButtonHeight;
            removeButton.Margin = new Thickness(5);
            removeButton.Click += (obj, args) => OnRemoveJobs();

            var clearButton = new Button();
            clearButton.HorizontalAlignment = HorizontalAlignment.Left;
            clearButton.VerticalAlignment = VerticalAlignment.Center;
            clearButton.Content = "Clear All";
            clearButton.Width = ButtonWidth;
            clearButton.Height = ButtonHeight;
            clearButton.Margin = new Thickness(5);
            clearButton.Click += (obj, args) => OnClearJobs();

            var clearSuccessfulButton = new Button();
            clearSuccessfulButton.HorizontalAlignment = HorizontalAlignment.Left;
            clearSuccessfulButton.VerticalAlignment = VerticalAlignment.Center;
            clearSuccessfulButton.Content = "Clear Successful";
            clearSuccessfulButton.Width = ButtonWidth;
            clearSuccessfulButton.Height = ButtonHeight;
            clearSuccessfulButton.Margin = new Thickness(5);
            clearSuccessfulButton.Click += (obj, args) => OnClearSuccessfulJobs();

            var saveBatchButton = new Button();
            saveBatchButton.HorizontalAlignment = HorizontalAlignment.Left;
            saveBatchButton.VerticalAlignment = VerticalAlignment.Center;
            saveBatchButton.Content = "Save to Batch...";
            saveBatchButton.Width = ButtonWidth;
            saveBatchButton.Height = ButtonHeight;
            saveBatchButton.Margin = new Thickness(5);
            saveBatchButton.Click += (obj, args) => OnSaveJobsToBatchFile();

            var goButton = new Button();
            goButton.HorizontalAlignment = HorizontalAlignment.Left;
            goButton.VerticalAlignment = VerticalAlignment.Center;
            goButton.Content = "Go!";
            goButton.Width = ButtonWidth;
            goButton.Height = ButtonHeight;
            goButton.Margin = new Thickness(5);
            goButton.Click += (obj, args) => OnProcessJobs();

            var jobButtonsPanel = new StackPanel();
            jobButtonsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            jobButtonsPanel.VerticalAlignment = VerticalAlignment.Top;
            jobButtonsPanel.Margin = new Thickness(5);
            jobButtonsPanel.Orientation = Orientation.Vertical;
            jobButtonsPanel.Children.Add(addFilesButton);
            jobButtonsPanel.Children.Add(addFoldersButton);
            jobButtonsPanel.Children.Add(new Separator() { Margin = new Thickness(5) });
            jobButtonsPanel.Children.Add(clearButton);
            jobButtonsPanel.Children.Add(clearSuccessfulButton);
            jobButtonsPanel.Children.Add(new Separator() { Margin = new Thickness(5) });
            jobButtonsPanel.Children.Add(removeButton);
            jobButtonsPanel.Children.Add(new Separator() { Margin = new Thickness(5) } );
            jobButtonsPanel.Children.Add(saveBatchButton);
            jobButtonsPanel.Children.Add(new Separator() { Margin = new Thickness(5) } );
            jobButtonsPanel.Children.Add(goButton);

            var jobButtonsGroupBox = new GroupBox();
            jobButtonsGroupBox.HorizontalAlignment = HorizontalAlignment.Left;
            jobButtonsGroupBox.VerticalAlignment = VerticalAlignment.Top;
            jobButtonsGroupBox.Margin = new Thickness(5);
            jobButtonsGroupBox.Header = "Job Actions";
            jobButtonsGroupBox.Content = jobButtonsPanel;

            var openReflexReplaysButton = new Button();
            openReflexReplaysButton.HorizontalAlignment = HorizontalAlignment.Left;
            openReflexReplaysButton.VerticalAlignment = VerticalAlignment.Center;
            openReflexReplaysButton.Content = "Open Replays";
            openReflexReplaysButton.ToolTip = "Open the replays folder in the file explorer";
            openReflexReplaysButton.Width = ButtonWidth;
            openReflexReplaysButton.Height = ButtonHeight;
            openReflexReplaysButton.Margin = new Thickness(5);
            openReflexReplaysButton.Click += (obj, args) => OnOpenReflexReplays();

            var loadReflexReplaysButton = new Button();
            loadReflexReplaysButton.HorizontalAlignment = HorizontalAlignment.Left;
            loadReflexReplaysButton.VerticalAlignment = VerticalAlignment.Center;
            loadReflexReplaysButton.Content = "Load Replays";
            loadReflexReplaysButton.ToolTip = "Add up all Reflex replay folders to the jobs list";
            loadReflexReplaysButton.Width = ButtonWidth;
            loadReflexReplaysButton.Height = ButtonHeight;
            loadReflexReplaysButton.Margin = new Thickness(5);
            loadReflexReplaysButton.Click += (obj, args) => OnLoadReflexReplays();

            var reflexButtonsPanel = new StackPanel();
            reflexButtonsPanel.HorizontalAlignment = HorizontalAlignment.Left;
            reflexButtonsPanel.VerticalAlignment = VerticalAlignment.Top;
            reflexButtonsPanel.Margin = new Thickness(5);
            reflexButtonsPanel.Orientation = Orientation.Vertical;
            reflexButtonsPanel.Children.Add(openReflexReplaysButton);
            reflexButtonsPanel.Children.Add(loadReflexReplaysButton);

            var reflexButtonsGroupBox = new GroupBox();
            reflexButtonsGroupBox.HorizontalAlignment = HorizontalAlignment.Left;
            reflexButtonsGroupBox.VerticalAlignment = VerticalAlignment.Top;
            reflexButtonsGroupBox.Margin = new Thickness(5);
            reflexButtonsGroupBox.Header = "Reflex Actions";
            reflexButtonsGroupBox.Content = reflexButtonsPanel;

            var rootPanel = new StackPanel();
            rootPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            rootPanel.VerticalAlignment = VerticalAlignment.Stretch;
            rootPanel.Margin = new Thickness(5);
            rootPanel.Orientation = Orientation.Horizontal;
            rootPanel.Children.Add(jobButtonsGroupBox);
            rootPanel.Children.Add(reflexButtonsGroupBox);

            return rootPanel;
        }  

        private void OnRemoveJobs()
        {
            if(_jobsListView.SelectedItems.Count == 0)
            {
                return;
            }

            var selectedItems = new List<object>();
            foreach(var item in _jobsListView.SelectedItems)
            {
                selectedItems.Add(item);
            }

            foreach(var job in selectedItems)
            {
                var display = job as JobDisplayInfo;
                if(display == null)
                {
                    continue;
                }
                
                _jobsListView.Items.Remove(job);
                _jobs.Remove(display.Job);
            }
        }

        private void OnAddFileJobs()
        {
            using(var openFileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                openFileDialog.CheckPathExists = true;
                openFileDialog.Multiselect = true;
                openFileDialog.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                openFileDialog.Filter = "Audio/video interleave (*.avi)|*.avi";
                if(openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                AddJobs(new List<string>(openFileDialog.FileNames), new List<string>());
            }
        }

        private void OnAddFolderJob()
        {
            var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderBrowserDialog.Description = "Select the video/image sequence(s) folder";
            folderBrowserDialog.RootFolder = Environment.SpecialFolder.Desktop;
            folderBrowserDialog.ShowNewFolderButton = false;
            if(folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var folderPath = folderBrowserDialog.SelectedPath;
            if(!Directory.Exists(folderPath))
            {
                return;
            }

            AddJobs(new List<string>(), new List<string>() { folderPath });
        }

        private void OnClearJobs()
        {
            _jobsListView.Items.Clear();
            _jobs.Clear();
        }

        private void OnClearSuccessfulJobs()
        {
            var jobViews = _jobsListView.Items;
            if(jobViews.Count != _jobs.Count)
            {
                return;
            }

            var jobCount = jobViews.Count;
            var indices = new List<int>();
            for(var i = 0; i < jobCount; ++i)
            {
                var display = jobViews[i] as JobDisplayInfo;
                if(display != null && display.Status == "success")
                {
                    indices.Add(i);
                }
            }
            indices.Reverse();

            foreach(var index in indices)
            {
                jobViews.RemoveAt(index);
                _jobs.RemoveAt(index);
            }
        }

        private void OnOpenReflexReplays()
        {
            try
            {
                var replaysPath = Reflex.GetReflexReplaysFolder();
                Process.Start(replaysPath);
            }
            catch(Exception exception)
            {
                UmmApp.Instance.LogError("Caught an exception while getting/opening the Reflex replays folder: " + exception.Message);
            }
        }

        private void OnLoadReflexReplays()
        {
            string replaysPath = null;
            try
            {
                replaysPath = Reflex.GetReflexReplaysFolder();
            }
            catch(Exception exception)
            {
                UmmApp.Instance.LogError("Caught an exception while getting the Reflex replays folder: " + exception.Message);
                return;
            }

            if(replaysPath == null)
            {
                return;
            }

            var allReplays = Directory.GetDirectories(replaysPath, "*", SearchOption.TopDirectoryOnly);
            var allReplaysList = new List<string>();
            allReplaysList.AddRange(allReplays);

            AddJobs(new List<string>(), allReplaysList);
        }
    }
}