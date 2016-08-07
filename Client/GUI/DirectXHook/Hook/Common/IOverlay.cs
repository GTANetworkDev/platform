using System.Collections.Generic;

namespace GTANetwork.GUI.DirectXHook.Hook.Common
{
    internal interface IOverlay: IOverlayElement
    {
        List<IOverlayElement> Elements { get; set; }
    }
}
