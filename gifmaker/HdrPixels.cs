using System;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ClipForgeWpf;

public static class HdrPixels
{
	public static byte[] Lookup(CaptureDisplay display)
	{
		byte[] array = new byte[65536];
		double num = (display.Hdr ? Math.Clamp(display.SdrWhiteNits, 80.0, 1000.0) : 80.0);
		double num2 = (display.Hdr ? Math.Max(1.01, display.PeakNits / num) : 1.0);
		if (!double.IsFinite(num2))
		{
			num2 = 5.0;
		}
		double num3 = (num2 - 0.8) / 0.19999999999999996;
		double num4 = num3 / (1.0 + num3);
		for (int i = 0; i < array.Length; i++)
		{
			double num5 = (double)((float)BitConverter.UInt16BitsToHalf((ushort)i) * 80f) / num;
			if (!double.IsNaN(num5) && !(num5 <= 0.0))
			{
				if (display.Hdr && num5 > 0.8)
				{
					double num6 = (Math.Min(num5, num2) - 0.8) / 0.19999999999999996;
					num5 = 0.8 + 0.19999999999999996 * num6 / (1.0 + num6) / num4;
				}
				num5 = Math.Clamp(num5, 0.0, 1.0);
				double num7 = ((num5 <= 0.0031308) ? (num5 * 12.92) : (1.055 * Math.Pow(num5, 5.0 / 12.0) - 0.055));
				array[i] = (byte)Math.Clamp((int)Math.Round(num7 * 255.0), 0, 255);
			}
		}
		return array;
	}

	public static void Convert(byte[] fp16, int width, int height, byte[] rgb, int canvasWidth, int x, int y, byte[] lookup)
	{
		Parallel.For(0, height, delegate(int row)
		{
			Span<ushort> span = MemoryMarshal.Cast<byte, ushort>(fp16.AsSpan(row * width * 8, width * 8));
			int num = ((y + row) * canvasWidth + x) * 3;
			for (int i = 0; i < width; i++)
			{
				int num2 = i * 4;
				rgb[num++] = lookup[span[num2]];
				rgb[num++] = lookup[span[num2 + 1]];
				rgb[num++] = lookup[span[num2 + 2]];
			}
		});
	}
}
