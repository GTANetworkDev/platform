using System;
using GTA;

namespace GTANetwork.GUI.DirectXHook
{
    public class SwapchainHooker : Script
    {
        public SwapchainHooker()
        {
            Present += SwapchainEventHandler;
            
            bool hooked = false;

            Tick += (sender, args) =>
            {
                if (!hooked)
                {
                    base.AttachD3DHook();

                    hooked = true;
                }
            };
        }

        private void SwapchainEventHandler(object sender, EventArgs e)
        {
            IntPtr swapchain = (IntPtr) sender;

            if (CEFManager.DirectXHook != null && !Main.MainMenu.Visible)
            {
                CEFManager.DirectXHook.ManualPresentHook(swapchain);
            }
        }
    }
}