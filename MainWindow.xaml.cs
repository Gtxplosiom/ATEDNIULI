using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using ATEDNIULI;
using static LiveTranscription;

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

        public void HighlightODIcon(bool showed_detected)
        {
            AnimateODIcon(showed_detected ? -20 : 0, showed_detected);
        }

        public void UpdateListeningIcon(bool isActive)
        {
            // Start the animation and update the icon simultaneously
            AnimateListeningIcon(isActive ? -20 : 0, isActive);
        }

        private void AnimateListeningIcon(double targetX, bool isActive)
        {
            // Update the icon source based on the active state
            SetListeningIcon(isActive);

            // Ensure the RenderTransform is initialized with a new TranslateTransform
            TranslateTransform transform;

            // Check if RenderTransform is a TranslateTransform
            if (listening_icon.RenderTransform is TranslateTransform existingTransform)
            {
                transform = existingTransform; // Use existing transform
            }
            else
            {
                transform = new TranslateTransform(); // Create a new transform
                listening_icon.RenderTransform = transform; // Assign the TranslateTransform to the icon
            }

            // Create the storyboard for the animation
            var storyboard = new Storyboard();

            // Create the animation to move to targetX
            var moveAnimation = new DoubleAnimation
            {
                From = transform.X, // Use the current X value
                To = targetX, // Move to the target X position
                Duration = TimeSpan.FromMilliseconds(150) // Duration of the animation
            };

            // Set the target of the animation
            Storyboard.SetTarget(moveAnimation, listening_icon); // Target the UI element
                                                                 // Correct property path for the TranslateTransform X property
            Storyboard.SetTargetProperty(moveAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            // Add the animation to the storyboard
            storyboard.Children.Add(moveAnimation);

            // Start the animation
            storyboard.Begin();
        }

        private void AnimateODIcon(double targetX, bool isActive)
        {
            // Update the icon source based on the active state
            SetODIcon(isActive);

            // Ensure the RenderTransform is initialized with a new TranslateTransform
            TranslateTransform transform;

            // Check if RenderTransform is a TranslateTransform
            if (object_detection_icon.RenderTransform is TranslateTransform existingTransform)
            {
                transform = existingTransform; // Use existing transform
            }
            else
            {
                transform = new TranslateTransform(); // Create a new transform
                object_detection_icon.RenderTransform = transform; // Assign the TranslateTransform to the icon
            }

            // Create the storyboard for the animation
            var storyboard = new Storyboard();

            // Create the animation to move to targetX
            var moveAnimation = new DoubleAnimation
            {
                From = transform.X, // Use the current X value
                To = targetX, // Move to the target X position
                Duration = TimeSpan.FromMilliseconds(150) // Duration of the animation
            };

            // Set the target of the animation
            Storyboard.SetTarget(moveAnimation, object_detection_icon); // Target the UI element
                                                                 // Correct property path for the TranslateTransform X property
            Storyboard.SetTargetProperty(moveAnimation, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));

            // Add the animation to the storyboard
            storyboard.Children.Add(moveAnimation);

            // Start the animation
            storyboard.Begin();
        }

        public void SetListeningIcon(bool isActive)
        {
            // Determine the icon path based on the active state
            string iconPath = isActive ? "/assets/icons/listening.png" : "/assets/icons/listening-disabled.png";

            // Preload the BitmapImage
            var icon = new BitmapImage(new Uri(iconPath, UriKind.Relative));

            // Create an animation for the opacity transition to fade out
            DoubleAnimation fadeOutAnimation = new DoubleAnimation
            {
                From = 1, // Start fully visible
                To = 0, // Fade out
                Duration = TimeSpan.FromMilliseconds(75)
            };

            // Attach the Completed event handler
            fadeOutAnimation.Completed += (s, e) =>
            {
                // Set the new icon source after fading out
                listening_icon.Source = icon;

                // Create a fade-in animation
                DoubleAnimation fadeInAnimation = new DoubleAnimation
                {
                    From = 0, // Start fully transparent
                    To = 1, // Fade in
                    Duration = TimeSpan.FromMilliseconds(75)
                };

                // Start the fade-in animation
                listening_icon.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };

            // Apply the fade-out animation to the icon
            listening_icon.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
        }

        public void SetODIcon(bool isActive)
        {
            // Determine the icon path based on the active state
            string iconPath = isActive ? "/assets/icons/object-detection.png" : "/assets/icons/object-detection-disabled.png";

            // Preload the BitmapImage
            var icon = new BitmapImage(new Uri(iconPath, UriKind.Relative));

            // Create an animation for the opacity transition to fade out
            DoubleAnimation fadeOutAnimation = new DoubleAnimation
            {
                From = 1, // Start fully visible
                To = 0, // Fade out
                Duration = TimeSpan.FromMilliseconds(75)
            };

            // Attach the Completed event handler
            fadeOutAnimation.Completed += (s, e) =>
            {
                // Set the new icon source after fading out
                object_detection_icon.Source = icon;

                // Create a fade-in animation
                DoubleAnimation fadeInAnimation = new DoubleAnimation
                {
                    From = 0, // Start fully transparent
                    To = 1, // Fade in
                    Duration = TimeSpan.FromMilliseconds(75)
                };

                // Start the fade-in animation
                object_detection_icon.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };

            // Apply the fade-out animation to the icon
            object_detection_icon.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
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
            double desiredHeight = settings_button.Visibility == Visibility.Visible ? 300 : 150;
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

        public void AdjustWindowHeight()
        {
            double desiredHeight = settings_button.Visibility == Visibility.Visible ? 300 : 150;
            Height = desiredHeight;

            Top = (screenHeight - Height) - 48;
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
