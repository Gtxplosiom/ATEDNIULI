using System;
using System.ComponentModel;
using System.Windows;

namespace ATEDNIULI
{
    public partial class IntentWindow : Window
    {
        private BackgroundWorker background_worker;
        private MainWindow main_window;

        public IntentWindow(MainWindow mainWindow)
        {
            InitializeComponent();

            // Initialize the BackgroundWorker
            background_worker = new BackgroundWorker();
            background_worker.DoWork += BackgroundWorker_DoWork;
            background_worker.RunWorkerCompleted += BackgroundWorker_RunWorkerCompleted;
            background_worker.WorkerSupportsCancellation = true;

            // Start the BackgroundWorker when the window is loaded
            Loaded += IntentWindow_Loaded;
            Topmost = true;

            // Store the reference to the MainWindow
            main_window = mainWindow;
        }

        private void IntentWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (main_window != null)
            {
                // Get the top-left corner of the MainWindow
                double main_window_top_left_x = main_window.Left;
                double main_window_top_y = main_window.Top;

                // Position the IntentWindow at the top of the MainWindow
                Left = main_window_top_left_x - 250;   // Align the left side
                Top = main_window_top_y - Height; // Position it above the MainWindow
            }

            background_worker.RunWorkerAsync(); // Start background worker
        }

        private void Window_Activated(object sender, EventArgs e)
        {
            // Immediately deactivate the window to prevent it from getting focus
            this.Hide();
            this.Show();
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                // Simulate intent recognition processing in the background
                while (!background_worker.CancellationPending)
                {
                    // Placeholder for processing logic (e.g., RecognizeIntent())
                    System.Threading.Thread.Sleep(100); // To reduce CPU usage
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => AppendText($"Error in background worker: {ex.Message}"));
                e.Cancel = true;
            }
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Cancelled)
            {
                AppendText("Intent recognition stopped.");
            }
            else if (e.Error != null)
            {
                AppendText($"An error occurred: {e.Error.Message}");
            }
            else
            {
                AppendText("Background worker completed intent recognition.");
            }
        }

        public void AppendText(string text, bool is_partial = true)
        {
            Console.WriteLine($"Appending text: {text}");

            Dispatcher.Invoke(() =>
            {
                if (is_partial)
                {
                    IntentOutput.Text = text; // Clear previous partial transcription
                }
                else
                {
                    IntentOutput.AppendText(text + "\n");
                }
                IntentOutput.ScrollToEnd();
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
