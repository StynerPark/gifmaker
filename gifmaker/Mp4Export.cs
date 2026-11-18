using System;

namespace ClipForgeWpf;

public static class Mp4Export
{
	public static string Options(int quality, int lastFrameMilliseconds, int frameCount)
	{
		int value = (int)Math.Round(35.0 - (double)Math.Clamp(quality, 1, 100) * 0.17);
		return $"-an -c:v libx264 -preset medium -crf {value} -pix_fmt yuv420p -fps_mode vfr -enc_time_base 1:1000 -bf 0 -bsf:v \"setts=duration='if(eq(N,{frameCount - 1}),{Math.Max(1, lastFrameMilliseconds)},DURATION)'\" -frames:v {frameCount} -video_track_timescale 1000 -movflags +faststart";
	}
}
