using System;
using System.Windows;
using System.Windows.Threading;

namespace ManagerADO
{
    class WPFProxy
    {
        private Dispatcher _appDispathcer;

        public WPFProxy()
        {
            _appDispathcer = Application.Current.Dispatcher;
        }

        public void Invoke(Delegate method, params object[] args)
        {
            _appDispathcer.BeginInvoke(method, DispatcherPriority.DataBind, args);
        }
    }
}
