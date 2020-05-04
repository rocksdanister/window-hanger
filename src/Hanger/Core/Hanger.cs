using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using static Hanger.WindowOperations;

namespace Hanger.Core
{
    public static class Hanger
    {       
        public static void PinWindow(IntPtr pgmhWnd, Window window, FrameworkElement element )
        {
            RemoveWindowFromTaskbar(pgmhWnd);
            BorderlessWinStyle(pgmhWnd);
            SetProgramToFramework(window, pgmhWnd, element);
        }
    }
}
