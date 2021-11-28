using InTheHand.Net;
using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;

namespace SoundSpacePresence
{
    internal class Program
    {
        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);
        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hwnd);
        [DllImport("gdi32.dll")]
        static extern int GetPixel(IntPtr hdc, int nXPos, int nYPos);
        [DllImport("user32.dll")]
        static extern bool GetCursorPos(ref Point lpPoint);
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetClientRect(IntPtr ptr, out RECT lpRect);
        [DllImport("user32.dll")]
        static extern bool ClientToScreen(IntPtr ptr, ref Point lpPoint);
        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        private static string _procName = "RobloxPlayerBeta";
        private static IntPtr _handle = IntPtr.Zero;
        private static BTDevice _device;
        private static Process _process = Process.GetCurrentProcess();
        private static string Title = "Sound Space Presence Client";

        static void Main(string[] args)
        {
            Console.Title = Title;

            string mac = "";

            if (args.Length == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Please put the MAC address as a launch parameter.");
                Console.ReadLine();
                return;
            }
            else
            {
                mac = args[0];

                if (!BluetoothAddress.TryParse(mac, out _))
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"'{mac}' is not a valid MAC address.");
                    Console.ReadLine();
                    return;
                }
            }

            var process = Process.GetCurrentProcess();
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;

            Console.ForegroundColor = ConsoleColor.White;
            Console.SetOut(TextWriter.Synchronized(Console.Out));

            new Thread(ProcessLoop).Start();
            new Thread(InputLoop).Start();

            if (BTDevice.TryCreate(mac, out var device))
            {
                using (device)
                {
                    Console.ForegroundColor = ConsoleColor.White;
                    Console.WriteLine($"Connecting to device '{device.Info.DeviceName}'");

                    _device = device;

                    device.OnReceived += OnData;
                    while (!device.Disposed)
                    {
                        var task = device.TryConnect();
                        if (task.Result)
                        {
                            ClientLoop(_device);
                        }
                    }
                }
            }

            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("< CLIENT STOPPED >");
            Console.ReadLine();
        }

        static void ProcessLoop()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;

            while (!_process.HasExited)
            {
                var tbl = Process.GetProcessesByName(_procName);
                var first = tbl.FirstOrDefault();

                foreach (var proc in tbl)
                {
                    if (first.Id != proc.Id)
                        proc.Dispose();
                }

                if (first != null)
                    _handle = first.MainWindowHandle;

                Thread.Sleep(750);
            }
        }

        static void InputLoop()
        {
            Thread.CurrentThread.Priority = ThreadPriority.Lowest;

            while (!_process.HasExited)
            {
                var data = Console.ReadLine();

                if (!string.IsNullOrEmpty(data))
                {
                    try
                    {
                        _device?.Send(data);
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine("ERROR: Failed to send data: " + e);
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
        }

        static void ClientLoop(BTDevice device)
        {
            var lastColor = Color.Empty;

            new Thread(() =>
            {
                Thread.CurrentThread.Priority = ThreadPriority.Lowest;

                var lastTime = DateTime.Now;
                var fps = 0;

                while (device.Connected)
                {
                    var gotPixel = GetPixel(out var color, _handle);
                    if (gotPixel && (color.R != lastColor.R || color.G != lastColor.G || color.B != lastColor.B))
                    {
                        try
                        {
                            device.Send($"{color.R},{color.G},{color.B}", "w");

                            lastColor = color;
                        }
                        catch (Exception e)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine("ERROR: Failed to send data: " + e);
                        }
                    }

                    fps++;

                    if (DateTime.Now.Subtract(lastTime).TotalSeconds >= 1)
                    {
                        Console.Title = $"{Title} [{_device}, {fps} FPS]";

                        fps = 0;
                        lastTime = DateTime.Now;
                    }

                    if (!gotPixel)
                        Thread.Sleep(20);
                }
            }).Start();

            while (device.Connected)
                Thread.Sleep(100);
        }

        private static bool GetPixel(out Color c, IntPtr hwnd)
        {
            c = Color.Empty;

            if (hwnd == IntPtr.Zero)
                return false;

            RECT crect;

            if (!GetClientRect(hwnd, out crect))
            {
                return false;
            }

            Point pos = Point.Empty;

            if (!ClientToScreen(hwnd, ref pos))
            {
                return false;
            }

            IntPtr hdc = GetDC(hwnd);
            int cr = GetPixel(hdc, pos.X + crect.Right - 1, pos.Y + crect.Bottom - 1);
            ReleaseDC(IntPtr.Zero, hdc);

            c = Color.FromArgb((cr & 0x000000FF),
                (cr & 0x0000FF00) >> 8,
                (cr & 0x00FF0000) >> 16);

            return true;
        }

        static void OnData(object sender, DataEventArgs evt)
        {
            var device = (BTDevice)sender;
            var data = evt.Data.Trim();

            if (data.Length == 0)
                return;

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{device.Info.DeviceName}] ");
            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine(data);
        }
    }
}