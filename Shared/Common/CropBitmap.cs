using System;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;

namespace OCR.Common
{
	public class CropBitmap
	{


		async public static Task<WriteableBitmap> GetCroppedBitmapAsync(BitmapDecoder decoder,
			Point startPoint, Size corpSize, double scale)
		{
			if (double.IsNaN(scale) || double.IsInfinity(scale))
			{
				scale = 1;
			}

			// Convert start point and size to integer.
			uint startPointX = (uint)Math.Floor(startPoint.X * scale);
			uint startPointY = (uint)Math.Floor(startPoint.Y * scale);
			uint height = (uint)Math.Floor(corpSize.Height * scale);
			uint width = (uint)Math.Floor(corpSize.Width * scale);

			// The scaledSize of original image.
			uint scaledWidth = (uint)Math.Floor(decoder.PixelWidth * scale);
			uint scaledHeight = (uint)Math.Floor(decoder.PixelHeight * scale);


			// Refine the start point and the size. 
			if (startPointX + width > scaledWidth)
			{
				startPointX = scaledWidth - width;
			}

			if (startPointY + height > scaledHeight)
			{
				startPointY = scaledHeight - height;
			}

			// Get the cropped pixels.
			byte[] pixels = await GetPixelData(decoder, startPointX, startPointY, width, height,
				scaledWidth, scaledHeight);

			// Stream the bytes into a WriteableBitmap
			WriteableBitmap cropBmp = new WriteableBitmap((int)width, (int)height);
			Stream pixStream = cropBmp.PixelBuffer.AsStream();
			pixStream.Write(pixels, 0, (int)(width * height * 4));

			return cropBmp;
		}

		/// <summary>
		/// Use BitmapTransform to define the region to crop, and then get the pixel data in the region.
		/// If you want to get the pixel data of a scaled image, set the scaledWidth and scaledHeight
		/// of the scaled image.
		/// </summary>
		/// <returns></returns>
		async static private Task<byte[]> GetPixelData(BitmapDecoder decoder, uint startPointX, uint startPointY,
			uint width, uint height, uint scaledWidth, uint scaledHeight)
		{

			BitmapTransform transform = new BitmapTransform();
			BitmapBounds bounds = new BitmapBounds();
			bounds.X = startPointX;
			bounds.Y = startPointY;
			bounds.Height = height;
			bounds.Width = width;
			transform.Bounds = bounds;

			transform.ScaledWidth = scaledWidth;
			transform.ScaledHeight = scaledHeight;

			// Get the cropped pixels within the bounds of transform.
			PixelDataProvider pix = await decoder.GetPixelDataAsync(
				BitmapPixelFormat.Bgra8,
				BitmapAlphaMode.Straight,
				transform,
				ExifOrientationMode.IgnoreExifOrientation,
				ColorManagementMode.ColorManageToSRgb);
			byte[] pixels = pix.DetachPixelData();
			return pixels;
		}
		

	}

}
