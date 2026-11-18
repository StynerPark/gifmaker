using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace ClipForgeWpf;

public static class PlaybackOrder
{
	public static void SetFinalWebpDuration(string path, int milliseconds)
	{
		using FileStream fileStream = new FileStream(path, FileMode.Open, FileAccess.ReadWrite);
		using BinaryReader binaryReader = new BinaryReader(fileStream, Encoding.ASCII, leaveOpen: true);
		if (fileStream.Length < 12 || Encoding.ASCII.GetString(binaryReader.ReadBytes(4)) != "RIFF")
		{
			return;
		}
		fileStream.Position = 8L;
		if (Encoding.ASCII.GetString(binaryReader.ReadBytes(4)) != "WEBP")
		{
			return;
		}
		long num = -1L;
		while (fileStream.Position + 8 <= fileStream.Length)
		{
			string text = Encoding.ASCII.GetString(binaryReader.ReadBytes(4));
			uint num2 = binaryReader.ReadUInt32();
			long num3 = fileStream.Position + num2 + (num2 & 1);
			if (num3 > fileStream.Length)
			{
				throw new InvalidDataException("Invalid WebP chunk.");
			}
			if (text == "ANMF" && num2 >= 16)
			{
				num = fileStream.Position + 12;
			}
			fileStream.Position = num3;
		}
		if (num >= 0)
		{
			int num4 = Math.Clamp(milliseconds, 1, 16777215);
			fileStream.Position = num;
			fileStream.WriteByte((byte)num4);
			fileStream.WriteByte((byte)(num4 >> 8));
			fileStream.WriteByte((byte)(num4 >> 16));
		}
	}

	public static int[] Build(int count, PlaybackMode mode)
	{
		if (count < 0)
		{
			throw new ArgumentOutOfRangeException("count");
		}
		IEnumerable<int> enumerable = Enumerable.Range(0, count);
		return mode switch
		{
			PlaybackMode.Reverse => enumerable.Reverse().ToArray(),
			PlaybackMode.ReversePlus => enumerable.Concat(Enumerable.Range(0, Math.Max(0, count - 1)).Reverse()).ToArray(),
			_ => enumerable.ToArray(),
		};
	}
}
