using System;
using GTA;
using Xilium.CefGlue;

namespace GTANetwork.GUI.DirectXHook
{
    public class SwapchainHooker : Script
    {
        public SwapchainHooker()
        {
            if (CefUtil.DISABLE_CEF) return;

            var hooked = false;

            Present += (sender, args) =>
            {
                if (CEFManager.Draw && !Main.MainMenu.Visible && !Main._mainWarning.Visible && CEFManager.DirectXHook != null) CEFManager.DirectXHook.ManualPresentHook((IntPtr)sender);
            };

            Tick += (sender, args) =>
            {
                if (!hooked)
                {
                    base.AttachD3DHook();
                    hooked = true;
                }
            };
        }
    }
}