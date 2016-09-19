using System;
using System.Collections.Generic;
using System.Text;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Drawing.Drawing2D;

namespace System.Drawing
{
	/// <summary>
	/// A Class that manipulates the byte stream of an Bitmap image. 
	/// </summary>
	public class FastBitmap
	{
		#region private menbers
		private Bitmap _oBitmap;
		private int[] _oPixels;
		int _nStride;
		int _nHeight;
		int _nWidth;
		CompositingMode _oCompositingMode = CompositingMode.SourceOver;
		#endregion

		#region public properties
		/// <summary>
		/// Gets the width, in pixels, of this Image.(Inherited from Image.)
		/// </summary>
		public int Width { get { return _nWidth; } }
		/// <summary>
		/// Gets the height, in pixels, of this Image.
		/// </summary>
		public int Height { get { return _nHeight; } }
		/// <summary>
		/// Gets or sets the CompositingMode
		/// </summary>
		public CompositingMode CompositingMode { get { return _oCompositingMode; } set { _oCompositingMode = value; } }
		#endregion

		#region private methods
		private void _GetPixels()
		{
			BitmapData oData = _oBitmap.LockBits(new Rectangle(0, 0, _oBitmap.Width, _oBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
			_nWidth = oData.Width;
			_nHeight = oData.Height;
			_nStride = oData.Stride / 4;
			IntPtr nScan0 = oData.Scan0;

			int nInts = _nStride * _nHeight;
			_oPixels = new int[nInts];

			Marshal.Copy(nScan0, _oPixels, 0, nInts);
			_oBitmap.UnlockBits(oData);

		}
		private void _SetPixels()
		{
			BitmapData oData = _oBitmap.LockBits(new Rectangle(0, 0, _oBitmap.Width, _oBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

			IntPtr nScan0 = oData.Scan0;
			int nInts = _nStride * _nHeight;
			Marshal.Copy(_oPixels, 0, nScan0, nInts);
			//Array.Copy(_oPixels, nScan0, nInts);
			_oBitmap.UnlockBits(oData);

		}
		#endregion

		#region constructor
		/// <summary>
		/// Create a new FastBitmap object to manipulate the given Bitmap
		/// </summary>
		/// <param name="oBitmap"></param>
		public FastBitmap(String sFilename)
			: this(Image.FromFile(sFilename) as Bitmap)
		{
		}
		/// <summary>
		/// Create a new FastBitmap object to manipulate the given Bitmap
		/// </summary>
		/// <param name="oBitmap"></param>
		public FastBitmap(Bitmap oBitmap)
		{
			_oBitmap = oBitmap;

			_GetPixels();
		}
		/// <summary>
		/// Creates a new blank bitmap with the given size
		/// </summary>
		/// <param name="nWidth">the width of the bitmap</param>
		/// <param name="nHeight">the height of the bitmap</param>
		public FastBitmap(int nWidth, int nHeight)
			: this(new Bitmap(nWidth, nHeight,PixelFormat.Format32bppPArgb))
		{
		}
		/// <summary>
		/// Creates a new blank bitmap with the given size
		/// </summary>
		/// <param name="nWidth">the width of the bitmap</param>
		/// <param name="nHeight">the height of the bitmap</param>
		public FastBitmap(Size oSize)
			: this(new Bitmap(oSize.Width, oSize.Height, PixelFormat.Format32bppPArgb))
		{
		}
		#endregion

		#region public methods

		/// <summary>
		/// Sets the color of the specified pixel in this Bitmap. 
		/// </summary>
		/// <param name="x">the x coordinate of the pixel</param>
		/// <param name="y">the y coordinate of the pixel</param>
		/// <param name="oColor">the new color</param>
		RGBColor _SetPixel = new RGBColor();
		public void SetPixel(int x, int y, RGBColor oColor)
		{
			if (x < 0 || x >= _nWidth || y < 0 || y >= _nHeight)
				return;

			if (oColor.A == 0xFF || _oCompositingMode == CompositingMode.SourceCopy)//full opacity
				_oPixels[y * _nStride + x] = oColor.Argb;
			else if (oColor.A == 0x0)//no opacity
			{
				return;
			}
			else // alpha blend
			{
				int nPos = y * _nStride + x;
				_SetPixel.Argb = _oPixels[nPos];
				/*if(oOriginalColor.A==0)
				{
					_oPixels[nPos] = oColor.Argb;
					return;
				}*/
				float nColorAlpha = ((float)oColor.A / 255f);
				float nOriginalAlpha = 1 - nColorAlpha;

				_SetPixel.R = (byte)((nOriginalAlpha * _SetPixel.R) + (nColorAlpha * oColor.R));
				_SetPixel.G = (byte)((nOriginalAlpha * _SetPixel.G) + (nColorAlpha * oColor.G));
				_SetPixel.B = (byte)((nOriginalAlpha * _SetPixel.B) + (nColorAlpha * oColor.B));

				//_oPixels[y * _nStride + x] = new RGBColor(nNR, nNG, nNB);
				//int nRGB = 0xFF;

				_oPixels[nPos] = _SetPixel.Argb;

			}
		}
		/// <summary>
		/// Gets the color of the specified pixel in this Bitmap. 
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <returns></returns>
		RGBColor _GetPixel = new RGBColor();
		public RGBColor GetPixel(int x, int y)
		{
			x %= _nWidth;
			if (x < 0)x += _nWidth;
			y %= _nHeight;
			if (y < 0)y += _nHeight;

			_GetPixel.Argb = _oPixels[y * _nStride + x];
			return _GetPixel;
		}


		public void Clear(RGBColor oColor)
		{
			if (oColor.Argb == 0)
			{
				Array.Clear(_oPixels, 0, _oPixels.Length);
				return;
			}

			//only set one row
			for (int x = 0; x < _nStride; x++)
			{
				_oPixels[x] = oColor.Argb;
			}
			//copy the rest of rows
			for (int y = 1; y < this._nHeight; y++)
			{
				Buffer.BlockCopy(_oPixels, 0, _oPixels, (y * _nStride) * sizeof(int), _nStride * sizeof(int));
			}
		}

		#endregion

		#region cast operators
		/// <summary>
		/// Extracts a Bitmap image from a FastBitmap.
		/// </summary>
		/// <param name="oFast"></param>
		/// <returns></returns>
		public static implicit operator Bitmap(FastBitmap oFast)
		{
			oFast._SetPixels();
			return oFast._oBitmap;
		}
		/// <summary>
		/// Creates a FastBitmap from a Bitmap image.
		/// </summary>
		/// <param name="oSlow"></param>
		/// <returns></returns>
		public static implicit operator FastBitmap(Bitmap oSlow)
		{
			return new FastBitmap(oSlow);
		}
		#endregion

	}
}
