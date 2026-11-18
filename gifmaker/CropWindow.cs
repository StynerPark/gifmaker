using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace ClipForgeWpf;

public class CropWindow : Window
{
	private readonly Canvas _canvas = new Canvas();

	private readonly Image _image = new Image();

	private readonly Rectangle _rect = new Rectangle
	{
		Stroke = Brushes.OrangeRed,
		StrokeThickness = 2.0,
		Fill = new SolidColorBrush(Color.FromArgb(48, byte.MaxValue, 135, 8))
	};

	private readonly Border _resizeHandle = new Border
	{
		Width = 14.0,
		Height = 14.0,
		Background = Brushes.DeepSkyBlue,
		BorderBrush = Brushes.White,
		BorderThickness = new Thickness(1.0),
		Cursor = Cursors.SizeNWSE
	};

	private readonly BitmapSource _source;

	private Point _start;

	private Point _last;

	private bool _dragging;

	private bool _moving;

	private bool _resizing;

	private bool _hasSelection;

	private double _ratio;

	public Rect CropRect { get; private set; }

	public CropWindow(string imagePath, Rect? initial, string title = "Crop Selection", string applyText = "Apply", bool showRatios = true, string? helpText = null)
	{
		base.Loaded += delegate
		{
			UiLanguage.TranslateDialog(this);
		};
		base.Title = title;
		base.Width = 960.0;
		base.Height = 720.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		_source = MainWindowLoadBitmap(imagePath);
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(12.0)
		};
		base.Content = dockPanel;
		DockPanel dockPanel2 = dockPanel;
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel2.Children.Add(stackPanel);
		if (showRatios)
		{
			(string, double)[] array = new(string, double)[6]
			{
				("Free", 0.0),
				("1:1", 1.0),
				("16:9", 1.7777777777777777),
				("9:16", 0.5625),
				("3:2", 1.5),
				("2:3", 2.0 / 3.0)
			};
			for (int num = 0; num < array.Length; num++)
			{
				(string, double) tuple = array[num];
				string item = tuple.Item1;
				double ratio = tuple.Item2;
				Button button = new Button
				{
					Content = item,
					MinWidth = 72.0,
					Height = 36.0,
					Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
				};
				button.Click += delegate
				{
					SetRatioMode(ratio);
				};
				stackPanel.Children.Add(button);
			}
		}
		Button button2 = new Button
		{
			Content = applyText,
			MinWidth = 96.0,
			Height = 36.0,
			FontWeight = FontWeights.Bold
		};
		button2.Click += delegate
		{
			if (CommitCrop())
			{
				base.DialogResult = true;
			}
		};
		stackPanel.Children.Add(button2);
		if (!string.IsNullOrWhiteSpace(helpText))
		{
			TextBlock element = new TextBlock
			{
				Text = helpText,
				Margin = new Thickness(2.0, 0.0, 2.0, 10.0),
				Foreground = new SolidColorBrush(Color.FromRgb(82, 97, 116)),
				TextWrapping = TextWrapping.Wrap
			};
			DockPanel.SetDock(element, Dock.Top);
			dockPanel2.Children.Add(element);
		}
		ScrollViewer element2 = new ScrollViewer
		{
			Content = _canvas,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		dockPanel2.Children.Add(element2);
		double val = Math.Min(850.0 / (double)_source.PixelWidth, 560.0 / (double)_source.PixelHeight);
		val = Math.Min(val, 1.0);
		_image.Source = _source;
		_image.Width = (double)_source.PixelWidth * val;
		_image.Height = (double)_source.PixelHeight * val;
		_canvas.Width = _image.Width;
		_canvas.Height = _image.Height;
		_canvas.Children.Add(_image);
		_canvas.Children.Add(_rect);
		_canvas.Children.Add(_resizeHandle);
		_rect.Visibility = Visibility.Collapsed;
		_resizeHandle.Visibility = Visibility.Collapsed;
		_resizeHandle.MouseLeftButtonDown += HandleDown;
		_resizeHandle.MouseMove += CanvasMove;
		_resizeHandle.MouseLeftButtonUp += CanvasUp;
		if (initial.HasValue)
		{
			Rect valueOrDefault = initial.GetValueOrDefault();
			SetRect(new Rect(valueOrDefault.X * val, valueOrDefault.Y * val, valueOrDefault.Width * val, valueOrDefault.Height * val));
		}
		_canvas.MouseLeftButtonDown += CanvasDown;
		_canvas.MouseMove += CanvasMove;
		_canvas.MouseLeftButtonUp += CanvasUp;
	}

	private void CanvasDown(object sender, MouseButtonEventArgs e)
	{
		Point position = e.GetPosition(_canvas);
		Rect rect = CurrentRect();
		_start = position;
		_last = position;
		_moving = _hasSelection && rect.Contains(position);
		_dragging = !_moving;
		_resizing = false;
		if (_dragging)
		{
			SetRect(new Rect(_start, new Size(1.0, 1.0)));
		}
		_canvas.CaptureMouse();
		e.Handled = true;
	}

	private void CanvasMove(object sender, MouseEventArgs e)
	{
		if ((_dragging || _moving || _resizing) && e.LeftButton == MouseButtonState.Pressed)
		{
			Point position = e.GetPosition(_canvas);
			if (_moving)
			{
				double num = position.X - _last.X;
				double num2 = position.Y - _last.Y;
				Rect rect = CurrentRect();
				rect.X = Math.Clamp(rect.X + num, 0.0, Math.Max(0.0, _canvas.Width - rect.Width));
				rect.Y = Math.Clamp(rect.Y + num2, 0.0, Math.Max(0.0, _canvas.Height - rect.Height));
				SetRect(rect);
				_last = position;
			}
			else if (_resizing)
			{
				ResizeFromHandle(position);
			}
			else
			{
				SetRect(FitRectToCanvas(MakeDragRect(_start, position)));
			}
		}
	}

	private void CanvasUp(object sender, MouseButtonEventArgs e)
	{
		_dragging = false;
		_moving = false;
		_resizing = false;
		_canvas.ReleaseMouseCapture();
		_resizeHandle.ReleaseMouseCapture();
		e.Handled = true;
	}

	private void HandleDown(object sender, MouseButtonEventArgs e)
	{
		if (_hasSelection)
		{
			_resizing = true;
			_dragging = false;
			_moving = false;
			_last = e.GetPosition(_canvas);
			_resizeHandle.CaptureMouse();
			e.Handled = true;
		}
	}

	private void SetRatioMode(double ratio)
	{
		_ratio = ratio;
		if (!(_ratio <= 0.0))
		{
			double num = _canvas.Width * 0.72;
			double num2 = _canvas.Height * 0.72;
			double num3 = num;
			double num4 = num3 / _ratio;
			if (num4 > num2)
			{
				num4 = num2;
				num3 = num4 * _ratio;
			}
			double x = (_canvas.Width - num3) / 2.0;
			double y = (_canvas.Height - num4) / 2.0;
			SetRect(new Rect(x, y, num3, num4));
		}
	}

	private Rect MakeDragRect(Point a, Point b)
	{
		double num = b.X - a.X;
		double num2 = b.Y - a.Y;
		if (_ratio > 0.0)
		{
			int num3 = Math.Sign((num == 0.0) ? 1.0 : num);
			int num4 = Math.Sign((num2 == 0.0) ? 1.0 : num2);
			if (Math.Abs(num / Math.Max(1.0, num2)) > _ratio)
			{
				num2 = (double)num4 * Math.Abs(num) / _ratio;
			}
			else
			{
				num = (double)num3 * Math.Abs(num2) * _ratio;
			}
			b = new Point(a.X + num, a.Y + num2);
		}
		return new Rect(a, b);
	}

	private void ResizeFromHandle(Point p)
	{
		Rect rect = CurrentRect();
		double num = Math.Max(8.0, p.X - rect.X);
		double num2 = Math.Max(8.0, p.Y - rect.Y);
		if (_ratio > 0.0)
		{
			num2 = num / _ratio;
			if (rect.Y + num2 > _canvas.Height)
			{
				num2 = _canvas.Height - rect.Y;
				num = num2 * _ratio;
			}
		}
		num = Math.Min(num, _canvas.Width - rect.X);
		num2 = Math.Min(num2, _canvas.Height - rect.Y);
		if (_ratio > 0.0)
		{
			if (num / Math.Max(1.0, num2) > _ratio)
			{
				num = num2 * _ratio;
			}
			else
			{
				num2 = num / _ratio;
			}
		}
		SetRect(new Rect(rect.X, rect.Y, Math.Max(8.0, num), Math.Max(8.0, num2)));
	}

	private void SetRect(Rect r)
	{
		r = FitRectToCanvas(r);
		_hasSelection = true;
		_rect.Visibility = Visibility.Visible;
		_resizeHandle.Visibility = Visibility.Visible;
		Canvas.SetLeft(_rect, r.X);
		Canvas.SetTop(_rect, r.Y);
		_rect.Width = Math.Max(1.0, r.Width);
		_rect.Height = Math.Max(1.0, r.Height);
		Canvas.SetLeft(_resizeHandle, r.Right - _resizeHandle.Width / 2.0);
		Canvas.SetTop(_resizeHandle, r.Bottom - _resizeHandle.Height / 2.0);
	}

	private Rect FitRectToCanvas(Rect r)
	{
		r = new Rect(Math.Min(r.Left, r.Right), Math.Min(r.Top, r.Bottom), Math.Abs(r.Width), Math.Abs(r.Height));
		if (_ratio > 0.0 && r.Width > 1.0 && r.Height > 1.0)
		{
			if (r.Width / r.Height > _ratio)
			{
				r.Width = r.Height * _ratio;
			}
			else
			{
				r.Height = r.Width / _ratio;
			}
		}
		if (r.Width > _canvas.Width)
		{
			r.Width = _canvas.Width;
			if (_ratio > 0.0)
			{
				r.Height = r.Width / _ratio;
			}
		}
		if (r.Height > _canvas.Height)
		{
			r.Height = _canvas.Height;
			if (_ratio > 0.0)
			{
				r.Width = r.Height * _ratio;
			}
		}
		r.X = Math.Clamp(r.X, 0.0, Math.Max(0.0, _canvas.Width - r.Width));
		r.Y = Math.Clamp(r.Y, 0.0, Math.Max(0.0, _canvas.Height - r.Height));
		return r;
	}

	private Rect CurrentRect()
	{
		return new Rect(Canvas.GetLeft(_rect), Canvas.GetTop(_rect), _rect.Width, _rect.Height);
	}

	private bool CommitCrop()
	{
		if (!_hasSelection)
		{
			MessageBox.Show(this, "영역을 먼저 선택하세요.", base.Title, MessageBoxButton.OK, MessageBoxImage.Asterisk);
			return false;
		}
		double num = (double)_source.PixelWidth / _canvas.Width;
		double num2 = (double)_source.PixelHeight / _canvas.Height;
		CropRect = new Rect(Canvas.GetLeft(_rect) * num, Canvas.GetTop(_rect) * num2, _rect.Width * num, _rect.Height * num2);
		return true;
	}

	private static BitmapSource MainWindowLoadBitmap(string path)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.UriSource = new Uri(path);
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}
}
