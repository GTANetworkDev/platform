using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.InteropServices;

namespace System.Drawing
{
	/// <summary>
	/// Fast and optimized representation of ARGB color
	/// </summary>
	[StructLayout(LayoutKind.Explicit)]
	public struct RGBColor:IComparable<RGBColor>
	{
		/// <summary>
		/// Transparent Color
		/// </summary>
		public static readonly RGBColor Transparent = new RGBColor(0, 0, 0, 0);
		/// <summary>
		/// Black
		/// </summary>
		public static readonly RGBColor Black = new RGBColor(0xFF, 0, 0, 0);
		/// <summary>
		/// White
		/// </summary>
		public static readonly RGBColor White = new RGBColor(0xFF, 0xFF, 0xFF, 0xFF);
		/// <summary>
		/// Red
		/// </summary>
		public static readonly RGBColor Red = new RGBColor(0xFF, 0xFF, 0, 0);
		/// <summary>
		/// Green
		/// </summary>
		public static readonly RGBColor Green = new RGBColor(0xFF, 0, 0xFF, 0);
		/// <summary>
		/// Blue
		/// </summary>
		public static readonly RGBColor Blue = new RGBColor(0xFF, 0, 0, 0xFF);
		/// <summary>
		/// Yeloow
		/// </summary>
		public static readonly RGBColor Yellow = new RGBColor(0xFF, 0xFF, 0xFF, 0);
		/// <summary>
		/// Magante
		/// </summary>
		public static readonly RGBColor Magenta = new RGBColor(0xFF, 0xFF, 0, 0xFF);
		/// <summary>
		/// Cyan
		/// </summary>
		public static readonly RGBColor Cyan = new RGBColor(0xFF, 0, 0xFF, 0xFF);
		/// <summary>
		/// Grey
		/// </summary>
		public static readonly RGBColor Grey = new RGBColor(0xFF, 0x7F, 0x7F, 0x7F);
		/// <summary>
		/// the ARGB value of the instance
		/// </summary>
		[FieldOffset(0)]
		public Int32 Argb;
		/// <summary>
		/// the blue value of the instance
		/// </summary>
		[FieldOffset(0)]
		public byte B;
		/// <summary>
		/// the green value of the instance
		/// </summary>
		[FieldOffset(1)]
		public byte G;
		/// <summary>
		/// the red value of the instance
		/// </summary>
		[FieldOffset(2)]
		public byte R;
		/// <summary>
		/// the alpha value of the instance
		/// </summary>
		[FieldOffset(3)]
		public byte A;

		/// <summary>
		/// Creates a new RGBColor
		/// </summary>
		/// <param name="argb"></param>
		public RGBColor(int argb)
		{
			this.A = 0;
			this.R = 0;
			this.G = 0;
			this.B = 0;
			this.Argb = argb;
		}
		/// <summary>
		/// Creates a new RGBColor
		/// </summary>
		/// <param name="grey"></param>
		public RGBColor(byte grey)
		{
			this.Argb = 0;
			this.A = 0xFF;
			this.R = grey;
			this.G = grey;
			this.B = grey;
		}
		/// <summary>
		/// Creates a new RGBColor
		/// </summary>
		/// <param name="alpha"></param>
		/// <param name="oOriginal"></param>
        public RGBColor(byte alpha, RGBColor oOriginal)
        {
            this.Argb = 0;
            this.A = alpha;
            this.R = oOriginal.R;
            this.G = oOriginal.G;
            this.B = oOriginal.B;
        }
		/// <summary>
		/// Creates a new RGBColor
		/// </summary>
		/// <param name="red"></param>
		/// <param name="green"></param>
		/// <param name="blue"></param>
        public RGBColor(byte red, byte green, byte blue)
        {
            this.Argb = 0;
            this.A = 0xFF;
            this.R = red;
            this.G = green;
            this.B = blue;
        }
		/// <summary>
		/// Creates a new RGBColor
		/// </summary>
		/// <param name="alpha"></param>
		/// <param name="red"></param>
		/// <param name="green"></param>
		/// <param name="blue"></param>
        public RGBColor(byte alpha, byte red, byte green, byte blue)
        {
            this.Argb = 0;
            this.A = alpha;
            this.R = red;
            this.G = green;
            this.B = blue;
        }
		/// <summary>
		/// RGBColor String Representation
		/// </summary>
		/// <returns></returns>
        public override string ToString()
        {
            return String.Format("0x{0:X2}{1:X2}{2:X2}{3:X2}",this.A,this.R,this.G,this.B);
        }
		/// <summary>
		/// Sets and validates the values of the components
		/// </summary>
		/// <param name="red"></param>
		/// <param name="green"></param>
		/// <param name="blue"></param>
		public void SetValues(int red, int green, int blue)
		{
			if (red > Byte.MaxValue) red = Byte.MaxValue;
			else if( red < Byte.MinValue) red = Byte.MinValue;

			if (green > Byte.MaxValue) green = Byte.MaxValue;
			else if (green < Byte.MinValue) green = Byte.MinValue;

			if (blue > Byte.MaxValue) blue = Byte.MaxValue;
			else if (blue < Byte.MinValue) blue = Byte.MinValue;
			
			this.R = (byte)red;
			this.G = (byte)green;
			this.B = (byte)blue;
		}
		/// <summary>
		/// returns the Euclidean distance if the color
		/// </summary>
		/// <returns></returns>
        public int GetVector() { return _GetVector(); }
        private int _GetVector()
        {
        	double  nValue = (this.A * this.A) + (this.R * this.R) + (this.G * this.G) + (this.B * this.B);
        	nValue = Math.Sqrt(nValue);
        	return (int)nValue;
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
        public static bool operator !=(RGBColor left, RGBColor right)
        {
            return left.Argb != right.Argb;
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="left"></param>
		/// <param name="right"></param>
		/// <returns></returns>
        public static bool operator ==(RGBColor left, RGBColor right)
        {
            return left.Argb == right.Argb;
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
        public static RGBColor operator -(RGBColor first, RGBColor second)
        {
            int nADif = (first.A - second.A);
            int nRDif = (first.R - second.R);
            int nGDif = (first.G - second.G);
            int nBDif = (first.B - second.B);

            nADif = (nADif < 0 ? 0 : nADif);
            nRDif = (nRDif < 0 ? 0 : nRDif);
            nGDif = (nGDif < 0 ? 0 : nGDif);
            nBDif = (nBDif < 0 ? 0 : nBDif);

            return new RGBColor((byte)nADif, (byte)nRDif, (byte)nGDif, (byte)nBDif);
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
        public static RGBColor operator +(RGBColor first, RGBColor second)
        {
            int nADif = (first.A + second.A);
            int nRDif = (first.R + second.R);
            int nGDif = (first.G + second.G);
            int nBDif = (first.B + second.B);

            nADif = (nADif > 255 ? 255 : nADif);
            nRDif = (nRDif > 255 ? 255 : nRDif);
            nGDif = (nGDif > 255 ? 255 : nGDif);
            nBDif = (nBDif > 255 ? 255 : nBDif);

            return new RGBColor((byte)nADif, (byte)nRDif, (byte)nGDif, (byte)nBDif);
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
        public static bool operator <(RGBColor first, RGBColor second)
        {
            return first._GetVector() < second._GetVector();
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
        public static bool operator >(RGBColor first, RGBColor second)
        {
            return first._GetVector() > second._GetVector();
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
        public static bool operator <=(RGBColor first, RGBColor second)
        {
            return first._GetVector() <= second._GetVector();
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="first"></param>
		/// <param name="second"></param>
		/// <returns></returns>
        public static bool operator >=(RGBColor first, RGBColor second)
        {
            return first._GetVector() >= second._GetVector();
        }

		/// <summary>
		/// 
		/// </summary>
		/// <param name="obj"></param>
		/// <returns></returns>
        public override bool Equals(object obj)
        {
            if (obj is RGBColor)
            {
                RGBColor color = (RGBColor)obj;
                return this.Argb == color.Argb;
            }
            return false;
        }
		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
        public override int GetHashCode()
        {
            return this.Argb.GetHashCode();
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="oValue"></param>
		/// <returns></returns>
        public static implicit operator Color(RGBColor oValue)
        {
            return Color.FromArgb(oValue.Argb);
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="oValue"></param>
        /// <returns></returns>
		public static implicit operator RGBColor(Color oValue)
        {
            return new RGBColor(oValue.ToArgb());
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="oValue"></param>
		/// <returns></returns>
        public static implicit operator Int32(RGBColor oValue)
        {
            return oValue.Argb;
        }
		/// <summary>
		/// 
		/// </summary>
		/// <param name="oValue"></param>
		/// <returns></returns>
        public static implicit operator RGBColor(Int32 oValue)
        {
            return new RGBColor(oValue);
        }

        #region IComparable<RGBColor> Members

        int IComparable<RGBColor>.CompareTo(RGBColor other)
        {
           //return this.Argb - other.Argb;
           return this._GetVector() - other._GetVector();
        }

        #endregion
    }
}
