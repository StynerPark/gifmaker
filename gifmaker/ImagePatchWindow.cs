using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using Microsoft.Win32;
using OpenCvSharp;

namespace ClipForgeWpf;

public class ImagePatchWindow : System.Windows.Window
{
	private readonly string _framePath;

	private readonly string _storage;

	private readonly BitmapSource _original;

	private readonly Canvas _canvas = new Canvas();

	private readonly Image _image = new Image();

	private readonly Polyline _outline = new Polyline
	{
		Stroke = Brushes.OrangeRed,
		StrokeThickness = 2.0,
		IsHitTestVisible = false
	};

	private readonly List<System.Windows.Point> _points = new List<System.Windows.Point>();

	private readonly TextBlock _status = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(0.0, 8.0, 0.0, 8.0)
	};

	private readonly Slider _feather = new Slider
	{
		Minimum = 0.0,
		Maximum = 12.0,
		Value = 0.0,
		Width = 110.0,
		TickFrequency = 1.0,
		IsSnapToTickEnabled = true,
		VerticalAlignment = VerticalAlignment.Center
	};

	private readonly CheckBox _showOriginal = new CheckBox
	{
		Content = "원본 보기",
		Margin = new Thickness(12.0, 8.0, 12.0, 0.0)
	};

	private readonly Button _apply = new Button
	{
		Content = "모든 프레임에 적용",
		Padding = new Thickness(15.0, 8.0, 15.0, 8.0),
		IsEnabled = false
	};

	private string _replacement;

	private byte[] _mask;

	private bool _drawing;

	public string ReferencePath { get; private set; }

	public System.Windows.Rect Area { get; private set; }

	public ImagePatchWindow(string framePath, string storage, string existingReference)
	{
		base.Loaded += delegate
		{
			UiLanguage.TranslateDialog(this);
		};
		_framePath = framePath;
		_storage = storage;
		_original = Load(framePath);
		base.Title = "Area · 자유 선택 이미지 패치";
		base.Width = 1050.0;
		base.Height = 820.0;
		base.MinWidth = 720.0;
		base.MinHeight = 540.0;
		base.WindowStartupLocation = WindowStartupLocation.CenterOwner;
		base.AllowDrop = true;
		DockPanel obj = new DockPanel
		{
			Margin = new Thickness(14.0)
		};
		object obj2 = obj;
		base.Content = obj;
		DockPanel obj3 = (DockPanel)obj2;
		TextBlock element = new TextBlock
		{
			Text = "① 마우스로 교체할 영역을 둘러 그리세요.  ② 로고를 지운 전체 프레임 이미지를 이 창에 드래그하세요.\n③ 합성 결과를 확인하고 적용하세요. 이미지는 원본과 같은 해상도여야 하며, 같은 좌표의 영역만 사용합니다.",
			TextWrapping = TextWrapping.Wrap,
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		DockPanel.SetDock(element, Dock.Top);
		obj3.Children.Add(element);
		StackPanel stackPanel = new StackPanel();
		DockPanel.SetDock(stackPanel, Dock.Bottom);
		obj3.Children.Add(stackPanel);
		stackPanel.Children.Add(_status);
		WrapPanel wrapPanel = new WrapPanel();
		stackPanel.Children.Add(wrapPanel);
		Button button = new Button
		{
			Content = "이미지 불러오기…",
			Padding = new Thickness(10.0, 8.0, 10.0, 8.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		button.Click += delegate
		{
			OpenFileDialog openFileDialog = new OpenFileDialog
			{
				Filter = "이미지|*.png;*.jpg;*.jpeg;*.bmp;*.tif;*.tiff"
			};
			if (openFileDialog.ShowDialog(this) == true)
			{
				LoadReplacement(openFileDialog.FileName);
			}
		};
		wrapPanel.Children.Add(button);
		Button button2 = new Button
		{
			Content = "영역 다시 그리기",
			Padding = new Thickness(10.0, 8.0, 10.0, 8.0),
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0)
		};
		button2.Click += delegate
		{
			_points.Clear();
			_mask = null;
			_outline.Points.Clear();
			UpdatePreview();
		};
		wrapPanel.Children.Add(button2);
		wrapPanel.Children.Add(new TextBlock
		{
			Text = "경계(px)",
			VerticalAlignment = VerticalAlignment.Center
		});
		wrapPanel.Children.Add(_feather);
		TextBlock featherValue = new TextBlock
		{
			Width = 24.0,
			VerticalAlignment = VerticalAlignment.Center,
			Text = "0"
		};
		wrapPanel.Children.Add(featherValue);
		_feather.ValueChanged += delegate
		{
			featherValue.Text = _feather.Value.ToString("0");
			if (_points.Count >= 3)
			{
				BuildMask();
				UpdatePreview();
			}
		};
		wrapPanel.Children.Add(_showOriginal);
		_showOriginal.Checked += delegate
		{
			UpdatePreview();
		};
		_showOriginal.Unchecked += delegate
		{
			UpdatePreview();
		};
		wrapPanel.Children.Add(_apply);
		_apply.Click += delegate
		{
			Commit();
		};
		Button element2 = new Button
		{
			Content = "취소",
			Padding = new Thickness(10.0, 8.0, 10.0, 8.0),
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			IsCancel = true
		};
		wrapPanel.Children.Add(element2);
		_canvas.Width = _original.PixelWidth;
		_canvas.Height = _original.PixelHeight;
		_canvas.Background = Brushes.Transparent;
		_canvas.Cursor = Cursors.Cross;
		_image.Width = _canvas.Width;
		_image.Height = _canvas.Height;
		_image.Stretch = Stretch.Fill;
		_image.Source = _original;
		_canvas.Children.Add(_image);
		_canvas.Children.Add(_outline);
		Viewbox child = new Viewbox
		{
			Stretch = Stretch.Uniform,
			Child = _canvas
		};
		obj3.Children.Add(new Border
		{
			Background = new SolidColorBrush(Color.FromRgb(25, 30, 38)),
			Child = child,
			ClipToBounds = true
		});
		_canvas.MouseLeftButtonDown += delegate(object _, MouseButtonEventArgs e)
		{
			_drawing = true;
			_points.Clear();
			_mask = null;
			_outline.Points.Clear();
			_image.Source = _original;
			AddPoint(e.GetPosition(_canvas));
			_canvas.CaptureMouse();
			_apply.IsEnabled = false;
		};
		_canvas.MouseMove += delegate(object _, MouseEventArgs e)
		{
			if (_drawing)
			{
				AddPoint(e.GetPosition(_canvas));
			}
		};
		_canvas.MouseLeftButtonUp += delegate(object _, MouseButtonEventArgs e)
		{
			if (_drawing)
			{
				AddPoint(e.GetPosition(_canvas));
				_drawing = false;
				_canvas.ReleaseMouseCapture();
				if (_points.Count >= 3)
				{
					_outline.Points.Add(_points[0]);
					BuildMask();
				}
				UpdatePreview();
			}
		};
		_canvas.LostMouseCapture += delegate
		{
			if (_drawing)
			{
				_drawing = false;
				_mask = null;
				UpdatePreview();
			}
		};
		base.PreviewDragOver += delegate(object _, DragEventArgs e)
		{
			e.Effects = (e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None);
			e.Handled = true;
		};
		base.Drop += delegate(object _, DragEventArgs e)
		{
			if (e.Data.GetData(DataFormats.FileDrop) is string[] array && array.Length == 1)
			{
				LoadReplacement(array[0]);
			}
			else
			{
				_status.Text = "수정 이미지 한 개를 드래그하세요.";
			}
			e.Handled = true;
		};
		if (existingReference != null && File.Exists(existingReference + ".mask.png"))
		{
			_mask = File.ReadAllBytes(existingReference + ".mask.png");
			_replacement = existingReference;
		}
		UpdatePreview();
	}

	private void AddPoint(System.Windows.Point point)
	{
		point = new System.Windows.Point(Math.Clamp(point.X, 0.0, _original.PixelWidth - 1), Math.Clamp(point.Y, 0.0, _original.PixelHeight - 1));
		if (_points.Count <= 0 || !((point - _points[_points.Count - 1]).Length < 1.0))
		{
			_points.Add(point);
			_outline.Points.Add(point);
		}
	}

	private void BuildMask()
	{
		using Mat mat = new Mat(_original.PixelHeight, _original.PixelWidth, MatType.CV_8UC1, Scalar.Black);
		OpenCvSharp.Point[] array = _points.Select((System.Windows.Point p) => new OpenCvSharp.Point((int)Math.Round(p.X), (int)Math.Round(p.Y))).ToArray();
		Cv2.FillPoly(mat, new OpenCvSharp.Point[1][] { array }, Scalar.White);
		if (Cv2.CountNonZero(mat) < 9)
		{
			_mask = null;
			return;
		}
		if (_feather.Value > 0.0)
		{
			using Mat mat2 = new Mat();
			Cv2.GaussianBlur(mat, mat2, new OpenCvSharp.Size(0, 0), _feather.Value);
			Cv2.Min(mat, mat2, mat);
		}
		_mask = mat.ToBytes();
	}

	private void LoadReplacement(string path)
	{
		try
		{
			BitmapSource bitmapSource = Load(path);
			if (bitmapSource.PixelWidth != _original.PixelWidth || bitmapSource.PixelHeight != _original.PixelHeight)
			{
				throw new InvalidOperationException($"원본은 {_original.PixelWidth} × {_original.PixelHeight}px입니다. 같은 크기의 전체 프레임 이미지를 불러오세요.");
			}
			Directory.CreateDirectory(_storage);
			string text = System.IO.Path.Combine(_storage, "import_" + Guid.NewGuid().ToString("N") + ".png");
			Save(bitmapSource, text);
			_replacement = text;
			UpdatePreview();
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "이미지 불러오기", MessageBoxButton.OK, MessageBoxImage.Exclamation);
		}
	}

	private void UpdatePreview()
	{
		_apply.IsEnabled = _mask != null && _replacement != null;
		_outline.Visibility = ((_showOriginal.IsChecked == true) ? Visibility.Hidden : Visibility.Visible);
		if (!_apply.IsEnabled || _showOriginal.IsChecked == true)
		{
			_image.Source = _original;
		}
		else
		{
			using Mat frame = Cv2.ImRead(_framePath);
			using Mat replacement = Cv2.ImRead(_replacement);
			using Mat mask = Cv2.ImDecode(_mask, ImreadModes.Grayscale);
			using Mat mat = ImagePatchProcessor.Composite(frame, replacement, mask);
			using MemoryStream stream = new MemoryStream(mat.ToBytes());
			_image.Source = Decode(stream);
		}
		_status.Text = UiLanguage.T((_mask == null) ? "교체할 부분을 마우스로 둘러 그리세요. 마우스를 놓으면 영역이 닫힙니다." : ((_replacement == null) ? "영역 선택 완료 · 수정한 전체 프레임 이미지를 이 창에 드래그하세요." : "합성 미리보기 · 선택 영역만 모든 프레임의 같은 위치에 적용됩니다. 원본 보기를 눌러 비교할 수 있습니다."));
	}

	private void Commit()
	{
		if (!_apply.IsEnabled)
		{
			return;
		}
		try
		{
			Directory.CreateDirectory(_storage);
			ReferencePath = System.IO.Path.Combine(_storage, "patch_" + Guid.NewGuid().ToString("N") + ".png");
			File.Copy(_replacement, ReferencePath);
			File.WriteAllBytes(ReferencePath + ".mask.png", _mask);
			Area = new System.Windows.Rect(0.0, 0.0, _original.PixelWidth, _original.PixelHeight);
			base.DialogResult = true;
		}
		catch (Exception ex)
		{
			MessageBox.Show(this, ex.Message, "패치 적용 실패");
		}
	}

	private static BitmapSource Load(string path)
	{
		using FileStream stream = File.OpenRead(path);
		return Decode(stream);
	}

	private static BitmapSource Decode(Stream stream)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.StreamSource = stream;
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}

	private static void Save(BitmapSource bitmap, string path)
	{
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(bitmap));
		using FileStream stream = File.Create(path);
		pngBitmapEncoder.Save(stream);
	}
}
