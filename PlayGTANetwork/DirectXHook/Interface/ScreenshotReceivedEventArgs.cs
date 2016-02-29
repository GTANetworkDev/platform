using System;

namespace PlayGTANetwork.DirectXHook.Interface
{
    [Serializable]
    public class ScreenshotReceivedEventArgs: MarshalByRefObject
    {
        public Int32 ProcessId { get; set; }
        public Screenshot Screenshot { get; set; }

        public ScreenshotReceivedEventArgs(Int32 processId, Screenshot screenshot)
        {
            ProcessId = processId;
            Screenshot = screenshot;
        }
    }
}
