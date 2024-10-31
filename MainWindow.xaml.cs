using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ATEDNIULI;

namespace ATEDNIULI
{
    public partial class MainWindow : Window
    {
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += OnSourceInitialized;
            Loaded += OnWindowLoaded;

            var bg_color = (Brush)new BrushConverter().ConvertFromString("#77ACA2");
            Topmost = true;
            Background = bg_color;

            Dispatcher.UnhandledException += (sender, e) =>
            {
                Console.WriteLine("Dispatcher unhandled exception: " + e.Exception);
                // Log the exception or take other actions
            };
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Immediately deactivate the window to prevent it from getting focus
            this.Hide();
            this.Show();
        }

        public void SetListeningIcon(bool isActive)
        {
            string iconPath = isActive ? "/assets/icons/listening.png" : "/assets/icons/listening-disabled.png";
            var icon = new BitmapImage(new Uri(iconPath, UriKind.Relative));
            listening_icon.Source = icon;
        }

        public void UpdateListeningIcon(bool isActive)
        {
            SetListeningIcon(isActive);
        }

        private void ExecuteOpenApplication(object sender, ExecutedRoutedEventArgs e) //method for opening applications
        {
            string appPath = e.Parameter as string;

            if (!string.IsNullOrEmpty(appPath))
            {
                try
                {
                    Process.Start(appPath);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to open application: {ex.Message}");
                }
            }
        }

        private void OnSourceInitialized(object sender, EventArgs e)
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = SystemParameters.PrimaryScreenWidth - ActualWidth;
            double desiredHeight = settings_button.Visibility == Visibility.Visible ? 250 : 150;
            Height = desiredHeight;
            Top = (screenHeight - Height) - 48;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            AdjustWindowHeight();
            // Ensure that the OpenASRWindow and OpenIntentWindow methods are called after the window is fully loaded
            Dispatcher.BeginInvoke(new Action(() =>
            {
                OpenWindows();
            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

        private void toggle_button_Click(object sender, RoutedEventArgs e)
        {
            settings_button.Visibility = settings_button.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            help_button.Visibility = help_button.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
            AdjustWindowHeight();
        }

        public void AdjustWindowHeight()
        {
            double desiredHeight = settings_button.Visibility == Visibility.Visible ? 250 : 150;
            Height = desiredHeight;

            Top = (screenHeight - Height) - 48;
        }

        private void CommandHandler(string text)
        {
            if (text == "adjust")
            {
                settings_button.Visibility = settings_button.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                help_button.Visibility = help_button.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                AdjustWindowHeight();
            }
        }

        private void OpenWindows()
        {
            // Create the ASRWindow instance
            ASRWindow asrWindow = new ASRWindow(this);
            IntentWindow intentWindow = new IntentWindow(this);

            // Calculate the position
            Dispatcher.BeginInvoke(new Action(() =>
            {
                double mainWindowBottomLeftX = Left;
                double mainWindowBottomLeftY = Top + Height;

                asrWindow.Left = mainWindowBottomLeftX - asrWindow.Width;
                asrWindow.Top = mainWindowBottomLeftY - asrWindow.Height;

                double mainWindowTopLeftX = Left;
                double mainWindowTopY = Top;

                intentWindow.Left = mainWindowTopLeftX;
                intentWindow.Top = mainWindowTopY - intentWindow.Height;

                asrWindow.Show();

            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }
    }
}
