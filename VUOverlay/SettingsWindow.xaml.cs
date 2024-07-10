using System;
using System.Windows;
using System.Windows.Controls;
using NAudio.Wave;

namespace VUOverlay
{
    public partial class SettingsWindow : Window
    {
        public event Action SettingsSaved;
        public SettingsWindow()
        {
            InitializeComponent();

            // Load existing settings
            string position = Properties.Settings.Default.WindowPosition;
            switch (position)
            {
                case "TopLeft": TopLeft.IsChecked = true; break;
                case "TopRight": TopRight.IsChecked = true; break;
                case "BottomLeft": BottomLeft.IsChecked = true; break;
                case "BottomRight": BottomRight.IsChecked = true; break;
                default: TopLeft.IsChecked = true; break;
            }

            // Load hotkey settings
            string modifier = Properties.Settings.Default.HotkeyModifier;
            for (int i = 0; i < ModifierKeysComboBox.Items.Count; i++)
            {
                var item = (ComboBoxItem)ModifierKeysComboBox.Items[i];
                if (item.Content.ToString() == modifier)
                {
                    ModifierKeysComboBox.SelectedIndex = i;
                    break;
                }
            }

            KeyTextBox.Text = Properties.Settings.Default.HotkeyKey;

            // Populate input devices
            for (int n = 0; n < WaveIn.DeviceCount; n++)
            {
                var capabilities = WaveIn.GetCapabilities(n);
                InputDeviceComboBox.Items.Add(capabilities.ProductName);
            }

            // Load selected input device
            InputDeviceComboBox.SelectedItem = Properties.Settings.Default.InputDevice;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (TopLeft.IsChecked == true) Properties.Settings.Default.WindowPosition = "TopLeft";
            else if (TopRight.IsChecked == true) Properties.Settings.Default.WindowPosition = "TopRight";
            else if (BottomLeft.IsChecked == true) Properties.Settings.Default.WindowPosition = "BottomLeft";
            else if (BottomRight.IsChecked == true) Properties.Settings.Default.WindowPosition = "BottomRight";

            // Save hotkey settings
            Properties.Settings.Default.HotkeyModifier = ((ComboBoxItem)ModifierKeysComboBox.SelectedItem).Content.ToString();
            Properties.Settings.Default.HotkeyKey = KeyTextBox.Text;

            // Save selected input device
            Properties.Settings.Default.InputDevice = InputDeviceComboBox.SelectedItem.ToString();

            Properties.Settings.Default.Save();
            SettingsSaved?.Invoke();
            this.Close();
        }
    }
}
