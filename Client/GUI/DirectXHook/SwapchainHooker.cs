using System;
using GTA;

namespace GTANetwork.GUI.DirectXHook
{
    public class SwapchainHooker : Script
    {
        public SwapchainHooker()
        {
            if (CefUtil.DISABLE_CEF) return;

            bool hooked = false;

            Present += (sender, args) =>
            {
                if (CEFManager.DirectXHook != null && !Main.MainMenu.Visible)
                    CEFManager.DirectXHook.ManualPresentHook((IntPtr) sender);
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