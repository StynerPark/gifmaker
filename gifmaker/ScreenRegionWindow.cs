using System;
using System.ComponentModel;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;

namespace ClipForgeWpf;

public class ScreenRegionWindow : Window
{
	private readonly Canvas _canvas = new Canvas
	{
		Background = System.Windows.Media.Brushes.Transparent
	};

	private readonly System.Windows.Shapes.Rectangle _rect = new System.Windows.Shapes.Rectangle
	{
		Stroke = System.Windows.Media.Brushes.DeepSkyBlue,
		StrokeThickness = 2.0,
		Fill = new SolidColorBrush(System.Windows.Media.Color.FromArgb(45, 0, 180, byte.MaxValue)),
		IsHitTestVisible = false
	};

	private readonly StackPanel _toolbar = new StackPanel();

	private readonly System.Windows.Controls.Button _frames = MakeButton("Frame record");

	private readonly System.Windows.Controls.Button _video = MakeButton("Video record");

	private readonly TextBlock _help = new TextBlock
	{
		FontSize = 11.0,
		Foreground = System.Windows.Media.Brushes.White,
		Margin = new Thickness(4.0, 3.0, 4.0, 1.0),
		TextAlignment = TextAlignment.Center
	};

	private readonly Func<Rect, bool, Task<bool>> _startRecording;

	private readonly Func<Task> _stopRecording;

	private readonly Func<Task> _cancelRecording;

	private System.Windows.Point _start;

	private Rect _selectedDip;

	private bool _dragging;

	private bool _recording;

	private bool _hasSelection;

	private bool _videoMode;

	private bool _busy;

	private bool _allowClose;

	public Rect SelectedRect { get; private set; }

	public ScreenRegionWindow(Func<Rect, bool, Task<bool>> startRecording, Func<Task> stopRecording, Func<Task> cancelRecording, bool fullScreen = false)
	{
		ScreenRegionWindow screenRegionWindow = this;
		_startRecording = startRecording;
		_stopRecording = stopRecording;
		_cancelRecording = cancelRecording;
		_video.ToolTip = UiLanguage.T("Record screen and PC sound as MP4");
		base.Title = UiLanguage.T("Record Area");
		base.WindowStyle = WindowStyle.None;
		base.ResizeMode = ResizeMode.NoResize;
		base.AllowsTransparency = true;
		base.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(60, 0, 0, 0));
		base.Topmost = true;
		base.ShowInTaskbar = false;
		base.Left = SystemParameters.VirtualScreenLeft;
		base.Top = SystemParameters.VirtualScreenTop;
		base.Width = SystemParameters.VirtualScreenWidth;
		base.Height = SystemParameters.VirtualScreenHeight;
		base.Cursor = System.Windows.Input.Cursors.Cross;
		Grid obj = (Grid)(base.Content = new Grid());
		obj.Children.Add(_canvas);
		_canvas.Children.Add(_rect);
		StackPanel stackPanel = new StackPanel
		{
			Orientation = System.Windows.Controls.Orientation.Horizontal,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Center
		};
		stackPanel.Children.Add(_frames);
		stackPanel.Children.Add(_video);
		_toolbar.Children.Add(stackPanel);
		_toolbar.Children.Add(_help);
		_toolbar.Background = new SolidColorBrush(System.Windows.Media.Color.FromArgb(220, 30, 35, 43));
		_toolbar.HorizontalAlignment = System.Windows.HorizontalAlignment.Center;
		_toolbar.VerticalAlignment = VerticalAlignment.Top;
		_toolbar.Margin = new Thickness(0.0, 10.0, 0.0, 0.0);
		obj.Children.Add(_toolbar);
		_frames.Click += async delegate
		{
			await screenRegionWindow.ToggleRecording(video: false);
		};
		_video.Click += async delegate
		{
			await screenRegionWindow.ToggleRecording(video: true);
		};
		_canvas.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			if (!screenRegionWindow._recording && !screenRegionWindow._busy)
			{
				screenRegionWindow._dragging = true;
				screenRegionWindow._hasSelection = false;
				screenRegionWindow._start = e.GetPosition(screenRegionWindow._canvas);
				screenRegionWindow._toolbar.Visibility = Visibility.Collapsed;
				screenRegionWindow.Draw(new Rect(screenRegionWindow._start, new System.Windows.Size(1.0, 1.0)));
				screenRegionWindow._canvas.CaptureMouse();
			}
		};
		_canvas.MouseMove += delegate(object _, System.Windows.Input.MouseEventArgs e)
		{
			if (screenRegionWindow._dragging)
			{
				screenRegionWindow.Draw(new Rect(screenRegionWindow._start, e.GetPosition(screenRegionWindow._canvas)));
			}
		};
		_canvas.MouseLeftButtonUp += delegate
		{
			if (screenRegionWindow._dragging)
			{
				screenRegionWindow._dragging = false;
				screenRegionWindow._canvas.ReleaseMouseCapture();
				screenRegionWindow._hasSelection = screenRegionWindow._selectedDip.Width >= 8.0 && screenRegionWindow._selectedDip.Height >= 8.0;
				if (screenRegionWindow._hasSelection)
				{
					System.Windows.Point point = screenRegionWindow._canvas.PointToScreen(screenRegionWindow._selectedDip.TopLeft);
					System.Windows.Point point2 = screenRegionWindow._canvas.PointToScreen(screenRegionWindow._selectedDip.BottomRight);
					screenRegionWindow.SelectedRect = new Rect(point, point2);
					screenRegionWindow.PositionToolbar();
				}
				screenRegionWindow._toolbar.Visibility = Visibility.Visible;
				screenRegionWindow.UpdateControls();
			}
		};
		base.PreviewMouseRightButtonDown += async delegate(object _, MouseButtonEventArgs e)
		{
			e.Handled = true;
			await screenRegionWindow.Cancel();
		};
		base.KeyDown += async delegate(object _, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == Key.Escape)
			{
				e.Handled = true;
				await screenRegionWindow.Cancel();
			}
		};
		base.SourceInitialized += delegate
		{
			DesktopCapture.ExcludeOverlay(new WindowInteropHelper(screenRegionWindow).Handle);
		};
		base.Closing += async delegate(object? _, CancelEventArgs e)
		{
			if (!screenRegionWindow._allowClose)
			{
				if (screenRegionWindow._busy)
				{
					e.Cancel = true;
				}
				else if (screenRegionWindow._recording)
				{
					e.Cancel = true;
					await screenRegionWindow.Cancel();
				}
			}
		};
		base.Loaded += delegate
		{
			if (fullScreen)
			{
				System.Drawing.Rectangle virtualScreen = SystemInformation.VirtualScreen;
				screenRegionWindow.SelectedRect = new Rect(virtualScreen.X, virtualScreen.Y, virtualScreen.Width, virtualScreen.Height);
				screenRegionWindow.Draw(new Rect(0.0, 0.0, screenRegionWindow.ActualWidth, screenRegionWindow.ActualHeight));
				screenRegionWindow._hasSelection = true;
				screenRegionWindow.PositionToolbar();
			}
			screenRegionWindow.UpdateControls();
			screenRegionWindow.Activate();
			screenRegionWindow.Focus();
		};
	}

	private static System.Windows.Controls.Button MakeButton(string label)
	{
		return new System.Windows.Controls.Button
		{
			Content = UiLanguage.T(label),
			Width = 96.0,
			Height = 31.0,
			Margin = new Thickness(4.0, 3.0, 4.0, 2.0),
			Padding = new Thickness(2.0),
			FontSize = 11.0,
			FontWeight = FontWeights.SemiBold
		};
	}

	private void Draw(Rect rect)
	{
		_selectedDip = rect;
		Canvas.SetLeft(_rect, rect.Left);
		Canvas.SetTop(_rect, rect.Top);
		_rect.Width = Math.Max(1.0, rect.Width);
		_rect.Height = Math.Max(1.0, rect.Height);
		_rect.Visibility = Visibility.Visible;
	}

	private void PositionToolbar()
	{
		double num = _selectedDip.Top - 64.0;
		if (num < 6.0)
		{
			num = _selectedDip.Bottom + 8.0;
		}
		if (num + 60.0 > base.ActualHeight)
		{
			num = 6.0;
		}
		double left = Math.Clamp(_selectedDip.Left, 6.0, Math.Max(6.0, base.ActualWidth - 270.0));
		_toolbar.HorizontalAlignment = System.Windows.HorizontalAlignment.Left;
		_toolbar.Margin = new Thickness(left, num, 0.0, 0.0);
	}

	private void UpdateControls()
	{
		_frames.Content = UiLanguage.T((_recording && !_videoMode) ? "■ Stop" : "Frame record");
		_video.Content = UiLanguage.T((_recording && _videoMode) ? "■ Stop" : "Video record");
		_frames.IsEnabled = !_busy && _hasSelection && (!_recording || !_videoMode);
		_video.IsEnabled = !_busy && _hasSelection && (!_recording || _videoMode);
		_help.Text = UiLanguage.T(_recording ? "Press Stop to finish · Right-click to cancel" : "Drag again to reselect · Right-click to cancel");
	}

	private async Task ToggleRecording(bool video)
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		UpdateControls();
		try
		{
			if (_recording)
			{
				await _stopRecording();
				_recording = false;
				_allowClose = true;
				base.DialogResult = true;
			}
			else if (_hasSelection)
			{
				base.Opacity = 0.0;
				base.Topmost = false;
				bool num = await _startRecording(SelectedRect, video);
				base.Opacity = 1.0;
				base.Topmost = true;
				if (num)
				{
					_recording = true;
					_videoMode = video;
					base.Background = System.Windows.Media.Brushes.Transparent;
					_rect.Fill = System.Windows.Media.Brushes.Transparent;
					_canvas.Background = System.Windows.Media.Brushes.Transparent;
				}
			}
		}
		catch (Exception ex)
		{
			base.Opacity = 1.0;
			base.Topmost = true;
			System.Windows.MessageBox.Show(this, ex.Message, UiLanguage.T("Recording failed"));
		}
		finally
		{
			base.Opacity = 1.0;
			_busy = false;
			UpdateControls();
		}
	}

	private async Task Cancel()
	{
		if (_busy)
		{
			return;
		}
		_busy = true;
		try
		{
			if (_recording)
			{
				await _cancelRecording();
			}
			_recording = false;
			_allowClose = true;
			base.DialogResult = false;
		}
		finally
		{
			_busy = false;
		}
	}
}
