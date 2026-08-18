using System;
using System.Windows.Input;

namespace ScreenRecorderApp.ViewModels;

public class RelayCommand : ICommand
{
	private readonly Action _execute;

	private readonly Func<bool>? _canExecute;

	public event EventHandler? CanExecuteChanged
	{
		add
		{
			CommandManager.RequerySuggested += value;
		}
		remove
		{
			CommandManager.RequerySuggested -= value;
		}
	}

	public RelayCommand(Action execute, Func<bool>? canExecute = null)
	{
		_execute = execute ?? throw new ArgumentNullException("execute");
		_canExecute = canExecute;
	}

	public bool CanExecute(object? parameter)
	{
		return _canExecute?.Invoke() ?? true;
	}

	public void Execute(object? parameter)
	{
		_execute();
	}

	public void RaiseCanExecuteChanged()
	{
		CommandManager.InvalidateRequerySuggested();
	}
}
