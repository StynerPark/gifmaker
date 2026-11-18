namespace ClipForgeWpf;

public class OverlaySpec
{
	public string Kind { get; set; } = "text";

	public string Path { get; set; } = "";

	public string Text { get; set; } = "Text";

	public string Font { get; set; } = "Segoe UI";

	public string Color { get; set; } = "#FFFFFF";

	public bool Bold { get; set; }

	public bool Italic { get; set; }

	public bool Underline { get; set; }

	public bool Strike { get; set; }

	public double FontSize { get; set; } = 32.0;

	public double X { get; set; } = 0.08;

	public double Y { get; set; } = 0.08;

	public double Width { get; set; } = 0.25;

	public double Height { get; set; } = 0.12;

	public double Opacity { get; set; } = 1.0;

	public OverlaySpec Clone()
	{
		return (OverlaySpec)MemberwiseClone();
	}
}
