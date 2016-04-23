using System.Drawing;
using System.IO;
using Rage;
using RAGENativeUI.Elements;
using RAGENativeUI.PauseMenu;

namespace GTANetwork.GUI
{
    public class TabWelcomeMessageItem : TabTextItem
    {
        public TabWelcomeMessageItem(string defaultTitle, string defaultText) : base("news", defaultTitle, defaultText)
        {
        }

        public string PromoPicturePath { get; set; }
        private Texture PromoPictureTexture { get; set; }

        public override void Draw()
        {

            base.Draw();

            if (!string.IsNullOrEmpty(PromoPicturePath) && File.Exists(PromoPicturePath) && !Game.IsLoading)
            {
                WordWrap = BottomRight.X - TopLeft.X - 40 - 400 - 20;
            }
        }

        public void DrawTexture(Rage.GraphicsEventArgs e)
        {
            if (!string.IsNullOrEmpty(PromoPicturePath) && File.Exists(PromoPicturePath) && !Game.IsLoading)
            {
                if (PromoPictureTexture == null)
                {
                    PromoPictureTexture = Game.CreateTextureFromFile(PromoPicturePath);
                }

                Sprite.DrawTexture(PromoPictureTexture, new Point(BottomRight.X - 400, TopLeft.Y), new Size(400, 600), e);
            }
        }
    }
}