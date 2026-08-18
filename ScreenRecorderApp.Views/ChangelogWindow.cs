using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;

namespace ScreenRecorderApp.Views;

public partial class ChangelogWindow : Window
{
	public ChangelogWindow(string markdownText)
	{
		InitializeComponent();
		ParseMarkdown(markdownText);
	}

	private void ParseMarkdown(string markdown)
	{
		ChangelogText.Inlines.Clear();
		string[] lines = markdown.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);

		foreach (string line in lines)
		{
			string trimmed = line.Trim();
			if (string.IsNullOrEmpty(trimmed))
			{
				ChangelogText.Inlines.Add(new Run("\n"));
				continue;
			}

			if (trimmed.StartsWith("###"))
			{
				AppendText("\n" + trimmed.TrimStart('#').Trim() + "\n", bold: true, size: 13);
			}
			else if (trimmed.StartsWith("##"))
			{
				AppendText("\n" + trimmed.TrimStart('#').Trim() + "\n", bold: true, size: 14);
			}
			else if (trimmed.StartsWith("#"))
			{
				AppendText("\n" + trimmed.TrimStart('#').Trim() + "\n", bold: true, size: 16);
			}
			else if (trimmed.StartsWith("-") || trimmed.StartsWith("*"))
			{
				AppendText("  •  " + trimmed.Substring(1).Trim() + "\n");
			}
			else
			{
				AppendText(line + "\n");
			}
		}
	}

	private void AppendText(string text, bool bold = false, double size = 12)
	{
		Run run = new Run(text)
		{
			FontSize = size
		};
		if (bold)
		{
			run.FontWeight = FontWeights.Bold;
		}
		ChangelogText.Inlines.Add(run);
	}

	private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		DragMove();
	}

	private void CloseButton_Click(object sender, RoutedEventArgs e)
	{
		Close();
	}
}
