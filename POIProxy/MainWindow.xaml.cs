using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

using POIProxy.SignalRFun;
using SignalR;
using SignalR.Hubs;
using SignalR.Hosting.Self;

using System.Threading;

namespace POIProxy
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {

        public MainWindow()
        {
            InitializeComponent();


            Thread kernelThread = new Thread(StartKernelThread);
            kernelThread.Start();
        }

        private void StartKernelThread()
        {
            POIProxyGlobalVar.Kernel = new POIProxyKernel();
            POIProxyGlobalVar.Kernel.Start();
        }
    }
}
