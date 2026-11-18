using System;
using System.IO;
using System.Windows;
using OpenCvSharp;

namespace ClipForgeWpf;

public static class SubtitleRemovalProcessor
{
	private const int FlowMaxWidth = 960;

	private const double DifferenceThreshold = 34.0;

	public static void ProcessFile(string inputPath, string referencePath, System.Windows.Rect area, string outputPath)
	{
		if (File.Exists(referencePath + ".mask.png"))
		{
			ImagePatchProcessor.ProcessFile(inputPath, referencePath, outputPath);
			return;
		}
		Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
		if (string.Equals(Path.GetFullPath(inputPath), Path.GetFullPath(referencePath), StringComparison.OrdinalIgnoreCase))
		{
			File.Copy(inputPath, outputPath, overwrite: true);
			return;
		}
		using Mat mat = Cv2.ImRead(inputPath);
		using Mat mat2 = Cv2.ImRead(referencePath);
		if (mat.Empty())
		{
			throw new InvalidOperationException("Could not read the input frame.");
		}
		if (mat2.Empty())
		{
			throw new InvalidOperationException("Could not read the subtitle reference frame.");
		}
		using Mat img = Remove(mat, mat2, area);
		if (!Cv2.ImWrite(outputPath, img))
		{
			throw new InvalidOperationException("Could not write the restored frame.");
		}
	}

	public static Mat Remove(Mat frame, Mat reference, System.Windows.Rect area)
	{
		using Mat mat = new Mat();
		if (reference.Size() == frame.Size())
		{
			reference.CopyTo(mat);
		}
		else
		{
			Cv2.Resize(reference, mat, frame.Size(), 0.0, 0.0, InterpolationFlags.Lanczos4);
		}
		OpenCvSharp.Rect rect = ClampArea(area, frame.Cols, frame.Rows);
		if (rect.Width < 2 || rect.Height < 2)
		{
			return frame.Clone();
		}
		double num = Math.Min(1.0, 960.0 / (double)Math.Max(1, frame.Cols));
		OpenCvSharp.Size size = new OpenCvSharp.Size(Math.Max(2, (int)Math.Round((double)frame.Cols * num)), Math.Max(2, (int)Math.Round((double)frame.Rows * num)));
		using Mat mat2 = new Mat();
		using Mat mat3 = new Mat();
		Cv2.Resize(frame, mat2, size, 0.0, 0.0, InterpolationFlags.Area);
		Cv2.Resize(mat, mat3, size, 0.0, 0.0, InterpolationFlags.Area);
		using Mat mat4 = new Mat();
		using Mat mat5 = new Mat();
		Cv2.CvtColor(mat2, mat4, ColorConversionCodes.BGR2GRAY);
		Cv2.CvtColor(mat3, mat5, ColorConversionCodes.BGR2GRAY);
		using Mat mat6 = new Mat();
		Cv2.CalcOpticalFlowFarneback(mat4, mat5, mat6, 0.5, 5, 31, 5, 7, 1.5, OpticalFlowFlags.FarnebackGaussian);
		OpenCvSharp.Rect rect2 = ScaleArea(rect, (double)size.Width / (double)frame.Cols, (double)size.Height / (double)frame.Rows, size.Width, size.Height);
		using Mat mat7 = Mat.Zeros(size, MatType.CV_8UC1).ToMat();
		Cv2.Rectangle(mat7, rect2, Scalar.White, -1);
		Mat[] array = Cv2.Split(mat6);
		using Mat mat8 = new Mat();
		using Mat mat9 = new Mat();
		try
		{
			double inpaintRadius = Math.Max(3.0, (double)Math.Min(rect2.Width, rect2.Height) / 8.0);
			Cv2.Inpaint(array[0], mat7, mat8, inpaintRadius, InpaintMethod.NS);
			Cv2.Inpaint(array[1], mat7, mat9, inpaintRadius, InpaintMethod.NS);
		}
		finally
		{
			Mat[] array2 = array;
			for (int i = 0; i < array2.Length; i++)
			{
				array2[i].Dispose();
			}
		}
		using Mat mat10 = new Mat();
		using Mat mat11 = new Mat();
		BuildCoordinateMaps(mat8, mat9, mat10, mat11);
		using Mat mat12 = new Mat();
		Cv2.Remap(mat3, mat12, mat10, mat11, InterpolationFlags.Cubic, BorderTypes.Reflect101);
		using Mat mat13 = new Mat();
		Cv2.Resize(mat12, mat13, frame.Size(), 0.0, 0.0, InterpolationFlags.Lanczos4);
		using Mat mat14 = Mat.Zeros(frame.Size(), MatType.CV_8UC1).ToMat();
		Cv2.Rectangle(mat14, rect, Scalar.White, -1);
		using Mat mat15 = new Mat();
		Cv2.Absdiff(frame, mat13, mat15);
		Mat[] array3 = Cv2.Split(mat15);
		using Mat mat16 = new Mat();
		using Mat mat17 = new Mat();
		try
		{
			Cv2.Max(array3[0], array3[1], mat16);
			Cv2.Max(mat16, array3[2], mat17);
		}
		finally
		{
			Mat[] array4 = array3;
			for (int j = 0; j < array4.Length; j++)
			{
				array4[j].Dispose();
			}
		}
		using Mat mat18 = new Mat();
		Cv2.GaussianBlur(mat17, mat18, new OpenCvSharp.Size(5, 5), 0.0);
		using Mat mat19 = new Mat();
		Cv2.Threshold(mat18, mat19, 34.0, 255.0, ThresholdTypes.Binary);
		Cv2.BitwiseAnd(mat19, mat14, mat19);
		double num2 = Math.Max(0.5, (double)frame.Cols / 1920.0);
		int num3 = OddKernel(5.0 * num2);
		int num4 = OddKernel(9.0 * num2);
		using (Mat mat20 = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(num3, num3)))
		{
			Cv2.MorphologyEx(mat19, mat19, MorphTypes.Close, mat20);
		}
		using (Mat mat21 = Cv2.GetStructuringElement(MorphShapes.Ellipse, new OpenCvSharp.Size(num4, num4)))
		{
			Cv2.Dilate(mat19, mat19, mat21);
		}
		Cv2.BitwiseAnd(mat19, mat14, mat19);
		using Mat mat22 = new Mat();
		Cv2.GaussianBlur(mat19, mat22, new OpenCvSharp.Size(0, 0), Math.Max(0.8, 1.5 * num2));
		using Mat mat23 = new Mat();
		mat22.ConvertTo(mat23, MatType.CV_32FC1, 1.0 / 255.0);
		using Mat mat24 = new Mat();
		Cv2.Merge(new Mat[3] { mat23, mat23, mat23 }, mat24);
		using Mat mat25 = new Mat();
		using (Mat mat26 = new Mat(mat24.Size(), mat24.Type(), Scalar.All(1.0)))
		{
			Cv2.Subtract(mat26, mat24, mat25);
		}
		using Mat mat27 = new Mat();
		using Mat mat28 = new Mat();
		frame.ConvertTo(mat27, MatType.CV_32FC3);
		mat13.ConvertTo(mat28, MatType.CV_32FC3);
		using Mat mat29 = new Mat();
		using Mat mat30 = new Mat();
		using Mat mat31 = new Mat();
		Cv2.Multiply(mat27, mat25, mat29);
		Cv2.Multiply(mat28, mat24, mat30);
		Cv2.Add(mat29, mat30, mat31);
		Mat mat32 = new Mat();
		mat31.ConvertTo(mat32, MatType.CV_8UC3);
		return mat32;
	}

	private static OpenCvSharp.Rect ClampArea(System.Windows.Rect area, int width, int height)
	{
		int num = Math.Clamp((int)Math.Floor(area.X), 0, Math.Max(0, width - 1));
		int num2 = Math.Clamp((int)Math.Floor(area.Y), 0, Math.Max(0, height - 1));
		int num3 = Math.Clamp((int)Math.Ceiling(area.Right), num + 1, width);
		int num4 = Math.Clamp((int)Math.Ceiling(area.Bottom), num2 + 1, height);
		return new OpenCvSharp.Rect(num, num2, Math.Max(1, num3 - num), Math.Max(1, num4 - num2));
	}

	private static OpenCvSharp.Rect ScaleArea(OpenCvSharp.Rect area, double scaleX, double scaleY, int width, int height)
	{
		int num = Math.Clamp((int)Math.Round((double)area.X * scaleX), 0, Math.Max(0, width - 1));
		int num2 = Math.Clamp((int)Math.Round((double)area.Y * scaleY), 0, Math.Max(0, height - 1));
		int num3 = Math.Clamp((int)Math.Round((double)area.Right * scaleX), num + 1, width);
		int num4 = Math.Clamp((int)Math.Round((double)area.Bottom * scaleY), num2 + 1, height);
		return new OpenCvSharp.Rect(num, num2, Math.Max(1, num3 - num), Math.Max(1, num4 - num2));
	}

	private static void BuildCoordinateMaps(Mat flowX, Mat flowY, Mat mapX, Mat mapY)
	{
		using Mat mat = new Mat(1, flowX.Cols, MatType.CV_32FC1);
		using Mat mat2 = new Mat(flowX.Rows, 1, MatType.CV_32FC1);
		for (int i = 0; i < flowX.Cols; i++)
		{
			mat.Set(0, i, (float)i);
		}
		for (int j = 0; j < flowX.Rows; j++)
		{
			mat2.Set(j, 0, (float)j);
		}
		using Mat mat3 = new Mat();
		using Mat mat4 = new Mat();
		Cv2.Repeat(mat, flowX.Rows, 1, mat3);
		Cv2.Repeat(mat2, 1, flowX.Cols, mat4);
		Cv2.Add(mat3, flowX, mapX);
		Cv2.Add(mat4, flowY, mapY);
	}

	private static int OddKernel(double value)
	{
		int num = Math.Max(3, (int)Math.Round(value));
		if (num % 2 != 0)
		{
			return num;
		}
		return num + 1;
	}
}
