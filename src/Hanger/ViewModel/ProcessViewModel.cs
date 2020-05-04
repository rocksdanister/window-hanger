using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ProcessModel;

namespace Hanger.ViewModel
{
    class ProcessViewModel
    {
        private ProcessInfo obj;
        public ProcessViewModel(ProcessInfo obj)
        {
            this.obj = obj;
            TabIndex = obj.Id;
            img = CreateBitMapSrcFromAppIcon();
        }

        public int TabIndex { get; private set; }

        public string ProcessName
        {
            get { return obj.Proc.ProcessName; }
            private set { }
        }

        private BitmapSource img { get; set; }
        public BitmapSource Img
        {
            get { return img; }
            private set
            {
                img = value;
            }
        }

        private BitmapSource CreateBitMapSrcFromAppIcon()
        {
            BitmapSource src;
            IntPtr res = IntPtr.Zero;
            if (Environment.Is64BitProcess)
            {
                NativeMethods.SHFILEINFO info = new NativeMethods.SHFILEINFO();
                res = NativeMethods.SHGetFileInfo(obj.Proc.MainModule.FileName, 0, ref info, (uint)Marshal.SizeOf(info), 0x100 | 0x000 | 0x010);
                //dont forget to call DestroyIcon
                src = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            }
            else
                src = BitmapSource.Create(1, 1, 1, 1, PixelFormats.BlackWhite, null, new byte[] { 0 }, 1);

            NativeMethods.DestroyIcon(res);
            return src;
        }

    }
}
