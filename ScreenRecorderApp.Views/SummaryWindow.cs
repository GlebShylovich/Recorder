using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using ScreenRecorderApp.ViewModels;

namespace ScreenRecorderApp.Views;

public partial class SummaryWindow : Window
{
	private const string VirtualHost = "screenrecorderapp.video";

	private static Task<CoreWebView2Environment>? _environmentTask;

	private readonly SummaryViewModel _viewModel;

	private bool _isFullScreen;

	private WindowStyle _restoreWindowStyle;

	private ResizeMode _restoreResizeMode;

	private WindowState _restoreWindowState;

	private bool _restoreTopmost;

	private double _restoreLeft;

	private double _restoreTop;

	private double _restoreWidth;

	private double _restoreHeight;


	public SummaryWindow(SummaryViewModel viewModel)
	{
		SummaryWindow summaryWindow = this;
		InitializeComponent();
		_viewModel = viewModel;
		base.DataContext = viewModel;
		viewModel.RecordAgainRequested += delegate
		{
			summaryWindow.Close();
		};
		viewModel.DoneRequested += delegate
		{
			summaryWindow.Close();
		};
		viewModel.VideoReady += async delegate
		{
			await summaryWindow.LoadVideoAsync();
		};
		base.Loaded += async delegate
		{
			await viewModel.RunCompressionAsync();
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

	private void MaximizeButton_Click(object sender, RoutedEventArgs e)
	{
		base.WindowState = ((base.WindowState != WindowState.Maximized) ? WindowState.Maximized : WindowState.Normal);
	}

	private static Task<CoreWebView2Environment> GetEnvironmentAsync()
	{
		Task<CoreWebView2Environment> environmentTask = _environmentTask;
		if (environmentTask != null && environmentTask.IsCompleted && environmentTask.IsFaulted)
		{
			_environmentTask = null;
		}
		if (_environmentTask == null)
		{
			_environmentTask = CoreWebView2Environment.CreateAsync(null, Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScreenRecorderApp", "WebView2"), new CoreWebView2EnvironmentOptions
			{
				AdditionalBrowserArguments = "--autoplay-policy=no-user-gesture-required"
			});
		}
		return _environmentTask;
	}

	private async Task LoadVideoAsync()
	{
		string videoPath = _viewModel.VideoPath;
		if (string.IsNullOrEmpty(videoPath) || !File.Exists(videoPath))
		{
			return;
		}
		try
		{
			WebView2 videoWebView = VideoWebView;
			await videoWebView.EnsureCoreWebView2Async(await GetEnvironmentAsync());
			string directoryName = Path.GetDirectoryName(videoPath);
			string fileName = Path.GetFileName(videoPath);
			if (directoryName != null)
			{
				VideoWebView.CoreWebView2.SetVirtualHostNameToFolderMapping("screenrecorderapp.video", directoryName, CoreWebView2HostResourceAccessKind.Allow);
			}
			string text = "https://screenrecorderapp.video/" + Uri.EscapeDataString(fileName);
			string htmlContent = "<html>\n<head>\n<style>\n    html, body { margin: 0; padding: 0; background: #000; height: 100%; overflow: hidden; }\n    video { width: 100%; height: 100%; object-fit: contain; background: #000; }\n</style>\n</head>\n<body>\n    <video src=\"" + text + "\" controls autoplay></video>\n</body>\n</html>";
			VideoWebView.CoreWebView2.NavigateToString(htmlContent);
			VideoWebView.CoreWebView2.ContainsFullScreenElementChanged += CoreWebView2_ContainsFullScreenElementChanged;
		}
		catch (Exception)
		{
		}
	}

	private void CoreWebView2_ContainsFullScreenElementChanged(object? sender, object e)
	{
		if (VideoWebView.CoreWebView2.ContainsFullScreenElement)
		{
			EnterFullScreen();
		}
		else
		{
			ExitFullScreen();
		}
	}

	private void EnterFullScreen()
	{
		if (!_isFullScreen)
		{
			_isFullScreen = true;
			_restoreWindowStyle = base.WindowStyle;
			_restoreResizeMode = base.ResizeMode;
			_restoreWindowState = base.WindowState;
			_restoreTopmost = base.Topmost;
			_restoreLeft = base.Left;
			_restoreTop = base.Top;
			_restoreWidth = base.Width;
			_restoreHeight = base.Height;
			Rectangle bounds = Screen.FromHandle(new WindowInteropHelper(this).EnsureHandle()).Bounds;
			Matrix matrix = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformFromDevice ?? Matrix.Identity;
			System.Windows.Point point = matrix.Transform(new System.Windows.Point(bounds.Left, bounds.Top));
			System.Windows.Point point2 = matrix.Transform(new System.Windows.Point(bounds.Width, bounds.Height));
			base.WindowState = WindowState.Normal;
			base.WindowStyle = WindowStyle.None;
			base.ResizeMode = ResizeMode.NoResize;
			base.Topmost = true;
			base.Left = point.X;
			base.Top = point.Y;
			base.Width = point2.X;
			base.Height = point2.Y;
		}
	}

	private void ExitFullScreen()
	{
		if (_isFullScreen)
		{
			_isFullScreen = false;
			base.WindowStyle = _restoreWindowStyle;
			base.ResizeMode = _restoreResizeMode;
			base.Topmost = _restoreTopmost;
			base.Left = _restoreLeft;
			base.Top = _restoreTop;
			base.Width = _restoreWidth;
			base.Height = _restoreHeight;
			base.WindowState = _restoreWindowState;
		}
	}
}
