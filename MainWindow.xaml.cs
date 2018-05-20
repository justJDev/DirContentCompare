using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace DirContentCompare {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		public MainWindow() {
			InitializeComponent();
			showCommon.IsEnabled = showUnique.IsEnabled = showDuplicates.IsEnabled = false;
		}

		private void leftPathSelect_Click(object sender, RoutedEventArgs e) {
			System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.Description = "Select Left folder to compare...";
			dialog.ShowNewFolderButton = false;
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				leftPath.Text = dialog.SelectedPath;
			}
		}

		private void rightPathSelect_Click(object sender, RoutedEventArgs e) {
			System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
			dialog.Description = "Select Right folder to compare...";
			dialog.ShowNewFolderButton = false;
			if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK) {
				rightPath.Text = dialog.SelectedPath;
			}
		}

		private void compare_Click(object sender, RoutedEventArgs e) {
			DirectoryInfo leftDir = null;
			DirectoryInfo rightDir = null;
			try {
				leftDir = new DirectoryInfo(leftPath.Text);
				rightDir = new DirectoryInfo(rightPath.Text);
			} catch (Exception ex) {
				MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (!leftDir.Exists) {
				MessageBox.Show("Left folder does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (!rightDir.Exists) {
				MessageBox.Show("Right folder does not exist.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			if (leftPath.Text == rightPath.Text) {
				MessageBox.Show("Please select two folders", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
				return;
			}
			DirComparator dc = new DirComparator(leftDir, rightDir, ignore.Text);
			dc.Completed += Dc_Completed;
			dc.StatusChanged += Dc_StatusChanged;
			dc.CompareAsync();
		}

		private void Dc_StatusChanged(DirComparatorStatus status) {
			Dispatcher.Invoke(() => { this.status.Content = status; });
		}

		DirComparatorResult result;

		private void Dc_Completed(DirComparatorResult result) {
			this.result = result;
			status.Content = "Done";
			showDuplicates.IsChecked = false;

			showCommon.IsEnabled = showUnique.IsEnabled = showDuplicates.IsEnabled = true;
			showCommon.IsChecked = showDuplicates.IsChecked = false;
			showUnique.IsChecked = true;
		}

		private void FilesView_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			DirComparatorFileInfo file = ((ListViewItem)sender).Content as DirComparatorFileInfo;
			if (file != null) {
				Process.Start("explorer.exe", "/select," + file.File.FullName);
			}
		}

		private void listDisplayChanged(object sender, RoutedEventArgs e) {
			List<DirComparatorFileInfo> left = new List<DirComparatorFileInfo>();
			List<DirComparatorFileInfo> right = new List<DirComparatorFileInfo>();
			if (showCommon.IsChecked == true) {
				left.AddRange(result.Common);
				right.AddRange(result.Common);
			}
			if (showUnique.IsChecked == true) {
				left.AddRange(result.Left);
				right.AddRange(result.Right);
			}
			if (showDuplicates.IsChecked == true) {
				left.AddRange(result.DuplicateLeft);
				right.AddRange(result.DuplicateRight);
			}
			leftFilesView.ItemsSource = left;
			rightFilesView.ItemsSource = right;
		}
	}
}
