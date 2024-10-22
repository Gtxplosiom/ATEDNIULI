using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System;
using System.Collections.Generic;
using System.Windows.Threading;
using NetMQ;
using NetMQ.Sockets;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ATEDNIULI
{
    public partial class ShowItems : Window
    {
        private DispatcherTimer _tagRemovalTimer; // Timer for removing tags
        private List<Label> _tags; // List to store tags
        private double ScalingFactor; // Declare the scaling factor
        private AutomationElement _taskbarElement; // Cached taskbar element
        private List<ClickableItem> _clickableItems; // List to store clickable items

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int LOGPIXELSX = 88;

        private SubscriberSocket _subscriberSocket; // ZMQ Subscriber Socket

        private void StartZMQListener()
        {
            // Set up a new thread to listen for ZMQ messages
            Thread zmqThread = new Thread(new ThreadStart(ZMQListener));
            zmqThread.IsBackground = true; // Ensure it doesn't block the application from closing
            zmqThread.Start();
        }

        public void StartGridInference()
        {
            ProcessStartInfo start = new ProcessStartInfo
            {
                FileName = @"C:\Users\super.admin\AppData\Local\Programs\Python\Python312\python.exe",
                Arguments = "C:\\Users\\super.admin\\Desktop\\Capstone\\ATEDNIULI\\edn-app\\ATEDNIULI\\python\\grid_inference_optimized.py",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true, // Prevents the console window from appearing
                EnvironmentVariables =
                {
                    { "PYTHONIOENCODING", "utf-8:replace" }
                }
            };

            Process process = new Process
            {
                StartInfo = start
            };

            process.OutputDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Console.WriteLine(args.Data);
                }
            };

            process.ErrorDataReceived += (sender, args) =>
            {
                if (!string.IsNullOrEmpty(args.Data))
                {
                    Console.WriteLine($"Error: {args.Data}");
                }
            };

            process.Start();
            process.BeginOutputReadLine(); // Start async read of output
            process.BeginErrorReadLine(); // Start async read of error output
        }


        private void ZMQListener()
        {
            // Set up ZMQ Subscriber to listen to port 5555
            using (_subscriberSocket = new SubscriberSocket())
            {
                _subscriberSocket.Connect("tcp://localhost:5555");
                _subscriberSocket.Subscribe(""); // Subscribe to all messages

                while (true)
                {
                    // Receive message from Python
                    string message = _subscriberSocket.ReceiveFrameString();
                    Dispatcher.Invoke(() => ProcessDetectionMessage(message)); // Process in UI thread
                }
            }
        }

        private void ProcessDetectionMessage(string message)
        {
            Console.WriteLine($"Received message: {message}");

            // Check for "no detections" message first
            if (message.Contains("no detections"))
            {
                Console.WriteLine("No detections found.");
                DetectedItemLabel.Visibility = Visibility.Collapsed;
                DetectedItemText.Visibility = Visibility.Collapsed;
                return;
            }

            // Parse the received message (format: "label,x1,y1,x2,y2")
            var parts = message.Split(',');
            if (parts.Length == 5)
            {
                string label = parts[0];
                int x1 = int.Parse(parts[1]);
                int y1 = int.Parse(parts[2]);
                int x2 = int.Parse(parts[3]);
                int y2 = int.Parse(parts[4]);

                // Update the UI with the new detection
                DetectedItemLabel.Visibility = Visibility.Visible;
                DetectedItemText.Text = label;
                DetectedItemText.Visibility = Visibility.Visible;

                // Create the tag label
                Label tag = new Label
                {
                    Content = label,
                    Background = Brushes.Transparent,
                    Foreground = Brushes.Black,
                    Padding = new Thickness(5),
                    FontSize = 16,
                    Opacity = 1.0
                };

                // Position the label above the bounding box
                Canvas.SetLeft(tag, x1);
                Canvas.SetTop(tag, y1 - 20);
                OverlayCanvas.Children.Add(tag);
            }
            else
            {
                Console.WriteLine("Invalid detection message.");
                DetectedItemLabel.Visibility = Visibility.Collapsed;
                DetectedItemText.Visibility = Visibility.Collapsed;
            }
        }

        private double GetScalingFactor()
        {
            IntPtr hdc = GetDC(IntPtr.Zero); // Get the device context for the entire screen
            int dpiX = GetDeviceCaps(hdc, LOGPIXELSX); // Get the horizontal DPI
            return dpiX / 96.0; // Standard DPI is 96 (1x scaling)
        }

        public ShowItems()
        {
            InitializeComponent();
            _tags = new List<Label>(); // Initialize the tag list
            ScalingFactor = GetScalingFactor();
            Show();
            StartGridInference();
            StartZMQListener();
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the window size to cover the entire screen
            var primaryScreenWidth = SystemParameters.PrimaryScreenWidth;
            var primaryScreenHeight = SystemParameters.PrimaryScreenHeight;

            this.Left = 0;
            this.Top = 0;
            this.Width = primaryScreenWidth;
            this.Height = primaryScreenHeight;

            OverlayCanvas.Width = this.Width;
            OverlayCanvas.Height = this.Height;
        }

        public class ClickableItem
        {
            public string Name { get; set; }
            public Rect BoundingRectangle { get; set; } // This holds the coordinates
        }

        public void ListClickableItemsInCurrentWindow()
        {
            // Initialize the list of clickable items
            _clickableItems = new List<ClickableItem>();

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Dispatcher.Invoke(() => ListClickableItemsInCurrentWindow());
                return;
            }

            try
            {
                IntPtr windowHandle = GetForegroundWindow();
                var currentWindow = AutomationElement.FromHandle(windowHandle);

                if (currentWindow != null)
                {
                    var clickableCondition = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)
                    );

                    var clickableElements = currentWindow.FindAll(TreeScope.Subtree, clickableCondition);
                    int counter = 1;

                    foreach (AutomationElement element in clickableElements)
                    {
                        if (!element.Current.IsOffscreen)
                        {
                            var boundingRect = element.Current.BoundingRectangle;

                            if (!boundingRect.IsEmpty)
                            {
                                // Adjust the bounding rectangle coordinates for scaling
                                Rect adjustedBoundingRect = new Rect(
                                    boundingRect.Left / ScalingFactor,
                                    boundingRect.Top / ScalingFactor,
                                    boundingRect.Width / ScalingFactor,
                                    boundingRect.Height / ScalingFactor
                                );

                                string controlName = element.Current.Name;
                                Console.WriteLine($"Clickable Item {counter}: {controlName}");

                                Label tag = new Label
                                {
                                    Content = counter,
                                    Background = Brushes.Yellow,
                                    Foreground = Brushes.Black,
                                    Padding = new Thickness(5),
                                    Opacity = 0.7
                                };

                                // Set the adjusted position based on the adjusted bounding rectangle
                                Canvas.SetLeft(tag, adjustedBoundingRect.Left);
                                Canvas.SetTop(tag, adjustedBoundingRect.Top - 20); // Position above the bounding box
                                OverlayCanvas.Children.Add(tag);

                                // Add the tag to the list
                                _tags.Add(tag);

                                // Create and store the clickable item with the adjusted bounding rectangle
                                _clickableItems.Add(new ClickableItem
                                {
                                    Name = controlName,
                                    BoundingRectangle = boundingRect // Store the adjusted bounding rectangle
                                });

                                counter++;
                            }
                        }
                    }

                    StartTagRemovalTimer();
                    ListTaskbarItems();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        public void ListTaskbarItems()
        {
            // Initialize the list of clickable items on the taskbar
            var taskbarItems = new List<ClickableItem>();

            try
            {
                // Find the handle of the taskbar
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);

                if (taskbarHandle != IntPtr.Zero)
                {
                    var taskbarElement = AutomationElement.FromHandle(taskbarHandle);

                    if (taskbarElement != null)
                    {
                        var clickableCondition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                        );

                        var clickableElements = taskbarElement.FindAll(TreeScope.Subtree, clickableCondition);
                        int counter = 1;

                        foreach (AutomationElement element in clickableElements)
                        {
                            if (!element.Current.IsOffscreen)
                            {
                                var boundingRect = element.Current.BoundingRectangle;

                                if (!boundingRect.IsEmpty)
                                {
                                    // Adjust the bounding rectangle coordinates for scaling
                                    Rect adjustedBoundingRect = new Rect(
                                        boundingRect.Left / ScalingFactor,
                                        boundingRect.Top / ScalingFactor,
                                        boundingRect.Width / ScalingFactor,
                                        boundingRect.Height / ScalingFactor
                                    );

                                    string controlName = element.Current.Name;
                                    Console.WriteLine($"Taskbar Item {counter}: {controlName}");

                                    // Create and store the taskbar item
                                    taskbarItems.Add(new ClickableItem
                                    {
                                        Name = controlName,
                                        BoundingRectangle = boundingRect
                                    });

                                    // Create a label (tag) for the taskbar item
                                    Label tag = new Label
                                    {
                                        Content = "T-" + counter, // Prefix with 'T' for taskbar items
                                        Background = Brushes.Green,
                                        Foreground = Brushes.White,
                                        Padding = new Thickness(5),
                                        Opacity = 0.7
                                    };

                                    // Set the adjusted position based on the bounding rectangle
                                    Canvas.SetLeft(tag, adjustedBoundingRect.Left);
                                    Canvas.SetTop(tag, adjustedBoundingRect.Top - 20); // Position above the bounding box
                                    OverlayCanvas.Children.Add(tag);

                                    // Add the tag to the list
                                    _tags.Add(tag);

                                    counter++;
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error listing taskbar items: {ex.Message}");
            }
        }


        public List<ClickableItem> GetClickableItems()
        {
            return _clickableItems;
        }

        private void StartTagRemovalTimer()
        {
            // Initialize the timer if it's not already initialized
            if (_tagRemovalTimer == null)
            {
                _tagRemovalTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(5) // Set timer for 10 seconds
                };
                _tagRemovalTimer.Tick += RemoveTags; // Attach the event handler
            }

            _tagRemovalTimer.Start(); // Start the timer
        }

        private void RemoveTags(object sender, EventArgs e)
        {
            // Stop the timer
            _tagRemovalTimer.Stop();

            // Remove tags from the overlay canvas
            foreach (var tag in _tags)
            {
                OverlayCanvas.Children.Remove(tag);
            }

            _tags.Clear(); // Clear the list of tags
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }
}