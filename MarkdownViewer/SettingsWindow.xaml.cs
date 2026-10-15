using Microsoft.Win32;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace MarkdownViewer;

public partial class SettingsWindow : Window {
  public string EditorPath => EditorPathTextBox.Text.Trim();

  public ThemeSetting SelectedTheme =>
    (ThemeComboBox.SelectedItem as ComboBoxItem)?.Tag is string tag
      ? ThemeManager.ParseSetting(tag)
      : ThemeSetting.System;

  public SettingsWindow(string? initialEditorPath, ThemeSetting initialTheme) {
    InitializeComponent();
    EditorPathTextBox.Text = initialEditorPath ?? string.Empty;

    foreach(var item in ThemeComboBox.Items.OfType<ComboBoxItem>()) {
      if(item.Tag is string tag && ThemeManager.ParseSetting(tag) == initialTheme) {
        ThemeComboBox.SelectedItem = item;
        break;
      }
    }
  }

  private void BrowseButton_Click(object sender, RoutedEventArgs e) {
    var dialog = new OpenFileDialog {
      Title = "エディタを選択",
      Filter = "Executable Files (*.exe)|*.exe|All Files (*.*)|*.*",
      CheckFileExists = true,
      Multiselect = false
    };

    if(!string.IsNullOrWhiteSpace(EditorPathTextBox.Text)) {
      try {
        var currentPath = Path.GetFullPath(EditorPathTextBox.Text);
        var directory = Path.GetDirectoryName(currentPath);
        if(!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory)) {
          dialog.InitialDirectory = directory;
        }

        if(File.Exists(currentPath)) {
          dialog.FileName = currentPath;
        }
      } catch {
        // Ignore invalid input and fall back to default dialog location.
      }
    }

    if(dialog.ShowDialog(this) == true) {
      EditorPathTextBox.Text = dialog.FileName;
    }
  }

  private void ClearButton_Click(object sender, RoutedEventArgs e) {
    EditorPathTextBox.Clear();
  }

  private void SaveButton_Click(object sender, RoutedEventArgs e) {
    if(!string.IsNullOrWhiteSpace(EditorPath) && !File.Exists(EditorPath)) {
      MessageBox.Show(
        $"指定されたエディタが見つかりません。{Environment.NewLine}{EditorPath}",
        "設定",
        MessageBoxButton.OK,
        MessageBoxImage.Warning);
      return;
    }

    DialogResult = true;
  }
}
