using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using NAudio.Wave;

namespace ClipForgeWpf;

public sealed class SystemAudioRecorder : IDisposable
{
	private readonly WasapiLoopbackCapture _capture = new WasapiLoopbackCapture();

	private readonly WaveFileWriter _writer;

	private readonly Stopwatch _clock = new Stopwatch();

	private readonly TaskCompletionSource<bool> _stopped = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

	private readonly object _gate = new object();

	private readonly byte[] _silence = new byte[65536];

	private bool _started;

	private bool _finished;

	private bool _disposed;

	private Exception _failure;

	public SystemAudioRecorder(string path)
	{
		_writer = new WaveFileWriter(path, _capture.WaveFormat);
		_capture.DataAvailable += delegate(object? _, WaveInEventArgs e)
		{
			lock (_gate)
			{
				if (!_finished)
				{
					long num = PositionAt(_clock.Elapsed.TotalSeconds);
					Pad(Math.Max(0L, num - e.BytesRecorded));
					_writer.Write(e.Buffer, 0, e.BytesRecorded);
				}
			}
		};
		_capture.RecordingStopped += delegate(object? _, StoppedEventArgs e)
		{
			_failure = e.Exception;
			_stopped.TrySetResult(result: true);
		};
	}

	public void Start()
	{
		if (!_started)
		{
			_started = true;
			_clock.Start();
			_capture.StartRecording();
		}
	}

	private long PositionAt(double seconds)
	{
		return (long)Math.Round(seconds * (double)_capture.WaveFormat.SampleRate) * _capture.WaveFormat.BlockAlign;
	}

	private void Pad(long target)
	{
		while (_writer.Length < target)
		{
			int count = (int)Math.Min(_silence.Length, target - _writer.Length);
			_writer.Write(_silence, 0, count);
		}
	}

	public async Task Stop()
	{
		if (_finished)
		{
			return;
		}
		if (_started)
		{
			_capture.StopRecording();
			if (await Task.WhenAny(_stopped.Task, Task.Delay(5000)) != _stopped.Task)
			{
				throw new IOException(UiLanguage.T("System audio capture did not stop."));
			}
		}
		lock (_gate)
		{
			Pad(PositionAt(_clock.Elapsed.TotalSeconds));
			_finished = true;
			_writer.Flush();
		}
		if (_failure == null)
		{
			return;
		}
		throw new IOException(UiLanguage.T("System audio capture failed."), _failure);
	}

	public void Dispose()
	{
		if (_disposed)
		{
			return;
		}
		_disposed = true;
		_capture.Dispose();
		lock (_gate)
		{
			_finished = true;
			_writer.Dispose();
		}
	}
}
