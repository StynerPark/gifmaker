using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace ClipForgeWpf;

public sealed class DesktopRecordingSession : IDisposable
{
	private nint _job;

	private readonly Process _process;

	private readonly List<Process> _sources = new List<Process>();

	private readonly List<Task<string>> _sourceErrors = new List<Task<string>>();

	private readonly Task<string> _errors;

	private readonly Task _output;

	private readonly Task _pump;

	private readonly CapturePlan _plan;

	private readonly bool _software;

	private readonly string _ffmpeg;

	private readonly string _audioPath;

	private readonly SystemAudioRecorder _audio;

	private Exception _pumpFailure;

	private int _encodedFrames;

	public bool HasExited
	{
		get
		{
			if (!_process.HasExited)
			{
				return _pump.IsCompleted;
			}
			return true;
		}
	}

	[DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
	private static extern nint CreateJobObject(nint attributes, string name);

	[DllImport("kernel32.dll")]
	private static extern bool SetInformationJobObject(nint job, int type, nint info, uint length);

	[DllImport("kernel32.dll")]
	private static extern bool AssignProcessToJobObject(nint job, nint process);

	[DllImport("kernel32.dll")]
	private static extern bool CloseHandle(nint handle);

	private void Own(Process process)
	{
		if (!AssignProcessToJobObject(_job, process.Handle))
		{
			throw new IOException(UiLanguage.T("Could not attach the recording process to the application."));
		}
	}

	public DesktopRecordingSession(string ffmpeg, CapturePlan plan, Action<int> progress, bool software = false)
	{
		DesktopRecordingSession desktopRecordingSession = this;
		_plan = plan;
		_software = software;
		_ffmpeg = ffmpeg;
		_job = CreateJobObject(IntPtr.Zero, null);
		nint num = Marshal.AllocHGlobal(144);
		try
		{
			for (int i = 0; i < 144; i++)
			{
				Marshal.WriteByte(num, i, 0);
			}
			Marshal.WriteInt32(num, 16, 8192);
			if (_job == IntPtr.Zero || !SetInformationJobObject(_job, 9, num, 144u))
			{
				throw new IOException("Could not create capture job.");
			}
		}
		finally
		{
			Marshal.FreeHGlobal(num);
		}
		try
		{
			int value = (int)plan.Area.Width;
			int value2 = (int)plan.Area.Height;
			string value3 = $"-f rawvideo -pixel_format rgb24 -video_size {value}x{value2} -framerate {plan.Fps} -use_wallclock_as_timestamps 1 -i pipe:0";
			string value4 = (plan.Video ? "-vf \"pad=ceil(iw/2)*2:ceil(ih/2)*2,zscale=pin=bt709:tin=iec61966-2-1:min=gbr:rin=full:p=bt709:t=bt709:m=bt709:r=limited,format=yuv420p\" -an -c:v libx264 -preset veryfast -crf 18 -pix_fmt yuv420p -color_primaries bt709 -color_trc bt709 -colorspace bt709 -movflags +faststart" : "-c:v png -pix_fmt rgb24");
			_process = Process.Start(new ProcessStartInfo(ffmpeg, $"-hide_banner -y -nostats -progress pipe:1 {value3} {value4} -fps_mode cfr \"{plan.Destination}\"")
			{
				UseShellExecute = false,
				CreateNoWindow = true,
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true
			});
			Own(_process);
			_errors = _process.StandardError.ReadToEndAsync();
			_output = Task.Run(async delegate
			{
				string text;
				while ((text = await desktopRecordingSession._process.StandardOutput.ReadLineAsync()) != null)
				{
					if (text.StartsWith("frame=") && int.TryParse(text.Substring(6), out var result))
					{
						Interlocked.Exchange(ref desktopRecordingSession._encodedFrames, result);
						progress(result);
					}
				}
			});
			if (plan.Video)
			{
				_audioPath = Path.Combine(Path.GetDirectoryName(plan.Destination), "system-audio.wav");
				_audio = new SystemAudioRecorder(_audioPath);
			}
			CapturePiece[] pieces = plan.Pieces;
			foreach (CapturePiece piece in pieces)
			{
				Process process = Process.Start(new ProcessStartInfo(ffmpeg, "-hide_banner -nostats " + DesktopCapture.RawArguments(piece, plan.Fps, software))
				{
					UseShellExecute = false,
					CreateNoWindow = true,
					RedirectStandardInput = true,
					RedirectStandardOutput = true,
					RedirectStandardError = true
				});
				_sources.Add(process);
				Own(process);
				_sourceErrors.Add(process.StandardError.ReadToEndAsync());
			}
			_pump = Task.Run((Func<Task?>)Pump);
		}
		catch
		{
			Dispose();
			throw;
		}
	}

	private async Task Pump()
	{
		byte[][] frames = _plan.Pieces.Select((CapturePiece p) => new byte[checked((int)p.Area.Width * (int)p.Area.Height * (_software ? 3 : 8))]).ToArray();
		byte[][] maps = _plan.Pieces.Select((CapturePiece p) => HdrPixels.Lookup(p.Display)).ToArray();
		int width = (int)_plan.Area.Width;
		int num = (int)_plan.Area.Height;
		byte[] rgb = new byte[checked(width * num * 3)];
		bool first = true;
		try
		{
			while (true)
			{
				for (int i = 0; i < frames.Length; i++)
				{
					byte[] frame = frames[i];
					int num2;
					for (int count = 0; count < frame.Length; count += num2)
					{
						num2 = await _sources[i].StandardOutput.BaseStream.ReadAsync(frame.AsMemory(count));
						if (num2 == 0)
						{
							return;
						}
					}
					CapturePiece capturePiece = _plan.Pieces[i];
					if (_software)
					{
						int num3 = (int)capturePiece.Area.Width;
						int num4 = (int)capturePiece.Area.Height;
						int num5 = (int)(capturePiece.Area.X - _plan.Area.X);
						int num6 = (int)(capturePiece.Area.Y - _plan.Area.Y);
						for (int num7 = 0; num7 < num4; num7++)
						{
							Buffer.BlockCopy(frame, num7 * num3 * 3, rgb, ((num7 + num6) * width + num5) * 3, num3 * 3);
						}
					}
					else
					{
						HdrPixels.Convert(frame, (int)capturePiece.Area.Width, (int)capturePiece.Area.Height, rgb, width, (int)(capturePiece.Area.X - _plan.Area.X), (int)(capturePiece.Area.Y - _plan.Area.Y), maps[i]);
					}
				}
				if (first)
				{
					_audio?.Start();
					first = false;
				}
				await _process.StandardInput.BaseStream.WriteAsync(rgb);
			}
		}
		catch (Exception pumpFailure)
		{
			_pumpFailure = pumpFailure;
		}
		finally
		{
			try
			{
				_process.StandardInput.Close();
			}
			catch
			{
			}
		}
	}

	public async Task Finish(bool cancel)
	{
		foreach (Process source in _sources)
		{
			if (!source.HasExited)
			{
				try
				{
					await source.StandardInput.WriteLineAsync("q");
					await source.StandardInput.FlushAsync();
				}
				catch
				{
				}
			}
		}
		if (await Task.WhenAny(_pump, Task.Delay(15000)) != _pump)
		{
			foreach (Process source3 in _sources)
			{
				try
				{
					if (!source3.HasExited)
					{
						source3.Kill(entireProcessTree: true);
					}
				}
				catch
				{
				}
			}
			await _pump;
		}
		if (_audio != null)
		{
			await _audio.Stop();
			_audio.Dispose();
		}
		await _process.WaitForExitAsync();
		await _output;
		string error = await _errors;
		foreach (Process source4 in _sources)
		{
			await source4.WaitForExitAsync();
		}
		string text;
		for (int i = 0; i < _sources.Count; i++)
		{
			if (!cancel && _sources[i].ExitCode != 0)
			{
				text = error;
				string text2 = text;
				error = text2 + "\n" + await _sourceErrors[i];
			}
		}
		if (!cancel && (_process.ExitCode != 0 || _pumpFailure != null || _sources.Any((Process p) => p.ExitCode != 0)))
		{
			throw new IOException((error.Length > 1500) ? error.Substring(error.Length - 1500) : error, _pumpFailure);
		}
		if (cancel || !_plan.Video)
		{
			return;
		}
		text = _plan.Destination + ".muxed.mp4";
		string value = ((double)Math.Max(1, _encodedFrames) / (double)_plan.Fps).ToString("0.########", CultureInfo.InvariantCulture);
		using Process source2 = Process.Start(new ProcessStartInfo(_ffmpeg, $"-hide_banner -nostdin -v error -y -i \"{_plan.Destination}\" -i \"{_audioPath}\" -map 0:v:0 -map 1:a:0 -c:v copy -af apad -c:a aac -b:a 192k -t {value} -movflags +faststart \"{text}\"")
		{
			UseShellExecute = false,
			CreateNoWindow = true,
			RedirectStandardError = true
		});
		Own(source2);
		Task<string> muxErrors = source2.StandardError.ReadToEndAsync();
		if (await Task.WhenAny(source2.WaitForExitAsync(), Task.Delay(30000)) != null && !source2.HasExited)
		{
			source2.Kill(entireProcessTree: true);
			throw new IOException(UiLanguage.T("Audio merge timed out."));
		}
		string message = await muxErrors;
		if (source2.ExitCode != 0)
		{
			throw new IOException(message);
		}
		File.Move(text, _plan.Destination, overwrite: true);
	}

	public void Dispose()
	{
		if (_job != IntPtr.Zero)
		{
			CloseHandle(_job);
			_job = IntPtr.Zero;
		}
		foreach (Process source in _sources)
		{
			try
			{
				if (!source.HasExited)
				{
					source.Kill(entireProcessTree: true);
				}
			}
			catch
			{
			}
			source.Dispose();
		}
		try
		{
			if (_process != null && !_process.HasExited)
			{
				_process.Kill(entireProcessTree: true);
			}
		}
		catch
		{
		}
		_process?.Dispose();
		_audio?.Dispose();
	}
}
