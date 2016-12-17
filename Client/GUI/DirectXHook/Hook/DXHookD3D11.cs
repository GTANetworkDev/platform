using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using GTANetwork.GUI.DirectXHook.Hook.Common;
using GTANetwork.GUI.DirectXHook.Hook.DX11;
using GTANetwork.Util;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.DXGI;

namespace GTANetwork.GUI.DirectXHook.Hook
{
    enum D3D11DeviceVTbl : short
    {
        // IUnknown
        QueryInterface = 0,
        AddRef = 1,
        Release = 2,

        // ID3D11Device
        CreateBuffer = 3,
        CreateTexture1D = 4,
        CreateTexture2D = 5,
        CreateTexture3D = 6,
        CreateShaderResourceView = 7,
        CreateUnorderedAccessView = 8,
        CreateRenderTargetView = 9,
        CreateDepthStencilView = 10,
        CreateInputLayout = 11,
        CreateVertexShader = 12,
        CreateGeometryShader = 13,
        CreateGeometryShaderWithStreamOutput = 14,
        CreatePixelShader = 15,
        CreateHullShader = 16,
        CreateDomainShader = 17,
        CreateComputeShader = 18,
        CreateClassLinkage = 19,
        CreateBlendState = 20,
        CreateDepthStencilState = 21,
        CreateRasterizerState = 22,
        CreateSamplerState = 23,
        CreateQuery = 24,
        CreatePredicate = 25,
        CreateCounter = 26,
        CreateDeferredContext = 27,
        OpenSharedResource = 28,
        CheckFormatSupport = 29,
        CheckMultisampleQualityLevels = 30,
        CheckCounterInfo = 31,
        CheckCounter = 32,
        CheckFeatureSupport = 33,
        GetPrivateData = 34,
        SetPrivateData = 35,
        SetPrivateDataInterface = 36,
        GetFeatureLevel = 37,
        GetCreationFlags = 38,
        GetDeviceRemovedReason = 39,
        GetImmediateContext = 40,
        SetExceptionMode = 41,
        GetExceptionMode = 42,
    }

    /// <summary>
    /// Direct3D 11 Hook - this hooks the SwapChain.Present to take screenshots
    /// </summary>
    internal class DXHookD3D11: BaseDXHook
    {
        const int D3D11_DEVICE_METHOD_COUNT = 43;
        private int Width;
        private int Height;

        public DXHookD3D11(int w, int h)
            : base()
        {
            Width = w;
            Height = h;
        }

        List<IntPtr> _d3d11VTblAddresses = null;
        List<IntPtr> _dxgiSwapChainVTblAddresses = null;

        Hook<DXGISwapChain_PresentDelegate> DXGISwapChain_PresentHook = null;
        Hook<DXGISwapChain_ResizeTargetDelegate> DXGISwapChain_ResizeTargetHook = null;

        object _lock = new object();

        #region Internal device resources
        SharpDX.Direct3D11.Device _device;
        SwapChain _swapChain;
        SharpDX.Windows.RenderForm _renderForm;
        Texture2D _resolvedRTShared;
        SharpDX.DXGI.KeyedMutex _resolvedRTSharedKeyedMutex;
        ShaderResourceView _resolvedSharedSRV;
        ScreenAlignedQuadRenderer _saQuad;
        Texture2D _finalRT;
        Texture2D _resizedRT;
        RenderTargetView _resizedRTV;
        #endregion

        Query _query;
        bool _queryIssued;
        bool _finalRTMapped;

        #region Main device resources
        Texture2D _resolvedRT;
        SharpDX.DXGI.KeyedMutex _resolvedRTKeyedMutex;
        SharpDX.DXGI.KeyedMutex _resolvedRTKeyedMutex_Dev2;
        //ShaderResourceView _resolvedSRV;
        #endregion

        protected override string HookName
        {
            get
            {
                return "DXHookD3D11";
            }
        }

        public override void Hook()
        {
            this.DebugMessage("Hook: Begin");
            if (_d3d11VTblAddresses == null)
            {
                _d3d11VTblAddresses = new List<IntPtr>();
                _dxgiSwapChainVTblAddresses = new List<IntPtr>();

                #region Get Device and SwapChain method addresses
                // Create temporary device + swapchain and determine method addresses
                _renderForm = ToDispose(new SharpDX.Windows.RenderForm());
                this.DebugMessage("Hook: Before device creation");
                SharpDX.Direct3D11.Device.CreateWithSwapChain(
                    DriverType.Hardware,
                    DeviceCreationFlags.BgraSupport,
                    DXGI.CreateSwapChainDescription(_renderForm.Handle),
                    out _device,
                    out _swapChain);

                ToDispose(_device);
                ToDispose(_swapChain);

                if (_device != null && _swapChain != null)
                {
                    this.DebugMessage("Hook: Device created");
                    _d3d11VTblAddresses.AddRange(GetVTblAddresses(_device.NativePointer, D3D11_DEVICE_METHOD_COUNT));
                    _dxgiSwapChainVTblAddresses.AddRange(GetVTblAddresses(_swapChain.NativePointer, DXGI.DXGI_SWAPCHAIN_METHOD_COUNT));
                }
                else
                {
                    this.DebugMessage("Hook: Device creation failed");
                }
                #endregion
            }

            // We will capture the backbuffer here
            DXGISwapChain_PresentHook = new Hook<DXGISwapChain_PresentDelegate>(
                _dxgiSwapChainVTblAddresses[(int)DXGI.DXGISwapChainVTbl.Present],
                new DXGISwapChain_PresentDelegate(PresentHook),
                this);
            
            
            // We will capture target/window resizes here
            DXGISwapChain_ResizeTargetHook = new Hook<DXGISwapChain_ResizeTargetDelegate>(
                _dxgiSwapChainVTblAddresses[(int)DXGI.DXGISwapChainVTbl.ResizeTarget],
                new DXGISwapChain_ResizeTargetDelegate(ResizeTargetHook),
                this);
                
            /*
             * Don't forget that all hooks will start deactivated...
             * The following ensures that all threads are intercepted:
             * Note: you must do this for each hook.
             */
            DXGISwapChain_PresentHook.Activate();
            
            DXGISwapChain_ResizeTargetHook.Activate();

            Hooks.Add(DXGISwapChain_PresentHook);
            Hooks.Add(DXGISwapChain_ResizeTargetHook);
        }

        public override void Cleanup()
        {
            try
            {
                if (OverlayEngine != null)
                {
                    OverlayEngine.Dispose();
                    OverlayEngine = null;
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// The IDXGISwapChain.Present function definition
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int DXGISwapChain_PresentDelegate(IntPtr swapChainPtr, int syncInterval, /* int */ SharpDX.DXGI.PresentFlags flags);

        /// <summary>
        /// The IDXGISwapChain.ResizeTarget function definition
        /// </summary>
        /// <param name="device"></param>
        /// <returns></returns>
        [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
        delegate int DXGISwapChain_ResizeTargetDelegate(IntPtr swapChainPtr, ref ModeDescription newTargetParameters);

        /// <summary>
        /// Hooked to allow resizing a texture/surface that is reused. Currently not in use as we create the texture for each request
        /// to support different sizes each time (as we use DirectX to copy only the region we are after rather than the entire backbuffer)
        /// </summary>
        /// <param name="swapChainPtr"></param>
        /// <param name="newTargetParameters"></param>
        /// <returns></returns>
        int ResizeTargetHook(IntPtr swapChainPtr, ref ModeDescription newTargetParameters)
        {
            // Dispose of overlay engine (so it will be recreated with correct renderTarget view size)
            if (OverlayEngine != null)
            {
                OverlayEngine.Dispose();
                OverlayEngine = null;
            }

            return DXGISwapChain_ResizeTargetHook.Original(swapChainPtr, ref newTargetParameters);
        }

        public void SetBitmap(Bitmap bt)
        {
            if (OverlayEngine == null || OverlayEngine.Overlays == null) return;

            if (OverlayEngine.Overlays.Count == 0)
                OverlayEngine.Overlays.Add(new Overlay());

            if (this.OverlayEngine.Overlays[0].Elements.Count == 1)
                OverlayEngine.Overlays[0].Elements.Add(new Common.ImageElement(new Bitmap(Width, Height))
                {
                    Location = new System.Drawing.Point(0, 0)
                });
            else if (OverlayEngine.Overlays[0].Elements.Count == 0)
            {
                OverlayEngine.Overlays[0].Elements.Add(
                    new Common.TextElement(new System.Drawing.Font("Times New Roman", 22))
                    {
                        Text = "*",
                        Location = new System.Drawing.Point(0, 0),
                        Color = System.Drawing.Color.Red,
                        AntiAliased = false
                    });

                OverlayEngine.Overlays[0].Elements.Add(new Common.ImageElement(new Bitmap(Width, Height))
                {
                    Location = new System.Drawing.Point(0, 0)
                });
            }

            ((ImageElement) this.OverlayEngine.Overlays[0].Elements[1]).Bitmap = null;
            ((ImageElement)this.OverlayEngine.Overlays[0].Elements[1]).Bitmap = bt;
            this.OverlayEngine.FlushCache();
        }

        public void SetText(string txt)
        {
            if (((TextElement)this.OverlayEngine.Overlays[0].Elements[0]) != null)
                ((TextElement) this.OverlayEngine.Overlays[0].Elements[0]).Text = txt;
        }

        private int counter;

        public bool NewSwapchain;

        /// <summary>
        /// Our present hook that will grab a copy of the backbuffer when requested. Note: this supports multi-sampling (anti-aliasing)
        /// </summary>
        /// <param name="swapChainPtr"></param>
        /// <param name="syncInterval"></param>
        /// <param name="flags"></param>
        /// <returns>The HRESULT of the original method</returns>
        int PresentHook(IntPtr swapChainPtr, int syncInterval, SharpDX.DXGI.PresentFlags flags)
        {
            SwapChain swapChain = (SharpDX.DXGI.SwapChain)swapChainPtr;

            if (swapChainPtr != IntPtr.Zero)
            {
                try
                {
                    #region Draw overlay (after screenshot so we don't capture overlay as well)

                    // Initialise Overlay Engine
                    if (_swapChainPointer != swapChain.NativePointer || OverlayEngine == null)
                    {
                        NewSwapchain = true;

                        if (OverlayEngine != null)
                            OverlayEngine.Dispose();
                        OverlayEngine = new DX11.DXOverlayEngine();
                        OverlayEngine.Overlays.Add(new DirectXHook.Hook.Common.Overlay
                        {
                            Elements =
                            {
                                new Common.TextElement(new System.Drawing.Font("Times New Roman", 22))
                                {
                                    Text = "*",
                                    Location = new System.Drawing.Point(0, 0),
                                    Color = System.Drawing.Color.Red,
                                    AntiAliased = false
                                },
                                new Common.ImageElement(new Bitmap(Width, Height))
                                {
                                    Location = new System.Drawing.Point(0, 0)
                                },
                            }
                        });
                        OverlayEngine.Initialise(swapChain);

                        _swapChainPointer = swapChain.NativePointer;
                    }

                    // Draw Overlay(s)

                    if (OverlayEngine != null)
                    {
                        foreach (var overlay in OverlayEngine.Overlays)
                            overlay.Frame();
                        OverlayEngine.Draw();
                    }

                    #endregion
                }
                catch (Exception e)
                {
                    // If there is an error we do not want to crash the hooked application, so swallow the exception
                    LogManager.DebugLog("PresentHook: Exeception: " + e.GetType().FullName + ": " + e.ToString());
                    LogManager.LogException(e, "PresentHook");
                    //return unchecked((int)0x8000FFFF); //E_UNEXPECTED
                }
            }

            // As always we need to call the original method, note that EasyHook will automatically skip the hook and call the original method
            // i.e. calling it here will not cause a stack overflow into this function
            return DXGISwapChain_PresentHook.Original(swapChainPtr, syncInterval, flags);
        }

        public DXOverlayEngine OverlayEngine;

        IntPtr _swapChainPointer = IntPtr.Zero;
        
    }
}
