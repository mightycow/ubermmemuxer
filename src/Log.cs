using System;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;


namespace Uber
{
    public class Log
    {
        private delegate void VoidDelegate();

        private ListBox _logListBox;
        private MenuItem _logMenuItem;

        private static RoutedCommand _clearLogCommand = new RoutedCommand();
        private static RoutedCommand _copyLogCommand = new RoutedCommand();
        private static RoutedCommand _copyChatCommand = new RoutedCommand();

        public ListBox LogListBox
        {
            get { return _logListBox; }
        }

        public MenuItem LogMenuItem
        {
            get { return _logMenuItem; }
        }

        public Log(int height)
        {
            var logListBox = new ListBox();
            _logListBox = logListBox;
            _logListBox.SelectionMode = SelectionMode.Single;
            logListBox.HorizontalAlignment = HorizontalAlignment.Stretch;
            logListBox.VerticalAlignment = VerticalAlignment.Stretch;
            logListBox.Margin = new Thickness(5);
            logListBox.Height = height;
            logListBox.Resources.Add(SystemColors.HighlightBrushKey, new SolidColorBrush(Color.FromRgb(255, 255, 191)));

            InitLogListBoxClearCommand();
            InitLogListBoxCopyCommand();
            InitLogListBoxContextualMenu();

            var clearLogMenuItem = new MenuItem();
            clearLogMenuItem.Header = "Clear";
            clearLogMenuItem.Click += (obj, arg) => ClearLog();
            clearLogMenuItem.ToolTip = new ToolTip { Content = "Clears the log box" };

            var copyLogMenuItem = new MenuItem();
            copyLogMenuItem.Header = "Copy Everything";
            copyLogMenuItem.Click += (obj, arg) => CopyLog();
            copyLogMenuItem.ToolTip = new ToolTip { Content = "Copies the log to the Windows clipboard" };

            var copyLogSelectionMenuItem = new MenuItem();
            copyLogSelectionMenuItem.Header = "Copy Selection";
            copyLogSelectionMenuItem.Click += (obj, arg) => CopyLogSelection();
            copyLogSelectionMenuItem.ToolTip = new ToolTip { Content = "Copies the selected log line to the Windows clipboard" };

            var saveLogMenuItem = new MenuItem();
            saveLogMenuItem.Header = "Save to File...";
            saveLogMenuItem.Click += (obj, arg) => SaveLog();
            saveLogMenuItem.ToolTip = new ToolTip { Content = "Saves the log to the specified file" };

            var logMenuItem = new MenuItem();
            _logMenuItem = logMenuItem;
            logMenuItem.Header = "_Log";
            logMenuItem.Items.Add(clearLogMenuItem);
            logMenuItem.Items.Add(copyLogMenuItem);
            logMenuItem.Items.Add(copyLogSelectionMenuItem);
            logMenuItem.Items.Add(new Separator());
            logMenuItem.Items.Add(saveLogMenuItem);
        }

        public void LogMessage(string message, Color color)
        {
            VoidDelegate itemAdder = delegate
            {
                var textBlock = new TextBlock();
                textBlock.Foreground = new SolidColorBrush(color);
                textBlock.Text = message;
                _logListBox.Items.Add(textBlock);
                _logListBox.ScrollIntoView(textBlock);
            };

            _logListBox.Dispatcher.Invoke(itemAdder);
        }

        public void LogInfo(string message, params object[] args)
        {
            LogMessage(string.Format(message, args), Color.FromRgb(0, 0, 0));
        }

        public void LogWarning(string message, params object[] args)
        {
            LogMessage(string.Format(message, args), Color.FromRgb(255, 127, 0));
        }

        public void LogError(string message, params object[] args)
        {
            LogMessage(string.Format(message, args), Color.FromRgb(255, 0, 0));
        }

        private void InitLogListBoxClearCommand()
        {
            var inputGesture = new KeyGesture(Key.X, ModifierKeys.Control);
            var inputBinding = new KeyBinding(_clearLogCommand, inputGesture);
            var commandBinding = new CommandBinding();
            commandBinding.Command = _clearLogCommand;
            commandBinding.Executed += (obj, args) => ClearLog();
            commandBinding.CanExecute += (obj, args) => { args.CanExecute = true; };
            _logListBox.InputBindings.Add(inputBinding);
            _logListBox.CommandBindings.Add(commandBinding);
        }

        private void InitLogListBoxCopyCommand()
        {
            var inputGesture = new KeyGesture(Key.C, ModifierKeys.Control);
            var inputBinding = new KeyBinding(_copyLogCommand, inputGesture);
            var commandBinding = new CommandBinding();
            commandBinding.Command = _copyLogCommand;
            commandBinding.Executed += (obj, args) => CopyLogSelection();
            commandBinding.CanExecute += (obj, args) => { args.CanExecute = true; };
            _logListBox.InputBindings.Add(inputBinding);
            _logListBox.CommandBindings.Add(commandBinding);
        }

        private void InitLogListBoxContextualMenu()
        {
            var clearLogMenuItem = new MenuItem();
            clearLogMenuItem.Header = "Clear (Ctrl-X)";
            clearLogMenuItem.Command = _clearLogCommand;
            clearLogMenuItem.Click += (obj, args) => ClearLog();

            var copyLogMenuItem = new MenuItem();
            copyLogMenuItem.Header = "Copy (Ctrl-C)";
            copyLogMenuItem.Command = _copyLogCommand;
            copyLogMenuItem.Click += (obj, args) => CopyLogSelection();

            var contextMenu = new ContextMenu();
            contextMenu.Items.Add(clearLogMenuItem);
            contextMenu.Items.Add(copyLogMenuItem);

            _logListBox.ContextMenu = contextMenu;
        }

        private string GetLog()
        {
            var stringBuilder = new StringBuilder();

            foreach(var item in _logListBox.Items)
            {
                var label = item as Label;
                if(label == null)
                {
                    continue;
                }

                var line = label.Content as string;
                if(line == null)
                {
                    continue;
                }

                stringBuilder.AppendLine(line);
            }

            return stringBuilder.ToString();
        }

        private void ClearLog()
        {
            _logListBox.Items.Clear();
        }

        private void CopyLog()
        {
            Clipboard.SetDataObject(GetLog(), true);
        }

        private void CopyLogSelection()
        {
            var label = _logListBox.SelectedItem as Label;
            if(label == null)
            {
                return;
            }

            var line = label.Content as string;
            if(line == null)
            {
                return;
            }

            Clipboard.SetDataObject(line, true);
        }

        private void SaveLog()
        {
            using(var saveFileDialog = new System.Windows.Forms.SaveFileDialog())
            {
                saveFileDialog.InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                saveFileDialog.Filter = "text file (*.txt)|*.txt";
                if(saveFileDialog.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                {
                    return;
                }

                File.WriteAllText(saveFileDialog.FileName, GetLog());
            }
        }
    }
}