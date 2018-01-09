using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ScreenManager;
using System.Diagnostics;

namespace testApp
{
    class Program
    {
        static void Main(string[] args)
        {
            
            foreach(Display.ScreenInfo monitor in Display.Screens)
            {
                //output Screen Information
                Console.WriteLine("Screen Scaling: " + monitor.Scaling);
                Console.WriteLine("Device Name: " + monitor.DeviceName);
                Console.WriteLine("Is Primary: " + monitor.IsPrimaryScreen);
                Console.WriteLine("Work Area: " + monitor.WorkArea);
                Console.WriteLine("Monitor Area: " + monitor.MonitorArea);
                Console.WriteLine("Native Work Area: " + monitor.NativeWorkArea);
                Console.WriteLine("Native Area: " + monitor.NativeArea);
            }

            Process[] myProcesses = Process.GetProcessesByName("powershell");
            List<IntPtr> windowHandles = new List<IntPtr>();
            foreach(Process myProcess in myProcesses)
            {
                if(myProcess.MainWindowHandle != IntPtr.Zero)
                {
                    windowHandles.Add(myProcess.MainWindowHandle);
                }
            }

            List<Display.WindowInfo> WindowInfos = new List<Display.WindowInfo>();
            foreach(IntPtr windowHandle in windowHandles)
            {
                WindowInfos.Add(Display.GetWindowInfo(windowHandle));

                //example to show all Window details
                Display.WindowInfo window = Display.GetWindowInfo(windowHandle);
                Console.WriteLine("Handle: " + window.hwnd);
                Console.WriteLine("Window Rect: " + window.WindowRect);
                Console.WriteLine("Frame Rect : " + window.ExtendedFrameBounds);
                Console.WriteLine("Scaling: " + window.ScalingFactor);
                Console.WriteLine("Size: " + window.SizeWindow);
                Console.WriteLine("Size Extended Frame Bounds: " + window.SizeExtendedFrameBounds);
                Console.WriteLine("Border Left: " + window.Border.left);
                Console.WriteLine("Border Right: " + window.Border.right);
                Console.WriteLine("Border Top: " + window.Border.top);
                Console.WriteLine("Border Bottom: " + window.Border.bottom);
                Console.WriteLine("Screen Name: " + window.ScreenName);
                
                //move & resize the window, including border and scaling
                Display.MoveWindow(window.hwnd, 1 - window.Border.left, 1 - window.Border.top, (int)((400+window.Border.left+window.Border.right)/window.ScalingFactor), (int)((500+window.Border.top+window.Border.bottom)/window.ScalingFactor), true);
            }
            
            //Console.WriteLine(myProcess[0].MainWindowHandle);
            Console.ReadLine();
        }
    }
}
