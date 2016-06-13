using System.Collections.Generic;

namespace CEFInjector.DirectXHook.Hook.Common
{
    internal interface IOverlay: IOverlayElement
    {
        List<IOverlayElement> Elements { get; set; }
    }
}
