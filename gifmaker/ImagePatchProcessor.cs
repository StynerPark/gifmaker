using System;
using System.IO;
using OpenCvSharp;

namespace ClipForgeWpf;

public static class ImagePatchProcessor
{
	public static Mat Composite(Mat frame, Mat replacement, Mat mask)
	{
		if (frame.Size() != replacement.Size() || frame.Size() != mask.Size())
		{
			throw new InvalidOperationException("교체 이미지는 원본 프레임과 같은 해상도여야 합니다.");
		}
		using Mat mat = new Mat();
		mask.ConvertTo(mat, MatType.CV_32FC1, 1.0 / 255.0);
		using Mat mat2 = new Mat();
		Cv2.Merge(new Mat[3] { mat, mat, mat }, mat2);
		using Mat mat3 = new Mat(mat2.Size(), mat2.Type(), Scalar.All(1.0));
		using Mat mat4 = new Mat();
		Cv2.Subtract(mat3, mat2, mat4);
		using Mat mat5 = new Mat();
		using Mat mat6 = new Mat();
		frame.ConvertTo(mat5, MatType.CV_32FC3);
		replacement.ConvertTo(mat6, MatType.CV_32FC3);
		Cv2.Multiply(mat5, mat4, mat5);
		Cv2.Multiply(mat6, mat2, mat6);
		Cv2.Add(mat5, mat6, mat5);
		Mat mat7 = new Mat();
		mat5.ConvertTo(mat7, MatType.CV_8UC3);
		return mat7;
	}

	public static void ProcessFile(string input, string reference, string output)
	{
		using Mat mat = Cv2.ImRead(input);
		using Mat mat2 = Cv2.ImRead(reference);
		using Mat mat3 = Cv2.ImRead(reference + ".mask.png", ImreadModes.Grayscale);
		if (mat.Empty() || mat2.Empty() || mat3.Empty())
		{
			throw new InvalidOperationException("이미지 패치 파일을 읽을 수 없습니다.");
		}
		using Mat img = Composite(mat, mat2, mat3);
		Directory.CreateDirectory(Path.GetDirectoryName(output));
		if (!Cv2.ImWrite(output, img))
		{
			throw new IOException("패치 프레임을 저장할 수 없습니다.");
		}
	}
}
