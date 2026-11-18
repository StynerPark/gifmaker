using System;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Media.Imaging;

namespace ClipForgeWpf;

public class FrameItem : INotifyPropertyChanged
{
	private int _index;

	private int _duration;

	private BitmapImage? _thumbnail;

	public int Index
	{
		get
		{
			return _index;
		}
		set
		{
			_index = value;
			Changed("Index");
		}
	}

	public required string Path { get; set; }

	public double Time { get; set; }

	public int Duration
	{
		get
		{
			return _duration;
		}
		set
		{
			_duration = value;
			Changed("Duration");
			Changed("DurationText");
		}
	}

	public BitmapImage? Thumbnail
	{
		get
		{
			return _thumbnail;
		}
		set
		{
			_thumbnail = value;
			Changed("Thumbnail");
		}
	}

	public string TimeText => TimeSpan.FromSeconds(Time).ToString("mm\\:ss\\.fff", CultureInfo.InvariantCulture);

	public string DurationText => Duration + "ms";

	public event PropertyChangedEventHandler? PropertyChanged;

	private void Changed(string name)
	{
		this.PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
	}
}
