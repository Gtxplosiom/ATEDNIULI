using ATEDNIULI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media.Imaging;

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

            // Initialize IntentWindow
            intent_window = new IntentWindow(mainWindow);

            live_transcription = new LiveTranscription(this, intent_window, mainWindow, show_items, camera_mouse);

            // Initialize BackgroundWorker
            background_worker = new BackgroundWorker();
            background_worker.DoWork += BackgroundWorker_DoWork;
            background_worker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            background_worker.WorkerSupportsCancellation = true;

            // Start the BackgroundWorker when the window is loaded
            Loaded += ASRWindow_Loaded;
            Topmost = true;

            // Store the reference to the MainWindow
            main_window = mainWindow;

            Dispatcher.UnhandledException += (sender, e) =>
            {
                Console.WriteLine("Dispatcher unhandled exception: " + e.Exception);
                // Log the exception or take other actions
            };
        }


        public void UpdateListeningIcon(bool isActive)
        {
            main_window.SetListeningIcon(isActive);
        }

        private void ASRWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (main_window != null)
            {
                // Ensure the dimensions are correct when setting the position
                double main_window_bottom_left_x = main_window.Left;
                double main_window_bottom_y = main_window.Top + main_window.Height;

                // Position the ASRWindow
                Left = main_window_bottom_left_x - Width;
                Top = main_window_bottom_y - Height;
            }

            background_worker.RunWorkerAsync();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // Start transcription in the background
                live_transcription.StartTranscription();

                // Check for cancellation
                while (!background_worker.CancellationPending)
                {
                    // Let the transcription continue
                    System.Threading.Thread.Sleep(100); // Reduce CPU usage
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendText($"Error in background worker: {ex.Message}"));
                e.Cancel = true;
            }
            finally
            {
                if (background_worker.CancellationPending)
                {
                    // live_transcription.StopTranscription();
                }
            }
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Immediately deactivate the window to prevent it from getting focus
            this.Hide();
            this.Show();
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

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            // Request cancellation of the background worker
            if (background_worker.IsBusy)
            {
                background_worker.CancelAsync();
            }
        }
    }
}
