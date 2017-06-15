using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using GTANetwork.GUI.DirectXHook.Hook.Common;
using GTANetworkShared;
using SharpDX;
using SharpDX.Direct3D11;
using Device = SharpDX.Direct3D11.Device;

namespace GTANetwork.GUI.DirectXHook.Hook.DX11
{
    internal class DXOverlayEngine: DisposeCollector
    {
        public List<IOverlay> Overlays { get; set; }
        public bool DeferredContext
        {
            get
            {
                return _deviceContext.TypeInfo == DeviceContextType.Deferred;
            }
        }

        bool _initialised = false;
        bool _initialising = false;

        Device _device;
        DeviceContext _deviceContext;
        Texture2D _renderTarget;
        RenderTargetView _renderTargetView;
        DXSprite _spriteEngine;
        Dictionary<string, DXFont> _fontCache = new Dictionary<string, DXFont>();
        Dictionary<Element, DXImage> _imageCache = new Dictionary<Element, DXImage>();
        DXHookD3D11 _hook;

        public DXOverlayEngine(DXHookD3D11 hook)
        {
            _hook = hook;
            Overlays = new List<IOverlay>();
        }

        private void EnsureInitiliased()
        {
            Debug.Assert(_initialised);
        }

        public bool Initialise(SharpDX.DXGI.SwapChain swapChain)
        {
            return Initialise(swapChain.GetDevice<Device>(), swapChain.GetBackBuffer<Texture2D>(0));
        }

        public bool Initialise(Device device, Texture2D renderTarget)
        {
            //Debug.Assert(!_initialised);
            if (_initialised) return false;
            if (_initialising)
                return false;

            _initialising = true;

            try
            {

                _device = device;
                _renderTarget = renderTarget;
                try
                {
                    //_deviceContext = ToDispose(new DeviceContext(_device)); // TODO: Test if this fixes crash 
                    _deviceContext = new DeviceContext(_device); // WARN: may cause a mem leak
                }
                catch (SharpDXException)
                {
                    _deviceContext = _device.ImmediateContext;
                }

                _renderTargetView = Collect(new RenderTargetView(_device, _renderTarget));

                //if (DeferredContext)
                //{
                //    ViewportF[] viewportf = { new ViewportF(0, 0, _renderTarget.Description.Width, _renderTarget.Description.Height, 0, 1) };
                //    _deviceContext.Rasterizer.SetViewports(viewportf);
                //    _deviceContext.OutputMerger.SetTargets(_renderTargetView);
                //}

                _spriteEngine = new DXSprite(_device, _deviceContext);
                if (!_spriteEngine.Initialize())
                    return false;

                // Initialise any resources required for overlay elements
                IntialiseElementResources();

                _initialised = true;
                return true;
            }
            finally
            {
                _initialising = false;
            }
        }

        private void IntialiseElementResources()
        {
            lock (_hook._overlayLock)
            foreach (var overlay in Overlays)
            {
                foreach (var element in overlay.Elements)
                {
                    var textElement = element as TextElement;
                    var imageElement = element as ImageElement;

                    if (textElement != null)
                    {
                        GetFontForTextElement(textElement);
                    }
                    else if (imageElement != null)
                    {
                        GetImageForImageElement(imageElement);
                    }
                }
            }
        }

        private void Begin()
        {
            //if (!DeferredContext)
            //{
                SharpDX.Mathematics.Interop.RawViewportF[] viewportf = { new ViewportF(0, 0, _renderTarget.Description.Width, _renderTarget.Description.Height, 0, 1) };
                _deviceContext.Rasterizer.SetViewports(viewportf);
                _deviceContext.OutputMerger.SetTargets(_renderTargetView);
            //}
        }

        /// <summary>
        /// Draw the overlay(s)
        /// </summary>
        public void Draw()
        {
            if (!_initialised) return;

            Begin();

            lock (_hook._overlayLock)
            foreach (var overlay in Overlays)
            {
                foreach (var element in overlay.Elements)
                {
                    if (element.Hidden)
                        continue;

                    var textElement = element as TextElement;
                    var imageElement = element as ImageElement;
                    
                    if (textElement != null)
                    {
                        DXFont font = GetFontForTextElement(textElement);
                        if (font != null && !String.IsNullOrEmpty(textElement.Text))
                            _spriteEngine.DrawString(textElement.Location.X, textElement.Location.Y, textElement.Text, textElement.Color, font);
                    }
                    else if (imageElement != null)
                    {
                        lock (_imageCache)
                        {
                            DXImage image = GetImageForImageElement(imageElement);
                            if (image != null)
                                _spriteEngine.DrawImage(imageElement.Location.X, imageElement.Location.Y,
                                    imageElement.Scale, imageElement.Angle, imageElement.Tint, image);
                        }
                    }
                }
            }

            End();
        }

        private void End()
        {
            if (DeferredContext)
            {
                var commandList = _deviceContext.FinishCommandList(true);
                _device.ImmediateContext.ExecuteCommandList(commandList, true);
                commandList.Dispose();
            }
        }

        DXFont GetFontForTextElement(TextElement element)
        {
            DXFont result = null;

            string fontKey = String.Format("{0}{1}{2}", element.Font.Name, element.Font.Size, element.Font.Style, element.AntiAliased);

            if (!_fontCache.TryGetValue(fontKey, out result))
            {
                result = Collect(new DXFont(_device, _deviceContext));
                result.Initialize(element.Font.Name, element.Font.Size, element.Font.Style, element.AntiAliased);
                _fontCache[fontKey] = result;
            }
            return result;
        }

        public bool Disposable;

        public void FlushCache()
        {
            
        }

        DXImage GetImageForImageElement(ImageElement element)
        {
            if (element.Dirty)
            {
                lock (element.SwitchLock)
                {
                    element.Image?.Dispose();
                    element.Image = null;

                    element.Bitmap?.Dispose();
                    element.Bitmap = element.NextBitmap;
                    element.NextBitmap = null;

                    element.Dirty = false;
                }
            }

            if (element.Image == null && element.Bitmap != null)
            {
                element.Image = Collect(new DXImage(_device, _deviceContext));
                element.Image.Initialise(element.Bitmap);
            }


            return element.Image;
        }

        /// <summary>
        /// Releases unmanaged and optionally managed resources
        /// </summary>
        /// <param name="disposing">true if disposing both unmanaged and managed</param>
        protected override void Dispose(bool disposing)
        {
            if (true)
            {
                _device = null;
            }
        }

        void SafeDispose(DisposeBase disposableObj)
        {
            if (disposableObj != null)
                disposableObj.Dispose();
        }
    }
}
