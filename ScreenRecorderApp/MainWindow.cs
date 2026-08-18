using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using ScreenRecorderApp.ViewModels;
using ScreenRecorderApp.Views;

namespace ScreenRecorderApp;

public partial class MainWindow : Window
{
	private readonly MainViewModel _viewModel;

	public MainWindow()
	{
		InitializeComponent();
		_viewModel = new MainViewModel();
		base.DataContext = _viewModel;
		base.Closed += delegate
		{
			_viewModel.Dispose();
		};
		_viewModel.RecordingSaved += ViewModel_RecordingSaved;
		base.SourceInitialized += delegate
		{
			_viewModel.SetOwnWindowHandle(new WindowInteropHelper(this).Handle);
		};
		base.Left = SystemParameters.PrimaryScreenWidth - base.Width - 10.0;
		base.Top = (SystemParameters.PrimaryScreenHeight - base.Height) / 2.0;
	}

	private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void MinimizeButton_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void ViewModel_RecordingSaved(object? sender, string filePath)
	{
		Hide();
		SummaryWindow summaryWindow = new SummaryWindow(new SummaryViewModel(_viewModel.SessionState));
		summaryWindow.Closed += delegate
		{
			Show();
		};
		summaryWindow.Show();
}

}
