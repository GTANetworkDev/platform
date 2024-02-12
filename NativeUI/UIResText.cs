﻿using System;
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
    public class UIResText : GTA.UI.TextElement
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
        public new bool Outline { get; set; } = false;

        /// <summary>
        /// Push a long string into the stack.
        /// </summary>
        /// <param name="str"></param>
        public static void AddLongString(string str)
        {
            const int strLen = 99;
            for (int i = 0; i < str.Length; i += strLen)
            {
                string substr = str.Substring(i, Math.Min(strLen, str.Length - i));
                Function.Call(Hash.ADD_TEXT_COMPONENT_SUBSTRING_PLAYER_NAME, substr);
            }
        }


        public static float MeasureStringWidth(string str, Font font, float scale)
        {
            int screenw = Screen.Resolution.Width;
            int screenh = Screen.Resolution.Height;
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

        public override void Draw(SizeF offset)
        {
            int screenw = Screen.Resolution.Width;
            int screenh = Screen.Resolution.Height;
            const float height = 1080f;
            float ratio = (float)screenw / screenh;
            var width = height * ratio;

            float x = (Position.X) / width;
            float y = (Position.Y) / height;
            
            Function.Call(Hash.SET_TEXT_FONT, (int)Font);
            Function.Call(Hash.SET_TEXT_SCALE, 1.0f, Scale);
            Function.Call(Hash.SET_TEXT_COLOUR, Color.R, Color.G, Color.B, Color.A);
            if (DropShadow)
                Function.Call(Hash.SET_TEXT_DROP_SHADOW);
            if (Outline)
                Function.Call(Hash.SET_TEXT_OUTLINE);
            switch (TextAlignment)
            {
                case Alignment.Centered:
                    Function.Call(Hash.SET_TEXT_CENTRE, true);
                    break;
                case Alignment.Right:
                    Function.Call(Hash.SET_TEXT_RIGHT_JUSTIFY, true);
                    Function.Call(Hash.SET_TEXT_WRAP, 0, x);
                    break;
            }

            if (WordWrap != new Size(0, 0))
            {
                float xsize = (Position.X + WordWrap.Width)/width;
                Function.Call(Hash.SET_TEXT_WRAP, x, xsize);
            }

            Function.Call(Hash.BEGIN_TEXT_COMMAND_DISPLAY_TEXT, "jamyfafi");
            AddLongString(Caption);
            

            Function.Call(Hash.END_TEXT_COMMAND_DISPLAY_TEXT, x, y);
        }

        public new enum Alignment
        {
            Left,
            Centered,
            Right,
        }
    }
}