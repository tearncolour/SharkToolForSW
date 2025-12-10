using System;
using SolidWorks.Interop.sldworks;
using SolidWorks.Interop.swconst;

namespace SharkTools
{
    public static class ExampleCommand
    {
        public static void ShowHello(ISldWorks swApp)
        {
            if (swApp != null)
            {
                swApp.SendMsgToUser2("Hello from SharkTools!", (int)swMessageBoxIcon_e.swMbInformation, (int)swMessageBoxBtn_e.swMbOk);
            }
        }
    }
}
