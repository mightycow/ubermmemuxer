using System;
using System.Collections.Generic;
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

            var goButton = new Button();
            goButton.HorizontalAlignment = HorizontalAlignment.Left;
            goButton.VerticalAlignment = VerticalAlignment.Center;
            goButton.Content = "Go!";
            goButton.Width = ButtonWidth;
            goButton.Height = ButtonHeight;
            goButton.Margin = new Thickness(5);
            goButton.Click += (obj, args) => OnProcessJobs();

            var buttonPanel = new StackPanel();
            buttonPanel.HorizontalAlignment = HorizontalAlignment.Left;
            buttonPanel.VerticalAlignment = VerticalAlignment.Top;
            buttonPanel.Margin = new Thickness(5);
            buttonPanel.Orientation = Orientation.Vertical;
            buttonPanel.Children.Add(addFilesButton);
            buttonPanel.Children.Add(addFoldersButton);
            buttonPanel.Children.Add(new Separator() { Margin = new Thickness(5) });
            buttonPanel.Children.Add(clearButton);
            buttonPanel.Children.Add(clearSuccessfulButton);
            buttonPanel.Children.Add(new Separator() { Margin = new Thickness(5) });
            buttonPanel.Children.Add(removeButton);
            buttonPanel.Children.Add(new Separator() { Margin = new Thickness(5) } );
            buttonPanel.Children.Add(goButton);

            var buttonGroup = new GroupBox();
            buttonGroup.HorizontalAlignment = HorizontalAlignment.Left;
            buttonGroup.VerticalAlignment = VerticalAlignment.Top;
            buttonGroup.Margin = new Thickness(5);
            buttonGroup.Header = "Actions";
            buttonGroup.Content = buttonPanel;

            return buttonGroup;
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
    }
}