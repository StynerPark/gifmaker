using System.Windows;

namespace ClipForgeWpf;

public sealed record CapturePlan(Rect Area, int Fps, bool Video, string Destination, CapturePiece[] Pieces);
