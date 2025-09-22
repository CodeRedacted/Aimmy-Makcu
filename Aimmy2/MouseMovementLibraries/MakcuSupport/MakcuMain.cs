using System;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using Visuality;

namespace MouseMovementLibraries.MakcuSupport
{
    internal class MakcuMain
    {
        public static MakcuMouse? MakcuInstance { get; private set; }

        private static bool _isMakcuLoaded = false;

        public static async Task<bool> Load(string? portName = null, int baudRate = 4000000)
        {
            try
            {
                // Pick port
                string? portToUse = portName;
                if (string.IsNullOrEmpty(portToUse))
                {
                    portToUse = MakcuMouse.GetAvailablePort(baudRate);
                    if (string.IsNullOrEmpty(portToUse))
                    {
                        MessageBox.Show(
                            "No Makcu device found on any COM port.\nPlease connect the device and try again.",
                            "Makcu Connection Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return false;
                    }
                }

                // Clean up any old instance
                DisposeInstance();

                // Create new instance (opens serial port immediately)
                MakcuInstance = new MakcuMouse(portToUse, baudRate);

                // Verify connection
                string version = MakcuInstance.GetKmVersion();
                if (string.IsNullOrWhiteSpace(version))
                {
                    MessageBox.Show(
                        $"Makcu initialized on {portToUse}, but no version response.\nEnsure firmware is compatible with 'km.version()'.",
                        "Makcu Warning",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }

                _isMakcuLoaded = true;
                new NoticeBar($"MAKCU initialized on {portToUse} @ {baudRate} baud", 5000).Show();
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Critical Makcu initialization error: {ex.Message}\n{ex.StackTrace}",
                    "Makcu Critical Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                _isMakcuLoaded = false;
                DisposeInstance();
                return false;
            }
        }

        public static void Unload()
        {
            DisposeInstance();
            _isMakcuLoaded = false;
            Console.WriteLine("MakcuMain: Makcu unloaded.");
        }

        public static void DisposeInstance()
        {
            try
            {
                MakcuInstance?.Dispose();
            }
            catch { /* ignore */ }
            MakcuInstance = null;
            _isMakcuLoaded = false;
            Console.WriteLine("MakcuMain: Instance disposed.");
        }
    }
}
