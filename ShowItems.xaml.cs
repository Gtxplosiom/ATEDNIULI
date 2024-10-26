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
using System.Windows.Media.Imaging;

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

        private CancellationTokenSource _cancellationTokenSource;

        public void ListClickableItemsInCurrentWindow()
        {
            // Initialize the list of clickable items
            _clickableItems = new List<ClickableItem>();

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Dispatcher.Invoke(() => ListClickableItemsInCurrentWindow());
                return;
            }

            // Cancel the previous task if it exists
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => ProcessClickableItems(_cancellationTokenSource.Token));
        }

        private async Task ProcessClickableItems(CancellationToken token)
        {
            try
            {
                // Throttle the execution
                await Task.Delay(200, token);

                IntPtr windowHandle = GetForegroundWindow();
                var currentWindow = AutomationElement.FromHandle(windowHandle);

                if (currentWindow != null)
                {
                    string windowTitle = currentWindow.Current.Name;
                    bool isBrowser = IsBrowserWindow(windowTitle);

                    // Define conditions for clickable control types
                    OrCondition clickableCondition = isBrowser
                        ? new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit), // Added
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Custom)
                        )
                        : new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)
                        );

                    // If the window is a browser, find the main content
                    AutomationElement mainContent = null;
                    AutomationElementCollection webClickables = null;
                    if (isBrowser)
                    {
                        mainContent = currentWindow.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
                        if (mainContent != null)
                        {
                            Console.WriteLine("Main content found in the browser.");
                            // You can now perform further actions on the mainContent if needed
                            webClickables = mainContent.FindAll(TreeScope.Descendants, clickableCondition);
                        }
                        else
                        {
                            Console.WriteLine("No main content found in the browser.");
                        }
                    }

                    // Use TreeScope.Element for browsers to limit the search
                    var clickableElements = currentWindow.FindAll(TreeScope.Descendants, clickableCondition);
                    

                    Console.WriteLine($"Total Elements Found: {clickableElements.Count}");

                    foreach (AutomationElement element in clickableElements)
                    {
                        Console.WriteLine($"Element Name: {element.Current.Name}, Control Type: {element.Current.ControlType.ProgrammaticName}");
                    }

                    // Ensure UI updates are done on the UI thread
                    Dispatcher.Invoke(() => ProcessClickableElements(clickableElements, webClickables, isBrowser));
                }

                // Ensure UI updates are done on the UI thread
                Dispatcher.Invoke(() =>
                {
                    StartTagRemovalTimer();
                    ListTaskbarItems();
                });
            }
            catch (TaskCanceledException)
            {
                // Task was canceled
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Error: {ex.Message}"));
            }
        }


        private void ProcessClickableElements(AutomationElementCollection clickableElements, AutomationElementCollection webClickables, bool isBrowser)
        {

            int counter = 1;
            int browser_counter = 1;

            foreach (AutomationElement element in clickableElements)
            {
                if (element == null || element.Current.IsOffscreen) continue;

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

                    // UI updates must be done on the UI thread
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

            if (isBrowser)
            {
                
                foreach (AutomationElement element in webClickables)
                {
                    if (element == null || element.Current.IsOffscreen) continue;

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
                        Console.WriteLine($"Clickable Item {browser_counter}: {controlName}");

                        // UI updates must be done on the UI thread
                        Label tag = new Label
                        {
                            Content = browser_counter,
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

                        browser_counter++;
                    }
                }
            }
        }

        // Method to check if the current window is a browser
        private bool IsBrowserWindow(string windowTitle)
        {
            bool isBrowser = windowTitle.Contains("Chrome") ||
                             windowTitle.Contains("Firefox") ||
                             windowTitle.Contains("Edge") ||
                             windowTitle.Contains("Internet Explorer");

            // Debug output to verify window title and browser detection
            Console.WriteLine($"Window Title: {windowTitle}, Is Browser: {isBrowser}");
            return isBrowser;
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