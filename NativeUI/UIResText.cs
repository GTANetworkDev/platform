using System;
using System.Drawing;
using GTA;
using GTA.Native;
using GTA.UI;
using Font = GTA.UI.Font;

namespace NativeUI
{
    /// <summary>
    /// A Text object in the 1080 pixels height base system.
    /// </summary>
    public class UIResText : GTA.UI.Text
    {
        public UIResText(string caption, Point position, float scale) : base(caption, position, scale)
        {
            TextAlignment = Alignment.Left;
        }

        public UIResText(string caption, Point position, float scale, Color color)
            : base(caption, position, scale, color)
        {
            TextAlignment = Alignment.Left;
        }

        public UIResText(string caption, Point position, float scale, Color color, Font font, Alignment justify)
            : base(caption, position, scale, color, font, 0)
        {
            TextAlignment = justify;
        }


        public Alignment TextAlignment { get; set; }
        public bool DropShadow { get; set; } = false;
        public bool Outline { get; set; } = false;

        /// <summary>
        /// Push a long string into the stack.
        /// </summary>
        /// <param name="str"></param>
        public static void AddLongString(string str)
        {

            CallCollection thisCol = new CallCollection();
            const int strLen = 99;
            for (int i = 0; i < str.Length; i += strLen)
            {
                string substr = str.Substring(i, Math.Min(strLen, str.Length - i));
                thisCol.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, substr);
            }
            thisCol.Execute();
        }


        public static float MeasureStringWidth(string str, Font font, float scale)
        {
            int screenw = BigMessageHandler.ScreenResolution.Width;
            int screenh = BigMessageHandler.ScreenResolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            float width = height * ratio;
            return MeasureStringWidthNoConvert(str, font, scale) * width;
        }

        public static float MeasureStringWidthNoConvert(string str, Font font, float scale)
        {
            Function.Call((Hash)0x54CE8AC98E120CAB, "STRING");
            AddLongString(str);
            return Function.Call<float>((Hash)0x85F061DA64ED2F67, (int)font) * scale;
        }

        public Size WordWrap { get; set; }


        const float height = 1080f;
        static float ratio = (float)BigMessageHandler.ScreenResolution.Width / BigMessageHandler.ScreenResolution.Height;
        static float width = height * ratio;

        public override void Draw(SizeF offset)
        {

            CallCollection thisCol = new CallCollection();
            float x = (Position.X) / width;
            float y = (Position.Y) / height;
            
            thisCol.Call(Hash.SET_TEXT_FONT, (int)Font);
            thisCol.Call(Hash.SET_TEXT_SCALE, 1.0f, Scale);
            thisCol.Call(Hash.SET_TEXT_COLOUR, Color.R, Color.G, Color.B, Color.A);
            if (DropShadow)
                thisCol.Call(Hash.SET_TEXT_DROP_SHADOW);
            if (Outline)
                thisCol.Call(Hash.SET_TEXT_OUTLINE);
            switch (TextAlignment)
            {
                case Alignment.Centered:
                    thisCol.Call(Hash.SET_TEXT_CENTRE, true);
                    break;
                case Alignment.Right:
                    thisCol.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);
                    thisCol.Call(Hash.SET_TEXT_WRAP, 0, x);
                    break;
            }

            if (WordWrap != new Size(0, 0))
            {
                float xsize = (Position.X + WordWrap.Width)/width;
                thisCol.Call(Hash.SET_TEXT_WRAP, x, xsize);
            }

            thisCol.Call(Hash._SET_TEXT_ENTRY, "jamyfafi");
            //AddLongString(Caption);

            const int maxStringLength = 99;
            int count = Caption.Length;
            for (int i = 0; i < count; i += maxStringLength)
            {
                thisCol.Call((Hash)0x6C188BE134E074AA, Caption.Substring(i, System.Math.Min(maxStringLength, Caption.Length - i)));
            }


            thisCol.Call(Hash._DRAW_TEXT, x, y);
            thisCol.Execute();
        }

        public enum Alignment
        {
            Left,
            Centered,
            Right,
        }
    }
}