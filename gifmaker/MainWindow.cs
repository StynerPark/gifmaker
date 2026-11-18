using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;
using LibVLCSharp.Shared;
using LibVLCSharp.WPF;
using Microsoft.Win32;

namespace ClipForgeWpf;

public class MainWindow : Window, IComponentConnector
{
	private enum ExportFormat
	{
		Gif,
		Webp,
		Mp4
	}

	private sealed record ExportPlan(int CropX, int CropY, int CropWidth, int CropHeight, int OutputWidth, int OutputHeight, bool HasOverlays, Rect? SubtitleRect, string? SubtitleReferencePath, int Loop, int Quality, int[] FrameOrder)
	{
		public bool HasSubtitleRemoval
		{
			get
			{
				if (SubtitleRect.HasValue)
				{
					return !string.IsNullOrWhiteSpace(SubtitleReferencePath);
				}
				return false;
			}
		}
	}

	private PlaybackMode _playbackMode;

	private readonly ObservableCollection<FrameItem> _frames = new ObservableCollection<FrameItem>();

	private readonly DispatcherTimer _uiTimer = new DispatcherTimer
	{
		Interval = TimeSpan.FromMilliseconds(40.0)
	};

	private readonly DispatcherTimer _previewTimer = new DispatcherTimer();

	private readonly LibVLC _libVlc;

	private LibVLCSharp.Shared.MediaPlayer _player;

	private Media? _media;

	private string? _videoPath;

	private readonly string _tempRoot = System.IO.Path.Combine(TempBase, "session_" + Guid.NewGuid().ToString("N"));

	private double _duration;

	private double _fps;

	private int _width;

	private int _height;

	private double _start;

	private double _end;

	private int _previewIndex;

	private bool _updatingTimeline;

	private bool _extracting;

	private bool _muted = true;

	private bool _suppressFramePreview;

	private int _loadSerial;

	private DesktopRecordingSession _desktopRecording;

	private string _recordVideoDestination;

	private bool _recordVideoMode;

	private bool _sourcePaused = true;

	private CancellationTokenSource? _extractCancel;

	private Rect? _recordRect;

	private bool _recording;

	private string? _recordDir;

	private int _recordFrameIndex;

	private readonly int _recordFps = 30;

	private WindowState _beforeAreaRecordState;

	private Rect? _cropRect;

	private int _imageScalePercent = 100;

	private TextBlock _imageScaleLabel;

	private TextBlock _imageScaleCustomLabel;

	private System.Windows.Controls.TextBox _imageScaleInput;
	private readonly System.Collections.Generic.Dictionary<int, System.Windows.Controls.Button> _imageScaleButtons = new();

	private System.Windows.Controls.Button _imageScaleApply;

	private Rect? _subtitleRect;

	private string? _subtitleReferencePath;

	private int _subtitleRevision;

	private int _subtitlePreviewSerial;

	private readonly SemaphoreSlim _subtitlePreviewGate = new SemaphoreSlim(1, 1);

	private readonly ObservableCollection<OverlaySpec> _overlays = new ObservableCollection<OverlaySpec>();

	private string _lastStatus = "Status: idle";

	internal Grid ResponsiveHost;

	internal Viewbox ResponsiveView;

	internal Grid LayoutRoot;

	internal System.Windows.Controls.Button ClearCacheButton;

	internal System.Windows.Controls.Button LanguageButton;

	internal Border VideoHost;

	internal System.Windows.Controls.Image SourceStillImage;

	internal VideoView VideoView;

	internal TextBlock EmptyVideoText;

	internal Popup VideoDropPopup;

	internal TextBlock MetaText;

	internal TextBlock StartText;

	internal TextBlock CurrentText;

	internal TextBlock EndText;

	internal System.Windows.Controls.Button MuteButton;

	internal System.Windows.Shapes.Path MuteSlash;

	internal Slider Timeline;

	internal TextBlock SelectedStartText;

	internal TextBlock SelectedEndText;

	internal TextBlock SegmentText;

	internal TextBlock StatusText;

	internal System.Windows.Controls.ProgressBar ExtractProgress;

	internal TextBlock ProgressText;

	internal System.Windows.Controls.ListView FrameList;

	internal System.Windows.Controls.Image ResultImage;

	internal TextBlock SubtitleRemovalText;

	internal System.Windows.Controls.ComboBox ResolutionBox;

	internal System.Windows.Controls.TextBox LoopText;

	internal System.Windows.Controls.TextBox QualityText;

	internal Slider QualitySlider;

	internal System.Windows.Controls.Button RecordButton;

	internal ToggleButton ReverseToggle;

	internal ToggleButton ReversePlusToggle;
	private System.Windows.Controls.Button _saveZipButton;
	private System.Windows.Controls.Button _tmpOpenButton;

	private bool _contentLoaded;

	private static string BaseDir => AppContext.BaseDirectory;

	private static string TempBase => System.IO.Path.Combine(BaseDir, "tmp");

	private static string FfmpegPath => System.IO.Path.Combine(BaseDir, "ffmpeg.exe");

	private double CurrentSeconds
	{
		get
		{
			if (_player.Time <= 0)
			{
				return Math.Clamp(Timeline.Value, 0.0, Math.Max(Timeline.Maximum, 0.0));
			}
			return (double)_player.Time / 1000.0;
		}
	}

	private bool SubtitleRemovalActive
	{
		get
		{
			Rect? subtitleRect = _subtitleRect;
			if (subtitleRect.HasValue)
			{
				Rect valueOrDefault = subtitleRect.GetValueOrDefault();
				if (valueOrDefault.Width >= 2.0 && valueOrDefault.Height >= 2.0 && !string.IsNullOrWhiteSpace(_subtitleReferencePath))
				{
					return File.Exists(_subtitleReferencePath);
				}
			}
			return false;
		}
	}

	public MainWindow()
	{
		Core.Initialize();
		UiLanguage.Load();
		InitializeComponent();
		InstallTmpControl();
		InstallScaleControls();
		UpdatePlaybackLabels();
		Rect workArea = SystemParameters.WorkArea;
		base.MinWidth = Math.Min(base.MinWidth, Math.Max(320.0, workArea.Width - 16.0));
		base.MinHeight = Math.Min(base.MinHeight, Math.Max(280.0, workArea.Height - 16.0));
		base.Width = Math.Min(base.Width, Math.Max(base.MinWidth, workArea.Width - 24.0));
		base.Height = Math.Min(base.Height, Math.Max(base.MinHeight, workArea.Height - 24.0));
		base.WindowStartupLocation = WindowStartupLocation.CenterScreen;
		LanguageButton.Content = (UiLanguage.Korean ? "KO" : "EN");
		CleanupOldTempSessions();
		Directory.CreateDirectory(_tempRoot);
		_libVlc = new LibVLC("--no-video-title-show", "--quiet");
		_player = new LibVLCSharp.Shared.MediaPlayer(_libVlc);
		_player.Mute = _muted;
		VideoView.MediaPlayer = _player;
		VideoView.Visibility = Visibility.Collapsed;
		UpdateMuteButton();
		UpdateSubtitleUi();
		FrameList.ItemsSource = _frames;
		_uiTimer.Tick += delegate
		{
			SyncPositionFromPlayer();
		};
		_previewTimer.Tick += delegate
		{
			TickPreview();
		};
		base.Closing += delegate
		{
			_desktopRecording?.Dispose();
			_desktopRecording = null;
			try
			{
				_player.Stop();
			}
			catch
			{
			}
			StopRecordingAsync(addFrames: false);
			try
			{
				VideoView.MediaPlayer = null;
			}
			catch
			{
			}
			_media?.Dispose();
			_player.Dispose();
			_libVlc.Dispose();
			ClearTempRoot(deleteRoot: true);
		};
		base.LocationChanged += delegate
		{
			RefreshVideoDropGuard();
		};
		base.Loaded += delegate
		{
			UpdateResponsiveLayout();
		};
		ResponsiveHost.SizeChanged += delegate
		{
			UpdateResponsiveLayout();
		};
		base.SizeChanged += delegate
		{
			UpdateResponsiveLayout();
			RefreshVideoDropGuard();
		};
	}

	private void InstallTmpControl()
	{
		if (_saveZipButton.Parent is not System.Windows.Controls.Primitives.UniformGrid buttons ||
			buttons.Parent is not StackPanel saveContent || saveContent.Parent is not Border saveCard ||
			saveCard.Parent is not Grid parent) return;

		buttons.Children.Remove(_saveZipButton);
		buttons.Columns = 3;
		buttons.Rows = 1;
		var templateButton = (System.Windows.Controls.Button)buttons.Children[0];
		var saveHeader = (TextBlock)saveContent.Children[0];
		int row = Grid.GetRow(saveCard);
		parent.Children.Remove(saveCard);

		var pair = new Grid { Margin = saveCard.Margin };
		pair.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(3, GridUnitType.Star) });
		pair.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		Grid.SetRow(pair, row);
		parent.Children.Add(pair);
		saveCard.Margin = new Thickness(0);
		pair.Children.Add(saveCard);

		var tmpCard = new Border
		{
			Style = saveCard.Style,
			Background = saveCard.Background,
			BorderBrush = saveCard.BorderBrush,
			BorderThickness = saveCard.BorderThickness,
			CornerRadius = saveCard.CornerRadius,
			Padding = saveCard.Padding,
			Margin = new Thickness(7, 0, 0, 0)
		};
		Grid.SetColumn(tmpCard, 1);
		pair.Children.Add(tmpCard);
		var tmpContent = new StackPanel();
		tmpCard.Child = tmpContent;
		tmpContent.Children.Add(new TextBlock
		{
			Text = "tmp",
			Style = saveHeader.Style,
			FontFamily = saveHeader.FontFamily,
			FontSize = saveHeader.FontSize,
			FontWeight = saveHeader.FontWeight,
			Foreground = saveHeader.Foreground,
			Margin = saveHeader.Margin
		});
		_tmpOpenButton = new System.Windows.Controls.Button
		{
			Content = UiLanguage.Korean ? "열기" : "Open",
			Style = templateButton.Style,
			Height = templateButton.Height,
			FontFamily = templateButton.FontFamily,
			FontSize = templateButton.FontSize,
			FontWeight = templateButton.FontWeight,
			Background = templateButton.Background,
			BorderBrush = templateButton.BorderBrush,
			BorderThickness = templateButton.BorderThickness,
			Padding = templateButton.Padding,
			Margin = buttons.Margin
		};
		_tmpOpenButton.Click += delegate
		{
			Directory.CreateDirectory(TempBase);
			System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(TempBase) { UseShellExecute = true });
		};
		tmpContent.Children.Add(_tmpOpenButton);
	}

	private void InstallScaleControls()
	{
		if (QualitySlider.Parent is not StackPanel parent) return;
		int index = parent.Children.IndexOf(QualitySlider);
		var box = new StackPanel { Margin = new Thickness(0, 4, 0, 0) };
		_imageScaleLabel = new TextBlock { Margin = new Thickness(0, 0, 0, 2) };
		TextBlock referenceLabel = FindTextLabel(LayoutRoot, "Resolution", "해상도");
		if (referenceLabel != null)
		{
			_imageScaleLabel.Style = referenceLabel.Style;
			_imageScaleLabel.FontFamily = referenceLabel.FontFamily;
			_imageScaleLabel.FontSize = referenceLabel.FontSize;
			_imageScaleLabel.FontWeight = referenceLabel.FontWeight;
			_imageScaleLabel.FontStyle = referenceLabel.FontStyle;
			_imageScaleLabel.FontStretch = referenceLabel.FontStretch;
			_imageScaleLabel.Foreground = referenceLabel.Foreground;
		}
		box.Children.Add(_imageScaleLabel);
		var row = new Grid();
		for (int i = 0; i < 4; i++) row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		int column = 0;
		foreach (int percent in new[] { 50, 100, 200 })
		{
			var button = new System.Windows.Controls.Button { Content = percent + "%", Height = 25,
				FontSize = 11, Margin = new Thickness(0, 0, 3, 0), Padding = new Thickness(0) };
			button.Click += delegate { SetImageScale(percent); };
			_imageScaleButtons[percent] = button;
			Grid.SetColumn(button, column++);
			row.Children.Add(button);
		}
		var customCell = new Grid();
		customCell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
		customCell.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
		Grid.SetColumn(customCell, 3);
		row.Children.Add(customCell);
		_imageScaleInput = new System.Windows.Controls.TextBox { Text = "100", Height = 25, FontSize = 11,
			TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(1, 0, 1, 0),
			ToolTip = UiLanguage.Korean ? "자유 배율" : "Custom scale" };
		customCell.Children.Add(_imageScaleInput);
		var percentMark = new TextBlock { Text = "%", FontSize = 11, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = System.Windows.HorizontalAlignment.Center };
		Grid.SetColumn(percentMark, 1);
		customCell.Children.Add(percentMark);
		void ApplyCustom()
		{
			if (int.TryParse(_imageScaleInput.Text.Trim().TrimEnd('%'), out int value) && value >= 10 && value <= 2000)
			{
				SetImageScale(value);
			}
			else
			{
				_imageScaleInput.Text = _imageScalePercent.ToString();
			}
		}
		_imageScaleInput.LostFocus += delegate { ApplyCustom(); };
		_imageScaleInput.TextChanged += delegate
		{
			if (int.TryParse(_imageScaleInput.Text.Trim().TrimEnd('%'), out int value) && value >= 10 && value <= 2000)
			{
				_imageScalePercent = value;
				UpdateScaleButtonColors();
				RefreshResultPreview();
			}
		};
		_imageScaleInput.KeyDown += delegate(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (e.Key == System.Windows.Input.Key.Enter) { ApplyCustom(); e.Handled = true; }
		};
		box.Children.Add(row);
		parent.Children.Insert(index + 1, box);
		UpdateScaleLabels();
		UpdateScaleButtonColors();
	}

	private static TextBlock FindTextLabel(DependencyObject root, string english, string korean)
	{
		if (root is TextBlock t && (t.Text == english || t.Text == korean)) return t;
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is DependencyObject d)
			{
				TextBlock found = FindTextLabel(d, english, korean);
				if (found != null) return found;
			}
		}
		return null;
	}

	private void UpdateScaleLabels()
	{
		if (_imageScaleLabel == null) return;
		_imageScaleLabel.Text = UiLanguage.Korean ? "이미지 축소 확대" : "Image scale";
		_imageScaleInput.ToolTip = UiLanguage.Korean ? "자유 배율" : "Custom scale";
	}

	private void SetImageScale(int percent)
	{
		_imageScalePercent = percent;
		if (_imageScaleInput != null) _imageScaleInput.Text = percent.ToString();
		UpdateScaleButtonColors();
		RefreshResultPreview();
	}

	private void UpdateScaleButtonColors()
	{
		foreach (var entry in _imageScaleButtons)
		{
			entry.Value.Background = new SolidColorBrush(entry.Key == _imageScalePercent
				? System.Windows.Media.Color.FromRgb(205, 216, 232)
				: System.Windows.Media.Color.FromRgb(237, 242, 248));
		}
	}

	private void UpdatePlaybackLabels()
	{
		SetPlaybackLabel(ReverseToggle, UiLanguage.Korean ? "역방향" : "Reverse");
		SetPlaybackLabel(ReversePlusToggle, UiLanguage.Korean ? "왕복" : "Ping-Pong");
	}

	private static void SetPlaybackLabel(DependencyObject root, string label)
	{
		if (root is TextBlock text && (text.Text == "Reverse" || text.Text == "Reverse+" || text.Text == "역방향" || text.Text == "왕복" || text.Text == "Ping-Pong"))
		{
			text.Text = label;
			return;
		}
		if (root is ContentControl control && control.Content is string)
		{
			control.Content = label;
			return;
		}
		foreach (object child in LogicalTreeHelper.GetChildren(root))
		{
			if (child is DependencyObject d) SetPlaybackLabel(d, label);
		}
	}

	private void UpdateResponsiveLayout()
	{
		if (ResponsiveHost == null || LayoutRoot == null)
		{
			return;
		}
		double num = Math.Max(1.0, ResponsiveHost.ActualWidth - 16.0);
		double num2 = Math.Max(1.0, ResponsiveHost.ActualHeight - 16.0);
		if (num < 100.0 || num2 < 100.0)
		{
			return;
		}
		double num3 = Math.Min(1.0, Math.Min(num / 1100.0, num2 / 600.0));
		double num4 = num / num3;
		double num5 = num2 / num3;
		if (Math.Abs(LayoutRoot.Width - num4) > 0.1)
		{
			LayoutRoot.Width = num4;
		}
		if (Math.Abs(LayoutRoot.Height - num5) > 0.1)
		{
			LayoutRoot.Height = num5;
		}
		base.Dispatcher.BeginInvoke((Action)delegate
		{
			if (FrameList.View is GridView gridView && gridView.Columns.Count == 4)
			{
				double num6 = Math.Max(210.0, FrameList.ActualWidth - 8.0);
				gridView.Columns[0].Width = num6 * 0.13;
				gridView.Columns[1].Width = num6 * 0.29;
				gridView.Columns[2].Width = num6 * 0.29;
				gridView.Columns[3].Width = num6 * 0.29;
			}
			if (VideoDropPopup?.Child is FrameworkElement frameworkElement && VideoHost.IsVisible && VideoHost.ActualWidth > 0.0 && PresentationSource.FromVisual(VideoHost) != null)
			{
				System.Windows.Point point = VideoHost.PointToScreen(new System.Windows.Point(0.0, 0.0));
				System.Windows.Point point2 = VideoHost.PointToScreen(new System.Windows.Point(VideoHost.ActualWidth, VideoHost.ActualHeight));
				DpiScale dpi = VisualTreeHelper.GetDpi(this);
				frameworkElement.Width = Math.Max(0.0, (point2.X - point.X) / dpi.DpiScaleX);
				frameworkElement.Height = Math.Max(0.0, (point2.Y - point.Y) / dpi.DpiScaleY);
			}
		}, DispatcherPriority.Loaded);
	}

	private void Language_Click(object sender, RoutedEventArgs e)
	{
		UiLanguage.Set(!UiLanguage.Korean);
		LanguageButton.Content = (UiLanguage.Korean ? "KO" : "EN");
		SetStatus(_lastStatus);
		UpdateTexts();
		UpdateMuteButton();
		UpdateSubtitleUi();
		RecordButton.Content = UiLanguage.T(_recording ? "■ Stop" : "● Full Record");
		UpdatePlaybackLabels();
		UpdateScaleLabels();
		if (_tmpOpenButton != null) _tmpOpenButton.Content = UiLanguage.Korean ? "열기" : "Open";
	}

	private void SetStatus(string text)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			if (!base.Dispatcher.HasShutdownStarted)
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					SetStatus(text);
				});
			}
		}
		else
		{
			_lastStatus = text;
			StatusText.Text = UiLanguage.T(text);
		}
	}

	private void SetProgress(int current, int total, string label)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			if (!base.Dispatcher.HasShutdownStarted)
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					SetProgress(current, total, label);
				});
			}
			return;
		}
		if (total <= 0)
		{
			ExtractProgress.IsIndeterminate = true;
			ProgressText.Text = label;
			return;
		}
		ExtractProgress.IsIndeterminate = false;
		ExtractProgress.Maximum = total;
		ExtractProgress.Value = Math.Clamp(current, 0, total);
		double value = Math.Clamp((double)current * 100.0 / (double)total, 0.0, 100.0);
		ProgressText.Text = $"{current:N0}/{total:N0} ({value:0}%)";
	}

	private void ResetProgress(string text = "-")
	{
		if (!base.Dispatcher.CheckAccess())
		{
			if (!base.Dispatcher.HasShutdownStarted)
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					ResetProgress(text);
				});
			}
		}
		else
		{
			ExtractProgress.IsIndeterminate = false;
			ExtractProgress.Maximum = 100.0;
			ExtractProgress.Value = 0.0;
			ProgressText.Text = text;
		}
	}

	private static string TimeText(double seconds)
	{
		return TimeSpan.FromSeconds(Math.Max(0.0, seconds)).ToString("mm\\:ss\\.fff", CultureInfo.InvariantCulture);
	}

	private void Window_DragOver(object sender, System.Windows.DragEventArgs e)
	{
		if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
		{
			e.Effects = System.Windows.DragDropEffects.Copy;
		}
		e.Handled = true;
	}

	private void Window_PreviewDragOver(object sender, System.Windows.DragEventArgs e)
	{
		Window_DragOver(sender, e);
	}

	private void Window_PreviewDrop(object sender, System.Windows.DragEventArgs e)
	{
		LoadDropped(e);
	}

	private void Window_Drop(object sender, System.Windows.DragEventArgs e)
	{
		LoadDropped(e);
	}

	private void Video_Drop(object sender, System.Windows.DragEventArgs e)
	{
		LoadDropped(e);
	}

	private void RefreshVideoDropGuard()
	{
		if (VideoDropPopup != null)
		{
			bool isOpen = VideoDropPopup.IsOpen;
			VideoDropPopup.IsOpen = false;
			VideoDropPopup.IsOpen = isOpen;
		}
	}

	private void LoadDropped(System.Windows.DragEventArgs e)
	{
		e.Handled = true;
		if (e.Data.GetDataPresent(System.Windows.DataFormats.FileDrop))
		{
			string text = ((string[])e.Data.GetData(System.Windows.DataFormats.FileDrop)).FirstOrDefault(File.Exists);
			if (!string.IsNullOrWhiteSpace(text))
			{
				LoadVideo(text);
			}
		}
	}

	private void OpenVideo_Click(object sender, RoutedEventArgs e)
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Video|*.mp4;*.mov;*.mkv;*.avi;*.webm;*.m4v;*.wmv|All|*.*"
		};
		if (openFileDialog.ShowDialog() == true)
		{
			LoadVideo(openFileDialog.FileName);
		}
	}

	private async void ClearCache_Click(object sender, RoutedEventArgs e)
	{
		StopExtract();
		await StopRecordingAsync(addFrames: false);
		_loadSerial++;
		_sourcePaused = true;
		StopSourceForNewVideo();
		_player = new LibVLCSharp.Shared.MediaPlayer(_libVlc)
		{
			Mute = _muted
		};
		VideoView.MediaPlayer = _player;
		VideoView.Visibility = Visibility.Collapsed;
		_frames.Clear();
		FrameList.Items.Refresh();
		ResultImage.Source = null;
		SourceStillImage.Source = null;
		SourceStillImage.Visibility = Visibility.Visible;
		VideoView.Visibility = Visibility.Collapsed;
		EmptyVideoText.Visibility = Visibility.Visible;
		_videoPath = null;
		_duration = 0.0;
		_fps = 0.0;
		_width = 0;
		_height = 0;
		_start = 0.0;
		_end = 0.0;
		_cropRect = null;
		_imageScalePercent = 100;

		ResetSubtitleState();
		Timeline.Maximum = 1.0;
		Timeline.Value = 0.0;
		MetaText.Text = "-";
		UpdateTexts();
		ResetProgress();
		SetStatus("Status: clearing cache");
		await Task.Delay(150);
		await Task.Run(delegate
		{
			ForceDeleteDirectory(TempBase);
		});
		Directory.CreateDirectory(_tempRoot);
		SetStatus("Status: cache cleared");
	}

	private async void LoadVideo(string path)
	{
		if (!File.Exists(path))
		{
			return;
		}
		int loadId = ++_loadSerial;
		try
		{
			_videoPath = path;
			SetStatus("Status: reading original info");
			ResetProgress();
			EmptyVideoText.Visibility = Visibility.Collapsed;
			SourceStillImage.Source = null;
			SourceStillImage.Visibility = Visibility.Visible;
			VideoView.Visibility = Visibility.Collapsed;
			StopSourceForNewVideo();
			await Task.Delay(40);
			if (loadId != _loadSerial)
			{
				return;
			}
			_player = new LibVLCSharp.Shared.MediaPlayer(_libVlc)
			{
				Mute = _muted
			};
			LibVLCSharp.Shared.MediaPlayer sourcePlayer = _player;
			_sourcePaused = true;
			sourcePlayer.Playing += delegate
			{
				base.Dispatcher.BeginInvoke((Action)delegate
				{
					if (_player == sourcePlayer && _sourcePaused)
					{
						sourcePlayer.SetPause(pause: true);
					}
				});
			};
			VideoView.MediaPlayer = sourcePlayer;
			SourceStillImage.Visibility = Visibility.Collapsed;
			VideoView.Visibility = Visibility.Visible;
			_media = new Media(_libVlc, path, FromType.FromPath);
			_player.Media = _media;
			_player.Mute = _muted;
			_player.Play();
			_player.Time = 0L;
			_uiTimer.Stop();
			_player.Mute = _muted;
			(double, double, int, int) tuple = await Task.Run(() => Probe(path));
			if (loadId == _loadSerial)
			{
				MainWindow mainWindow = this;
				double duration;
				if (tuple.Item1 > 0.0)
				{
					(duration, _, _, _) = tuple;
				}
				else
				{
					duration = Math.Max(0.0, (double)_player.Length / 1000.0);
				}
				mainWindow._duration = duration;
				_fps = tuple.Item2;
				_width = tuple.Item3;
				_height = tuple.Item4;
				_start = 0.0;
				_end = _duration;
				_cropRect = null;
		_imageScalePercent = 100;

				ResetSubtitleState();
				Timeline.Maximum = Math.Max(_duration, 0.001);
				Timeline.Value = 0.0;
				MetaText.Text = $"{_width}x{_height} | {_fps:0.###} FPS | {TimeText(_duration)}";
				SetStatus((_fps > 0.0) ? "Status: idle" : "Status: failed to read source FPS");
				UpdateTexts();
			}
		}
		catch (Exception ex)
		{
			SetStatus("Status: load failed " + Short(ex.Message));
		}
	}

	private void StopSourceForNewVideo()
	{
		_uiTimer.Stop();
		_previewTimer.Stop();
		try
		{
			_player.Pause();
		}
		catch
		{
		}
		try
		{
			_player.Stop();
		}
		catch
		{
		}
		try
		{
			VideoView.Visibility = Visibility.Collapsed;
		}
		catch
		{
		}
		try
		{
			VideoView.MediaPlayer = null;
		}
		catch
		{
		}
		try
		{
			_player.Media = null;
		}
		catch
		{
		}
		try
		{
			_media?.Dispose();
		}
		catch
		{
		}
		_media = null;
		try
		{
			_player.Dispose();
		}
		catch
		{
		}
	}

	private void PlayPause_Click(object sender, RoutedEventArgs e)
	{
		TogglePlayback();
	}

	private void Mute_Click(object sender, RoutedEventArgs e)
	{
		_muted = !_muted;
		try
		{
			_player.Mute = _muted;
		}
		catch
		{
		}
		UpdateMuteButton();
	}

	private void UpdateMuteButton()
	{
		if (MuteSlash != null && MuteButton != null)
		{
			MuteSlash.Visibility = ((!_muted) ? Visibility.Collapsed : Visibility.Visible);
			MuteButton.ToolTip = UiLanguage.T(_muted ? "Muted" : "Sound");
		}
	}

	private void Window_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
	{
		if (e.Key == Key.Space && !(Keyboard.FocusedElement is System.Windows.Controls.TextBox))
		{
			TogglePlayback();
			e.Handled = true;
		}
	}

	private void TogglePlayback()
	{
		if (_videoPath != null)
		{
			SourceStillImage.Visibility = Visibility.Collapsed;
			VideoView.Visibility = Visibility.Visible;
			_sourcePaused = !_sourcePaused;
			if (_sourcePaused)
			{
				_player.SetPause(pause: true);
				_uiTimer.Stop();
				SyncPositionFromPlayer();
			}
			else
			{
				_player.Mute = _muted;
				_player.Play();
				_uiTimer.Start();
			}
		}
	}

	private void Back5_Click(object sender, RoutedEventArgs e)
	{
		SeekBy(-5.0);
	}

	private void Back1_Click(object sender, RoutedEventArgs e)
	{
		SeekBy(-1.0);
	}

	private void Forward1_Click(object sender, RoutedEventArgs e)
	{
		SeekBy(1.0);
	}

	private void Forward5_Click(object sender, RoutedEventArgs e)
	{
		SeekBy(5.0);
	}

	private void PrevFrame_Click(object sender, RoutedEventArgs e)
	{
		SeekBy(-1.0 / Math.Max(_fps, 1.0));
	}

	private void NextFrame_Click(object sender, RoutedEventArgs e)
	{
		SeekBy(1.0 / Math.Max(_fps, 1.0));
	}

	private void SeekBy(double delta)
	{
		double num = Math.Clamp(Math.Max(Timeline.Value, CurrentSeconds) + delta, 0.0, Math.Max(_duration, 0.0));
		_player.Time = (long)Math.Round(num * 1000.0);
		_updatingTimeline = true;
		Timeline.Value = num;
		_updatingTimeline = false;
		UpdateTexts();
	}

	private void Timeline_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (!_updatingTimeline && _videoPath != null)
		{
			double num = Math.Clamp(Timeline.Value, 0.0, Math.Max(_duration, 0.0));
			_player.Time = (long)Math.Round(num * 1000.0);
			UpdateTexts();
		}
	}

	private void SyncPositionFromPlayer()
	{
		_updatingTimeline = true;
		Timeline.Value = Math.Clamp((double)_player.Time / 1000.0, 0.0, Timeline.Maximum);
		_updatingTimeline = false;
		UpdateTexts();
	}

	private void SetStart_Click(object sender, RoutedEventArgs e)
	{
		(_start, _end) = SegmentSelection.SetStart(CurrentSeconds, _end, _duration);
		UpdateTexts();
	}

	private void SetEnd_Click(object sender, RoutedEventArgs e)
	{
		(_start, _end) = SegmentSelection.SetEnd(CurrentSeconds, _start, _duration);
		UpdateTexts();
	}

	private void UpdateTexts()
	{
		StartText.Text = TimeText(_start);
		CurrentText.Text = TimeText(CurrentSeconds);
		EndText.Text = TimeText(_end);
		SelectedStartText.Text = TimeText(_start);
		SelectedEndText.Text = TimeText(_end);
		SegmentText.Text = UiLanguage.T("Segment: " + TimeText(_start) + " ~ " + TimeText(_end));
	}

	private async void Extract_Click(object sender, RoutedEventArgs e)
	{
		if (_videoPath == null)
		{
			return;
		}
		string sourcePath = _videoPath;
		if (_extracting)
		{
			SetStatus("Status: extraction is already running");
			return;
		}
		if (_fps <= 0.0)
		{
			SetStatus("Status: no source FPS info");
			return;
		}
		if (_end <= _start)
		{
			SetStatus("Status: choose an end after the start before extracting");
			return;
		}
		string outDir = System.IO.Path.Combine(_tempRoot, "frames_" + DateTime.Now.Ticks);
		Directory.CreateDirectory(outDir);
		int expectedFrames = EstimateFrameCount(_start, _end);
		SetStatus("Status: extracting original frames");
		SetProgress(0, expectedFrames, "0/" + expectedFrames.ToString("N0"));
		_extracting = true;
		_extractCancel?.Dispose();
		_extractCancel = new CancellationTokenSource();
		CancellationToken token = _extractCancel.Token;
		try
		{
			string[] files = await Task.Run(() => ExtractOriginalFrames(sourcePath, outDir, _start, _end, expectedFrames, delegate(int current)
			{
				SetStatus($"Status: extracting {current:N0}/{expectedFrames:N0} frames");
				SetProgress(current, expectedFrames, "");
			}, token), token);
			await AddFramesAsync(files);
			SetProgress(files.Length, Math.Max(expectedFrames, files.Length), "done");
			SetStatus($"Status: done {files.Length} frames");
		}
		catch (OperationCanceledException)
		{
			ResetProgress("stopped");
			SetStatus("Status: extraction stopped");
		}
		catch (Exception ex2)
		{
			ResetProgress("failed");
			SetStatus("Status: extract failed " + Short(ex2.Message));
		}
		finally
		{
			_extracting = false;
			_extractCancel?.Dispose();
			_extractCancel = null;
		}
	}

	private void StopExtract_Click(object sender, RoutedEventArgs e)
	{
		StopExtract();
	}

	private void StopExtract()
	{
		if (_extracting)
		{
			try
			{
				_extractCancel?.Cancel();
			}
			catch
			{
			}
			SetStatus("Status: stopping extraction");
		}
	}

	private void RecordArea_Click(object sender, RoutedEventArgs e)
	{
		if (_recording)
		{
			SetStatus("Status: stop recording before changing area");
		}
		else
		{
			BeginAreaSelection();
		}
	}

	private void BeginAreaSelection(bool fullScreen = false)
	{
		_beforeAreaRecordState = base.WindowState;
		base.WindowState = WindowState.Minimized;
		ScreenRegionWindow screenRegionWindow = new ScreenRegionWindow(async delegate(Rect rect, bool video)
		{
			_recordRect = rect;
			return await StartRecordingAsync(rect, video);
		}, () => StopRecordingAsync(addFrames: true), () => StopRecordingAsync(addFrames: false), fullScreen);
		bool valueOrDefault = screenRegionWindow.ShowDialog() == true;
		if (!_recording)
		{
			base.WindowState = _beforeAreaRecordState;
			Activate();
		}
		if (valueOrDefault)
		{
			_recordRect = screenRegionWindow.SelectedRect;
			SetStatus($"Status: record area {Math.Round(_recordRect.Value.Width):0}x{Math.Round(_recordRect.Value.Height):0}");
		}
	}

	private async void RecordToggle_Click(object sender, RoutedEventArgs e)
	{
		if (!_recording)
		{
			BeginAreaSelection(fullScreen: true);
		}
		else
		{
			await StopRecordingAsync(addFrames: true);
		}
	}

	private async Task<bool> StartRecordingAsync(Rect? rectOverride, bool video)
	{
		if (_recording)
		{
			return false;
		}
		System.Drawing.Rectangle virtualScreen = SystemInformation.VirtualScreen;
		Rect rect = rectOverride ?? new Rect(virtualScreen.X, virtualScreen.Y, virtualScreen.Width, virtualScreen.Height);
		rect = new Rect(Math.Round(rect.X), Math.Round(rect.Y), Math.Max(2.0, Math.Round(rect.Width)), Math.Max(2.0, Math.Round(rect.Height)));
		string recordVideoDestination = null;
		if (video)
		{
			Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
			{
				Filter = "MP4|*.mp4",
				FileName = "recording.mp4",
				DefaultExt = ".mp4",
				AddExtension = true
			};
			Window owner = (Window)(((object)System.Windows.Application.Current.Windows.OfType<ScreenRegionWindow>().FirstOrDefault()) ?? ((object)this));
			if (saveFileDialog.ShowDialog(owner) != true)
			{
				return false;
			}
			recordVideoDestination = saveFileDialog.FileName;
		}
		_recordDir = System.IO.Path.Combine(_tempRoot, "record_" + DateTime.Now.Ticks);
		Directory.CreateDirectory(_recordDir);
		string destination = System.IO.Path.Combine(_recordDir, video ? "recording.mp4" : "record_%06d.png");
		try
		{
			CapturePlan plan = DesktopCapture.Arguments(rect, _recordFps, video, destination);
			_recordFrameIndex = 0;
			_recordVideoMode = video;
			_recordVideoDestination = recordVideoDestination;
			bool softwareCapture = false;
			_desktopRecording = new DesktopRecordingSession(FfmpegPath, plan, delegate(int frame)
			{
				Interlocked.Exchange(ref _recordFrameIndex, frame);
				if (frame % _recordFps == 0)
				{
					SetStatus($"Status: recording {frame:N0} frames");
				}
			});
			await Task.Delay(700);
			if (_desktopRecording.HasExited)
			{
				_desktopRecording.Dispose();
				softwareCapture = true;
				_desktopRecording = new DesktopRecordingSession(FfmpegPath, plan, delegate(int frame)
				{
					Interlocked.Exchange(ref _recordFrameIndex, frame);
					if (frame % _recordFps == 0)
					{
						SetStatus($"Status: recording {frame:N0} frames");
					}
				}, software: true);
				await Task.Delay(700);
				if (_desktopRecording.HasExited)
				{
					await _desktopRecording.Finish(cancel: false);
					throw new IOException(UiLanguage.T("Recording did not start."));
				}
			}
			_recording = true;
			SetRecordingUi(recording: true);
			SetStatus(video ? "Video recording · PC audio" : (softwareCapture ? "Frame recording · SDR" : "Frame recording · HDR/SDR auto"));
			return true;
		}
		catch (Exception ex)
		{
			_desktopRecording?.Dispose();
			_desktopRecording = null;
			_recordDir = null;
			SetStatus("Status: recording failed " + Short(ex.Message));
			System.Windows.MessageBox.Show(ex.Message, UiLanguage.T("Recording failed"));
			return false;
		}
	}

	private async Task StopRecordingAsync(bool addFrames)
	{
		if (!_recording)
		{
			return;
		}
		_recording = false;
		SetRecordingUi(recording: false);
		DesktopRecordingSession session = _desktopRecording;
		_desktopRecording = null;
		string dir = _recordDir;
		_recordDir = null;
		try
		{
			if (session != null)
			{
				await session.Finish(!addFrames);
			}
			if (!addFrames || dir == null)
			{
				SetStatus("Status: recording stopped");
				return;
			}
			if (_recordVideoMode)
			{
				string text = System.IO.Path.Combine(dir, "recording.mp4");
				if (!File.Exists(text) || new FileInfo(text).Length == 0L)
				{
					throw new IOException(UiLanguage.T("Recording file is empty."));
				}
				File.Copy(text, _recordVideoDestination, overwrite: true);
				SetStatus("Status: saved");
				return;
			}
			string[] files = (from path in Directory.GetFiles(dir, "record_*.png")
				orderby path
				select path).ToArray();
			if (files.Length == 0)
			{
				SetStatus("Status: recording stopped, no frames");
				return;
			}
			await AddRecordedFramesAsync(files);
			SetStatus($"Status: recording added {files.Length:N0} frames");
		}
		catch (Exception ex)
		{
			SetStatus("Status: recording failed " + Short(ex.Message));
			if (addFrames)
			{
				System.Windows.MessageBox.Show(ex.Message, UiLanguage.T("Recording failed"));
			}
		}
		finally
		{
			session?.Dispose();
		}
	}

	private void SetRecordingUi(bool recording)
	{
		RecordButton.Content = UiLanguage.T(recording ? "■ Stop" : "● Full Record");
		RecordButton.Foreground = (recording ? System.Windows.Media.Brushes.Black : new SolidColorBrush(System.Windows.Media.Color.FromRgb(225, 29, 72)));
		RecordButton.Background = (recording ? new SolidColorBrush(System.Windows.Media.Color.FromRgb(byte.MaxValue, 232, 232)) : System.Windows.SystemColors.ControlBrush);
	}

	private int EstimateFrameCount(double start, double end)
	{
		double num = Math.Max(0.001, end - start);
		return Math.Max(1, (int)Math.Ceiling(num * Math.Max(_fps, 1.0)));
	}

	private string[] ExtractOriginalFrames(string video, string outDir, double start, double end, int expectedFrames, Action<int> progress, CancellationToken token)
	{
		double num = Math.Max(0.001, end - start);
		string value = System.IO.Path.Combine(outDir, "frame_%06d.png");
		RunFfmpegWithProgress($"-ss {Q(start)} -t {Q(num)} -i {Q(video)} -map 0:v:0 -fps_mode passthrough {Q(value)}", expectedFrames, progress, token);
		string[] array = (from x in Directory.GetFiles(outDir, "frame_*.png")
			orderby x
			select x).ToArray();
		token.ThrowIfCancellationRequested();
		if (array.Length == 0)
		{
			throw new Exception("no frames extracted");
		}
		return array;
	}

	private async Task AddFramesAsync(string[] files)
	{
		int first = _frames.Count;
		int duration = (int)Math.Round(1000.0 / Math.Max(_fps, 1.0));
		SetStatus($"Status: building frame list 0/{files.Length:N0}");
		for (int i = 0; i < files.Length; i++)
		{
			_frames.Add(new FrameItem
			{
				Index = _frames.Count + 1,
				Path = files[i],
				Time = _start + (double)i / Math.Max(_fps, 1.0),
				Duration = duration
			});
			if (i % 150 == 0)
			{
				SetStatus($"Status: building frame list {i:N0}/{files.Length:N0}");
				await Dispatcher.Yield(DispatcherPriority.Background);
			}
		}
		if (_frames.Count > first)
		{
			FrameList.SelectedIndex = first;
		}
		SetStatus($"Status: loading thumbnails 0/{files.Length:N0}");
		LoadThumbnailsAsync(first, files.Length);
	}

	private async Task AddRecordedFramesAsync(string[] files)
	{
		int first = _frames.Count;
		int duration = (int)Math.Round(1000.0 / (double)_recordFps);
		SetStatus($"Status: building frame list 0/{files.Length:N0}");
		for (int i = 0; i < files.Length; i++)
		{
			_frames.Add(new FrameItem
			{
				Index = _frames.Count + 1,
				Path = files[i],
				Time = (double)i / (double)_recordFps,
				Duration = duration
			});
			if (i % 150 == 0)
			{
				SetStatus($"Status: building frame list {i:N0}/{files.Length:N0}");
				await Dispatcher.Yield(DispatcherPriority.Background);
			}
		}
		_fps = _recordFps;
		if (_frames.Count > first)
		{
			FrameList.SelectedIndex = first;
		}
		SetStatus($"Status: loading thumbnails 0/{files.Length:N0}");
		LoadThumbnailsAsync(first, files.Length);
	}

	private async Task LoadThumbnailsAsync(int startIndex, int count)
	{
		for (int i = 0; i < count; i++)
		{
			int index = startIndex + i;
			if (index < 0 || index >= _frames.Count)
			{
				break;
			}
			FrameItem frame = _frames[index];
			try
			{
				string path = frame.Path;
				BitmapImage thumbnail = await Task.Run(() => LoadBitmap(path, 90));
				if (index < _frames.Count && _frames[index] == frame)
				{
					frame.Thumbnail = thumbnail;
				}
			}
			catch
			{
			}
			if (i % 100 == 0)
			{
				SetStatus($"Status: loading thumbnails {i:N0}/{count:N0}");
				await Dispatcher.Yield(DispatcherPriority.Background);
			}
		}
	}

	private void FrameList_SelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if (!_suppressFramePreview)
		{
			RefreshResultPreview();
		}
	}

	private void OutputSettings_Changed(object sender, SelectionChangedEventArgs e)
	{
		RefreshResultPreview();
	}

	private async void RefreshResultPreview()
	{
		object selectedItem = FrameList.SelectedItem;
		FrameItem item = selectedItem as FrameItem;
		if (item == null)
		{
			return;
		}
		int previewSerial = Interlocked.Increment(ref _subtitlePreviewSerial);
		string path = item.Path;
		try
		{
			if (SubtitleRemovalActive)
			{
				Rect? subtitleRect = _subtitleRect;
				if (subtitleRect.HasValue)
				{
					Rect subtitleRect2 = subtitleRect.GetValueOrDefault();
					string referencePath = _subtitleReferencePath;
					if (referencePath != null)
					{
						await _subtitlePreviewGate.WaitAsync();
						try
						{
							if (previewSerial != _subtitlePreviewSerial)
							{
								return;
							}
							string text = System.IO.Path.Combine(_tempRoot, "subtitle_preview");
							Directory.CreateDirectory(text);
							uint hashCode = (uint)StringComparer.OrdinalIgnoreCase.GetHashCode(item.Path);
							string restoredPath = System.IO.Path.Combine(text, $"r{_subtitleRevision:0000}_{hashCode:x8}.png");
							if (!File.Exists(restoredPath))
							{
								SetStatus($"Status: restoring subtitle preview #{item.Index}");
								await Task.Run(delegate
								{
									SubtitleRemovalProcessor.ProcessFile(item.Path, referencePath, subtitleRect2, restoredPath);
								});
							}
							path = restoredPath;
						}
						finally
						{
							_subtitlePreviewGate.Release();
						}
						if (previewSerial != _subtitlePreviewSerial)
						{
							return;
						}
					}
				}
			}
			ResultImage.Source = RenderOutputFrame(path);
			if (SubtitleRemovalActive)
			{
				SetStatus("Status: image patch preview ready");
			}
		}
		catch (Exception ex)
		{
			if (previewSerial == _subtitlePreviewSerial)
			{
				SetStatus("Status: preview failed " + Short(ex.Message));
			}
		}
	}

	private void ToggleSelect_Click(object sender, RoutedEventArgs e)
	{
		if (_frames.Count == 0)
		{
			return;
		}
		bool flag = FrameList.SelectedItems.Count != _frames.Count;
		try
		{
			base.Cursor = System.Windows.Input.Cursors.Wait;
			_suppressFramePreview = true;
			if (flag)
			{
				FrameList.SelectAll();
			}
			else
			{
				FrameList.SelectedItems.Clear();
			}
		}
		finally
		{
			_suppressFramePreview = false;
			base.Cursor = null;
		}
		base.Dispatcher.BeginInvoke(new Action(RefreshResultPreview), DispatcherPriority.Background);
	}

	private void Duration_Click(object sender, RoutedEventArgs e)
	{
		List<FrameItem> list = FrameList.SelectedItems.Cast<FrameItem>().ToList();
		if (list.Count == 0 && FrameList.SelectedItem is FrameItem item)
		{
			list.Add(item);
		}
		if (list.Count == 0)
		{
			return;
		}
		int sourceDurationMs = (int)Math.Max(1.0, Math.Round(1000.0 / Math.Max(_fps, 1.0)));
		DurationDialog durationDialog = new DurationDialog(_fps, sourceDurationMs, list[0].Duration)
		{
			Owner = this
		};
		if (durationDialog.ShowDialog() != true)
		{
			return;
		}
		int durationMs = durationDialog.DurationMs;
		try
		{
			base.Cursor = System.Windows.Input.Cursors.Wait;
			_suppressFramePreview = true;
			foreach (FrameItem item2 in list)
			{
				item2.Duration = durationMs;
			}
		}
		finally
		{
			_suppressFramePreview = false;
			base.Cursor = null;
		}
		Renumber();
	}

	private void DeleteSelected_Click(object sender, RoutedEventArgs e)
	{
		List<FrameItem> list = FrameList.SelectedItems.Cast<FrameItem>().ToList();
		bool flag = list.Any((FrameItem f) => string.Equals(f.Path, _subtitleReferencePath, StringComparison.OrdinalIgnoreCase));
		foreach (FrameItem item in list)
		{
			_frames.Remove(item);
			try
			{
				if (File.Exists(item.Path))
				{
					File.Delete(item.Path);
				}
			}
			catch
			{
			}
		}
		if (flag)
		{
			_subtitleReferencePath = null;
			_subtitleRevision++;
			UpdateSubtitleUi();
		}
		Renumber();
	}

	private void ClearFrames_Click(object sender, RoutedEventArgs e)
	{
		_frames.Clear();
		ResultImage.Source = null;
		ResetSubtitleState();
		ClearTempRoot(deleteRoot: false);
		SetStatus("Status: idle");
	}

	private void ClearTempRoot(bool deleteRoot)
	{
		try
		{
			if (Directory.Exists(_tempRoot))
			{
				Directory.Delete(_tempRoot, recursive: true);
			}
			if (!deleteRoot)
			{
				Directory.CreateDirectory(_tempRoot);
			}
		}
		catch
		{
		}
	}

	private static void CleanupOldTempSessions()
	{
		try
		{
			if (!Directory.Exists(TempBase))
			{
				return;
			}
			DateTime dateTime = DateTime.UtcNow.AddHours(-12.0);
			string[] directories = Directory.GetDirectories(TempBase, "session_*");
			for (int i = 0; i < directories.Length; i++)
			{
				DirectoryInfo directoryInfo = new DirectoryInfo(directories[i]);
				if (directoryInfo.LastWriteTimeUtc < dateTime)
				{
					directoryInfo.Delete(recursive: true);
				}
			}
		}
		catch
		{
		}
	}

	private void Renumber()
	{
		for (int i = 0; i < _frames.Count; i++)
		{
			_frames[i].Index = i + 1;
		}
		FrameList.Items.Refresh();
		UpdateSubtitleUi();
	}

	private void PlaybackMode_Click(object sender, RoutedEventArgs e)
	{
		ToggleButton toggleButton = (ToggleButton)sender;
		_playbackMode = ((toggleButton.IsChecked == true) ? ((toggleButton == ReverseToggle) ? PlaybackMode.Reverse : PlaybackMode.ReversePlus) : PlaybackMode.Forward);
		ReverseToggle.IsChecked = _playbackMode == PlaybackMode.Reverse;
		ReversePlusToggle.IsChecked = _playbackMode == PlaybackMode.ReversePlus;
		_previewIndex = 0;
		if (_frames.Count > 0)
		{
			TickPreview();
		}
		SetStatus("Status: playback order " + ((_playbackMode == PlaybackMode.ReversePlus) ? (UiLanguage.Korean ? "왕복" : "Ping-Pong") : (_playbackMode == PlaybackMode.Reverse && UiLanguage.Korean ? "역방향" : _playbackMode.ToString())));
	}

	private void PreviewPlay_Click(object sender, RoutedEventArgs e)
	{
		if (_previewTimer.IsEnabled)
		{
			_previewTimer.Stop();
		}
		else if (_frames.Count != 0)
		{
			_previewIndex = 0;
			TickPreview();
			_previewTimer.Start();
		}
	}

	private void TickPreview()
	{
		int[] array = PlaybackOrder.Build(_frames.Count, _playbackMode);
		if (array.Length == 0)
		{
			_previewTimer.Stop();
			return;
		}
		_previewIndex %= array.Length;
		int num = array[_previewIndex];
		FrameList.SelectedIndex = num;
		FrameList.ScrollIntoView(_frames[num]);
		_previewTimer.Interval = TimeSpan.FromMilliseconds(Math.Max(20, _frames[num].Duration));
		_previewIndex = (_previewIndex + 1) % array.Length;
	}

	private void SaveZip_Click(object sender, RoutedEventArgs e)
	{
		if (_frames.Count == 0)
		{
			return;
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "ZIP|*.zip",
			FileName = "frames.zip"
		};
		if (saveFileDialog.ShowDialog() != true)
		{
			return;
		}
		using ZipArchive destination = ZipFile.Open(saveFileDialog.FileName, ZipArchiveMode.Create);
		int[] array = PlaybackOrder.Build(_frames.Count, _playbackMode);
		for (int i = 0; i < array.Length; i++)
		{
			destination.CreateEntryFromFile(_frames[array[i]].Path, $"frame_{i + 1:000000}.png");
		}
		SetStatus("Status: ZIP saved");
	}

	private void SaveGif_Click(object sender, RoutedEventArgs e)
	{
		SaveAnimation("GIF|*.gif", "output.gif", ExportFormat.Gif);
	}

	private void SaveWebp_Click(object sender, RoutedEventArgs e)
	{
		SaveAnimation("WebP|*.webp", "output.webp", ExportFormat.Webp);
	}

	private void SaveMp4_Click(object sender, RoutedEventArgs e)
	{
		SaveAnimation("MP4|*.mp4", "output.mp4", ExportFormat.Mp4);
	}

	private async void SaveAnimation(string filter, string name, ExportFormat format)
	{
		if (_frames.Count == 0)
		{
			return;
		}
		Microsoft.Win32.SaveFileDialog dialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = filter,
			FileName = name
		};
		if (dialog.ShowDialog() != true)
		{
			return;
		}
		ExportPlan plan = CreateExportPlan();
		SetStatus("Status: preparing export");
		SetProgress(0, plan.FrameOrder.Length, "0/" + plan.FrameOrder.Length.ToString("N0"));
		try
		{
			await Task.Run(delegate
			{
				EncodeAnimation(dialog.FileName, format, plan);
			});
			SetProgress(_frames.Count, _frames.Count, "done");
			SetStatus("Status: saved");
		}
		catch (Exception ex)
		{
			try
			{
				if (File.Exists(dialog.FileName) && new FileInfo(dialog.FileName).Length == 0L)
				{
					File.Delete(dialog.FileName);
				}
			}
			catch
			{
			}
			SetStatus("Status: save failed " + Short(ex.Message));
		}
	}

	private ExportPlan CreateExportPlan()
	{
		BitmapSource bitmapSource = LoadBitmapSource(_frames.First().Path);
		Int32Rect cropRect = GetCropRect(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
		(int, int) outputSize = GetOutputSize(cropRect.Width, cropRect.Height);
		Rect? subtitleRect = (SubtitleRemovalActive ? _subtitleRect : ((Rect?)null));
		string subtitleReferencePath = (SubtitleRemovalActive ? _subtitleReferencePath : null);
		return new ExportPlan(cropRect.X, cropRect.Y, cropRect.Width, cropRect.Height, outputSize.Item1, outputSize.Item2, _overlays.Count > 0, subtitleRect, subtitleReferencePath, LoopValue(), QualityValue(), PlaybackOrder.Build(_frames.Count, _playbackMode));
	}

	private void EncodeAnimation(string output, ExportFormat format, ExportPlan plan)
	{
		string text = System.IO.Path.Combine(_tempRoot, "render_" + DateTime.Now.Ticks);
		Directory.CreateDirectory(text);
		string text2 = System.IO.Path.Combine(text, "concat.txt");
		string[] array = new string[_frames.Count];
		if (plan.HasOverlays || plan.HasSubtitleRemoval)
		{
			SetStatus(plan.HasSubtitleRemoval ? "Status: applying image patch" : "Status: rendering frames");
			for (int i = 0; i < _frames.Count; i++)
			{
				string text3 = _frames[i].Path;
				if (plan.HasSubtitleRemoval)
				{
					string text4 = System.IO.Path.Combine(text, $"restored_{i + 1:000000}.png");
					SubtitleRemovalProcessor.ProcessFile(text3, plan.SubtitleReferencePath, plan.SubtitleRect.Value, text4);
					text3 = text4;
				}
				if (plan.HasOverlays)
				{
					array[i] = System.IO.Path.Combine(text, $"render_{i + 1:000000}.png");
					SaveBitmap(RenderOutputFrame(text3), array[i]);
				}
				else
				{
					array[i] = text3;
				}
				if (i % 10 == 0 || i == _frames.Count - 1)
				{
					string value = (plan.HasSubtitleRemoval ? "restoring" : "rendering");
					SetStatus($"Status: {value} {i + 1:N0}/{_frames.Count:N0} frames");
					SetProgress(i + 1, _frames.Count, "");
				}
			}
		}
		else
		{
			for (int j = 0; j < _frames.Count; j++)
			{
				array[j] = _frames[j].Path;
			}
		}
		using (StreamWriter streamWriter = new StreamWriter(text2, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
		{
			int[] frameOrder = plan.FrameOrder;
			foreach (int num in frameOrder)
			{
				streamWriter.WriteLine("file '" + array[num].Replace("\\", "/").Replace("'", "'\\''") + "'");
				streamWriter.WriteLine("option framerate 1000");
				streamWriter.WriteLine($"duration {(double)Math.Max(_frames[num].Duration, 1) / 1000.0:0.000000}");
			}
			streamWriter.WriteLine("file '" + array[plan.FrameOrder[^1]].Replace("\\", "/").Replace("'", "'\\''") + "'");
		}
		SetStatus("Status: encoding 0/" + plan.FrameOrder.Length.ToString("N0") + " frames");
		SetProgress(0, plan.FrameOrder.Length, "0/" + plan.FrameOrder.Length.ToString("N0"));
		string text5 = System.IO.Path.Combine(text, "encoded" + System.IO.Path.GetExtension(output));
		int scalePercent = _imageScalePercent;
		string scaledCrop = scalePercent == 100
			? $"crop={plan.CropWidth}:{plan.CropHeight}:{plan.CropX}:{plan.CropY}"
			: $"scale=ceil(iw*{scalePercent}/100):ceil(ih*{scalePercent}/100):flags=lanczos,crop={Math.Max(1, (int)Math.Ceiling(plan.CropWidth * scalePercent / 100.0))}:{Math.Max(1, (int)Math.Ceiling(plan.CropHeight * scalePercent / 100.0))}:{(int)Math.Round(plan.CropX * scalePercent / 100.0)}:{(int)Math.Round(plan.CropY * scalePercent / 100.0)}";
		string text6 = (plan.HasOverlays ? $"scale={plan.OutputWidth}:{plan.OutputHeight}:flags=lanczos" : $"{scaledCrop},scale={plan.OutputWidth}:{plan.OutputHeight}:flags=lanczos");
		string args;
		if (format == ExportFormat.Mp4)
		{
			string value2 = text6 + ",pad=ceil(iw/2)*2:ceil(ih/2)*2,setsar=1";
			args = $"-f concat -safe 0 -i {Q(text2)} -vf {Q(value2)} " + Mp4Export.Options(plan.Quality, _frames[plan.FrameOrder[^1]].Duration, plan.FrameOrder.Length) + " " + Q(text5);
		}
		else
		{
			args = ((format == ExportFormat.Gif) ? $"-f concat -safe 0 -i {Q(text2)} -filter_complex {Q(text6 + ",split[s0][s1];[s0]palettegen=stats_mode=diff[p];[s1][p]paletteuse=dither=sierra2_4a")} -fps_mode vfr -frames:v {plan.FrameOrder.Length} -final_delay {Math.Max(1, (int)Math.Round((double)_frames[plan.FrameOrder[^1]].Duration / 10.0))} -loop {plan.Loop} {Q(text5)}" : $"-f concat -safe 0 -i {Q(text2)} -vf {Q(text6)} -fps_mode vfr -frames:v {plan.FrameOrder.Length} -an -c:v libwebp_anim -lossless 0 -quality {plan.Quality} -loop {plan.Loop} {Q(text5)}");
		}
		RunFfmpegWithProgress(args, plan.FrameOrder.Length, delegate(int current)
		{
			SetStatus($"Status: encoding {current:N0}/{plan.FrameOrder.Length:N0} frames");
			SetProgress(current, plan.FrameOrder.Length, "");
		});
		if (!File.Exists(text5) || new FileInfo(text5).Length == 0L)
		{
			throw new Exception("encoded file is empty");
		}
		if (format == ExportFormat.Webp)
		{
			PlaybackOrder.SetFinalWebpDuration(text5, _frames[plan.FrameOrder[^1]].Duration);
		}
		File.Copy(text5, output, overwrite: true);
	}

	private void Crop_Click(object sender, RoutedEventArgs e)
	{
		FrameItem frameItem = (FrameList.SelectedItem as FrameItem) ?? _frames.FirstOrDefault();
		if (frameItem != null)
		{
			CropWindow cropWindow = new CropWindow(frameItem.Path, _cropRect)
			{
				Owner = this
			};
			if (cropWindow.ShowDialog() == true)
			{
				_cropRect = cropWindow.CropRect;
				RefreshResultPreview();
			}
		}
	}

	private void ResetCrop_Click(object sender, RoutedEventArgs e)
	{
		_cropRect = null;
		RefreshResultPreview();
		SetStatus("Status: crop reset");
	}

	private void SubtitleArea_Click(object sender, RoutedEventArgs e)
	{
		FrameItem frameItem = (FrameList.SelectedItem as FrameItem) ?? _frames.FirstOrDefault();
		if (frameItem == null)
		{
			SetStatus("Status: extract frames before selecting a patch area");
			return;
		}
		try
		{
			ImagePatchWindow imagePatchWindow = new ImagePatchWindow(frameItem.Path, System.IO.Path.Combine(_tempRoot, "image_patches"), _subtitleReferencePath)
			{
				Owner = this
			};
			if (imagePatchWindow.ShowDialog() == true)
			{
				_subtitleRect = imagePatchWindow.Area;
				_subtitleReferencePath = imagePatchWindow.ReferencePath;
				_subtitleRevision++;
				UpdateSubtitleUi();
				RefreshResultPreview();
			}
		}
		catch (Exception ex)
		{
			System.Windows.MessageBox.Show(this, ex.Message, "Image patch");
		}
	}

	private void SubtitleReference_Click(object sender, RoutedEventArgs e)
	{
		FrameItem frameItem = (FrameList.SelectedItem as FrameItem) ?? _frames.FirstOrDefault();
		if (frameItem == null)
		{
			SetStatus("Status: extract frames before setting a subtitle reference");
			return;
		}
		_subtitleReferencePath = frameItem.Path;
		_subtitleRevision++;
		UpdateSubtitleUi();
		RefreshResultPreview();
		SetStatus($"Status: clean subtitle reference set to frame #{frameItem.Index}");
	}

	private void ResetSubtitle_Click(object sender, RoutedEventArgs e)
	{
		ResetSubtitleState();
		RefreshResultPreview();
		SetStatus("Status: subtitle removal reset");
	}

	private void ResetSubtitleState()
	{
		_subtitleRect = null;
		_subtitleReferencePath = null;
		_subtitleRevision++;
		Interlocked.Increment(ref _subtitlePreviewSerial);
		UpdateSubtitleUi();
	}

	private void UpdateSubtitleUi()
	{
		if (SubtitleRemovalText != null)
		{
			_frames.FirstOrDefault((FrameItem f) => string.Equals(f.Path, _subtitleReferencePath, StringComparison.OrdinalIgnoreCase));
			if (SubtitleRemovalActive)
			{
				SubtitleRemovalText.Text = UiLanguage.T("Image patch: on");
				SubtitleRemovalText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(26, 127, 86));
			}
			else if (_subtitleRect.HasValue)
			{
				SubtitleRemovalText.Text = UiLanguage.T("Subtitle removal: choose a clean Ref");
				SubtitleRemovalText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(183, 94, 0));
			}
			else
			{
				SubtitleRemovalText.Text = UiLanguage.T("Image patch: off");
				SubtitleRemovalText.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(111, 123, 139));
			}
		}
	}

	private void Insert_Click(object sender, RoutedEventArgs e)
	{
		FrameItem frameItem = (FrameList.SelectedItem as FrameItem) ?? _frames.FirstOrDefault();
		if (frameItem == null)
		{
			return;
		}
		OverlayWindow overlayWindow = new OverlayWindow(RenderOutputFrame(frameItem.Path), _overlays)
		{
			Owner = this
		};
		if (overlayWindow.ShowDialog() != true)
		{
			return;
		}
		_overlays.Clear();
		foreach (OverlaySpec overlay in overlayWindow.Overlays)
		{
			_overlays.Add(overlay);
		}
		RefreshResultPreview();
	}

	private void ResetInsert_Click(object sender, RoutedEventArgs e)
	{
		_overlays.Clear();
		RefreshResultPreview();
		SetStatus("Status: insert overlays reset");
	}

	private BitmapSource RenderOutputFrame(string path)
	{
		if (!base.Dispatcher.CheckAccess())
		{
			return base.Dispatcher.Invoke(() => RenderOutputFrame(path));
		}
		BitmapSource bitmapSource = LoadBitmapSource(path);
		Int32Rect cropRect = GetCropRect(bitmapSource.PixelWidth, bitmapSource.PixelHeight);
		CroppedBitmap imageSource = new CroppedBitmap(bitmapSource, cropRect);
		(int, int) outputSize = GetOutputSize(cropRect.Width, cropRect.Height);
		DrawingVisual drawingVisual = new DrawingVisual();
		using (DrawingContext drawingContext = drawingVisual.RenderOpen())
		{
			drawingContext.DrawImage(imageSource, new Rect(0.0, 0.0, outputSize.Item1, outputSize.Item2));
			foreach (OverlaySpec overlay in _overlays)
			{
				Rect rectangle = new Rect(overlay.X * (double)outputSize.Item1, overlay.Y * (double)outputSize.Item2, overlay.Width * (double)outputSize.Item1, overlay.Height * (double)outputSize.Item2);
				drawingContext.PushOpacity(Math.Clamp(overlay.Opacity, 0.0, 1.0));
				if (overlay.Kind == "image" && File.Exists(overlay.Path))
				{
					drawingContext.DrawImage(LoadBitmapSource(overlay.Path), rectangle);
				}
				else if (overlay.Kind == "text")
				{
					System.Windows.Media.Color color = (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(overlay.Color);
					FormattedText formattedText = new FormattedText(overlay.Text, CultureInfo.CurrentCulture, System.Windows.FlowDirection.LeftToRight, new Typeface(new System.Windows.Media.FontFamily(overlay.Font), overlay.Italic ? FontStyles.Italic : FontStyles.Normal, overlay.Bold ? FontWeights.Bold : FontWeights.Normal, FontStretches.Normal), Math.Clamp(overlay.FontSize, 8.0, Math.Max(8.0, rectangle.Height)), new SolidColorBrush(color), VisualTreeHelper.GetDpi(this).PixelsPerDip);
					TextDecorationCollection textDecorationCollection = new TextDecorationCollection();
					if (overlay.Underline)
					{
						textDecorationCollection.Add(TextDecorations.Underline[0]);
					}
					if (overlay.Strike)
					{
						textDecorationCollection.Add(TextDecorations.Strikethrough[0]);
					}
					if (textDecorationCollection.Count > 0)
					{
						formattedText.SetTextDecorations(textDecorationCollection);
					}
					formattedText.MaxTextWidth = Math.Max(1.0, rectangle.Width);
					formattedText.MaxTextHeight = Math.Max(1.0, rectangle.Height);
					drawingContext.DrawText(formattedText, rectangle.TopLeft);
				}
				drawingContext.Pop();
			}
		}
		RenderTargetBitmap renderTargetBitmap = new RenderTargetBitmap(outputSize.Item1, outputSize.Item2, 96.0, 96.0, PixelFormats.Pbgra32);
		renderTargetBitmap.Render(drawingVisual);
		renderTargetBitmap.Freeze();
		return renderTargetBitmap;
	}

	private Int32Rect GetCropRect(int sourceWidth, int sourceHeight)
	{
		Rect rect = _cropRect ?? new Rect(0.0, 0.0, sourceWidth, sourceHeight);
		rect.Intersect(new Rect(0.0, 0.0, sourceWidth, sourceHeight));
		if (rect.Width < 1.0 || rect.Height < 1.0)
		{
			rect = new Rect(0.0, 0.0, sourceWidth, sourceHeight);
		}
		return new Int32Rect(Math.Max(0, (int)Math.Round(rect.X)), Math.Max(0, (int)Math.Round(rect.Y)), Math.Max(1, Math.Min(sourceWidth, (int)Math.Round(rect.Width))), Math.Max(1, Math.Min(sourceHeight, (int)Math.Round(rect.Height))));
	}

	private (int Width, int Height) GetOutputSize(int cropWidth, int cropHeight)
	{
		if (!int.TryParse(((ComboBoxItem)ResolutionBox.SelectedItem)?.Content?.ToString() ?? "Source", out var result))
		{
			return (Width: Math.Max(1, (int)Math.Round(cropWidth * _imageScalePercent / 100.0)), Height: Math.Max(1, (int)Math.Round(cropHeight * _imageScalePercent / 100.0)));
		}
		double num = (double)cropWidth / (double)Math.Max(1, cropHeight);
		if (!(num >= 1.0))
		{
			return (Width: Math.Max(1, (int)Math.Round(result * num * _imageScalePercent / 100.0)), Height: Math.Max(1, (int)Math.Round(result * _imageScalePercent / 100.0)));
		}
		return (Width: Math.Max(1, (int)Math.Round(result * _imageScalePercent / 100.0)), Height: Math.Max(1, (int)Math.Round(result / num * _imageScalePercent / 100.0)));
	}

	private static BitmapSource LoadBitmapSource(string path)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.UriSource = new Uri(path);
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}

	private static void SaveBitmap(BitmapSource source, string path)
	{
		using FileStream stream = File.Create(path);
		PngBitmapEncoder pngBitmapEncoder = new PngBitmapEncoder();
		pngBitmapEncoder.Frames.Add(BitmapFrame.Create(source));
		pngBitmapEncoder.Save(stream);
	}

	private void QualitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (QualityText != null)
		{
			QualityText.Text = ((int)e.NewValue).ToString();
		}
	}

	private int LoopValue()
	{
		if (!int.TryParse(LoopText.Text, out var result))
		{
			return 0;
		}
		return Math.Max(0, result);
	}

	private int QualityValue()
	{
		if (!int.TryParse(QualityText.Text, out var result))
		{
			return 100;
		}
		return Math.Clamp(result, 1, 100);
	}

	private static (double duration, double fps, int width, int height) Probe(string path)
	{
		string input = RunFfmpegCapture("-i " + Q(path), allowFail: true);
		double item = 0.0;
		Match match = Regex.Match(input, "Duration:\\s*(\\d+):(\\d+):(\\d+(?:\\.\\d+)?)");
		if (match.Success)
		{
			item = (double)(int.Parse(match.Groups[1].Value) * 3600 + int.Parse(match.Groups[2].Value) * 60) + double.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture);
		}
		double item2 = 0.0;
		Match match2 = Regex.Match(input, "(\\d+(?:\\.\\d+)?)\\s*fps");
		if (match2.Success)
		{
			item2 = double.Parse(match2.Groups[1].Value, CultureInfo.InvariantCulture);
		}
		Match match3 = Regex.Match(input, "(\\d{3,5})x(\\d{3,5})");
		int item3 = (match3.Success ? int.Parse(match3.Groups[1].Value) : 0);
		int item4 = (match3.Success ? int.Parse(match3.Groups[2].Value) : 0);
		return (duration: item, fps: item2, width: item3, height: item4);
	}

	private static string Q(object value)
	{
		return "\"" + Convert.ToString(value, CultureInfo.InvariantCulture).Replace("\"", "\\\"") + "\"";
	}

	private static void RunFfmpeg(string args)
	{
		RunFfmpegCapture(args);
	}

	private static void RunFfmpegWithProgress(string args, int totalFrames, Action<int> progress, CancellationToken token = default(CancellationToken))
	{
		ProcessStartInfo startInfo = new ProcessStartInfo(FfmpegPath, "-hide_banner -y -nostats -progress pipe:2 " + args)
		{
			CreateNoWindow = true,
			UseShellExecute = false,
			RedirectStandardError = true,
			RedirectStandardOutput = true
		};
		Process p = Process.Start(startInfo);
		try
		{
			StringBuilder output = new StringBuilder();
			int num = 0;
			int num2 = 0;
			p.OutputDataReceived += delegate(object _, DataReceivedEventArgs e)
			{
				if (e.Data != null)
				{
					output.AppendLine(e.Data);
				}
			};
			p.BeginOutputReadLine();
			using (token.Register(delegate
			{
				try
				{
					if (!p.HasExited)
					{
						p.Kill(entireProcessTree: true);
					}
				}
				catch
				{
				}
			}))
			{
				string text;
				while ((text = p.StandardError.ReadLine()) != null)
				{
					token.ThrowIfCancellationRequested();
					output.AppendLine(text);
					Match match = Regex.Match(text, "^frame=(\\d+)");
					if (!match.Success)
					{
						continue;
					}
					int num3 = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
					if (num3 > num)
					{
						num = num3;
						if (num3 - num2 >= 10 || num3 >= totalFrames)
						{
							num2 = num3;
							progress(Math.Min(num3, Math.Max(totalFrames, num3)));
						}
					}
				}
				p.WaitForExit();
				token.ThrowIfCancellationRequested();
				if (p.ExitCode != 0)
				{
					throw new Exception(output.ToString());
				}
			}
		}
		finally
		{
			if (p != null)
			{
				((IDisposable)p).Dispose();
			}
		}
	}

	private static string RunFfmpegCapture(string args, bool allowFail = false)
	{
		using Process process = Process.Start(new ProcessStartInfo(FfmpegPath, "-hide_banner -y " + args)
		{
			CreateNoWindow = true,
			UseShellExecute = false,
			RedirectStandardError = true,
			RedirectStandardOutput = true
		});
		string text = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
		process.WaitForExit();
		if (!allowFail && process.ExitCode != 0)
		{
			throw new Exception(text);
		}
		return text;
	}

	private static BitmapImage LoadBitmap(string path, int decodeWidth)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.UriSource = new Uri(path);
		if (decodeWidth > 0)
		{
			bitmapImage.DecodePixelWidth = decodeWidth;
		}
		bitmapImage.EndInit();
		bitmapImage.Freeze();
		return bitmapImage;
	}

	private static string Short(string text)
	{
		if (text.Length > 180)
		{
			return text.Substring(text.Length - 180);
		}
		return text;
	}

	private static void ForceDeleteDirectory(string path)
	{
		if (!Directory.Exists(path))
		{
			return;
		}
		for (int i = 0; i < 8; i++)
		{
			try
			{
				string[] files = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
				foreach (string path2 in files)
				{
					try
					{
						File.SetAttributes(path2, FileAttributes.Normal);
					}
					catch
					{
					}
				}
				Directory.Delete(path, recursive: true);
				break;
			}
			catch
			{
				Thread.Sleep(120);
			}
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	public void InitializeComponent()
	{
		if (!_contentLoaded)
		{
			_contentLoaded = true;
			Uri resourceLocator = new Uri("/ClipForgeWpf;component/mainwindow.xaml", UriKind.Relative);
			System.Windows.Application.LoadComponent(this, resourceLocator);
		}
	}

	[DebuggerNonUserCode]
	[GeneratedCode("PresentationBuildTasks", "10.0.12.0")]
	[EditorBrowsable(EditorBrowsableState.Never)]
	void IComponentConnector.Connect(int connectionId, object target)
	{
		switch (connectionId)
		{
		case 1:
			((MainWindow)target).Drop += Window_Drop;
			((MainWindow)target).DragOver += Window_DragOver;
			((MainWindow)target).PreviewDrop += Window_PreviewDrop;
			((MainWindow)target).PreviewDragOver += Window_PreviewDragOver;
			((MainWindow)target).KeyDown += Window_KeyDown;
			break;
		case 2:
			ResponsiveHost = (Grid)target;
			break;
		case 3:
			ResponsiveView = (Viewbox)target;
			break;
		case 4:
			LayoutRoot = (Grid)target;
			break;
		case 5:
			ClearCacheButton = (System.Windows.Controls.Button)target;
			ClearCacheButton.Click += ClearCache_Click;
			break;
		case 6:
			LanguageButton = (System.Windows.Controls.Button)target;
			LanguageButton.Click += Language_Click;
			break;
		case 7:
			VideoHost = (Border)target;
			VideoHost.Drop += Video_Drop;
			VideoHost.DragOver += Window_DragOver;
			break;
		case 8:
			SourceStillImage = (System.Windows.Controls.Image)target;
			SourceStillImage.Drop += Video_Drop;
			SourceStillImage.DragOver += Window_DragOver;
			break;
		case 9:
			VideoView = (VideoView)target;
			break;
		case 10:
			EmptyVideoText = (TextBlock)target;
			break;
		case 11:
			((Border)target).Drop += Video_Drop;
			((Border)target).DragOver += Window_DragOver;
			break;
		case 12:
			VideoDropPopup = (Popup)target;
			break;
		case 13:
			((Border)target).Drop += Video_Drop;
			((Border)target).DragOver += Window_DragOver;
			break;
		case 14:
			MetaText = (TextBlock)target;
			break;
		case 15:
			StartText = (TextBlock)target;
			break;
		case 16:
			CurrentText = (TextBlock)target;
			break;
		case 17:
			EndText = (TextBlock)target;
			break;
		case 18:
			MuteButton = (System.Windows.Controls.Button)target;
			MuteButton.Click += Mute_Click;
			break;
		case 19:
			MuteSlash = (System.Windows.Shapes.Path)target;
			break;
		case 20:
			Timeline = (Slider)target;
			Timeline.ValueChanged += Timeline_ValueChanged;
			break;
		case 21:
			((System.Windows.Controls.Button)target).Click += Back5_Click;
			break;
		case 22:
			((System.Windows.Controls.Button)target).Click += Back1_Click;
			break;
		case 23:
			((System.Windows.Controls.Button)target).Click += PrevFrame_Click;
			break;
		case 24:
			((System.Windows.Controls.Button)target).Click += PlayPause_Click;
			break;
		case 25:
			((System.Windows.Controls.Button)target).Click += NextFrame_Click;
			break;
		case 26:
			((System.Windows.Controls.Button)target).Click += Forward1_Click;
			break;
		case 27:
			((System.Windows.Controls.Button)target).Click += Forward5_Click;
			break;
		case 28:
			SelectedStartText = (TextBlock)target;
			break;
		case 29:
			SelectedEndText = (TextBlock)target;
			break;
		case 30:
			((System.Windows.Controls.Button)target).Click += SetStart_Click;
			break;
		case 31:
			((System.Windows.Controls.Button)target).Click += SetEnd_Click;
			break;
		case 32:
			SegmentText = (TextBlock)target;
			break;
		case 33:
			((System.Windows.Controls.Button)target).Click += Extract_Click;
			break;
		case 34:
			((System.Windows.Controls.Button)target).Click += StopExtract_Click;
			break;
		case 35:
			StatusText = (TextBlock)target;
			break;
		case 36:
			ExtractProgress = (System.Windows.Controls.ProgressBar)target;
			break;
		case 37:
			ProgressText = (TextBlock)target;
			break;
		case 38:
			FrameList = (System.Windows.Controls.ListView)target;
			FrameList.SelectionChanged += FrameList_SelectionChanged;
			break;
		case 39:
			((System.Windows.Controls.Button)target).Click += ToggleSelect_Click;
			break;
		case 40:
			((System.Windows.Controls.Button)target).Click += Duration_Click;
			break;
		case 41:
			((System.Windows.Controls.Button)target).Click += DeleteSelected_Click;
			break;
		case 42:
			((System.Windows.Controls.Button)target).Click += ClearFrames_Click;
			break;
		case 43:
			ResultImage = (System.Windows.Controls.Image)target;
			break;
		case 44:
			((System.Windows.Controls.Button)target).Click += PreviewPlay_Click;
			break;
		case 45:
			((System.Windows.Controls.Button)target).Click += Crop_Click;
			break;
		case 46:
			((System.Windows.Controls.Button)target).Click += ResetCrop_Click;
			break;
		case 47:
			((System.Windows.Controls.Button)target).Click += SubtitleArea_Click;
			break;
		case 48:
			((System.Windows.Controls.Button)target).Click += ResetSubtitle_Click;
			break;
		case 49:
			SubtitleRemovalText = (TextBlock)target;
			break;
		case 50:
			ResolutionBox = (System.Windows.Controls.ComboBox)target;
			ResolutionBox.SelectionChanged += OutputSettings_Changed;
			break;
		case 51:
			LoopText = (System.Windows.Controls.TextBox)target;
			break;
		case 52:
			QualityText = (System.Windows.Controls.TextBox)target;
			break;
		case 53:
			QualitySlider = (Slider)target;
			QualitySlider.ValueChanged += QualitySlider_ValueChanged;
			break;
		case 54:
			((System.Windows.Controls.Button)target).Click += Insert_Click;
			break;
		case 55:
			((System.Windows.Controls.Button)target).Click += ResetInsert_Click;
			break;
		case 56:
			((System.Windows.Controls.Button)target).Click += RecordArea_Click;
			break;
		case 57:
			RecordButton = (System.Windows.Controls.Button)target;
			RecordButton.Click += RecordToggle_Click;
			break;
		case 58:
			ReverseToggle = (ToggleButton)target;
			ReverseToggle.Click += PlaybackMode_Click;
			break;
		case 59:
			ReversePlusToggle = (ToggleButton)target;
			ReversePlusToggle.Click += PlaybackMode_Click;
			break;
		case 60:
			_saveZipButton = (System.Windows.Controls.Button)target;
			_saveZipButton.Click += SaveZip_Click;
			break;
		case 61:
			((System.Windows.Controls.Button)target).Click += SaveGif_Click;
			break;
		case 62:
			((System.Windows.Controls.Button)target).Click += SaveWebp_Click;
			break;
		case 63:
			((System.Windows.Controls.Button)target).Click += SaveMp4_Click;
			break;
		default:
			_contentLoaded = true;
			break;
		}
	}
}
