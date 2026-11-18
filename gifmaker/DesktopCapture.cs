using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;

namespace ClipForgeWpf;

public static class DesktopCapture
{
	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int EnumObject(nint self, uint index, out nint result);

	[UnmanagedFunctionPointer(CallingConvention.StdCall)]
	private delegate int GetDescription(nint self, nint description);

	[DllImport("dxgi.dll")]
	private static extern int CreateDXGIFactory1(ref Guid id, out nint factory);

	[DllImport("user32.dll")]
	private static extern int GetDisplayConfigBufferSizes(uint flags, out uint paths, out uint modes);

	[DllImport("user32.dll")]
	private static extern int QueryDisplayConfig(uint flags, ref uint paths, nint pathArray, ref uint modes, nint modeArray, nint topology);

	[DllImport("user32.dll")]
	private static extern int DisplayConfigGetDeviceInfo(nint packet);

	[DllImport("user32.dll")]
	private static extern bool SetWindowDisplayAffinity(nint hwnd, uint affinity);

	public static void ExcludeOverlay(nint hwnd)
	{
		SetWindowDisplayAffinity(hwnd, 17u);
	}

	private static T Method<T>(nint obj, int slot) where T : Delegate
	{
		return Marshal.GetDelegateForFunctionPointer<T>(Marshal.ReadIntPtr(Marshal.ReadIntPtr(obj), slot * IntPtr.Size));
	}

	public static IReadOnlyList<CaptureDisplay> Displays()
	{
		List<CaptureDisplay> list = new List<CaptureDisplay>();
		Guid id = new Guid("770aae78-f26f-4dba-a829-253c83d1b387");
		Marshal.ThrowExceptionForHR(CreateDXGIFactory1(ref id, out var factory));
		try
		{
			nint result;
			for (uint num = 0u; Method<EnumObject>(factory, 12)(factory, num, out result) >= 0; num++)
			{
				try
				{
					nint result2;
					for (uint num2 = 0u; Method<EnumObject>(result, 7)(result, num2, out result2) >= 0; num2++)
					{
						try
						{
							nint num3 = Marshal.AllocHGlobal(160);
							try
							{
								Marshal.ThrowExceptionForHR(Method<GetDescription>(result2, 7)(result2, num3));
								if (Marshal.ReadInt32(num3, 80) == 0)
								{
									continue;
								}
								string name = Marshal.PtrToStringUni(num3, 32).TrimEnd('\0');
								int num4 = Marshal.ReadInt32(num3, 64);
								int num5 = Marshal.ReadInt32(num3, 68);
								Rect bounds = new Rect(num4, num5, Marshal.ReadInt32(num3, 72) - num4, Marshal.ReadInt32(num3, 76) - num5);
								bool flag = false;
								double val = 1000.0;
								Guid iid = new Guid("068346e8-aaec-4b84-add7-137f513f77a1");
								if (Marshal.QueryInterface(result2, in iid, out var ppv) >= 0)
								{
									try
									{
										if (Method<GetDescription>(ppv, 27)(ppv, num3) >= 0)
										{
											flag = Marshal.ReadInt32(num3, 100) == 12;
											val = BitConverter.Int32BitsToSingle(Marshal.ReadInt32(num3, 140));
										}
									}
									finally
									{
										Marshal.Release(ppv);
									}
								}
								list.Add(new CaptureDisplay((int)num, (int)num2, name, bounds, flag, flag ? SdrWhite(name) : 80.0, Math.Max(80.0, val)));
							}
							finally
							{
								Marshal.FreeHGlobal(num3);
							}
						}
						finally
						{
							Marshal.Release(result2);
						}
					}
				}
				finally
				{
					Marshal.Release(result);
				}
			}
		}
		finally
		{
			Marshal.Release(factory);
		}
		if (list.Count == 0)
		{
			throw new InvalidOperationException(UiLanguage.T("No capturable display was found."));
		}
		return list;
	}

	private static double SdrWhite(string name)
	{
		if (GetDisplayConfigBufferSizes(2u, out var paths, out var modes) != 0)
		{
			return 203.0;
		}
		nint num = Marshal.AllocHGlobal((int)(paths * 72));
		nint num2 = Marshal.AllocHGlobal((int)(modes * 64));
		nint packet = Marshal.AllocHGlobal(84);
		try
		{
			if (QueryDisplayConfig(2u, ref paths, num, ref modes, num2, IntPtr.Zero) != 0)
			{
				return 203.0;
			}
			for (int i = 0; i < paths; i++)
			{
				nint path = IntPtr.Add(num, i * 72);
				Header(1, 84, 0, 8);
				if (DisplayConfigGetDeviceInfo(packet) == 0 && string.Equals(Marshal.PtrToStringUni(IntPtr.Add(packet, 20), 32).TrimEnd('\0'), name, StringComparison.OrdinalIgnoreCase))
				{
					Header(11, 24, 20, 28);
					if (DisplayConfigGetDeviceInfo(packet) == 0)
					{
						return Math.Clamp((double)Marshal.ReadInt32(packet, 20) * 0.08, 80.0, 1000.0);
					}
				}
				void Header(int type, int size, int sourceOffset, int idOffset)
				{
					for (int j = 0; j < 84; j++)
					{
						Marshal.WriteByte(packet, j, 0);
					}
					Marshal.WriteInt32(packet, 0, type);
					Marshal.WriteInt32(packet, 4, size);
					Marshal.WriteInt64(packet, 8, Marshal.ReadInt64(path, sourceOffset));
					Marshal.WriteInt32(packet, 16, Marshal.ReadInt32(path, idOffset));
				}
			}
		}
		finally
		{
			Marshal.FreeHGlobal(num);
			Marshal.FreeHGlobal(num2);
			Marshal.FreeHGlobal(packet);
		}
		return 203.0;
	}

	public static CapturePlan Arguments(Rect area, int fps, bool video, string destination, IReadOnlyList<CaptureDisplay> displays = null)
	{
		CapturePiece[] array = (from d in displays ?? Displays()
			select new CapturePiece(d, Rect.Intersect(d.Bounds, area)) into p
			where !p.Area.IsEmpty && p.Area.Width >= 2.0 && p.Area.Height >= 2.0
			select p).ToArray();
		if (array.Length == 0)
		{
			throw new InvalidOperationException(UiLanguage.T("Select an area inside a display."));
		}
		return new CapturePlan(area, fps, video, destination, array);
	}

	public static string RawArguments(CapturePiece piece, int fps, bool software = false)
	{
		CaptureDisplay display = piece.Display;
		Rect area = piece.Area;
		if (!software)
		{
			return $"-init_hw_device d3d11va=capture:{display.Adapter} -filter_hw_device capture -f lavfi -i \"ddagrab=output_idx={display.Output}:output_fmt=16bit:allow_fallback=0:draw_mouse=1:framerate={fps}:video_size={(int)area.Width}x{(int)area.Height}:offset_x={(int)(area.X - display.Bounds.X)}:offset_y={(int)(area.Y - display.Bounds.Y)}\" -vf \"hwdownload,format=rgbaf16le\" -an -c:v rawvideo -pix_fmt rgbaf16le -f rawvideo pipe:1";
		}
		return $"-f gdigrab -draw_mouse 1 -framerate {fps} -offset_x {(int)area.X} -offset_y {(int)area.Y} -video_size {(int)area.Width}x{(int)area.Height} -i desktop -an -c:v rawvideo -pix_fmt rgb24 -f rawvideo pipe:1";
	}
}
