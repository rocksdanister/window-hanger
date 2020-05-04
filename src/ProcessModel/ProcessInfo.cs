using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ProcessModel
{
    public class ProcessInfo
    {
        public Process Proc { get; set; }
        /// <summary>
        /// unique id used by Hanger to identify the app(not appid.), tab index maybe?
        /// </summary>
        public int Id { get; set; }
        /// <summary>
        /// some cases require specific searches, process.mainwindowhandle is not always correct.
        /// </summary>
        public IntPtr HWnd { get; set; }
    }
}
