using System;
using PlayGTANetwork.DirectXHook.Interface;

namespace PlayGTANetwork.DirectXHook.Hook
{
    public interface IDXHook: IDisposable
    {
        CaptureInterface Interface
        {
            get;
            set;
        }
        CaptureConfig Config
        {
            get;
            set;
        }

        ScreenshotRequest Request
        {
            get;
            set;
        }

        void Hook();

        void Cleanup();
    }
}
