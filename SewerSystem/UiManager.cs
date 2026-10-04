using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace SewerSystem
{
    internal class UiManager
    {
        private static Thread _uiThread = null;
        
        public static void OpenInterface()
        {
            if (_uiThread == null || !_uiThread.IsAlive)
            {
                _uiThread = new Thread(() =>
                {
                    Thread.CurrentThread.SetApartmentState(ApartmentState.STA);
                    Thread.CurrentThread.IsBackground = true;

                    Dispatcher.Run();
                });
            }
            
        }
    }
}