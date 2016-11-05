using clientVMP.other;
using GTA;
using network;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace clientVMP.classes
{
    public static class CEFTriggerEvent
    {

        public static void OnTick()
        {
            List<GTA.Native.OutputArgument> CEFTriggerParams =  CEF.GetEventTriggerParams().ToList();
            List<String> param = new List<string>();
            try
            {
                foreach (GTA.Native.OutputArgument c in CEFTriggerParams)
                {
                    if (c != null)
                    {
                        
                        string tmp = c.GetResult<string>();
                        param.Add(tmp);
                    }
                }
                Ligdren.NetLigdren.SendEventCEF(param);
            }
            catch (Exception ex)
            {
                utils.WriteLine(ex.Message);
            }
        }
    }
}
