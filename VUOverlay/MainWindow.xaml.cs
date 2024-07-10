using NAudio.Wave;
using System;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Shapes;
using System.Windows.Threading;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using NHotkey;
using NHotkey.Wpf;

namespace VUOverlay
{
    public partial class MainWindow : Window
    {
        private WaveInEvent waveIn;
        private DispatcherTimer timer;
        private float maxVolume;
        private const int SegmentCount = 12;
        private NotifyIcon notifyIcon;
        private SettingsWindow settingsWindow;

        public MainWindow()
        {
            InitializeComponent();

            InitializeNotifyIcon();

            InitializeAudioDevice();

            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromMilliseconds(20); // Faster updates
            timer.Tick += UpdateVUMeter;
            timer.Start();

            HotkeyManager.Current.AddOrReplace("ShowSettings", Key.S, ModifierKeys.Control, OnShowSettings);
            LoadWindowPosition();

            // Hide the window from the taskbar
            this.ShowInTaskbar = false;
        }

        private void InitializeNotifyIcon()
        {
            // Initialize NotifyIcon
            notifyIcon = new NotifyIcon
            {
                Icon = new System.Drawing.Icon("Resources/Icon/VU_icon.ico"), // Default icon
                Visible = true
            };
            notifyIcon.DoubleClick += NotifyIcon_DoubleClick;

            // Add context menu to NotifyIcon
            var contextMenu = new ContextMenuStrip();
            contextMenu.Items.Add(new ToolStripMenuItem("Show Settings", null, (s, e) => ShowSettings()));
            contextMenu.Items.Add(new ToolStripMenuItem("Exit", null, (s, e) => ExitApplication()));
            notifyIcon.ContextMenuStrip = contextMenu;
        }

        private void InitializeAudioDevice()
        {
            if (waveIn != null)
            {
                waveIn.StopRecording();
                waveIn.Dispose();
            }

            int deviceNumber = -1;
            string selectedDevice = Properties.Settings.Default.InputDevice;
            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var capabilities = WaveIn.GetCapabilities(n);
                if (capabilities.ProductName == selectedDevice)
                {
                    deviceNumber = n;
                    break;
                }
            }

            if (deviceNumber == -1)
            {
                // Default to the first device if not found
                deviceNumber = 0;
            }

            waveIn = new WaveInEvent
            {
                DeviceNumber = deviceNumber,
                WaveFormat = new WaveFormat(44100, 16, 1) // 16-bit PCM, 44.1kHz, Mono
            };
            waveIn.DataAvailable += OnDataAvailable;
            waveIn.StartRecording();
        }

        private void Window_StateChanged(object sender, EventArgs e)
        {
            if (this.WindowState == WindowState.Minimized)
            {
                this.ShowInTaskbar = false;
            }
            else if (this.WindowState == WindowState.Normal)
            {
                this.ShowInTaskbar = false; // Ensure it does not appear in the taskbar when restored
            }
        }

        private void NotifyIcon_DoubleClick(object sender, EventArgs e)
        {
            ShowSettings();
        }

        private void ShowSettings()
        {
            this.ShowInTaskbar = false;
            if (settingsWindow == null || !settingsWindow.IsLoaded) // Check if the window is null or not loaded
            {
                settingsWindow = new SettingsWindow();
                settingsWindow.SettingsSaved += () => Dispatcher.Invoke(() =>
                {
                    InitializeAudioDevice();
                    ReRegisterHotkey(); // Re-register hotkey after saving settings
                });

                settingsWindow.Topmost = true; // Make the window topmost to ensure it's above other windows
                settingsWindow.Show();
                settingsWindow.Activate(); // Activate the window to bring it into focus
                settingsWindow.Topmost = false; // Optionally, set Topmost back to false if you don't want it to stay on top of all other windows

                LoadWindowPosition();
                RegisterHotkey();
            } else
            {
                settingsWindow.Activate(); // If the window is already open, bring it to the front
            }
            
        }

        private void ExitApplication()
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            System.Windows.Application.Current.Shutdown();
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            var buffer = new WaveBuffer(e.Buffer);
            short[] samples = new short[e.BytesRecorded / 2];
            Buffer.BlockCopy(buffer.ByteBuffer, 0, samples, 0, e.BytesRecorded);

            maxVolume = samples.Max(sample =>
            {
                if (sample == short.MinValue)
                {
                    return short.MaxValue;
                }
                return Math.Abs(sample);
            }) / (float)short.MaxValue;

            Debug.WriteLine($"Raw Max Volume: {maxVolume}");

            if (maxVolume > 1.0f)
            {
                maxVolume = 1.0f;
            }
            else if (maxVolume < 0.0f || float.IsNaN(maxVolume) || float.IsInfinity(maxVolume))
            {
                maxVolume = 0.0f;
            }

            Debug.WriteLine($"Normalized Max Volume: {maxVolume}");
        }

        private void UpdateVUMeter(object sender, EventArgs e)
        {
            if (float.IsNaN(maxVolume) || float.IsInfinity(maxVolume))
            {
                maxVolume = 0;
            }

            // Apply logarithmic scale for better sensitivity
            double logVolume = Math.Log10(maxVolume * 9 + 1); // Scale volume to a range [0, 1] logarithmically

            int segmentsToShow = (int)(logVolume * SegmentCount);

            for (int i = 0; i < SegmentCount; i++)
            {
                var segment = (Rectangle)VUStack.Children[SegmentCount - 1 - i];
                if (i < segmentsToShow)
                {
                    segment.Visibility = Visibility.Visible;
                }
                else
                {
                    segment.Visibility = Visibility.Hidden;
                }
            }
        }


        private void OnShowSettings(object sender, HotkeyEventArgs e)
        {
            ShowSettings();
        }

        private void RegisterHotkey()
        {
            try
            {
                ModifierKeys modifierKey = ModifierKeys.None;
                switch (Properties.Settings.Default.HotkeyModifier)
                {
                    case "Ctrl":
                        modifierKey = ModifierKeys.Control;
                        break;
                    case "Alt":
                        modifierKey = ModifierKeys.Alt;
                        break;
                    case "Shift":
                        modifierKey = ModifierKeys.Shift;
                        break;
                }

                Key key = (Key)Enum.Parse(typeof(Key), Properties.Settings.Default.HotkeyKey, true);

                HotkeyManager.Current.AddOrReplace("ShowSettings", key, modifierKey, OnShowSettings);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to register hotkey: {ex.Message}");
            }
        }

        private void ReRegisterHotkey()
        {
            try
            {
                HotkeyManager.Current.Remove("ShowSettings"); // Deregister the existing hotkey
                RegisterHotkey(); // Re-register the hotkey
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to re-register hotkey: {ex.Message}");
            }
        }

        private void LoadWindowPosition()
        {
            string position = Properties.Settings.Default.WindowPosition;
            switch (position)
            {
                case "TopLeft":
                    this.Left = 0;
                    this.Top = 0;
                    break;
                case "TopRight":
                    this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
                    this.Top = 0;
                    break;
                case "BottomLeft":
                    this.Left = 0;
                    this.Top = SystemParameters.PrimaryScreenHeight - this.Height;
                    break;
                case "BottomRight":
                    this.Left = SystemParameters.PrimaryScreenWidth - this.Width;
                    this.Top = SystemParameters.PrimaryScreenHeight - this.Height;
                    break;
                default:
                    this.Left = 0;
                    this.Top = 0;
                    break;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            notifyIcon.Visible = false;
            notifyIcon.Dispose();
            base.OnClosed(e);
        }
    }
}
