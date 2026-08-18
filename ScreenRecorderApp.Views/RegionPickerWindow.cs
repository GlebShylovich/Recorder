using System;
using System.CodeDom.Compiler;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Shapes;

namespace ScreenRecorderApp.Views;

public partial class RegionPickerWindow : Window
{
	private Point _startPoint;

	private bool _isDragging;

	public Rect? SelectedRect { get; private set; }

	public RegionPickerWindow()
	{
		InitializeComponent();
		base.KeyDown += delegate(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				SelectedRect = null;
				Close();
			}
		};
	}

	private void Canvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		_startPoint = e.GetPosition(DrawCanvas);
		_isDragging = true;
		DrawCanvas.CaptureMouse();
		Canvas.SetLeft(SelectionRect, _startPoint.X);
		Canvas.SetTop(SelectionRect, _startPoint.Y);
		SelectionRect.Width = 0.0;
		SelectionRect.Height = 0.0;
		SelectionRect.Visibility = Visibility.Visible;
	}

	private void Canvas_MouseMove(object sender, MouseEventArgs e)
	{
		if (_isDragging)
		{
			Point position = e.GetPosition(DrawCanvas);
			double length = Math.Min(position.X, _startPoint.X);
			double length2 = Math.Min(position.Y, _startPoint.Y);
			double width = Math.Abs(position.X - _startPoint.X);
			double height = Math.Abs(position.Y - _startPoint.Y);
			Canvas.SetLeft(SelectionRect, length);
			Canvas.SetTop(SelectionRect, length2);
			SelectionRect.Width = width;
			SelectionRect.Height = height;
		}
	}

	private void Canvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (_isDragging)
		{
			_isDragging = false;
			DrawCanvas.ReleaseMouseCapture();
			Point position = e.GetPosition(DrawCanvas);
			double num = Math.Min(position.X, _startPoint.X);
			double num2 = Math.Min(position.Y, _startPoint.Y);
			double num3 = Math.Abs(position.X - _startPoint.X);
			double num4 = Math.Abs(position.Y - _startPoint.Y);
			if (num3 > 10.0 && num4 > 10.0)
			{
				PresentationSource presentationSource = PresentationSource.FromVisual(this);
				double num5 = presentationSource?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
				double num6 = presentationSource?.CompositionTarget?.TransformToDevice.M22 ?? 1.0;
				SelectedRect = new Rect(num * num5, num2 * num6, num3 * num5, num4 * num6);
			}
			else
			{
				SelectedRect = null;
			}
			Close();
		}
	}
}
