using ATEDNIULI;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ATEDNIULI
{
    public partial class ASRWindow : Window
    {
        private LiveTranscription live_transcription;
        private BackgroundWorker background_worker;
        private MainWindow main_window;
        private IntentWindow intent_window;
        private ShowItems show_items;
        private CameraMouse camera_mouse;

        public ASRWindow(MainWindow mainWindow)
        {
            InitializeComponent();

            camera_mouse = new CameraMouse();
            show_items = new ShowItems();
            intent_window = new IntentWindow(mainWindow);

            live_transcription = new LiveTranscription(this, intent_window, mainWindow, show_items, camera_mouse);
            background_worker = new BackgroundWorker();
            background_worker.DoWork += BackgroundWorker_DoWork;
            background_worker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            background_worker.WorkerSupportsCancellation = true;
            Loaded += ASRWindow_Loaded;
            Topmost = true;
            main_window = mainWindow;

            Dispatcher.UnhandledException += (sender, e) =>
            {
                Console.WriteLine("Dispatcher unhandled exception: " + e.Exception);
            };
        }

        private bool allowTextUpdates = false;

        // Fade in the window
        public void ShowWithFadeIn(bool isTyping)
        {
            allowTextUpdates = false;

            if (isTyping)
            {
                this.Height = 100;
                SetInitialPosition();
            }
            else
            {
                SetInitialPosition();
            }

            this.Opacity = 0;
            this.Show();

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)),
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeInAnimation.Completed += (s, e) =>
            {
                StartBeatingAnimation();
                allowTextUpdates = true;
                AnimateWindowGrowth(300);  // Start the growth animation after fade-in
            };

            this.BeginAnimation(Window.OpacityProperty, fadeInAnimation);
        }

        public void HideWithFadeOut()
        {
            // Stop any ongoing animations to prevent conflicts
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);
            BeginAnimation(Window.OpacityProperty, null);

            AnimateWindowShrink(0);

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)), // Increased duration for smoother fade
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                this.Hide(); // Hide the window after fade-out
                StopBeatingAnimation();
                allowTextUpdates = false;
                OutputTextBox.Text = "";

                // Optionally reset Width to initial value if necessary
                // this.Width = initialWidth; // Define initialWidth as a class member
            };

            this.BeginAnimation(Window.OpacityProperty, fadeOutAnimation);
        }

        public void AnimateWindowGrowth(double targetWidth)
        {
            if (main_window == null) return;

            double main_window_right_x = main_window.Left + main_window.Width;
            double targetLeft = Math.Round(main_window_right_x - targetWidth - 25); // Ensure consistent offset

            // Stop any ongoing animations to prevent overlap
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);

            // Animate the Width
            var widthAnimation = new DoubleAnimation
            {
                From = Width,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(150),
                FillBehavior = FillBehavior.HoldEnd
            };

            // Animate the Left position
            var leftAnimation = new DoubleAnimation
            {
                From = Left,
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(150),
                FillBehavior = FillBehavior.HoldEnd
            };

            // Begin both animations
            BeginAnimation(WidthProperty, widthAnimation);
            BeginAnimation(LeftProperty, leftAnimation);
        }

        public void AnimateWindowShrink(double targetWidth)
        {
            if (main_window == null) return;

            double main_window_right_x = main_window.Left + main_window.Width;
            double targetLeft = Math.Round(main_window_right_x - targetWidth - 25); // Ensure consistent offset

            // Stop any ongoing animations to prevent overlap
            BeginAnimation(WidthProperty, null);
            BeginAnimation(LeftProperty, null);

            // Animate the Width
            var widthAnimation = new DoubleAnimation
            {
                From = Width,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(150),
                FillBehavior = FillBehavior.HoldEnd
            };

            // Animate the Left position
            var leftAnimation = new DoubleAnimation
            {
                From = Left,
                To = targetLeft,
                Duration = TimeSpan.FromMilliseconds(150),
                FillBehavior = FillBehavior.HoldEnd
            };

            // Begin both animations
            BeginAnimation(WidthProperty, widthAnimation);
            BeginAnimation(LeftProperty, leftAnimation);
        }

        private void StartBeatingAnimation()
        {
            var scaleUpAnimation = new DoubleAnimation
            {
                From = 1.0,
                To = 1.1, // Scale to 110%
                Duration = TimeSpan.FromMilliseconds(300), // Duration of the scale up
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever // Repeat forever
            };

            ListeningImage.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleUpAnimation);
            ListeningImage.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleUpAnimation);
        }

        private void StopBeatingAnimation()
        {
            ListeningImage.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            ListeningImage.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }

        private void ASRWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SetInitialPosition(); // Set initial position only here
            background_worker.RunWorkerAsync();
        }

        private void SetInitialPosition()
        {
            if (main_window != null)
            {
                double main_window_right_x = main_window.Left + main_window.Width;
                double main_window_bottom_y = main_window.Top + main_window.Height;
                Left = Math.Round(main_window_right_x - Width - 25); // Adjust 35 as needed
                Top = Math.Round(main_window_bottom_y - Height);
            }
        }

        // Optionally, separate AdjustWindow to only adjust Top if necessary
        private void AdjustWindow()
        {
            if (main_window != null)
            {
                double main_window_bottom_y = main_window.Top + main_window.Height;
                Top = Math.Round(main_window_bottom_y - Height);
            }
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                live_transcription.StartTranscription();
                while (!background_worker.CancellationPending)
                {
                    System.Threading.Thread.Sleep(100);
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendText($"Error in background worker: {ex.Message}"));
                e.Cancel = true;
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Immediately deactivate the window to prevent it from getting focus
            HideWithFadeOut();
            ShowWithFadeIn(false);
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                AppendText("Transcription stopped.");
            }
            else if (e.Error != null)
            {
                AppendText($"An error occurred: {e.Error.Message}");
            }
            else
            {
                AppendText("Background worker completed.");
            }
        }

        public void AppendText(string text, bool is_partial = false)
        {
            if (!allowTextUpdates)
            {
                return; // Prevent text updates if animations are in progress
            }
            else
            {
                if (is_partial)
                {
                    // Clear previous partial transcription
                    OutputTextBox.Text = text;
                }
                else
                {
                    // Append final transcription
                    OutputTextBox.AppendText(text + "\n");
                }
                OutputTextBox.ScrollToEnd();
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            if (background_worker.IsBusy)
            {
                background_worker.CancelAsync();
            }
        }
    }
}
