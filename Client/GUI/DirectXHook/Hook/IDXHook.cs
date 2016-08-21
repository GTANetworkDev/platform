using System;

namespace GTANetwork.GUI.DirectXHook.Hook
{
    public interface IDXHook: IDisposable
    {
        void Hook();

        void Cleanup();
    }
}
