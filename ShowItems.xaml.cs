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
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

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

        private IWebDriver driver;

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

        // Define a delegate for the detection event
        public delegate void ItemDetectedEventHandler(bool isDetected);

        // Add an event using the delegate
        public event ItemDetectedEventHandler ItemDetected;

        private void ProcessDetectionMessage(string message)
        {
            //Console.WriteLine($"Received message: {message}");

            // Check for "no detections" message first
            if (message.Contains("no detections"))
            {
                //Console.WriteLine("No detections found.");
                DetectedItemLabel.Visibility = Visibility.Collapsed;
                DetectedItemText.Visibility = Visibility.Collapsed;
                ActionList.Visibility = Visibility.Collapsed;
                ItemDetected?.Invoke(false);
                return;
            }

            var parts = message.Split(',');
            if (parts.Length == 5)
            {
                string label = parts[0];
                int x1 = int.Parse(parts[1]);
                int y1 = int.Parse(parts[2]);
                int x2 = int.Parse(parts[3]);
                int y2 = int.Parse(parts[4]);

                DetectedItemLabel.Visibility = Visibility.Visible;
                DetectedItemText.Text = label;
                DetectedItemText.Visibility = Visibility.Visible;

                // Display numbered actions based on the detected label
                var actions = GetActionsForLabel(label);
                if (actions != null && actions.Length > 0)
                {
                    ActionList.ItemsSource = actions;
                    ActionList.Visibility = Visibility.Visible;
                }
                else
                {
                    ActionList.Visibility = Visibility.Collapsed;
                }

                ItemDetected?.Invoke(true);
            }
            else
            {
                Console.WriteLine("Invalid detection message.");
                DetectedItemLabel.Visibility = Visibility.Collapsed;
                DetectedItemText.Visibility = Visibility.Collapsed;
                ActionList.Visibility = Visibility.Collapsed;

                ItemDetected?.Invoke(false);
            }
        }

        // Returns a numbered list of actions based on the detected item label
        private string[] GetActionsForLabel(string label)
        {
            switch (label.ToLower())
            {
                case "chrome":
                    return new string[]
                    {
                "1. Open new tab",
                "2. Open last visited website",
                "3. Bookmark this page",
                "4. Close tab",
                "5. Open incognito window"
                    };
                case "folder":
                    return new string[]
                    {
                "1. Open folder",
                "2. Copy folder path",
                "3. View properties",
                "4. Share folder"
                    };
                case "youtube":
                    return new string[]
                    {
                "1. Play/Pause video",
                "2. Like video",
                "3. Subscribe to channel",
                "4. View comments",
                "5. Share video link"
                    };
                default:
                    return null; // No actions for unrecognized labels
            }
        }

        private void ActionList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ActionList.SelectedItem != null)
            {
                // Parse the selected action number
                int actionNumber = int.Parse(ActionList.SelectedItem.ToString().Split('.')[0]);
                ExecuteAction(actionNumber);
            }
        }

        // Map action numbers to action methods
        public void ExecuteAction(int actionNumber)
        {
            switch (actionNumber)
            {
                case 1:
                    OpenNewTab();
                    break;
                case 2:
                    OpenLastVisitedWebsite();
                    break;
                case 3:
                    BookmarkPage();
                    break;
                case 4:
                    CloseTab();
                    break;
                case 5:
                    OpenIncognitoWindow();
                    break;
                default:
                    Console.WriteLine("Action not recognized.");
                    break;
            }
        }

        private void OpenNewTab()
        {
            driver = new ChromeDriver();
            Console.WriteLine("Opening a new tab in Chrome...");
            driver.Navigate().GoToUrl("chrome://newtab");
        }

        private void OpenLastVisitedWebsite()
        {
            Console.WriteLine("Opening the last visited website...");
            if (driver == null)
            {
                Console.WriteLine("No chrome window yet");
            }
            else
            {
                driver.Navigate().Back(); // This simulates going back to the last page
            }
        }
            
        private void BookmarkPage()
        {
            if (driver == null)
            {
                Console.WriteLine("No chrome window yet");
            }
            else
            {
                Console.WriteLine("Bookmarking the current page...");
                // You can execute JavaScript to trigger the bookmark dialog in Chrome
                ((IJavaScriptExecutor)driver).ExecuteScript("document.execCommand('AddBookmark');");
            }
        }

        private void CloseTab()
        {
            if (driver == null)
            {
                Console.WriteLine("No chrome window yet");
            }
            else
            {
                Console.WriteLine("Closing the current tab...");
                driver.Close(); // Closes the current tab
            }
        }

        private void OpenIncognitoWindow()
        {
            Console.WriteLine("Opening an incognito window...");
            var options = new ChromeOptions();
            options.AddArgument("--incognito");
            driver = new ChromeDriver(options); // Open a new incognito window
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

            // To prevent the window from being focusable
            this.IsHitTestVisible = false;
        }

        public class ClickableItem
        {
            public string Name { get; set; }
            public Rect BoundingRectangle { get; set; } // This holds the coordinates
        }

        private CancellationTokenSource _cancellationTokenSource;

        public void ListClickableItemsInCurrentWindow()
        {
            // Reset clickable items and tags
            _clickableItems = new List<ClickableItem>();
            foreach (var tag in _tags)
            {
                OverlayCanvas.Children.Remove(tag);
            }
            _tags.Clear();
            detected = false;

            // Ensure the method runs on the UI thread
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Dispatcher.Invoke(() => ListClickableItemsInCurrentWindow());
                return;
            }

            // Dispose of the previous token source to avoid memory leaks
            _cancellationTokenSource?.Cancel();
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            // Start processing clickable items with a new token
            Task.Run(() => ProcessClickableItems(_cancellationTokenSource.Token));
        }

        private async Task ProcessClickableItems(CancellationToken token)
        {
            AutomationElement currentWindow = null;
            try
            {
                // Throttle the execution to prevent overlap
                await Task.Delay(200, token);

                if (token.IsCancellationRequested)
                    return; // Early exit if the token was canceled

                IntPtr windowHandle = GetForegroundWindow();
                if (windowHandle == IntPtr.Zero)
                {
                    Console.WriteLine("Currently not focused in a window, processing desktop and taskbar items.");
                    // Skip current window scanning and continue with the rest
                }
                else
                {
                    currentWindow = AutomationElement.FromHandle(windowHandle);
                }

                // Proceed to get desktop window and its elements as before
                AutomationElement desktop = AutomationElement.RootElement.FindFirst(
                    TreeScope.Children,
                    new PropertyCondition(AutomationElement.ClassNameProperty, "Progman"));

                if (desktop != null)
                {
                    var iconCondition = new OrCondition(
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Image)
                    );

                    var desktopIcons = desktop.FindAll(TreeScope.Children, iconCondition);

                    Dispatcher.Invoke(() => ProcessClickableElements(null, null, false, desktopIcons));
                }

                if (currentWindow != null)
                {
                    string windowTitle = currentWindow.Current.Name;
                    bool isBrowser = IsBrowserWindow(windowTitle);

                    if (isBrowser)
                    {
                        Console.WriteLine("Currently on browser");
                        StartScanning(driver);
                    }
                }

                // Ensure ListTaskbarItems runs on the UI thread
                Dispatcher.Invoke(ListTaskbarItems);
                //Dispatcher.Invoke(StartTagRemovalTimer);
            }
            catch (TaskCanceledException)
            {
                Console.WriteLine("Process was canceled.");
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => MessageBox.Show($"Error: {ex.Message}"));
            }
        }

        public bool detected = false;
        private void ProcessClickableElements(AutomationElementCollection clickableElements = null, AutomationElementCollection webClickables = null, bool isBrowser = false, AutomationElementCollection desktopIcons = null)
        {

            int counter = 1;
            int desktop_counter = 1;

            if (clickableElements != null)
            {
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

                detected = true;
            } 
            else if (isBrowser)
            {
                // browser show items logic here
            }
            else if (desktopIcons != null)
            {
                foreach (AutomationElement element in desktopIcons)
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
                        Console.WriteLine($"Clickable Item {desktop_counter}: {controlName}");

                        // UI updates must be done on the UI thread
                        Label tag = new Label
                        {
                            Content = desktop_counter,
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

                        desktop_counter++;
                    }
                }

                detected = true;
            }
            else
            {
                Console.WriteLine("No icons detected");
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

        private void Window_Activated(object sender, EventArgs e)
        {
            // Immediately deactivate the window to prevent it from getting focus
            this.Hide();
            this.Show();
        }

        public List<ClickableItem> GetClickableItems()
        {
            if (_clickableItems != null)
            {
                return _clickableItems;
            }
            return null;
        }

        private void StartTagRemovalTimer()
        {
            // Initialize the timer if it's not already initialized
            if (_tagRemovalTimer == null)
            {
                _tagRemovalTimer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromSeconds(10) // Set timer for 10 seconds
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

        public void RemoveTagsNoTimer()
        {
            Dispatcher.Invoke(() =>
            {
                // Remove tags from the overlay canvas
                foreach (var tag in _tags)
                {
                    OverlayCanvas.Children.Remove(tag);
                }

                _tags.Clear(); // Clear the list of tags
            });
        }

        public static void StartScanning(IWebDriver driver)
        {
            try
            {
                // Find all link elements without navigating again
                IReadOnlyCollection<IWebElement> linkElements = driver.FindElements(By.CssSelector("a"));
                int linkCount = 1;
                int viewportWidth = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerWidth;"));
                int viewportHeight = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerHeight;"));
                var browserPosition = driver.Manage().Window.Position;

                foreach (IWebElement link in linkElements)
                {
                    try
                    {
                        var location = link.Location;
                        var size = link.Size;

                        if (IsInViewport(location.X, location.Y, size.Width, size.Height, viewportWidth, viewportHeight))
                        {
                            Console.WriteLine($"Link {linkCount}:");
                            Console.WriteLine($"Bounding Box (Browser Coordinates) - X: {location.X}, Y: {location.Y}, Width: {size.Width}, Height: {size.Height}");
                            int adjustedX = location.X + browserPosition.X;
                            int adjustedY = location.Y + browserPosition.Y;
                            Console.WriteLine($"Adjusted Bounding Box (Screen Coordinates) - X: {adjustedX}, Y: {adjustedY}");
                            Console.WriteLine($"Link URL: {link.GetAttribute("href")}");
                            Console.WriteLine();
                        }

                        linkCount++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing link: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during scanning: {ex.Message}");
            }
        }

        private static bool IsInViewport(int x, int y, int width, int height, int viewportWidth, int viewportHeight)
        {
            return x + width > 0 && x < viewportWidth && y + height > 0 && y < viewportHeight;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    }
}