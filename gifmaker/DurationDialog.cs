using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace ClipForgeWpf;

public class DurationDialog : Window
{
	private readonly TextBox _durationBox = new TextBox();

	private readonly double _sourceDuration;

	public int DurationMs { get; private set; }

	public DurationDialog(double sourceFps, int sourceDurationMs, int currentDurationMs)
	{
		base.Loaded += delegate
		{
			UiLanguage.TranslateDialog(this);
		};
		_sourceDuration = ((sourceFps > 0.0) ? (1000.0 / sourceFps) : ((double)Math.Max(sourceDurationMs, 1)));
		DurationMs = Math.Max(currentDurationMs, 1);
		base.Title = "Change Duration";
		base.Width = 520.0;
		base.Height = 260.0;
		base.ResizeMode = ResizeMode.NoResize;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.Background = Brushes.White;
		Grid grid = new Grid
		{
			Margin = new Thickness(18.0)
		};
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = GridLength.Auto
		});
		grid.RowDefinitions.Add(new RowDefinition
		{
			Height = new GridLength(1.0, GridUnitType.Star)
		});
		TextBlock element = new TextBlock
		{
			Text = UiLanguage.T($"Source: {sourceFps:0.##} FPS  |  Original duration: {_sourceDuration:0.##} ms"),
			FontSize = 14.0,
			FontWeight = FontWeights.SemiBold,
			Foreground = new SolidColorBrush(Color.FromRgb(68, 78, 96)),
			Margin = new Thickness(0.0, 0.0, 0.0, 14.0)
		};
		grid.Children.Add(element);
		UniformGrid uniformGrid = new UniformGrid
		{
			Columns = 4,
			Margin = new Thickness(0.0, 0.0, 0.0, 16.0)
		};
		Grid.SetRow(uniformGrid, 1);
		AddPreset(uniformGrid, "Original", 1.0);
		AddPreset(uniformGrid, "2x", 0.5);
		AddPreset(uniformGrid, "0.5x", 2.0);
		AddPreset(uniformGrid, "4x", 0.25);
		grid.Children.Add(uniformGrid);
		TextBlock element2 = new TextBlock
		{
			Text = "Duration (ms)",
			FontSize = 13.0,
			Foreground = new SolidColorBrush(Color.FromRgb(68, 78, 96)),
			Margin = new Thickness(0.0, 0.0, 0.0, 6.0)
		};
		Grid.SetRow(element2, 2);
		grid.Children.Add(element2);
		_durationBox.Text = DurationMs.ToString(CultureInfo.InvariantCulture);
		_durationBox.FontSize = 16.0;
		_durationBox.Height = 36.0;
		_durationBox.VerticalContentAlignment = VerticalAlignment.Center;
		_durationBox.PreviewTextInput += delegate(object _, TextCompositionEventArgs e)
		{
			e.Handled = !e.Text.All(char.IsDigit);
		};
		_durationBox.KeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Return)
			{
				Confirm();
			}
			else if (e.Key == Key.Escape)
			{
				base.DialogResult = false;
			}
		};
		Grid.SetRow(_durationBox, 3);
		grid.Children.Add(_durationBox);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			HorizontalAlignment = HorizontalAlignment.Right,
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		Button button = DialogButton("확인");
		button.Click += delegate
		{
			Confirm();
		};
		Button button2 = DialogButton("취소");
		button2.Click += delegate
		{
			base.DialogResult = false;
		};
		stackPanel.Children.Add(button);
		stackPanel.Children.Add(button2);
		Grid.SetRow(stackPanel, 4);
		grid.Children.Add(stackPanel);
		base.Content = grid;
		base.Loaded += delegate
		{
			_durationBox.Focus();
			_durationBox.SelectAll();
		};
	}

	private void AddPreset(Panel panel, string label, double multiplier)
	{
		Button button = DialogButton(label);
		button.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		button.Click += delegate
		{
			_durationBox.Text = Math.Max(1, (int)Math.Round(_sourceDuration * multiplier)).ToString(CultureInfo.InvariantCulture);
		};
		panel.Children.Add(button);
	}

	private static Button DialogButton(string text)
	{
		return new Button
		{
			Content = text,
			MinHeight = 34.0,
			Padding = new Thickness(16.0, 4.0, 16.0, 4.0),
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			Background = new SolidColorBrush(Color.FromRgb(239, 243, 248)),
			BorderBrush = new SolidColorBrush(Color.FromRgb(201, 213, 226)),
			Foreground = Brushes.Black
		};
	}

	private void Confirm()
	{
		if (!int.TryParse(_durationBox.Text.Trim(), out var result) || result < 1)
		{
			MessageBox.Show(this, "1ms 이상의 숫자를 입력하세요.", "Change Duration", MessageBoxButton.OK, MessageBoxImage.Exclamation);
			return;
		}
		DurationMs = result;
		base.DialogResult = true;
	}
}
