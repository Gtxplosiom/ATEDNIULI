using ATEDNIULI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using static System.Net.Mime.MediaTypeNames;

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
        public void ShowWithFadeIn()
        {
            allowTextUpdates = false;

            this.Opacity = 0; // Start fully transparent
            this.Show();

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(150)), // Increase duration
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeInAnimation.Completed += (s, e) =>
            {
                StartBeatingAnimation();
                allowTextUpdates = true;
            };

            this.BeginAnimation(Window.OpacityProperty, fadeInAnimation);

            AnimateWindowGrowth(300);
        }

        public void HideWithFadeOut()
        {
            AnimateWindowShrink(0);

            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(200)), // Increase duration
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                this.Hide(); // Hide the window after fade-out
                StopBeatingAnimation();
                allowTextUpdates = false;
                OutputTextBox.Text = "";
            };

            this.BeginAnimation(Window.OpacityProperty, fadeOutAnimation);
        }

        public void AnimateWindowGrowth(double targetWidth)
        {
            // Calculate the target left position to keep the right edge in place
            double targetLeft = Left - (targetWidth - Width) + 27;

            // Animate the Width
            var widthAnimation = new DoubleAnimation
            {
                From = Width,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(150),
                FillBehavior = FillBehavior.HoldEnd
            };

            // Animate the Left position to move the window leftward as it grows
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
            // Calculate the target left position to keep the right edge in place
            double targetLeft = Left + (Width - targetWidth) - 27;

            // Animate the Width
            var widthAnimation = new DoubleAnimation
            {
                From = Width,
                To = targetWidth,
                Duration = TimeSpan.FromMilliseconds(150),
                FillBehavior = FillBehavior.HoldEnd
            };

            // Animate the Left position to move the window rightward as it shrinks
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
                To = 1.1, // Scale to 120%
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
            if (main_window != null)
            {
                double main_window_bottom_left_x = main_window.Left;
                double main_window_bottom_y = main_window.Top + main_window.Height;
                Left = (main_window_bottom_left_x - Width) + 35;
                Top = main_window_bottom_y - Height;
            }

            background_worker.RunWorkerAsync();
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
            ShowWithFadeIn();
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
