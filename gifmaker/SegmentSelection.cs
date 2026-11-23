using System;

namespace ClipForgeWpf;

public static class SegmentSelection
{
	public static (double Start, double End) SetStart(double position, double end, double duration)
	{
		duration = Math.Max(0.0, duration);
		double num = Math.Clamp(position, 0.0, duration);
		return (Start: num, End: (end < num) ? duration : Math.Clamp(end, 0.0, duration));
	}

	public static (double Start, double End) SetEnd(double position, double start, double duration)
	{
		double num = Math.Clamp(position, 0.0, Math.Max(0.0, duration));
		return (Start: (start > num) ? 0.0 : Math.Clamp(start, 0.0, Math.Max(0.0, duration)), End: num);
	}
}
