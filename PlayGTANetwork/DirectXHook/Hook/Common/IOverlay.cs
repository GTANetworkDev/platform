using System.Collections.Generic;

namespace PlayGTANetwork.DirectXHook.Hook.Common
{
    internal interface IOverlay: IOverlayElement
    {
        List<IOverlayElement> Elements { get; set; }
    }
}
