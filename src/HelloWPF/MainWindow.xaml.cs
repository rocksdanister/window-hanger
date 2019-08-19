using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using MahApps.Metro.Controls;

namespace Hanger
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        private class ProcessInfo
        {
            public Process process;
            public TabItem tabPage;
            public IntPtr handle; //some cases require specific searches, process.mainwindowhandle is not always adequate.
        }
        private ProcessInfo processInfo;
        //List of active tab processes.
        List<ProcessInfo> procInfo = new List<ProcessInfo>();

        IntPtr hWnd, shellHandle, desktopHandle;
        Process currProcess = null;
        int processID;
        Rectangle screenBounds;
        StaticPinvoke.RECT appBounds;
        DispatcherTimer dispatcherTimer = new DispatcherTimer();

        public MainWindow()
        {
            InitializeComponent();
            this.LocationChanged += MainWindow_LocationChanged;
            //this.SizeChanged += MainWindow_SizeChanged;
            this.Closing += MainWindow_Closing;
            this.StateChanged += MainWindow_StateChanged;

            shellHandle = StaticPinvoke.GetShellWindow();
            desktopHandle = StaticPinvoke.GetDesktopWindow();

            dispatcherTimer.Tick += new EventHandler(dispatcherTimer_Tick);
            dispatcherTimer.Interval = new TimeSpan(0, 0, 1);
            dispatcherTimer.Start();

        }

        private void MainWindow_StateChanged(object sender, EventArgs e)
        {
            ProcessInfo tmp = null;
            switch (this.WindowState)
            {
                case WindowState.Maximized: //app not maximisin mmm
                    Debug.WriteLine("maximised");
                    foreach (var item in procInfo)
                    {
                        if (item.tabPage == tabControl1.SelectedItem)
                        {
                            tmp = item;
                            //I legit feel bad about this... YIKES
                            WaitForMax(tmp);                            
                        }

                    }
                    if (tmp != null)
                    {
                        StaticPinvoke.ShowWindow(tmp.handle, (uint)StaticPinvoke.ShowWindowCommands.SW_SHOW);
                        StaticPinvoke.SetForegroundWindow(tmp.handle);
                        StaticPinvoke.SetFocus(tmp.handle);
                    }
                    break;
                case WindowState.Minimized:
                    Debug.WriteLine("minimized");
                    foreach (var item in procInfo)
                    {
                        StaticPinvoke.ShowWindow(item.handle, (uint)StaticPinvoke.ShowWindowCommands.SW_HIDE);
                    }
                    break;
                case WindowState.Normal: //todo use wndproc to preventapplication focus on minimize ->normal
                    Debug.WriteLine("normal");
                    foreach (var item in procInfo)
                    {
                        //StaticPinvoke.ShowWindow(item.handle, (uint)StaticPinvoke.ShowWindowCommands.SW_RESTORE);

                        if (tabControl1.SelectedItem.Equals(item.tabPage))
                        {
                            tmp = item;
                            //imsoooofuckingsorry:(
                            WaitForMax(tmp);
                        }
                    }
                    if (tmp != null)
                    {
                        StaticPinvoke.ShowWindow(tmp.handle, (uint)StaticPinvoke.ShowWindowCommands.SW_SHOW);
                        StaticPinvoke.SetForegroundWindow(tmp.handle);
                        StaticPinvoke.SetFocus(tmp.handle);
                    }
                    break;
            }
        }

        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {

        }

        private void MainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            dispatcherTimer.Stop();

            //undo taskbar hide
            foreach (var item in procInfo)
            {
                StaticPinvoke.ShowWindow(item.handle, (uint)0);
                StaticPinvoke.SetWindowLong(item.handle, GWL_EXSTYLE, StaticPinvoke.GetWindowLong(item.handle, GWL_EXSTYLE) | WS_EX_APPWINDOW);
                StaticPinvoke.ShowWindow(item.handle, (uint)5);
            }
        }

        private void MainWindow_LocationChanged(object sender, EventArgs e)
        {
            foreach (var item in procInfo)
            {
                if (item.tabPage == tabControl1.SelectedItem)
                {
                    StaticPinvoke.ShowWindow(item.handle, (uint)5);
                }
                else
                {
                    //StaticPinvoke.ShowWindow(item.handle, (uint)0);
                }
                SetWindowPosTab(item.handle);
            }
        }

        private void dispatcherTimer_Tick(object sender, EventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine(tabControl1.SelectedIndex);
            hWnd = StaticPinvoke.GetForegroundWindow();
            try
            {
                StaticPinvoke.GetWindowThreadProcessId(hWnd, out processID);
                currProcess = Process.GetProcessById(processID);
            }
            catch (Exception)
            {
                //ignore, other admin pgms etc
            }

            StaticPinvoke.GetWindowRect(hWnd, out appBounds);
            if (currProcess.ProcessName.Equals(AppDomain.CurrentDomain.FriendlyName.Split('.')[0], StringComparison.InvariantCultureIgnoreCase)
                    || currProcess.ProcessName.Equals("devenv", StringComparison.InvariantCultureIgnoreCase)
                    || IntPtr.Equals(currProcess.MainWindowHandle, IntPtr.Zero) // no gui
                    || currProcess.ProcessName.Equals("shellexperiencehost", StringComparison.InvariantCultureIgnoreCase) //notification tray etc
                    || currProcess.ProcessName.Equals("searchui", StringComparison.InvariantCultureIgnoreCase) //startmenu search etc
                    || currProcess.ProcessName.Equals("applicationframehost", StringComparison.InvariantCultureIgnoreCase) //uwp apps, system clock etc
                    || hWnd.Equals(desktopHandle)
                    || hWnd.Equals(shellHandle)
                    //|| StaticPinvoke.IsWindowVisible(hWnd)
            )
            {
                return;
            }

            //check if already used.
            foreach (var item in procInfo)
            {
                if (currProcess.Id == item.process.Id)
                    return;
            }
            
            if ((Math.Abs(appBounds.Top - this.Top) + Math.Abs(appBounds.Left - this.Left)) <= 500) //how close it is to topleft
            {


                if (currProcess.MainWindowHandle == IntPtr.Zero || hWnd == null || hWnd == IntPtr.Zero)
                    return;

                Debug.WriteLine("Pinning! " + currProcess.ProcessName);
                
                processInfo = new ProcessInfo();
                if (currProcess.ProcessName.Equals("explorer"))
                {
                    return; // skipping for now

                    hWnd = StaticPinvoke.FindWindowEx(IntPtr.Zero, IntPtr.Zero, "CabinetWClass", IntPtr.Zero);
                    try
                    {
                        StaticPinvoke.GetWindowThreadProcessId(hWnd, out processID);
                        currProcess = Process.GetProcessById(processID);
                    }
                    catch (Exception)
                    {
                        //ignore
                    }

                    processInfo.handle = hWnd;
                }
                else
                {
                    processInfo.handle = currProcess.MainWindowHandle;
                }

                processInfo.process = currProcess;
                processInfo.process.EnableRaisingEvents = true;
                CreateTabPage(currProcess);
                procInfo.Add(processInfo);
                procInfo[procInfo.Count - 1].process.Exited += Process_Exited1;

                PinWindow(procInfo[procInfo.Count - 1]);
                    
            }
        }

        private const int GWL_EXSTYLE = -0x14;
        private const int WS_EX_TOOLWINDOW = 0x0080;
        private const int WS_EX_TOPMOST = 0x0008;
        private const int WS_EX_APPWINDOW = 0x00040000;

        //IntPtr tmp;
        private void PinWindow(ProcessInfo obj)
        {
            
            IntPtr tmp = obj.handle;
            // Remove the Window from the Taskbar, not working for some apps
            StaticPinvoke.ShowWindow(tmp, (uint)StaticPinvoke.ShowWindowCommands.SW_HIDE);
            StaticPinvoke.SetWindowLong(tmp, GWL_EXSTYLE, StaticPinvoke.GetWindowLong(tmp, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_TOPMOST);
            StaticPinvoke.ShowWindow(tmp, (uint)StaticPinvoke.ShowWindowCommands.SW_SHOW);

            SetWindowPosTab(tmp);

            IntPtr windowHandle = new WindowInteropHelper(Application.Current.MainWindow).Handle;

            if (procInfo.Count == 1)
            {
                StaticPinvoke.SetForegroundWindow(windowHandle);
                //StaticPinvoke.SetFocus(windowHandle);

                StaticPinvoke.SetForegroundWindow(tmp);
                //StaticPinvoke.SetFocus(tmp);

                //tabControl1.SelectedIndex = tabControl1.SelectedIndex + 1;
                tabControl1.SelectedItem = procInfo[procInfo.Count - 1].tabPage;
                /*
                foreach (var item in procInfo)
                {
                     SetWindowPosTab(temp);
                }
                */
                StaticPinvoke.SetForegroundWindow(windowHandle);
                //StaticPinvoke.SetFocus(windowHandle);

                StaticPinvoke.SetForegroundWindow(tmp);
                //StaticPinvoke.SetFocus(tmp);
            }
            
        }


        private void Process_Exited1(object sender, EventArgs e)
        {
            //Debug.WriteLine("Exit Event Fired! ");
            ProcessInfo tmp = null;
            foreach (var item in procInfo)
            {
                item.process.Refresh();
                if (item.process.HasExited == true)
                {
                    this.Invoke(new Action(() => tabControl1.Items.Remove(item.tabPage)));
                    tmp = item;
                    break;
                }
            }
            if (tmp != null)
            {
                procInfo.Remove(tmp);
                if(procInfo.Count >0)
                {
                    //fuck my tmp fix
                }
            }
        }

        private void CreateTabPage(Process obj)
        {
            System.Drawing.Icon temp = System.Drawing.Icon.ExtractAssociatedIcon(obj.MainModule.FileName);

            System.Drawing.Bitmap bitmap = temp.ToBitmap();
            IntPtr hBitmap = bitmap.GetHbitmap();

            ImageSource Icon =
            Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap, IntPtr.Zero, Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            Image image = new Image {
                Source = Icon,
                Width = 40,
                Height = 40
            };
            RenderOptions.SetBitmapScalingMode(image, BitmapScalingMode.HighQuality);


            TabItem tabPage1 = new TabItem
            {
                Header = image,
                MaxWidth=48,
                MaxHeight = 48+12,
                Width = 48,
                Height = 48+12,
                Padding = new Thickness(0,4,0,8),
                ToolTip = obj.ProcessName
            };
            //tabControl1.Items.Add(tabPage1);
            processInfo.tabPage = tabPage1;
            tabPage1.PreviewMouseLeftButtonDown += TabItemClicked;
            tabControl1.Items.Add(processInfo.tabPage);
            tabControl1.SelectedItem = tabPage1;
        }

        private void TabItemClicked(object sender, MouseButtonEventArgs e)
        {
            Debug.WriteLine("index changed!:- " + tabControl1.SelectedIndex + " " + tabControl1.SelectedItem);
                foreach (var item in procInfo)
                {
                    if (item.tabPage == sender)
                    {
                        StaticPinvoke.ShowWindow(item.handle, (uint)5);
                        StaticPinvoke.SetForegroundWindow(item.handle);
                        StaticPinvoke.SetFocus(item.handle);
                    }
                    else
                    {
                        StaticPinvoke.ShowWindow(item.handle, (uint)0);
                    }
                }

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

                    StaticPinvoke.WINDOWPOS mwp;
                    mwp = (StaticPinvoke.WINDOWPOS)Marshal.PtrToStructure(lParam, typeof(StaticPinvoke.WINDOWPOS));

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
            ProcessInfo tmp = null;
            foreach (var item in procInfo)
            {
                if (item.tabPage == tabControl1.SelectedItem)
                {
                    tmp = item;
                }
                SetWindowPosTab(item.handle);
            }
            if(tmp != null)
            {
                StaticPinvoke.ShowWindow(tmp.handle, (uint)StaticPinvoke.ShowWindowCommands.SW_SHOW);
                StaticPinvoke.SetForegroundWindow(tmp.handle);
                StaticPinvoke.SetFocus(tmp.handle);
            }
        
        }

        private void SetWindowPosTab(IntPtr handle) {
            int xOffSet = 60;
            int yOffSet = 5;
           
            StaticPinvoke.SetWindowPos(handle, 0, (int)tabControl1.PointToScreen(new Point(0, 0)).X + xOffSet, (int)tabControl1.PointToScreen(new Point(0, 0)).Y + yOffSet, (int)tabGrid.ActualWidth - xOffSet - 5, (int)tabGrid.ActualHeight - yOffSet - 5, 0x0010);
            
        }
        private async void WaitForMax(ProcessInfo tmp) {
            await Task.Delay(50);
            SetWindowPosTab(tmp.handle);
        }
        
        #endregion WINDOWS_MSG

    }
}
