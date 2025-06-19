using Newtonsoft.Json;
using System;
using System.Windows;
using System.Windows.Controls;
using static LiveTranscription;
using System.IO;
using Microsoft.Office.Interop.Excel;

namespace ATEDNIULI
{
    public partial class SettingsWindow : System.Windows.Window
    {
        public int mouseSpeed = 1;  // Default mouse speed

        private class AppSettings
        {
            public bool IsFirstTime { get; set; }
            public int MouseSpeed { get; set; }
        }

        private void UpdateConfiguration(AppConfig config, string filePath)
        {
            try
            {
                var json = JsonConvert.SerializeObject(config, Formatting.Indented);
                Console.WriteLine($"Serialized JSON: {json}");

                File.WriteAllText(filePath, json);
                Console.WriteLine("Configuration updated successfully.");

                // Read back to verify
                var writtenContent = File.ReadAllText(filePath);
                Console.WriteLine($"File Content After Update:\n{writtenContent}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to update configuration: {ex.Message}");
            }
        }

        private AppConfig config;

        public SettingsWindow()
        {
            InitializeComponent();
            config = AppConfig.Load("appsettings.json");
        }

        // Event handler for speed buttons (1, 2, and 3)
        private void SpeedButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is System.Windows.Controls.Button button && int.TryParse(button.Tag.ToString(), out int speed))
            {
                mouseSpeed = speed;
                CurrentSpeedLabel.Text = $"Current Speed: {mouseSpeed}";

            }
        }

        // Event handler for the Save button
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            config.AppSettings.MouseSpeed = mouseSpeed;
            UpdateConfiguration(config, "appsettings.json");
        }
    }
}
