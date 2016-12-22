using System.Drawing;
using GTANetwork.GUI.DirectXHook.Hook.DX11;

namespace GTANetwork.GUI.DirectXHook.Hook.Common
{
    public class ImageElement: Element
    {
        public virtual System.Drawing.Bitmap Bitmap { get; set; }
        
        /// <summary>
        /// This value is multiplied with the source color (e.g. White will result in same color as source image)
        /// </summary>
        /// <remarks>
        /// Defaults to <see cref="System.Drawing.Color.White"/>.
        /// </remarks>
        public virtual System.Drawing.Color Tint { get; set; }
        
        /// <summary>
        /// The location of where to render this image element
        /// </summary>
        public virtual System.Drawing.Point Location { get; set; }

        public float Angle { get; set; }

        public float Scale { get; set; }

        public string Filename { get; set; }

        bool _ownsBitmap = false;

        public DXImage Image { get; set; }

        public bool Dirty = true;

        public Bitmap NextBitmap { get; set; }

        public object SwitchLock = new object();

        public void SetBitmap(Bitmap bmp)
        {
            lock (SwitchLock)
            {
                if (Dirty && NextBitmap != null)
                {
                    SafeDispose(NextBitmap);
                }

                NextBitmap = bmp;
                Dirty = true;
            }
        }
       
        public ImageElement(string filename):
            this(new System.Drawing.Bitmap(filename), true)
        {
            Filename = filename;
        }

        public ImageElement(System.Drawing.Bitmap bitmap, bool ownsImage = false)
        {
            Tint = System.Drawing.Color.White;
            this.Bitmap = bitmap;
            _ownsBitmap = ownsImage;
            Scale = 1.0f;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing)
            {
                if (_ownsBitmap)
                {
                    SafeDispose(this.Bitmap);
                    this.Bitmap = null;
                }

                if (Image != null)
                {
                    Image.Dispose();
                    Image = null;
                }

                if (NextBitmap != null)
                {
                    NextBitmap.Dispose();
                    NextBitmap = null;
                }
            }
        }
    }
}
