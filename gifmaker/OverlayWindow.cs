using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace ClipForgeWpf;

public class OverlayWindow : Window
{
	private readonly Canvas _canvas = new Canvas();

	private readonly BitmapSource _baseImage;

	private readonly Slider _opacity = new Slider
	{
		Minimum = 0.0,
		Maximum = 100.0,
		Value = 100.0,
		Width = 150.0
	};

	private readonly TextBox _opacityNumber = new TextBox
	{
		Text = "100",
		Width = 52.0
	};

	private readonly StackPanel _textTools = new StackPanel
	{
		Orientation = Orientation.Horizontal,
		Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
		Visibility = Visibility.Collapsed
	};

	private readonly ComboBox _fontBox = new ComboBox
	{
		Width = 150.0,
		Height = 30.0,
		Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
	};

	private readonly TextBox _fontSizeBox = new TextBox
	{
		Width = 48.0,
		Height = 30.0,
		Text = "32",
		Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
	};

	private readonly ComboBox _colorBox = new ComboBox
	{
		Width = 96.0,
		Height = 30.0,
		Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
	};

	private readonly ToggleButton _boldButton = new ToggleButton
	{
		Content = "B",
		Width = 34.0,
		Height = 30.0,
		FontWeight = FontWeights.Bold,
		Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
	};

	private readonly ToggleButton _italicButton = new ToggleButton
	{
		Content = "I",
		Width = 34.0,
		Height = 30.0,
		FontStyle = FontStyles.Italic,
		Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
	};

	private readonly ToggleButton _underlineButton = new ToggleButton
	{
		Content = "U",
		Width = 34.0,
		Height = 30.0,
		Margin = new Thickness(0.0, 0.0, 4.0, 0.0)
	};

	private readonly ToggleButton _strikeButton = new ToggleButton
	{
		Content = "S",
		Width = 34.0,
		Height = 30.0,
		Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
	};

	private readonly List<FrameworkElement> _items = new List<FrameworkElement>();

	private FrameworkElement? _selected;

	private TextBox? _editingTextBox;

	private Point _dragStart;

	private bool _dragging;

	private bool _resizing;

	private bool _updatingTextTools;

	public ObservableCollection<OverlaySpec> Overlays { get; } = new ObservableCollection<OverlaySpec>();

	public OverlayWindow(BitmapSource baseImage, ObservableCollection<OverlaySpec> overlays)
	{
		base.Loaded += delegate
		{
			UiLanguage.TranslateDialog(this);
		};
		base.Title = "Insert Image/Text";
		base.Width = 980.0;
		base.Height = 760.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.AllowDrop = true;
		base.Focusable = true;
		_baseImage = baseImage;
		foreach (OverlaySpec overlay in overlays)
		{
			Overlays.Add(overlay.Clone());
		}
		DockPanel dockPanel = (DockPanel)(base.Content = new DockPanel
		{
			Margin = new Thickness(12.0)
		});
		BuildTextTools();
		StackPanel stackPanel = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 10.0, 0.0, 0.0)
		};
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		dockPanel.Children.Add(stackPanel);
		Button button = new Button
		{
			Content = "Add Image",
			Height = 36.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		button.Click += delegate
		{
			AddImage();
		};
		Button button2 = new Button
		{
			Content = "Add Text",
			Height = 36.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		button2.Click += delegate
		{
			AddText();
		};
		Button button3 = new Button
		{
			Content = "Delete Selected",
			Height = 36.0,
			Margin = new Thickness(8.0, 0.0, 8.0, 0.0)
		};
		button3.Click += delegate
		{
			DeleteSelected();
		};
		Button button4 = new Button
		{
			Content = "Apply",
			Height = 36.0,
			MinWidth = 92.0,
			FontWeight = FontWeights.Bold
		};
		button4.Click += delegate
		{
			SyncBack();
			base.DialogResult = true;
		};
		_opacity.ValueChanged += delegate
		{
			ApplyOpacity();
		};
		_opacityNumber.TextChanged += delegate
		{
			if (double.TryParse(_opacityNumber.Text, out var result))
			{
				_opacity.Value = Math.Clamp(result, 0.0, 100.0);
			}
		};
		UIElement[] array = new UIElement[7]
		{
			button,
			button2,
			new TextBlock
			{
				Text = "Opacity",
				VerticalAlignment = VerticalAlignment.Center
			},
			_opacity,
			_opacityNumber,
			button3,
			button4
		};
		foreach (UIElement element in array)
		{
			stackPanel.Children.Add(element);
		}
		ScrollViewer element2 = new ScrollViewer
		{
			Content = _canvas,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Auto,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		};
		dockPanel.Children.Add(element2);
		double val = Math.Min(850.0 / (double)_baseImage.PixelWidth, 560.0 / (double)_baseImage.PixelHeight);
		val = Math.Min(val, 1.0);
		_canvas.Width = (double)_baseImage.PixelWidth * val;
		_canvas.Height = (double)_baseImage.PixelHeight * val;
		_canvas.Background = new ImageBrush(_baseImage)
		{
			Stretch = Stretch.Fill
		};
		_canvas.AllowDrop = true;
		_canvas.Drop += CanvasDrop;
		_canvas.DragOver += CanvasDragOver;
		_canvas.MouseLeftButtonDown += CanvasDown;
		base.KeyDown += delegate(object _, KeyEventArgs e)
		{
			if (e.Key == Key.Delete)
			{
				TextBox editingTextBox = _editingTextBox;
				if (editingTextBox == null || !editingTextBox.IsKeyboardFocusWithin)
				{
					DeleteSelected();
				}
			}
		};
		base.Loaded += delegate
		{
			Focus();
		};
		Rebuild();
	}

	private void BuildTextTools()
	{
		string[] array = new string[9] { "Segoe UI", "Arial", "Calibri", "Tahoma", "Verdana", "Malgun Gothic", "Noto Sans KR", "Times New Roman", "Consolas" };
		foreach (string newItem in array)
		{
			_fontBox.Items.Add(newItem);
		}
		_fontBox.SelectedItem = "Segoe UI";
		array = new string[8] { "#FFFFFF", "#000000", "#FF0000", "#00AEEF", "#FFD400", "#00C853", "#FF7A00", "#E11D48" };
		foreach (string newItem2 in array)
		{
			_colorBox.Items.Add(newItem2);
		}
		_colorBox.SelectedItem = "#FFFFFF";
		_textTools.Children.Add(new TextBlock
		{
			Text = "Font",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		});
		_textTools.Children.Add(_fontBox);
		_textTools.Children.Add(new TextBlock
		{
			Text = "Size",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		});
		_textTools.Children.Add(_fontSizeBox);
		_textTools.Children.Add(new TextBlock
		{
			Text = "Color",
			VerticalAlignment = VerticalAlignment.Center,
			Margin = new Thickness(0.0, 0.0, 6.0, 0.0)
		});
		_textTools.Children.Add(_colorBox);
		_textTools.Children.Add(_boldButton);
		_textTools.Children.Add(_italicButton);
		_textTools.Children.Add(_underlineButton);
		_textTools.Children.Add(_strikeButton);
		_fontBox.SelectionChanged += delegate
		{
			ApplyTextTools();
		};
		_colorBox.SelectionChanged += delegate
		{
			ApplyTextTools();
		};
		_fontSizeBox.TextChanged += delegate
		{
			ApplyTextTools();
		};
		_boldButton.Checked += delegate
		{
			ApplyTextTools();
		};
		_boldButton.Unchecked += delegate
		{
			ApplyTextTools();
		};
		_italicButton.Checked += delegate
		{
			ApplyTextTools();
		};
		_italicButton.Unchecked += delegate
		{
			ApplyTextTools();
		};
		_underlineButton.Checked += delegate
		{
			ApplyTextTools();
		};
		_underlineButton.Unchecked += delegate
		{
			ApplyTextTools();
		};
		_strikeButton.Checked += delegate
		{
			ApplyTextTools();
		};
		_strikeButton.Unchecked += delegate
		{
			ApplyTextTools();
		};
	}

	private void AddImage()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "Images|*.png;*.jpg;*.jpeg;*.webp;*.bmp|All|*.*"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			AddImagePath(openFileDialog.FileName, new Point(_canvas.Width * 0.08, _canvas.Height * 0.08));
		}
	}

	private void AddImagePath(string path, Point point)
	{
		if (File.Exists(path))
		{
			SyncBack();
			double num = Math.Min(_canvas.Width * 0.24, 180.0);
			double num2 = ImageRatio(path);
			double num3 = num / num2;
			Overlays.Add(new OverlaySpec
			{
				Kind = "image",
				Path = path,
				X = Math.Clamp(point.X / _canvas.Width, 0.0, 0.95),
				Y = Math.Clamp(point.Y / _canvas.Height, 0.0, 0.95),
				Width = Math.Clamp(num / _canvas.Width, 0.03, 1.0),
				Height = Math.Clamp(num3 / _canvas.Height, 0.03, 1.0)
			});
			Rebuild();
		}
	}

	private void AddText()
	{
		AddTextAt(new Point(_canvas.Width * 0.08, _canvas.Height * 0.08));
	}

	private void AddTextAt(Point point)
	{
		SyncBack();
		Overlays.Add(new OverlaySpec
		{
			Kind = "text",
			Text = "Double click text",
			X = Math.Clamp(point.X / _canvas.Width, 0.0, 0.88),
			Y = Math.Clamp(point.Y / _canvas.Height, 0.0, 0.88),
			Width = 0.36,
			Height = 0.12
		});
		Rebuild();
	}

	private void DeleteSelected()
	{
		SyncBack();
		if (_selected?.Tag is OverlaySpec item)
		{
			Overlays.Remove(item);
		}
		_selected = null;
		_editingTextBox = null;
		_textTools.Visibility = Visibility.Collapsed;
		Rebuild();
	}

	private void Rebuild()
	{
		_canvas.Children.Clear();
		_items.Clear();
		foreach (OverlaySpec overlay in Overlays)
		{
			Border border = new Border
			{
				Tag = overlay,
				BorderBrush = Brushes.OrangeRed,
				BorderThickness = new Thickness(1.0),
				Background = new SolidColorBrush(Color.FromArgb(12, byte.MaxValue, byte.MaxValue, byte.MaxValue)),
				Opacity = overlay.Opacity,
				MinWidth = 24.0,
				MinHeight = 20.0
			};
			UIElement uIElement = (border.Child = new Grid
			{
				ClipToBounds = false
			});
			Grid grid = (Grid)uIElement;
			if (overlay.Kind == "image" && File.Exists(overlay.Path))
			{
				grid.Children.Add(new Image
				{
					Source = LoadForOverlay(overlay.Path),
					Stretch = Stretch.Fill,
					IsHitTestVisible = false
				});
			}
			else
			{
				TextBox text = new TextBox
				{
					Text = overlay.Text,
					FontFamily = new FontFamily(overlay.Font),
					FontSize = overlay.FontSize,
					Foreground = (Brush)new BrushConverter().ConvertFromString(overlay.Color),
					Background = Brushes.Transparent,
					FontWeight = (overlay.Bold ? FontWeights.Bold : FontWeights.Normal),
					FontStyle = (overlay.Italic ? FontStyles.Italic : FontStyles.Normal),
					TextDecorations = TextDecorationsFor(overlay),
					BorderThickness = new Thickness(0.0),
					AcceptsReturn = true,
					TextWrapping = TextWrapping.Wrap,
					IsHitTestVisible = false
				};
				text.TextChanged += delegate
				{
					overlay.Text = text.Text;
				};
				text.LostKeyboardFocus += delegate(object _, KeyboardFocusChangedEventArgs e)
				{
					if (!(e.NewFocus is DependencyObject dependencyObject) || (_textTools != dependencyObject && !_textTools.IsAncestorOf(dependencyObject)))
					{
						text.IsHitTestVisible = false;
						if (_editingTextBox == text)
						{
							_editingTextBox = null;
							_textTools.Visibility = Visibility.Collapsed;
						}
					}
				};
				grid.Children.Add(text);
			}
			Border border2 = new Border
			{
				Width = 13.0,
				Height = 13.0,
				Background = Brushes.DeepSkyBlue,
				BorderBrush = Brushes.White,
				BorderThickness = new Thickness(1.0),
				HorizontalAlignment = HorizontalAlignment.Right,
				VerticalAlignment = VerticalAlignment.Bottom,
				Cursor = Cursors.SizeNWSE,
				Visibility = Visibility.Collapsed,
				Tag = border
			};
			border2.MouseLeftButtonDown += ResizeHandleDown;
			grid.Children.Add(border2);
			border.Width = Math.Max(24.0, overlay.Width * _canvas.Width);
			border.Height = Math.Max(20.0, overlay.Height * _canvas.Height);
			Canvas.SetLeft(border, overlay.X * _canvas.Width);
			Canvas.SetTop(border, overlay.Y * _canvas.Height);
			border.MouseLeftButtonDown += ItemDown;
			border.MouseMove += ItemMove;
			border.MouseLeftButtonUp += ItemUp;
			border.MouseWheel += ItemWheel;
			_canvas.Children.Add(border);
			_items.Add(border);
		}
		_textTools.Background = new SolidColorBrush(Color.FromArgb(238, byte.MaxValue, byte.MaxValue, byte.MaxValue));
		_textTools.Effect = null;
		Panel.SetZIndex(_textTools, 1000);
		_canvas.Children.Add(_textTools);
		UpdateSelectionVisuals();
	}

	private void Select(FrameworkElement item)
	{
		_selected = item;
		_opacity.Value = item.Opacity * 100.0;
		_opacityNumber.Text = ((int)_opacity.Value).ToString();
		if (!(item.Tag is OverlaySpec { Kind: "text" }))
		{
			_editingTextBox = null;
			_textTools.Visibility = Visibility.Collapsed;
		}
		UpdateSelectionVisuals();
	}

	private void ItemDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is FrameworkElement frameworkElement)
		{
			Select(frameworkElement);
			if (e.ClickCount >= 2 && frameworkElement.Tag is OverlaySpec { Kind: "text" })
			{
				BeginTextEdit(frameworkElement);
				e.Handled = true;
				return;
			}
			_dragging = true;
			_resizing = false;
			_dragStart = e.GetPosition(_canvas);
			frameworkElement.CaptureMouse();
			Focus();
			e.Handled = true;
		}
	}

	private void ItemMove(object sender, MouseEventArgs e)
	{
		if ((_dragging || _resizing) && sender is FrameworkElement frameworkElement && _selected == frameworkElement && e.LeftButton == MouseButtonState.Pressed)
		{
			Point position = e.GetPosition(_canvas);
			double num = position.X - _dragStart.X;
			double num2 = position.Y - _dragStart.Y;
			if (_resizing)
			{
				ResizeItem(frameworkElement, num, num2);
			}
			else
			{
				Canvas.SetLeft(frameworkElement, Math.Clamp(Canvas.GetLeft(frameworkElement) + num, 0.0, Math.Max(0.0, _canvas.Width - frameworkElement.Width)));
				Canvas.SetTop(frameworkElement, Math.Clamp(Canvas.GetTop(frameworkElement) + num2, 0.0, Math.Max(0.0, _canvas.Height - frameworkElement.Height)));
			}
			_dragStart = position;
		}
	}

	private void ItemUp(object sender, MouseButtonEventArgs e)
	{
		_dragging = false;
		_resizing = false;
		if (sender is FrameworkElement frameworkElement)
		{
			frameworkElement.ReleaseMouseCapture();
		}
	}

	private void ItemWheel(object sender, MouseWheelEventArgs e)
	{
		if (sender is FrameworkElement frameworkElement)
		{
			Select(frameworkElement);
			double num = ((e.Delta > 0) ? 1.08 : 0.92);
			frameworkElement.Width = Math.Clamp(frameworkElement.Width * num, 20.0, _canvas.Width);
			frameworkElement.Height = Math.Clamp(frameworkElement.Height * num, 18.0, _canvas.Height);
			e.Handled = true;
		}
	}

	private void ResizeHandleDown(object sender, MouseButtonEventArgs e)
	{
		if (sender is Border { Tag: FrameworkElement tag })
		{
			Select(tag);
			_dragging = false;
			_resizing = true;
			_dragStart = e.GetPosition(_canvas);
			tag.CaptureMouse();
			e.Handled = true;
		}
	}

	private void ResizeItem(FrameworkElement item, double dx, double dy)
	{
		double max = Math.Max(24.0, _canvas.Width - Canvas.GetLeft(item));
		double max2 = Math.Max(20.0, _canvas.Height - Canvas.GetTop(item));
		double num = Math.Clamp(item.Width + dx, 24.0, max);
		double height = Math.Clamp(item.Height + dy, 20.0, max2);
		if (item.Tag is OverlaySpec { Kind: "image" })
		{
			double num2 = Math.Max(0.01, item.Width / Math.Max(1.0, item.Height));
			height = Math.Clamp(num / num2, 20.0, max2);
		}
		item.Width = num;
		item.Height = height;
	}

	private void BeginTextEdit(FrameworkElement item)
	{
		TextBox textBox = FindChild<TextBox>(item);
		if (textBox != null && item.Tag is OverlaySpec overlay)
		{
			_editingTextBox = textBox;
			LoadTextTools(overlay);
			_textTools.Visibility = Visibility.Visible;
			PositionTextTools(item);
			textBox.IsHitTestVisible = true;
			textBox.Focus();
			textBox.SelectAll();
		}
	}

	private void CanvasDown(object sender, MouseButtonEventArgs e)
	{
		if (e.OriginalSource == _canvas)
		{
			if (e.ClickCount >= 2)
			{
				AddTextAt(e.GetPosition(_canvas));
				e.Handled = true;
				return;
			}
			_selected = null;
			_editingTextBox = null;
			_textTools.Visibility = Visibility.Collapsed;
			UpdateSelectionVisuals();
			Focus();
		}
	}

	private void CanvasDragOver(object sender, DragEventArgs e)
	{
		e.Effects = (HasImageDrop(e) ? DragDropEffects.Copy : DragDropEffects.None);
		e.Handled = true;
	}

	private void CanvasDrop(object sender, DragEventArgs e)
	{
		if (HasImageDrop(e))
		{
			string text = ((string[])e.Data.GetData(DataFormats.FileDrop)).FirstOrDefault(IsImageFile);
			if (text != null)
			{
				AddImagePath(text, e.GetPosition(_canvas));
			}
			e.Handled = true;
		}
	}

	private void UpdateSelectionVisuals()
	{
		foreach (FrameworkElement item in _items)
		{
			bool flag = item == _selected;
			if (!(item is Border border))
			{
				continue;
			}
			border.BorderThickness = (flag ? new Thickness(2.0) : new Thickness(1.0));
			border.BorderBrush = (flag ? Brushes.DeepSkyBlue : Brushes.OrangeRed);
			border.Background = (flag ? new SolidColorBrush(Color.FromArgb(32, 0, 180, byte.MaxValue)) : new SolidColorBrush(Color.FromArgb(12, byte.MaxValue, byte.MaxValue, byte.MaxValue)));
			if (!(border.Child is Grid grid))
			{
				continue;
			}
			foreach (Border item2 in from x in grid.Children.OfType<Border>()
				where x.Tag == item
				select x)
			{
				item2.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			}
		}
		if (_selected != null && _textTools.Visibility == Visibility.Visible)
		{
			PositionTextTools(_selected);
		}
	}

	private void PositionTextTools(FrameworkElement item)
	{
		_textTools.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
		double num = Math.Min(_textTools.DesiredSize.Width, _canvas.Width);
		double height = _textTools.DesiredSize.Height;
		double left = Canvas.GetLeft(item);
		double num2 = Canvas.GetTop(item) - height - 8.0;
		if (num2 < 6.0)
		{
			num2 = Canvas.GetTop(item) + item.Height + 8.0;
		}
		if (num2 + height > _canvas.Height)
		{
			num2 = Math.Max(6.0, _canvas.Height - height - 6.0);
		}
		left = Math.Clamp(left, 6.0, Math.Max(6.0, _canvas.Width - num - 6.0));
		Canvas.SetLeft(_textTools, left);
		Canvas.SetTop(_textTools, num2);
	}

	private void ApplyOpacity()
	{
		if (_selected != null)
		{
			_selected.Opacity = _opacity.Value / 100.0;
			_opacityNumber.Text = ((int)_opacity.Value).ToString();
		}
	}

	private void SyncBack()
	{
		foreach (FrameworkElement item in _items)
		{
			if (item.Tag is OverlaySpec overlaySpec)
			{
				overlaySpec.X = Canvas.GetLeft(item) / _canvas.Width;
				overlaySpec.Y = Canvas.GetTop(item) / _canvas.Height;
				overlaySpec.Width = item.Width / _canvas.Width;
				overlaySpec.Height = item.Height / _canvas.Height;
				overlaySpec.Opacity = item.Opacity;
				TextBox textBox = FindChild<TextBox>(item);
				if (textBox != null)
				{
					overlaySpec.Text = textBox.Text;
					overlaySpec.Font = textBox.FontFamily.Source;
					overlaySpec.FontSize = textBox.FontSize;
					overlaySpec.Color = ((textBox.Foreground is SolidColorBrush { Color: var color }) ? color.ToString(CultureInfo.InvariantCulture) : overlaySpec.Color);
					overlaySpec.Bold = textBox.FontWeight == FontWeights.Bold;
					overlaySpec.Italic = textBox.FontStyle == FontStyles.Italic;
					overlaySpec.Underline = textBox.TextDecorations?.Contains(TextDecorations.Underline[0]) ?? false;
					overlaySpec.Strike = textBox.TextDecorations?.Contains(TextDecorations.Strikethrough[0]) ?? false;
				}
			}
		}
	}

	private void LoadTextTools(OverlaySpec overlay)
	{
		_updatingTextTools = true;
		_fontBox.SelectedItem = (_fontBox.Items.Contains(overlay.Font) ? overlay.Font : "Segoe UI");
		_fontSizeBox.Text = Math.Round(Math.Clamp(overlay.FontSize, 8.0, 240.0)).ToString(CultureInfo.InvariantCulture);
		_colorBox.SelectedItem = (_colorBox.Items.Contains(overlay.Color) ? overlay.Color : "#FFFFFF");
		_boldButton.IsChecked = overlay.Bold;
		_italicButton.IsChecked = overlay.Italic;
		_underlineButton.IsChecked = overlay.Underline;
		_strikeButton.IsChecked = overlay.Strike;
		_updatingTextTools = false;
	}

	private void ApplyTextTools()
	{
		if (!_updatingTextTools && _editingTextBox != null && _selected?.Tag is OverlaySpec overlaySpec)
		{
			string font = _fontBox.SelectedItem?.ToString() ?? "Segoe UI";
			string color = _colorBox.SelectedItem?.ToString() ?? "#FFFFFF";
			double result;
			double fontSize = (double.TryParse(_fontSizeBox.Text, out result) ? Math.Clamp(result, 8.0, 240.0) : overlaySpec.FontSize);
			overlaySpec.Font = font;
			overlaySpec.FontSize = fontSize;
			overlaySpec.Color = color;
			overlaySpec.Bold = _boldButton.IsChecked == true;
			overlaySpec.Italic = _italicButton.IsChecked == true;
			overlaySpec.Underline = _underlineButton.IsChecked == true;
			overlaySpec.Strike = _strikeButton.IsChecked == true;
			_editingTextBox.FontFamily = new FontFamily(overlaySpec.Font);
			_editingTextBox.FontSize = overlaySpec.FontSize;
			_editingTextBox.Foreground = (Brush)new BrushConverter().ConvertFromString(overlaySpec.Color);
			_editingTextBox.FontWeight = (overlaySpec.Bold ? FontWeights.Bold : FontWeights.Normal);
			_editingTextBox.FontStyle = (overlaySpec.Italic ? FontStyles.Italic : FontStyles.Normal);
			_editingTextBox.TextDecorations = TextDecorationsFor(overlaySpec);
		}
	}

	private static TextDecorationCollection? TextDecorationsFor(OverlaySpec overlay)
	{
		TextDecorationCollection textDecorationCollection = new TextDecorationCollection();
		if (overlay.Underline)
		{
			textDecorationCollection.Add(TextDecorations.Underline[0]);
		}
		if (overlay.Strike)
		{
			textDecorationCollection.Add(TextDecorations.Strikethrough[0]);
		}
		if (textDecorationCollection.Count != 0)
		{
			return textDecorationCollection;
		}
		return null;
	}

	private static bool HasImageDrop(DragEventArgs e)
	{
		if (!e.Data.GetDataPresent(DataFormats.FileDrop))
		{
			return false;
		}
		return ((string[])e.Data.GetData(DataFormats.FileDrop)).Any(IsImageFile);
	}

	private static bool IsImageFile(string path)
	{
		switch (Path.GetExtension(path).ToLowerInvariant())
		{
		case ".png":
		case ".jpg":
		case ".jpeg":
		case ".webp":
		case ".bmp":
			return true;
		default:
			return false;
		}
	}

	private static double ImageRatio(string path)
	{
		try
		{
			BitmapSource bitmapSource = LoadForOverlay(path);
			return Math.Max(0.01, (double)bitmapSource.PixelWidth / (double)Math.Max(1, bitmapSource.PixelHeight));
		}
		catch
		{
			return 1.0;
		}
	}

	private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
	{
		for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
		{
			DependencyObject child = VisualTreeHelper.GetChild(parent, i);
			if (child is T result)
			{
				return result;
			}
			T val = FindChild<T>(child);
			if (val != null)
			{
				return val;
			}
		}
		return null;
	}

	private static BitmapSource LoadForOverlay(string path)
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
