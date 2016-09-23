using System;
using System.Drawing;

namespace GTANetwork.GUI.Extern
{
	public class QuadDistort
	{
		//Computes the bounding box of the polygon
		private static Rectangle _ComputeBox(params Point[] oPoints)
		{
			int nBoxTop = int.MaxValue;
			int nBoxLeft = int.MaxValue;
			int nBoxRight = int.MinValue;
			int nBoxBottom = int.MinValue;
			foreach (Point oPoint in oPoints)
			{
				nBoxTop = Math.Min(nBoxTop, oPoint.Y);
				nBoxLeft = Math.Min(nBoxLeft, oPoint.X);
				nBoxRight = Math.Max(nBoxRight, oPoint.X);
				nBoxBottom = Math.Max(nBoxBottom, oPoint.Y);
			}
			return new Rectangle(nBoxLeft, nBoxTop, nBoxRight - nBoxLeft, nBoxBottom - nBoxTop);
		}
		//just to avoid the getter and setter of the PointF struct
		//saves a few milliseconds
		private struct FastPointF
		{
			public float X;
			public float Y;
			public FastPointF(float x, float y)
			{
				this.X = x;
				this.Y = y;
			}
		}
		private struct PointMap
		{
			public FastPointF From;
			public FastPointF To;
			public PointMap(FastPointF oFrom, FastPointF oTo)
			{
				this.From = oFrom;
				this.To = oTo;
			}
		}

		public static void DrawBitmap(FastBitmap oTexture, Point topLeft, Point topRight, Point bottomRight, Point bottomLeft, FastBitmap oCanvas)
		{
			//top line
			float nDeltaX = topRight.X - topLeft.X;
			float nDeltaY = topRight.Y - topLeft.Y;
			float nNumOfPixels = Math.Max(Math.Abs(nDeltaX), Math.Abs(nDeltaY));
			//nNumOfPixels *= 2;
			float nIncrementX = nDeltaX / nNumOfPixels;
			float nIncrementY = nDeltaY / nNumOfPixels;
			FastPointF oPixel = new FastPointF(topLeft.X, topLeft.Y);

			float nTextureIncrementX = (oTexture.Width / (nNumOfPixels + 1));
			float nTextureX = 0;
			

			PointMap[] oTopLine = new PointMap[(int)(nNumOfPixels + 1)];
			for (int nCurrentPixel = 0; nCurrentPixel <= nNumOfPixels; nCurrentPixel++)
			{
				oTopLine[nCurrentPixel] = new PointMap(new FastPointF(nTextureX, 0), oPixel);

				nTextureX += nTextureIncrementX;
				oPixel.X += nIncrementX;
				oPixel.Y += nIncrementY;
			}
			
			//bottom line
			nDeltaX = bottomRight.X - bottomLeft.X;
			nDeltaY = bottomRight.Y - bottomLeft.Y;
			nNumOfPixels = Math.Max(Math.Abs(nDeltaX), Math.Abs(nDeltaY));
			//nNumOfPixels *= 2;
			nIncrementX = nDeltaX / nNumOfPixels;
			nIncrementY = nDeltaY / nNumOfPixels;
			oPixel = new FastPointF(bottomLeft.X, bottomLeft.Y);


			nTextureIncrementX = (oTexture.Width / (nNumOfPixels + 1));
			nTextureX = 0;

			PointMap[] oBottomLine = new PointMap[(int)(nNumOfPixels + 1)];
			for (int nCurrentPixel = 0; nCurrentPixel <= nNumOfPixels; nCurrentPixel++)
			{
				oBottomLine[nCurrentPixel] = new PointMap(new FastPointF(nTextureX, oTexture.Height - 1), oPixel);

				oPixel.X += nIncrementX;
				oPixel.Y += nIncrementY;
				nTextureX += nTextureIncrementX;
			}

			//cross lines
			PointMap[] oStartLine = oTopLine.Length > oBottomLine.Length ? oTopLine : oBottomLine;
			PointMap[] oEndLine = oTopLine.Length > oBottomLine.Length ? oBottomLine : oTopLine;
			float nFactor = (float)oEndLine.Length / (float)oStartLine.Length;

			Rectangle oBox = _ComputeBox(topLeft, topRight, bottomLeft, bottomRight);
			Boolean[,] oPainted = new Boolean[oBox.Width + 1, oBox.Height + 1];

			for (int s = 0; s < oStartLine.Length; s++)
			{
				FastPointF oStart = oStartLine[s].To;
				FastPointF oStartTexture = oStartLine[s].From;
				float nEndPoint = (float)Math.Floor(nFactor * s);
				FastPointF oEnd = oEndLine[(int)nEndPoint].To;
				FastPointF oEndTexture = oEndLine[(int)nEndPoint].From;


				nDeltaX = oEnd.X - oStart.X;
				nDeltaY = oEnd.Y - oStart.Y;
				nNumOfPixels = Math.Max(Math.Abs(nDeltaX), Math.Abs(nDeltaY));
				//nNumOfPixels *= 2;
				nIncrementX = nDeltaX / nNumOfPixels;
				nIncrementY = nDeltaY / nNumOfPixels;
				
				float nTextureDeltaX = oEndTexture.X - oStartTexture.X;
				float nTextureDeltaY = oEndTexture.Y - oStartTexture.Y;
				nTextureIncrementX = nTextureDeltaX / (nNumOfPixels + 1);
				float nTextureIncrementY = nTextureDeltaY / (nNumOfPixels + 1);
				FastPointF oDestination = oStart;
				FastPointF oSource = oStartTexture;
				for (int nCurrentPixel = 0; nCurrentPixel <= nNumOfPixels; nCurrentPixel++)
				{
					RGBColor c = oTexture.GetPixel((int)oSource.X, (int)oSource.Y);
					oCanvas.SetPixel((int)oDestination.X, (int)oDestination.Y, c);
					oPainted[(int)(oDestination.X - oBox.X), (int)(oDestination.Y - oBox.Y)] = true;
					oDestination.X += nIncrementX;
					oDestination.Y += nIncrementY;
					oSource.X += nTextureIncrementX;
					oSource.Y += nTextureIncrementY;
				}
			}

			//paint missing pixels
			for (int ny = 0; ny < oBox.Height; ny++)
				for (int nx = 0; nx < oBox.Width; nx++)
				{
					if (oPainted[nx, ny] == true)
						continue;

					int nNeigh = 0;
					RGBColor oColor;
					int R = 0;
					int G = 0;
					int B = 0;
					if (nx > 0 && oPainted[nx - 1, ny] == true)
					{
						oColor = oCanvas.GetPixel((nx + oBox.X) - 1, (ny + oBox.Y));
						R += oColor.R;
						G += oColor.G;
						B += oColor.B;
						nNeigh++;
					}
					if (ny > 0 && oPainted[nx, ny - 1] == true)
					{
						oColor = oCanvas.GetPixel((nx + oBox.X), (ny + oBox.Y) - 1);
						R += oColor.R;
						G += oColor.G;
						B += oColor.B;
						nNeigh++;
					}
					if (nx < oCanvas.Width - 1 && oPainted[nx + 1, ny] == true)
					{
						oColor = oCanvas.GetPixel((nx + oBox.X) + 1, (ny + oBox.Y));
						R += oColor.R;
						G += oColor.G;
						B += oColor.B;
						nNeigh++;
					}
					if (ny < oCanvas.Height - 1 && oPainted[nx, ny + 1] == true)
					{
						oColor = oCanvas.GetPixel((nx + oBox.X), (ny + oBox.Y) + 1);
						R += oColor.R;
						G += oColor.G;
						B += oColor.B;
						nNeigh++;
					}
					if (nNeigh == 0)
						continue;
					oCanvas.SetPixel((nx + oBox.X), (ny + oBox.Y), new RGBColor((byte)(R / nNeigh), (byte)(G / nNeigh), (byte)(B / nNeigh)));
				}


		}
	}
}
