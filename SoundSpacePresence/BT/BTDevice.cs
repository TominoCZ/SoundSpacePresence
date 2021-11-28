using InTheHand.Net;
using InTheHand.Net.Bluetooth;
using InTheHand.Net.Sockets;
using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace SoundSpacePresence
{
    public class BTDevice : IDisposable
    {
        public BluetoothDeviceInfo Info { get; }
        public BluetoothClient Client { get; }
        public NetworkStream Stream { get; private set; }

        public event EventHandler OnConnected;
        public event EventHandler OnDisconnected;
        public event EventHandler<Exception> OnFailedConnecting;
        public event EventHandler<DataEventArgs> OnReceived;

        public bool Disposed { get; private set; }
        public bool Connected => !Disposed && Client != null && Client.Connected;
        /// <summary>
        /// The connection is automatically closed if no message is received within this time window
        /// </summary>
        public bool IsReady => !Disposed && Connected && Stream != null;
        /// <summary>
        /// The connection is automatically closed if no message is received within this time window
        /// </summary>
        public uint Timeout { get; set; } = 3500;
        /// <summary>
        /// The interval for the Thread reading incoming data
        /// </summary>
        public ushort ReadInterval { get; set; } = 50;
        /// <summary>
        /// The character that splits data segments.
        /// This is so that we get no data loss
        /// </summary>
        public char DataSeparator { get; set; } = '$';

        private Thread _receiveThread;
        private Stopwatch _sw = new Stopwatch();
        private string _buffer = "";

        [Obsolete]
        public BTDevice(BluetoothClient client, BluetoothDeviceInfo info)
        {
            Info = info;
            Client = client;

            _receiveThread = new Thread(ReceiveThread) { IsBackground = true };
            _receiveThread.Start();
        }

        private void ReceiveThread()
        {
            Thread.CurrentThread.Priority = ThreadPriority.AboveNormal;

            while (!Disposed)
            {
                if (IsReady)
                {
                    try
                    {
                        var splitter = DataSeparator;

                        if (Stream.DataAvailable)
                        {
                            var data = new byte[Client.Client.Available];
                            Stream.Read(data, 0, data.Length);

                            var received = Encoding.ASCII.GetString(data);

                            _buffer += received;

                            while (_buffer.Contains($"{splitter}"))
                            {
                                var index = _buffer.IndexOf(splitter);
                                var chunk = _buffer.Substring(0, index);

                                _buffer = _buffer.Substring(index + 1, _buffer.Length - (index + 1));

                                Receive(chunk);
                            }
                        }
                        else
                        {
                            if (_sw.Elapsed.TotalMilliseconds > Timeout)
                            {
                                Client.Close();

                                OnDisconnected?.Invoke(this, null);

                                Console.ForegroundColor = ConsoleColor.Red;
                                Console.WriteLine("Connection Timed Out!");
                                Console.ForegroundColor = ConsoleColor.White;
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        Console.ForegroundColor = ConsoleColor.Red;
                        Console.WriteLine($"Buffer Read Error: {e.Message}");
                        Console.ForegroundColor = ConsoleColor.White;
                    }
                }
                else
                {
                    _sw.Reset();
                }

                Thread.Sleep(ReadInterval);
            }
        }

        private void Receive(string data)
        {
            _sw.Restart();

            OnReceived?.Invoke(this, new DataEventArgs(data));
        }

        public bool Send(string text, string mode = "")
        {
            if (!IsReady)
                return false;

            lock (Stream)
            {
                if (!string.IsNullOrEmpty(mode))
                {
                    text = $"{mode}|{text}";
                }
                var data = Encoding.UTF8.GetBytes($"{text}{DataSeparator}");

                lock (Stream)
                {
                    Stream.Write(data, 0, data.Length);
                    Stream.Flush();
                }
            }
            return true;
        }

        public Task<bool> TryConnect()
        {
            if (Disposed || Connected)
                return Task.FromResult(false);

            try
            {
                Info.Refresh();

                for (int i = 0; i < 3; i++)
                {
                    try
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine($"Connecting..({i + 1})");
                        Console.ForegroundColor = ConsoleColor.White;

                        _buffer = "";

                        Client.Close();
                        Client.Connect(Info.DeviceAddress, BluetoothService.SerialPort);

                        if (Stream == null)
                        {
                            Stream = Client.GetStream();
                        }
                        else
                        {
                            lock (Stream)
                            {
                                Stream.Close();
                                Stream = Client.GetStream();
                            }
                        }

                        Send(""); //to clear out whatever data there is in the buffer on the device after we connect

                        break;
                    }
                    catch
                    {
                        if (i == 2)
                        {
                            Console.ForegroundColor = ConsoleColor.Red;
                            Console.WriteLine($"Failed to connect after {i + 1} attempts");
                            Console.ForegroundColor = ConsoleColor.White;

                            throw;
                        }
                    }

                    Task.Delay(250).Wait();
                }
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"Connected!");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("---");

                return Task.FromResult(true);
            }
            catch (SocketException ex)
            {
                string reason;
                switch (ex.ErrorCode)
                {
                    case 10048: // SocketError.AddressAlreadyInUse
                        reason = "This device is being used.";
                        break;
                    case 10049: // SocketError.AddressNotAvailable
                        reason = "Remote device is not available.";
                        break;
                    case 10013: // SocketError.AccessDenied:
                        reason = "Authentication required.";
                        break;
                    case 10060: // SocketError.TimedOut:
                        reason = "Timed-out.";
                        break;
                    default:
                        reason = null;
                        break;
                }

                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Connection Error: {reason} ({ex.ErrorCode})");
                Console.ForegroundColor = ConsoleColor.White;

                OnFailedConnecting?.Invoke(this, ex);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Internal Error: {e.StackTrace}");
                Console.ForegroundColor = ConsoleColor.White;

                OnFailedConnecting?.Invoke(this, e);
            }
            
            Client.Close();

            return Task.FromResult(false);
        }

        public override string ToString() => Info.DeviceName;

        public static bool TryCreate(string mac, out BTDevice device)
        {
            var client = new BluetoothClient();

            if (BluetoothAddress.TryParse(mac, out var address))
            {
                var info = new BluetoothDeviceInfo(address);
                if (info.LastSeen == DateTime.MinValue || info.InstalledServices.Count == 0 || info.DeviceName == "")
                {
                    device = null;
                    return false;
                }

#pragma warning disable CS0612 // Type or member is obsolete
                device = new BTDevice(client, info);
#pragma warning restore CS0612 // Type or member is obsolete

                return true;
            }

            device = null;
            return false;
        }

        public void Dispose()
        {
            Disposed = true;

            _receiveThread.Join();

            Client?.Dispose();
        }
    }
}