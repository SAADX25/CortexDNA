using System.Runtime.InteropServices;

namespace CortexDNA.Core.Startup
{
    internal static class StartupCom
    {
        public static void Release(object? comObject)
        {
            if (comObject == null) return;
            try
            {
                if (Marshal.IsComObject(comObject))
                    Marshal.FinalReleaseComObject(comObject);
            }
            catch { }
        }
    }
}
