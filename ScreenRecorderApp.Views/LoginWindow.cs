using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using ScreenRecorderApp.ViewModels;

namespace ScreenRecorderApp.Views;

public partial class LoginWindow : Window
{
	private readonly LoginViewModel _viewModel;





	public LoginWindow()
	{
		InitializeComponent();
		_viewModel = new LoginViewModel();
		base.DataContext = _viewModel;
		PasswordBox.PasswordChanged += delegate
		{
			_viewModel.Password = PasswordBox.Password;
		};
		_viewModel.LoginSucceeded += delegate
		{
			new MainWindow().Show();
			Close();
		};
		base.KeyDown += delegate(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Return && _viewModel.CanLogin)
			{
				_viewModel.LoginCommand.Execute(null);
			}
		};
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void MinimizeButton_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = WindowState.Minimized;
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Application.Current.Shutdown();
}

}
