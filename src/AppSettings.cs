using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;


namespace Uber.MmeMuxer
{
    public enum FileNamingMethod
    {
        NoChange,
        AddPrefix,
        AddSuffix,
        ApplyRegExp
    }

    public enum VideoCodec
    {
        Raw,
        Copy,
        Lagarith,
        Lavc,
        CustomVfw
    }

    public partial class UmmApp
    {
        private class CodecSettings
        {
            public VideoCodec Codec;
            public string Description;
            public RadioButton Button;
        }

        private class BuiltInVideoCodecSettings : CodecSettings
        {
            public static BuiltInVideoCodecSettings New(VideoCodec codec, string description)
            {
                var info = new BuiltInVideoCodecSettings();
                info.Codec = codec;
                info.Description = description;

                return info;
            }
        }

        private class CustomVideoCodecSettings : CodecSettings
        {
            public bool VideoForWindows;
            public TextBox EditBox;
            public FrameworkElement Panel;

            public static CustomVideoCodecSettings New(bool vfw)
            {
                var info = new CustomVideoCodecSettings();
                info.Codec = vfw ? VideoCodec.CustomVfw : VideoCodec.Lavc;
                info.VideoForWindows = vfw;
                info.Description = vfw ? "Custom VFW DLL" : "libavcodec";

                return info;
            }
        }

        private class VideoCodecSettings
        {
            private readonly List<CodecSettings> _allCodecs = new List<CodecSettings>();

            private readonly List<CustomVideoCodecSettings> CustomCodecs = new List<CustomVideoCodecSettings>
            {
                CustomVideoCodecSettings.New(false),
                CustomVideoCodecSettings.New(true)
            };

            public readonly List<BuiltInVideoCodecSettings> BuiltInCodecs = new List<BuiltInVideoCodecSettings>();
            public string RadioButtonGroupName;
            public string Title;
            public CheckBox ShowCodecDialogCheckBox;

            public FrameworkElement CreateGui()
            {
                _allCodecs.AddRange(BuiltInCodecs);
                _allCodecs.AddRange(CustomCodecs);

                foreach(var settings in BuiltInCodecs)
                {
                    var codecRadioButton = new RadioButton();
                    settings.Button = codecRadioButton;
                    codecRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
                    codecRadioButton.VerticalAlignment = VerticalAlignment.Top;
                    codecRadioButton.Margin = new Thickness(5, 5, 5, 0);
                    codecRadioButton.GroupName = RadioButtonGroupName;
                    codecRadioButton.Content = settings.Description;
                }

                foreach(var settings in CustomCodecs)
                {
                    var customCodecRadioButton = new RadioButton();
                    settings.Button = customCodecRadioButton;
                    customCodecRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
                    customCodecRadioButton.VerticalAlignment = VerticalAlignment.Center;
                    customCodecRadioButton.Margin = new Thickness(5, 5, 5, 0);
                    customCodecRadioButton.GroupName = RadioButtonGroupName;
                    customCodecRadioButton.Content = settings.Description;
            
                    var customCodecEditBox = new TextBox();
                    settings.EditBox = customCodecEditBox;
                    customCodecEditBox.HorizontalAlignment = HorizontalAlignment.Left;
                    customCodecEditBox.VerticalAlignment = VerticalAlignment.Center;
                    customCodecEditBox.Margin = new Thickness(5, 5, 5, 0);
                    if(settings.VideoForWindows)
                    {
                        customCodecEditBox.Width = 100;
                    }
                    else
                    {
                        customCodecEditBox.MinWidth = 100;
                    }
                    
                    var customCodecRowList = new List<FrameworkElement> { customCodecRadioButton, customCodecEditBox };
                    var customCodecRow = WpfHelper.CreateRow(customCodecRowList, 120, 0, 0);
                    customCodecRow.Margin = new Thickness(0);
                    settings.Panel = customCodecRow;
                }

                var showCodecDialogCheckBox = new CheckBox();
                ShowCodecDialogCheckBox = showCodecDialogCheckBox;
                showCodecDialogCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
                showCodecDialogCheckBox.VerticalAlignment = VerticalAlignment.Center;
                showCodecDialogCheckBox.Margin = new Thickness(5);
                showCodecDialogCheckBox.Content = "  Show CODEC Dialog? (Only for the first job)";

                var videoCodecStackPanel = new StackPanel();
                videoCodecStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                videoCodecStackPanel.VerticalAlignment = VerticalAlignment.Stretch;
                videoCodecStackPanel.Margin = new Thickness(5, 5, 5, 0);
                videoCodecStackPanel.Orientation = Orientation.Vertical;
                foreach(var settings in BuiltInCodecs) videoCodecStackPanel.Children.Add(settings.Button);
                foreach(var settings in CustomCodecs) videoCodecStackPanel.Children.Add(settings.Panel);
                videoCodecStackPanel.Children.Add(showCodecDialogCheckBox);

                var videoCodecGroupBox = new GroupBox();
                videoCodecGroupBox.HorizontalAlignment = HorizontalAlignment.Left;
                videoCodecGroupBox.VerticalAlignment = VerticalAlignment.Top;
                videoCodecGroupBox.Margin = new Thickness(5);
                videoCodecGroupBox.Header = Title;
                videoCodecGroupBox.Content = videoCodecStackPanel;

                return videoCodecGroupBox;
            }

            public void LoadSettings(VideoCodec codec, string customOptionsLavc, string customNameVfw, bool showCodecDialog)
            {
                foreach(var codecSettings in _allCodecs)
                {
                    if(codecSettings.Codec == codec)
                    {
                        codecSettings.Button.IsChecked = true;
                        break;
                    }
                }

                CustomCodecs[0].EditBox.Text = customOptionsLavc;
                CustomCodecs[1].EditBox.Text = customNameVfw;
                ShowCodecDialogCheckBox.IsChecked = showCodecDialog;
            }

            public void SaveSettings(out VideoCodec codec, out string customOptionsLavc, out string customNameVfw, out bool showCodecDialog)
            {
                codec = VideoCodec.Copy;
                foreach(var codecSettings in _allCodecs)
                {
                    var button = codecSettings.Button;
                    if(button.IsChecked.HasValue && button.IsChecked.Value)
                    {
                        codec = codecSettings.Codec;
                        break;
                    }
                }

                customOptionsLavc = CustomCodecs[0].EditBox.Text;
                customNameVfw = CustomCodecs[1].EditBox.Text;
                showCodecDialog = ShowCodecDialogCheckBox.IsChecked.HasValue && ShowCodecDialogCheckBox.IsChecked.Value;
            }
        }

        private class FileNamingOption
        {
            public FileNamingOption(FileNamingMethod method, RadioButton button)
            {
                Method = method;
                Button = button;
            }

            public FileNamingMethod Method;
            public RadioButton Button;
        }

        // CODEC.
        private readonly VideoCodecSettings _colorCodecSettings = new VideoCodecSettings();
        private readonly VideoCodecSettings _monochromeCodecSettings = new VideoCodecSettings();

        // General.
        private TextBox _mEncoderFilePathEditBox;
        private TextBox _inputFrameRateEditBox;
        private TextBox _outputFrameRateEditBox;
        private TextBox _framesToSkipEditBox;
        private CheckBox _displayMEncoderStdErrCheckBox;
        private CheckBox _outputAllFilesToSameFolderCheckBox;
        private TextBox _outputFolderTextBox;

        // File naming.
        private readonly List<FileNamingOption> _fileNamingOptions = new List<FileNamingOption>();
        private TextBox _fileNamingPrefixTextBox;
        private TextBox _fileNamingSuffixTextBox;
        private TextBox _fileNamingRegExpMatchTextBox;
        private TextBox _fileNamingReplacementTextBox;

        // Image sequence file naming.
        private RadioButton _fileNamingUseImageNameRadioButton;

        private FrameworkElement CreateSettingsTab()
        {
            _colorCodecSettings.Title = "Color Video CODEC";
            _colorCodecSettings.RadioButtonGroupName = "Color Video CODEC";
            _colorCodecSettings.BuiltInCodecs.Add(BuiltInVideoCodecSettings.New(VideoCodec.Raw, "Raw"));
            _colorCodecSettings.BuiltInCodecs.Add(BuiltInVideoCodecSettings.New(VideoCodec.Copy, "Copy"));
            _colorCodecSettings.BuiltInCodecs.Add(BuiltInVideoCodecSettings.New(VideoCodec.Lagarith, "Lagarith"));
            var colorCodecPanel = _colorCodecSettings.CreateGui();

            _monochromeCodecSettings.Title = "Monochrome Video CODEC (only for depth/stencil sequences)";
            _monochromeCodecSettings.RadioButtonGroupName = "Monochrome Video CODEC";
            _monochromeCodecSettings.BuiltInCodecs.Add(BuiltInVideoCodecSettings.New(VideoCodec.Raw, "Raw"));
            _monochromeCodecSettings.BuiltInCodecs.Add(BuiltInVideoCodecSettings.New(VideoCodec.Copy, "Copy"));
            var monochromeCodecPanel = _monochromeCodecSettings.CreateGui();

            var generalGroupBox = CreateGeneralSettingsGui();
            var fileNamingGroupBox = CreateFileNamingRulesGui();
            var fileNamingSequenceGroupBox = CreateSequenceNamingRulesGui();

            var wrapPanel = new WrapPanel();
            wrapPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            wrapPanel.VerticalAlignment = VerticalAlignment.Stretch;
            wrapPanel.Margin = new Thickness(5);
            wrapPanel.Orientation = Orientation.Horizontal;
            wrapPanel.Children.Add(colorCodecPanel);
            wrapPanel.Children.Add(monochromeCodecPanel);
            wrapPanel.Children.Add(generalGroupBox);
            wrapPanel.Children.Add(fileNamingSequenceGroupBox);
            wrapPanel.Children.Add(fileNamingGroupBox);

            var scrollViewer = new ScrollViewer();
            scrollViewer.HorizontalAlignment = HorizontalAlignment.Stretch;
            scrollViewer.VerticalAlignment = VerticalAlignment.Stretch;
            scrollViewer.Margin = new Thickness(5);
            scrollViewer.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
            scrollViewer.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
            scrollViewer.Content = wrapPanel;

            return scrollViewer;
        }

        private FrameworkElement CreateGeneralSettingsGui()
        {
            var mEncoderFilePathLabel = new Label();
            mEncoderFilePathLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            mEncoderFilePathLabel.VerticalAlignment = VerticalAlignment.Center;
            mEncoderFilePathLabel.Margin = new Thickness(4, 5, 5, 0);
            mEncoderFilePathLabel.Content = "MEncoder Path";

            var mEncoderFilePathEditBox = new TextBox();
            _mEncoderFilePathEditBox = mEncoderFilePathEditBox;
            mEncoderFilePathEditBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            mEncoderFilePathEditBox.VerticalAlignment = VerticalAlignment.Center;
            mEncoderFilePathEditBox.Margin = new Thickness(5, 5, 5, 0);
            mEncoderFilePathEditBox.MinWidth = 150;

            var mEncoderFilePathButton = new Button();
            mEncoderFilePathButton.HorizontalAlignment = HorizontalAlignment.Left;
            mEncoderFilePathButton.VerticalContentAlignment = VerticalAlignment.Center;
            mEncoderFilePathButton.Margin = new Thickness(5, 5, 5, 0);
            mEncoderFilePathButton.Width = 35;
            mEncoderFilePathButton.Height = 20;
            mEncoderFilePathButton.Content = "...";
            mEncoderFilePathButton.Click += (obj, args) => OnMEncoderFilePathBrowse();

            var mEncoderFilePathStackPanel = new DockPanel();
            mEncoderFilePathStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            mEncoderFilePathStackPanel.VerticalAlignment = VerticalAlignment.Stretch;
            mEncoderFilePathStackPanel.Margin = new Thickness(0);
            mEncoderFilePathStackPanel.LastChildFill = true;
            mEncoderFilePathStackPanel.Children.Add(mEncoderFilePathButton);
            mEncoderFilePathStackPanel.Children.Add(mEncoderFilePathLabel);
            mEncoderFilePathStackPanel.Children.Add(mEncoderFilePathEditBox);
            DockPanel.SetDock(mEncoderFilePathButton, Dock.Right);
            DockPanel.SetDock(mEncoderFilePathLabel, Dock.Left);
            DockPanel.SetDock(mEncoderFilePathEditBox, Dock.Left);

            var inputFrameRateLabel = new Label();
            inputFrameRateLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            inputFrameRateLabel.VerticalAlignment = VerticalAlignment.Center;
            inputFrameRateLabel.Margin = new Thickness(4, 5, 5, 0);
            inputFrameRateLabel.Content = "Input Frame Rate";

            var outputFrameRateLabel = new Label();
            outputFrameRateLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            outputFrameRateLabel.VerticalAlignment = VerticalAlignment.Center;
            outputFrameRateLabel.Margin = new Thickness(4, 5, 5, 0);
            outputFrameRateLabel.Content = "Output Frame Rate";
            outputFrameRateLabel.ToolTip = "Output FPS is only used for image sequences";

            var inputFrameRateEditBox = new TextBox();
            _inputFrameRateEditBox = inputFrameRateEditBox;
            inputFrameRateEditBox.HorizontalAlignment = HorizontalAlignment.Left;
            inputFrameRateEditBox.VerticalAlignment = VerticalAlignment.Center;
            inputFrameRateEditBox.Margin = new Thickness(5, 5, 5, 0);
            inputFrameRateEditBox.Width = 30;

            var outputFrameRateEditBox = new TextBox();
            _outputFrameRateEditBox = outputFrameRateEditBox;
            outputFrameRateEditBox.HorizontalAlignment = HorizontalAlignment.Left;
            outputFrameRateEditBox.VerticalAlignment = VerticalAlignment.Center;
            outputFrameRateEditBox.Margin = new Thickness(5, 5, 5, 0);
            outputFrameRateEditBox.Width = 30;
            outputFrameRateEditBox.ToolTip = "Output FPS is only used for image sequences";

            var framesToSkipLabel = new Label();
            framesToSkipLabel.HorizontalAlignment = HorizontalAlignment.Stretch;
            framesToSkipLabel.VerticalAlignment = VerticalAlignment.Center;
            framesToSkipLabel.Margin = new Thickness(4, 5, 5, 0);
            framesToSkipLabel.Content = "Frames to Skip";

            var framesToSkipEditBox = new TextBox();
            _framesToSkipEditBox = framesToSkipEditBox;
            framesToSkipEditBox.HorizontalAlignment = HorizontalAlignment.Left;
            framesToSkipEditBox.VerticalAlignment = VerticalAlignment.Center;
            framesToSkipEditBox.Margin = new Thickness(5, 5, 5, 0);
            framesToSkipEditBox.Width = 30;

            var frameRatePanelList = new List<Tuple<FrameworkElement, FrameworkElement>>();
            var inputFrameRatePanelTuple = Tuple.Create<FrameworkElement, FrameworkElement>(inputFrameRateLabel, inputFrameRateEditBox);
            var outputFrameRatePanelTuple = Tuple.Create<FrameworkElement, FrameworkElement>(outputFrameRateLabel, outputFrameRateEditBox);
            var framesToSkipPanelTuple = Tuple.Create<FrameworkElement, FrameworkElement>(framesToSkipLabel, framesToSkipEditBox);
            frameRatePanelList.Add(inputFrameRatePanelTuple);
            frameRatePanelList.Add(outputFrameRatePanelTuple);
            frameRatePanelList.Add(framesToSkipPanelTuple);
            var frameRatePanel = WpfHelper.CreateDualColumnPanel(frameRatePanelList, 120, 0, 0);
            frameRatePanel.Margin = new Thickness(0);

            var displayMEncoderStdErrCheckBox = new CheckBox();
            _displayMEncoderStdErrCheckBox = displayMEncoderStdErrCheckBox;
            displayMEncoderStdErrCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            displayMEncoderStdErrCheckBox.VerticalAlignment = VerticalAlignment.Center;
            displayMEncoderStdErrCheckBox.Margin = new Thickness(7, 5, 5, 5);
            displayMEncoderStdErrCheckBox.Content = "  Display MEncoder's stderr output?";

            var outputAllFilesToSameFolderCheckBox = new CheckBox();
            _outputAllFilesToSameFolderCheckBox = outputAllFilesToSameFolderCheckBox;
            outputAllFilesToSameFolderCheckBox.HorizontalAlignment = HorizontalAlignment.Left;
            outputAllFilesToSameFolderCheckBox.VerticalAlignment = VerticalAlignment.Center;
            outputAllFilesToSameFolderCheckBox.Margin = new Thickness(7, 5, 5, 5);
            outputAllFilesToSameFolderCheckBox.Content = "  Output all files to this folder?";
            outputAllFilesToSameFolderCheckBox.Checked += (obj, args) => OnOutputAllFilesToSameFolderChecked();
            outputAllFilesToSameFolderCheckBox.Unchecked += (obj, args) => OnOutputAllFilesToSameFolderChecked();

            var outputFolderTextBox = new TextBox();
            _outputFolderTextBox = outputFolderTextBox;
            outputFolderTextBox.HorizontalAlignment = HorizontalAlignment.Left;
            outputFolderTextBox.VerticalAlignment = VerticalAlignment.Center;
            outputFolderTextBox.Margin = new Thickness(7, 5, 5, 5);
            outputFolderTextBox.MinWidth = 150;

            var outputFolderBrowseButton = new Button();
            outputFolderBrowseButton.HorizontalAlignment = HorizontalAlignment.Stretch;
            outputFolderBrowseButton.VerticalAlignment = VerticalAlignment.Center;
            outputFolderBrowseButton.Margin = new Thickness(5);
            outputFolderBrowseButton.Width = 35;
            outputFolderBrowseButton.Height = 20;
            outputFolderBrowseButton.Content = "...";
            outputFolderBrowseButton.Click += (obj, args) => OnOutputFolderBrowse();

            var outputFolderOpenButton = new Button();
            outputFolderOpenButton.HorizontalAlignment = HorizontalAlignment.Left;
            outputFolderOpenButton.VerticalAlignment = VerticalAlignment.Center;
            outputFolderOpenButton.Margin = new Thickness(5);
            outputFolderOpenButton.Width = 70;
            outputFolderOpenButton.Height = 20;
            outputFolderOpenButton.Content = "Open";
            outputFolderOpenButton.Click += (obj, args) => OnOutputFolderOpen();

            var outputFolderStackPanel = new DockPanel();
            outputFolderStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            outputFolderStackPanel.VerticalAlignment = VerticalAlignment.Stretch;
            outputFolderStackPanel.Margin = new Thickness(0);
            outputFolderStackPanel.LastChildFill = true;
            outputFolderStackPanel.Children.Add(outputFolderOpenButton);
            outputFolderStackPanel.Children.Add(outputFolderBrowseButton);
            outputFolderStackPanel.Children.Add(outputFolderTextBox);
            DockPanel.SetDock(outputFolderOpenButton, Dock.Right);
            DockPanel.SetDock(outputFolderBrowseButton, Dock.Right);
            DockPanel.SetDock(outputFolderTextBox, Dock.Right);

            var generalStackPanel = new StackPanel();
            generalStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            generalStackPanel.VerticalAlignment = VerticalAlignment.Stretch;
            generalStackPanel.Margin = new Thickness(5);
            generalStackPanel.Orientation = Orientation.Vertical;
            generalStackPanel.Children.Add(mEncoderFilePathStackPanel);
            generalStackPanel.Children.Add(frameRatePanel);
            generalStackPanel.Children.Add(displayMEncoderStdErrCheckBox);
            generalStackPanel.Children.Add(outputAllFilesToSameFolderCheckBox);
            generalStackPanel.Children.Add(outputFolderStackPanel);

            var generalGroupBox = new GroupBox();
            generalGroupBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            generalGroupBox.VerticalAlignment = VerticalAlignment.Stretch;
            generalGroupBox.Margin = new Thickness(5);
            generalGroupBox.Header = "General Settings";
            generalGroupBox.Content = generalStackPanel;

            return generalGroupBox;
        }

        private FrameworkElement CreateFileNamingRulesGui()
        {
            var margin = new Thickness(5, 5, 5, 0);

            var fileNamingNoChangeRadioButton = new RadioButton();
            _fileNamingOptions.Add(new FileNamingOption(FileNamingMethod.NoChange, fileNamingNoChangeRadioButton));
            fileNamingNoChangeRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingNoChangeRadioButton.VerticalAlignment = VerticalAlignment.Center;
            fileNamingNoChangeRadioButton.Margin = margin;
            fileNamingNoChangeRadioButton.Content = "No change";
            fileNamingNoChangeRadioButton.GroupName = "File Naming Rules";

            var fileNamingPrefixRadioButton = new RadioButton();
            _fileNamingOptions.Add(new FileNamingOption(FileNamingMethod.AddPrefix, fileNamingPrefixRadioButton));
            fileNamingPrefixRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingPrefixRadioButton.VerticalAlignment = VerticalAlignment.Center;
            fileNamingPrefixRadioButton.Margin = margin;
            fileNamingPrefixRadioButton.Content = "Add prefix";
            fileNamingPrefixRadioButton.GroupName = "File Naming Rules";

            var fileNamingPrefixTextBox = new TextBox();
            _fileNamingPrefixTextBox = fileNamingPrefixTextBox;
            fileNamingPrefixTextBox.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingPrefixTextBox.VerticalAlignment = VerticalAlignment.Center;
            fileNamingPrefixTextBox.Margin = margin;
            fileNamingPrefixTextBox.MinWidth = 90;

            var fileNamingSuffixRadioButton = new RadioButton();
            _fileNamingOptions.Add(new FileNamingOption(FileNamingMethod.AddSuffix, fileNamingSuffixRadioButton));
            fileNamingSuffixRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingSuffixRadioButton.VerticalAlignment = VerticalAlignment.Center;
            fileNamingSuffixRadioButton.Margin = margin;
            fileNamingSuffixRadioButton.Content = "Add suffix";
            fileNamingSuffixRadioButton.GroupName = "File Naming Rules";

            var fileNamingSuffixTextBox = new TextBox();
            _fileNamingSuffixTextBox = fileNamingSuffixTextBox;
            fileNamingSuffixTextBox.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingSuffixTextBox.VerticalAlignment = VerticalAlignment.Center;
            fileNamingSuffixTextBox.Margin = margin;
            fileNamingSuffixTextBox.MinWidth = 90;

            var fileNamingRegExpRadioButton = new RadioButton();
            _fileNamingOptions.Add(new FileNamingOption(FileNamingMethod.ApplyRegExp, fileNamingRegExpRadioButton));
            fileNamingRegExpRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingRegExpRadioButton.VerticalAlignment = VerticalAlignment.Center;
            fileNamingRegExpRadioButton.Margin = margin;
            fileNamingRegExpRadioButton.Content = "Apply reg. exp.";
            fileNamingRegExpRadioButton.GroupName = "File Naming Rules";

            var fileNamingRegExpMatchTextBox = new TextBox();
            _fileNamingRegExpMatchTextBox = fileNamingRegExpMatchTextBox;
            fileNamingRegExpMatchTextBox.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingRegExpMatchTextBox.VerticalAlignment = VerticalAlignment.Center;
            fileNamingRegExpMatchTextBox.Margin = margin;
            fileNamingRegExpMatchTextBox.MinWidth = 90;

            var fileNamingReplacementTextBox = new TextBox();
            _fileNamingReplacementTextBox = fileNamingReplacementTextBox;
            fileNamingReplacementTextBox.HorizontalAlignment = HorizontalAlignment.Left;
            fileNamingReplacementTextBox.VerticalAlignment = VerticalAlignment.Center;
            fileNamingReplacementTextBox.Margin = margin;
            fileNamingReplacementTextBox.MinWidth = 90;

            var regExRow = new List<FrameworkElement>();
            regExRow.Add(fileNamingRegExpRadioButton);
            regExRow.Add(fileNamingRegExpMatchTextBox);
            regExRow.Add(fileNamingReplacementTextBox);

            var fileNamingStackPanel = new StackPanel();
            fileNamingStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            fileNamingStackPanel.VerticalAlignment = VerticalAlignment.Stretch;
            fileNamingStackPanel.Margin = new Thickness(5);
            fileNamingStackPanel.Orientation = Orientation.Vertical;
            fileNamingStackPanel.Children.Add(fileNamingNoChangeRadioButton);
            fileNamingStackPanel.Children.Add(WpfHelper.CreateRow(fileNamingPrefixRadioButton, fileNamingPrefixTextBox, 100, 0, 0));
            fileNamingStackPanel.Children.Add(WpfHelper.CreateRow(fileNamingSuffixRadioButton, fileNamingSuffixTextBox, 100, 0, 0));
            fileNamingStackPanel.Children.Add(WpfHelper.CreateRow(regExRow, 100, 0, 0));

            var fileNamingGroupBox = new GroupBox();
            fileNamingGroupBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            fileNamingGroupBox.VerticalAlignment = VerticalAlignment.Stretch;
            fileNamingGroupBox.Margin = new Thickness(5);
            fileNamingGroupBox.Header = "Output File Naming Rules";
            fileNamingGroupBox.Content = fileNamingStackPanel;

            return fileNamingGroupBox;
        }

        private FrameworkElement CreateSequenceNamingRulesGui()
        {
            var margin = new Thickness(5, 5, 5, 0);

            var useDirectoryRadioButton = new RadioButton();
            useDirectoryRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
            useDirectoryRadioButton.VerticalAlignment = VerticalAlignment.Center;
            useDirectoryRadioButton.Margin = margin;
            useDirectoryRadioButton.Content = "Use the parent directory's name";
            useDirectoryRadioButton.GroupName = "Sequence File Naming Rules";
            useDirectoryRadioButton.IsChecked = !Config.FileNamingUseImageName;

            var useFileRadioButton = new RadioButton();
            _fileNamingUseImageNameRadioButton = useFileRadioButton;
            useFileRadioButton.HorizontalAlignment = HorizontalAlignment.Left;
            useFileRadioButton.VerticalAlignment = VerticalAlignment.Center;
            useFileRadioButton.Margin = margin;
            useFileRadioButton.Content = "Use the image sequence's (file) name";
            useFileRadioButton.GroupName = "Sequence File Naming Rules";
            useFileRadioButton.IsChecked = Config.FileNamingUseImageName;

            var fileNamingStackPanel = new StackPanel();
            fileNamingStackPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
            fileNamingStackPanel.VerticalAlignment = VerticalAlignment.Stretch;
            fileNamingStackPanel.Margin = new Thickness(5);
            fileNamingStackPanel.Orientation = Orientation.Vertical;
            fileNamingStackPanel.Children.Add(useDirectoryRadioButton);
            fileNamingStackPanel.Children.Add(useFileRadioButton);

            var fileNamingGroupBox = new GroupBox();
            fileNamingGroupBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            fileNamingGroupBox.VerticalAlignment = VerticalAlignment.Stretch;
            fileNamingGroupBox.Margin = new Thickness(5);
            fileNamingGroupBox.Header = "Image Sequence File Naming Rules";
            fileNamingGroupBox.Content = fileNamingStackPanel;

            return fileNamingGroupBox;
        }

        private void OnOutputAllFilesToSameFolderChecked()
        {
            _outputFolderTextBox.IsEnabled = _outputAllFilesToSameFolderCheckBox.IsChecked.HasValue && _outputAllFilesToSameFolderCheckBox.IsChecked.Value;
        }

        private void OnOutputFolderBrowse()
        {
            var folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            folderBrowserDialog.Description = "Select the Output Folder";
            folderBrowserDialog.RootFolder = Environment.SpecialFolder.Desktop;
            folderBrowserDialog.ShowNewFolderButton = true;
            if(folderBrowserDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
            {
                return;
            }

            var folderPath = folderBrowserDialog.SelectedPath;
            if(!Directory.Exists(folderPath))
            {
                return;
            }

            _outputFolderTextBox.Text = folderPath;
        }

        private void OnOutputFolderOpen()
        {
            var folderPath = _outputFolderTextBox.Text;
            if(Directory.Exists(folderPath))
            {
                Process.Start(folderPath);
            }
        }

        private void OnMEncoderFilePathBrowse()
        {
            using(var openFileDialog = new System.Windows.Forms.OpenFileDialog())
            {
                openFileDialog.CheckPathExists = true;
                openFileDialog.Multiselect = false;
                openFileDialog.InitialDirectory = System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
                openFileDialog.Filter = "Executable (*.exe)|*.exe";
                if(openFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                _mEncoderFilePathEditBox.Text = openFileDialog.FileName;
            }

            IsMEncoderPathValid(_mEncoderFilePathEditBox.Text, true);
        }
        
        private void SaveSettings()
        {
            _colorCodecSettings.SaveSettings(out Config.ColorCodec, out Config.CustomColorOptionsLavc, out Config.CustomColorVfwCodecName, out Config.ShowColorCodecDialog);
            _monochromeCodecSettings.SaveSettings(out Config.MonochromeCodec, out Config.CustomMonochromeOptionsLavc, out Config.CustomMonochromeVfwCodecName, out Config.ShowMonochromeCodecDialog);
            SaveGeneralSettings();
            SaveFileNamingSettings();
        }

        private void LoadSettings()
        {
            _colorCodecSettings.LoadSettings(Config.ColorCodec, Config.CustomColorOptionsLavc, Config.CustomColorVfwCodecName, Config.ShowColorCodecDialog);
            _monochromeCodecSettings.LoadSettings(Config.MonochromeCodec, Config.CustomMonochromeOptionsLavc, Config.CustomMonochromeVfwCodecName, Config.ShowMonochromeCodecDialog);
            LoadGeneralSettings();
            LoadFileNamingSettings();
        }

        private void SaveGeneralSettings()
        {
            Config.MEncoderFilePath = _mEncoderFilePathEditBox.Text;
            int.TryParse(_inputFrameRateEditBox.Text, out Config.FrameRate);
            int.TryParse(_outputFrameRateEditBox.Text, out Config.OutputFrameRate);
            int.TryParse(_framesToSkipEditBox.Text, out Config.FramesToSkip);
            Config.DisplayMEncoderStdErr = _displayMEncoderStdErrCheckBox.IsChecked.HasValue && _displayMEncoderStdErrCheckBox.IsChecked.Value;
            Config.OutputAllFilesToSameFolder = _outputAllFilesToSameFolderCheckBox.IsChecked.HasValue && _outputAllFilesToSameFolderCheckBox.IsChecked.Value;
            Config.OutputFolderPath = _outputFolderTextBox.Text;
        }

        private void LoadGeneralSettings()
        {
            _mEncoderFilePathEditBox.Text = Config.MEncoderFilePath;
            _inputFrameRateEditBox.Text = Config.FrameRate.ToString();
            _outputFrameRateEditBox.Text = Config.OutputFrameRate.ToString();
            _framesToSkipEditBox.Text = Config.FramesToSkip.ToString();
            _displayMEncoderStdErrCheckBox.IsChecked = Config.DisplayMEncoderStdErr;
            _outputAllFilesToSameFolderCheckBox.IsChecked = Config.OutputAllFilesToSameFolder;
            _outputFolderTextBox.Text = Config.OutputFolderPath;
            OnOutputAllFilesToSameFolderChecked();
        }

        private void SaveFileNamingSettings()
        {
            var method = FileNamingMethod.NoChange;
            foreach(var option in _fileNamingOptions)
            {
                var button = option.Button;
                if(button.IsChecked.HasValue && button.IsChecked.Value)
                {
                    method = option.Method;
                    break;
                }
            }

            Config.FileNamingPolicy = method;
            Config.FileNamingPrefix = _fileNamingPrefixTextBox.Text;
            Config.FileNamingSuffix = _fileNamingSuffixTextBox.Text;
            Config.FileNamingRegExpMatch = _fileNamingRegExpMatchTextBox.Text;
            Config.FileNamingRegExpReplacement = _fileNamingReplacementTextBox.Text;

            // Image sequence mode.
            Config.FileNamingUseImageName = _fileNamingUseImageNameRadioButton.IsChecked ?? false;
        }

        private void LoadFileNamingSettings()
        {
            var method = Config.FileNamingPolicy;
            foreach(var option in _fileNamingOptions)
            {
                if(option.Method == method)
                {
                    option.Button.IsChecked = true;
                    break;
                }
            }

            _fileNamingPrefixTextBox.Text = Config.FileNamingPrefix;
            _fileNamingSuffixTextBox.Text = Config.FileNamingSuffix;
            _fileNamingRegExpMatchTextBox.Text = Config.FileNamingRegExpMatch;
            _fileNamingReplacementTextBox.Text = Config.FileNamingRegExpReplacement;

            // Image sequence mode.
            _fileNamingUseImageNameRadioButton.IsChecked = Config.FileNamingUseImageName;
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            LoadSettings();
        }

        protected override void SaveConfig()
        {
            SaveSettings();

            base.SaveConfig();
        }
    }
}