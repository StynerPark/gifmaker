using System.Windows;

namespace ClipForgeWpf;

public sealed record CaptureDisplay(int Adapter, int Output, string Name, Rect Bounds, bool Hdr, double SdrWhiteNits, double PeakNits);
