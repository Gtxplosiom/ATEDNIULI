using ATEDNIULI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
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
        public void ShowWithFadeIn()
        {
            this.Opacity = 0; // Start fully transparent
            this.Show();

            var fadeInAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)), // Adjust duration as needed
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeInAnimation.Completed += (s, e) =>
            {
                allowTextUpdates = true;
            };

            this.BeginAnimation(Window.OpacityProperty, fadeInAnimation);
        }

        // Fade out the window
        public void HideWithFadeOut()
        {
            var fadeOutAnimation = new DoubleAnimation
            {
                From = 1,
                To = 0,
                Duration = new Duration(TimeSpan.FromMilliseconds(100)), // Adjust duration as needed
                FillBehavior = FillBehavior.HoldEnd
            };

            fadeOutAnimation.Completed += (s, e) =>
            {
                this.Hide(); // Hide the window after fade-out
                allowTextUpdates = false;
            };

            this.BeginAnimation(Window.OpacityProperty, fadeOutAnimation);
        }

        public void UpdateListeningIcon(bool isActive)
        {
            main_window.SetListeningIcon(isActive);
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
                Dispatcher.Invoke(() =>
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
                });
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
