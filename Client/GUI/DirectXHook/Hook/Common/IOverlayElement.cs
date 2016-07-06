using System;

namespace GTANetwork.GUI.DirectXHook.Hook.Common
{
    public interface IOverlayElement : ICloneable
    {
        bool Hidden { get; set; }

        void Frame();
    }
}
