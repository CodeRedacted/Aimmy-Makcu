using System;
using System.Collections.Concurrent;
using System.IO.Ports;
using System.Threading;

namespace MouseMovementLibraries.MakcuSupport
{
    public enum MakcuMouseButton
    {
        Left = 0,
        Right = 1,
        Middle = 2,
        Mouse4 = 3,
        Mouse5 = 4
    }

    public class MakcuMouse : IDisposable
    {
        private SerialPort? _serialPort;
        private Thread? _writerThread;
        private readonly BlockingCollection<string> _commandQueue = new();
        private volatile bool _running = false;

        private readonly object _serialLock = new object();

        public string? PortName { get; private set; }
        public bool IsConnected => _serialPort != null && _serialPort.IsOpen;

        public MakcuMouse(string portName, int baudRate = 4000000)
        {
            _serialPort = new SerialPort(portName, baudRate)
            {
                ReadTimeout = 300,
                WriteTimeout = 500,
                DtrEnable = true,
                RtsEnable = true,
                NewLine = "\r\n"
            };

            _serialPort.Open();
            PortName = portName;

            _running = true;
            _writerThread = new Thread(WriterLoop)
            {
                IsBackground = true,
                Name = "MakcuWriterThread"
            };
            _writerThread.Start();
        }

        private void WriterLoop()
        {
            try
            {
                foreach (var cmd in _commandQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        if (_serialPort != null && _serialPort.IsOpen)
                        {
                            lock (_serialLock)
                            {
                                _serialPort.WriteLine(cmd);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[MakcuMouse] Error writing command: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MakcuMouse] Writer thread crashed: {ex.Message}");
            }
        }

        // ------------------- Public API -------------------

        public void Press(MakcuMouseButton button) =>
            _commandQueue.Add($"km.{GetButtonString(button)}(1)");

        public void Release(MakcuMouseButton button) =>
            _commandQueue.Add($"km.{GetButtonString(button)}(0)");

        public void Move(int x, int y) =>
            _commandQueue.Add($"km.move({x},{y})");

        public void MoveSmooth(int x, int y, int segments) =>
            _commandQueue.Add($"km.move({x},{y},{segments})");

        public void Scroll(int delta) =>
            _commandQueue.Add($"km.wheel({delta})");

        public string GetKmVersion()
        {
            try
            {
                if (_serialPort == null || !_serialPort.IsOpen) return string.Empty;

                lock (_serialLock)
                {
                    _serialPort.DiscardInBuffer();
                    _serialPort.WriteLine("km.version()");
                    Thread.Sleep(150);

                    string response = _serialPort.ReadExisting();
                    return response?.Trim() ?? string.Empty;
                }
            }
            catch
            {
                return string.Empty;
            }
        }

        // ------------------- Helpers -------------------

        private string GetButtonString(MakcuMouseButton button)
        {
            return button switch
            {
                MakcuMouseButton.Left => "left",
                MakcuMouseButton.Right => "right",
                MakcuMouseButton.Middle => "middle",
                MakcuMouseButton.Mouse4 => "x1",
                MakcuMouseButton.Mouse5 => "x2",
                _ => throw new ArgumentException($"Unsupported button: {button}")
            };
        }

        // ------------------- Cleanup -------------------

        public void Dispose()
        {
            _running = false;
            _commandQueue.CompleteAdding();

            try { _writerThread?.Join(200); } catch { /* ignore */ }

            if (_serialPort != null)
            {
                if (_serialPort.IsOpen)
                {
                    try { _serialPort.Close(); } catch { }
                }
                _serialPort.Dispose();
                _serialPort = null;
            }
        }

        // ------------------- Auto-detect -------------------

        /// <summary>
        /// Try to find a COM port that answers to "km.version()".
        /// Returns the port name if found, otherwise null.
        /// </summary>
        public static string? GetAvailablePort(int baudRate = 4000000)
        {
            foreach (var port in SerialPort.GetPortNames())
            {
                try
                {
                    using var sp = new SerialPort(port, baudRate)
                    {
                        ReadTimeout = 300,
                        WriteTimeout = 500,
                        DtrEnable = true,
                        RtsEnable = true,
                        NewLine = "\r\n"
                    };
                    sp.Open();
                    sp.DiscardInBuffer();
                    sp.WriteLine("km.version()");
                    Thread.Sleep(150);

                    string response = sp.ReadExisting();
                    if (!string.IsNullOrWhiteSpace(response) && response.Contains("km"))
                    {
                        return port;
                    }
                }
                catch
                {
                    // Ignore and try next port
                }
            }
            return null;
        }
    }
}
