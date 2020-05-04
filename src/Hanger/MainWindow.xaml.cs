using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Hanger.ViewModel;

namespace Hanger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        DispatcherTimer dispatcherTimer = new DispatcherTimer();

        ObservableCollection<ProcessViewModel> items = new ObservableCollection<ProcessViewModel>(); 
        public MainWindow()
        {
            InitializeComponent();
            //this.LocationChanged += MainWindow_LocationChanged;
            //this.SizeChanged += MainWindow_SizeChanged;
            //this.StateChanged += MainWindow_StateChanged;
            this.Closing += MainWindow_Closing;

            //bindings
            appListBox.ItemsSource = items;

            //timer
            dispatcherTimer.Tick += new EventHandler(DispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();
        }

        private async void DispatcherTimer_Tick(object sender, EventArgs e)
        {
            SolidColorBrush brushLight = new SolidColorBrush(Color.FromRgb(90, 90, 90));
            SolidColorBrush brushDark = new SolidColorBrush(Color.FromRgb(37, 37, 37));

            var hWnd = NativeMethods.GetForegroundWindow();
            Process currProcess = null;
            int processID = 0;
            try
            {
                NativeMethods.GetWindowThreadProcessId(hWnd, out processID);
                currProcess = Process.GetProcessById(processID);
            }
            catch (Exception)
            {
                //ignore, other admin pgms etc
                return;
            }

            NativeMethods.RECT appBounds;
            NativeMethods.GetWindowRect(hWnd, out appBounds);
            //todo: check for classname instead.
            if (currProcess.ProcessName.Equals(AppDomain.CurrentDomain.FriendlyName.Split('.')[0], StringComparison.InvariantCultureIgnoreCase)
                    || currProcess.ProcessName.Equals("devenv", StringComparison.InvariantCultureIgnoreCase)
                    || currProcess.ProcessName.Equals("discord", StringComparison.InvariantCultureIgnoreCase) //tray app, discord buggy
                    || currProcess.ProcessName.Equals("taskmgr", StringComparison.InvariantCultureIgnoreCase) //taskmanager
                    || IntPtr.Equals(currProcess.MainWindowHandle, IntPtr.Zero) // no gui
                    || currProcess.ProcessName.Equals("shellexperiencehost", StringComparison.InvariantCultureIgnoreCase) //notification tray etc
                    || currProcess.ProcessName.Equals("searchui", StringComparison.InvariantCultureIgnoreCase) //startmenu search etc
                    || currProcess.ProcessName.Equals("applicationframehost", StringComparison.InvariantCultureIgnoreCase) //uwp apps, system clock etc
                    || hWnd.Equals(NativeMethods.GetShellWindow())
                    || hWnd.Equals(NativeMethods.GetDesktopWindow())
                    || NativeMethods.IsZoomed(hWnd) //fullscreen
                    || NativeMethods.IsIconic(hWnd) //minmized, some apps can retain foreground when miminimzed to tray like discord
                    || currProcess.ProcessName.Equals("explorer", StringComparison.InvariantCultureIgnoreCase) //notification tray etc
            )
            {
                return;
            }

    
            Debug.WriteLine("this.top :- " + this.Top + " " + appBounds.Top + " this.left:- " + this.Left + " " + appBounds.Left);
            if ((Math.Abs(appBounds.Top - this.Top) + Math.Abs(appBounds.Left - this.Left)) <= 500 && this.Top < appBounds.Top && this.Left < appBounds.Left) //how close it is to topleft
            {

                if (currProcess.MainWindowHandle == IntPtr.Zero || hWnd == null || hWnd == IntPtr.Zero)
                    return;

                Debug.WriteLine("Pinning! " + currProcess.ProcessName);
                //wait before pinnin
                if (dispatcherTimer.IsEnabled == true)
                    dispatcherTimer.Stop();
                this.Background = brushLight;
                this.Title = "PIN:- " + currProcess.ProcessName;
                for (int i = 0; i < 7; i++)
                {
                    await Task.Delay(100);
                    NativeMethods.GetWindowRect(hWnd, out appBounds);
                    if (!((Math.Abs(appBounds.Top - this.Top) + Math.Abs(appBounds.Left - this.Left)) <= 500 && this.Top < appBounds.Top && this.Left < appBounds.Left)) //window moved away after waitin
                    {
                        if (dispatcherTimer.IsEnabled == false)
                            dispatcherTimer.Start();
                        this.Title = "Hanger : Drag applications to pin";
                        this.Background = brushDark;
                        return;
                    }
                }
                this.Title = "Hanger : Drag applications to pin";
                this.Background = brushDark;

                Hanger.Core.Hanger.PinWindow(currProcess.MainWindowHandle, this, PreviewBorder);
                items.Add(new ProcessViewModel(new ProcessModel.ProcessInfo() { Proc = currProcess, HWnd = currProcess.MainWindowHandle, Id = appListBox.SelectedIndex + 1 }));

                //restarting
                if (dispatcherTimer.IsEnabled == false)
                    dispatcherTimer.Start();
            }
        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();
            /*
            //undo taskbar hide
            foreach (var item in procInfo)
            {
                NativeMethods.ShowWindow(item.handle, (uint)0);
                NativeMethods.SetWindowLong(item.handle, GWL_EXSTYLE, WS_EX_APPWINDOW);
                NativeMethods.ShowWindow(item.handle, (uint)5);
            }
            */
        }

        #region WINDOWS_MSG

        protected override void OnSourceInitialized(EventArgs e)
        {           
            base.OnSourceInitialized(e);
            HwndSource source = PresentationSource.FromVisual(this) as HwndSource;
            source.AddHook(WndProc);
            
        }
        

        private const int WM_WINDOWPOSCHANGING = 0x0046;
        const int WM_SYSCOMMAND = 0x0112;
        const int SC_MAXIMIZE = 0xF030;
        const int WM_EXITSIZEMOVE = 0x0232;
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            switch (msg)
            {
                case WM_WINDOWPOSCHANGING:

                    NativeMethods.WINDOWPOS mwp;
                    mwp = (NativeMethods.WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(NativeMethods.WINDOWPOS));

                    mwp.flags = mwp.flags | 4; //SWP_NOZORDER, prevent focus change to wpf window when titlebar clicked.

                    Marshal.StructureToPtr(mwp, lParam, true); //struct update
                    break;
                    /*
                    case WM_SYSCOMMAND: //window maximise
                    if (wParam == (IntPtr)SC_MAXIMIZE)
                    {
                        Debug.WriteLine("SC_MAXIMIZE");
                        ResizeEnd();
                    }
                    break;
                    */
                case WM_EXITSIZEMOVE: //resize
                    ResizeEnd();
                    break;
                
                    
            }
            return IntPtr.Zero;
        }

        private void ResizeEnd()
        {
            Debug.WriteLine("resize end called");       
        }

        #endregion WINDOWS_MSG

    }
}
