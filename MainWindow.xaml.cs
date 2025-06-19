using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using ATEDNIULI;
using static LiveTranscription;

namespace ATEDNIULI
{
    public partial class MainWindow : Window
    {
        double screenHeight = SystemParameters.PrimaryScreenHeight;

        private Window ODoverlay; //design a simple ODoverlay
        private Window Typingoverlay;

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

            ODWindow();
            TypingWindow();
        }

        public void ShowWithFadeIn(Window window)
        {
            window.Opacity = 0; // Start fully transparent
            window.Show();

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)), // Increase duration
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeInAnimation.Completed += (s, e) =>
            {
                //
            };

            window.BeginAnimation(Window.OpacityProperty, fadeInAnimation);
        }

        public void HideWithFadeOut(Window window)
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(300)), // Increase duration
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                window.Hide(); // Hide the window after fade-out
            };

            window.BeginAnimation(Window.OpacityProperty, fadeOutAnimation);
        }

        private void ODWindow()
        {
            // Initialize the overlay window
            ODoverlay = new Window
            {
                Title = "Object Detection Overlay",  // Title of the overlay
                Width = 100,                  // Adjust width for a compact overlay
                Height = 100,                 // Adjust height for a compact overlay
                Background = Brushes.Transparent,
                WindowStyle = WindowStyle.None, // No window chrome
                AllowsTransparency = true, // Allow transparency
                ShowInTaskbar = false,     // Do not show in taskbar
                Topmost = true,            // Stay on top of other windows
                Visibility = Visibility.Hidden // Start hidden
            };

            // Create the layout similar to the Xamarin design
            var mainGrid = new Grid();

            var border = new Border
            {
                Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#F4E9CD"),
                CornerRadius = new CornerRadius(30, 30, 30, 30),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0),
                Width = 70,
                Height = 55,
            };

            // Create the inner Grid for the image
            var innerGrid = new Grid();

            var imageBorder = new Border
            {
                Width = 55,
                Height = 55,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 3,
                    ShadowDepth = 2,
                    Opacity = 0.5
                }
            };

            var ellipse = new Ellipse
            {
                Width = 55,
                Height = 45,
                Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#77ACA2"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Create the Listening Image
            var listeningImage = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/assets/icons/object-detection.png")),
                Width = 35,
                Height = 35,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                Stretch = Stretch.UniformToFill
            };

            // Add elements to the inner Grid
            imageBorder.Child = ellipse;
            innerGrid.Children.Add(imageBorder);
            innerGrid.Children.Add(listeningImage);

            // Add inner grid to the border
            border.Child = innerGrid;

            // Add border to the main grid
            mainGrid.Children.Add(border);

            // Set the main grid as the content of the overlay window
            ODoverlay.Content = mainGrid;
        }

        private void TypingWindow()
        {
            // Initialize the overlay window
            Typingoverlay = new Window
            {
                Title = "Typing Overlay",  // Title of the overlay
                Width = 100,                  // Adjust width for a compact overlay
                Height = 100,                 // Adjust height for a compact overlay
                Background = Brushes.Transparent,
                WindowStyle = WindowStyle.None, // No window chrome
                AllowsTransparency = true, // Allow transparency
                ShowInTaskbar = false,     // Do not show in taskbar
                Topmost = true,            // Stay on top of other windows
                Visibility = Visibility.Hidden, // Start hidden
                ShowActivated = false
            };

            // Create the layout similar to the Xamarin design
            var mainGrid = new Grid();

            var border = new Border
            {
                Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#F4E9CD"),
                CornerRadius = new CornerRadius(30, 30, 30, 30),
                BorderBrush = Brushes.Black,
                BorderThickness = new Thickness(0),
                Width = 70,
                Height = 55,
            };

            // Create the inner Grid for the image
            var innerGrid = new Grid();

            var imageBorder = new Border
            {
                Width = 55,
                Height = 55,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Effect = new DropShadowEffect
                {
                    Color = Colors.Black,
                    BlurRadius = 3,
                    ShadowDepth = 2,
                    Opacity = 0.5
                }
            };

            var ellipse = new Ellipse
            {
                Width = 55,
                Height = 45,
                Fill = (SolidColorBrush)new BrushConverter().ConvertFrom("#77ACA2"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            // Create the Listening Image
            var typingImage = new Image
            {
                Source = new BitmapImage(new Uri("pack://application:,,,/assets/icons/typing.png")),
                Width = 35,
                Height = 35,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                ClipToBounds = true,
                Stretch = Stretch.UniformToFill
            };

            // Add elements to the inner Grid
            imageBorder.Child = ellipse;
            innerGrid.Children.Add(imageBorder);
            innerGrid.Children.Add(typingImage);

            // Add inner grid to the border
            border.Child = innerGrid;

            // Add border to the main grid
            mainGrid.Children.Add(border);

            // Set the main grid as the content of the overlay window
            Typingoverlay.Content = mainGrid;
        }

        // Call this method to show the overlay
        private void ShowODOverlay()
        {
            //ODoverlay.Show(); // Show the overlay
            if (ODoverlay != null)
            {
                ShowWithFadeIn(ODoverlay);
            }
        }

        // Call this method to hide the overlay
        public void HideODOverlay()
        {
            HideWithFadeOut(ODoverlay);
        }

        private void ShowTypingOverlay()
        {
            //ODoverlay.Show(); // Show the overlay
            if (Typingoverlay != null)
            {
                ShowWithFadeIn(Typingoverlay);
            }
        }

        // Call this method to hide the overlay
        public void HideTypingOverlay()
        {
            HideWithFadeOut(Typingoverlay);
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

            if (showed_detected)
            {
                ShowODOverlay();
            }
            else
            {
                HideODOverlay();
            }
        }

        public void HighlightTypingIcon(bool typing)
        {
            AnimateTypingIcon(typing ? -20 : 0, typing);

            if (typing)
            {
                ShowTypingOverlay();
            }
            else
            {
                HideTypingOverlay();
            }
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

        private void AnimateTypingIcon(double targetX, bool isActive)
        {
            // Update the icon source based on the active state
            SetTypingIcon(isActive);

            // Ensure the RenderTransform is initialized with a new TranslateTransform
            TranslateTransform transform;

            // Check if RenderTransform is a TranslateTransform
            if (typing_icon.RenderTransform is TranslateTransform existingTransform)
            {
                transform = existingTransform; // Use existing transform
            }
            else
            {
                transform = new TranslateTransform(); // Create a new transform
                typing_icon.RenderTransform = transform; // Assign the TranslateTransform to the icon
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
            Storyboard.SetTarget(moveAnimation, typing_icon); // Target the UI element
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

        public void SetTypingIcon(bool isActive)
        {
            // Determine the icon path based on the active state
            string iconPath = isActive ? "/assets/icons/typing.png" : "/assets/icons/typing-disabled.png";

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
                typing_icon.Source = icon;

                // Create a fade-in animation
                DoubleAnimation fadeInAnimation = new DoubleAnimation
                {
                    From = 0, // Start fully transparent
                    To = 1, // Fade in
                    Duration = TimeSpan.FromMilliseconds(75)
                };

                // Start the fade-in animation
                typing_icon.BeginAnimation(UIElement.OpacityProperty, fadeInAnimation);
            };

            // Apply the fade-out animation to the icon
            typing_icon.BeginAnimation(UIElement.OpacityProperty, fadeOutAnimation);
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

                ODoverlay.Left = (mainWindowBottomLeftX - ODoverlay.Width) + 45;
                ODoverlay.Top = (mainWindowBottomLeftY - ODoverlay.Height) - 85;

                Typingoverlay.Left = (mainWindowBottomLeftX - Typingoverlay.Width) + 45;
                Typingoverlay.Top = (mainWindowBottomLeftY - Typingoverlay.Height) - 35;

                double mainWindowTopLeftX = Left;
                double mainWindowTopY = Top;

                intentWindow.Left = mainWindowTopLeftX;
                intentWindow.Top = mainWindowTopY - intentWindow.Height;

                asrWindow.Show();

            }), System.Windows.Threading.DispatcherPriority.ApplicationIdle);
        }

    }
}
