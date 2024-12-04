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
using OpenQA.Selenium.Interactions.Internal;
using System.Windows.Shapes;
using System.IO;

namespace ATEDNIULI
{
    public partial class ShowItems : Window
    {
        private DispatcherTimer _tagRemovalTimer; // Timer for removing tags
        private List<Label> _tags; // List to store tags
        private double ScalingFactor; // Declare the scaling factor
        private AutomationElement _taskbarElement; // Cached taskbar element
        private List<ClickableItem> _clickableItems; // List to store clickable items
        private CameraMouse camera_mouse;
        private int globalCounter = 1;

        [DllImport("user32.dll")]
        private static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("gdi32.dll")]
        private static extern int GetDeviceCaps(IntPtr hdc, int nIndex);

        private const int LOGPIXELSX = 88;

        private SubscriberSocket _subscriberSocket; // ZMQ Subscriber Socket

        public IWebDriver driver;

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

        public bool mouse_active = false;
        public string detected_item = "";
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

                detected_item = label;

                if (actions != null && actions.Length > 0 && mouse_active)
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
                detected_item = "";
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
                        "2. Rename folder",
                        "3. View properties",
                        "4. Share folder"
                    };
                case "file manager":
                    return new string[]
                    {
                        "1. Open Desktop folder",
                        "2. Open Downloads folder",
                        "3. Open Documents folder",
                        "4. Open Videos folder",
                        "5. Open Pictures folder"
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
                ExecuteAction(detected_item, actionNumber);
            }
        }

        // Map action numbers to action methods
        // Dynamically execute actions based on the label and selected action number
        public void ExecuteAction(string label, int actionNumber)
        {
            switch (label.ToLower())
            {
                case "chrome":
                    ExecuteChromeAction(actionNumber);
                    break;

                case "folder":
                    ExecuteFolderAction(actionNumber);
                    break;

                case "file manager":
                    ExecuteFileManagerAction(actionNumber);
                    break;

                case "youtube":
                    ExecuteYouTubeAction(actionNumber);
                    break;

                default:
                    Console.WriteLine($"No actions available for label: {label}");
                    break;
            }
        }

        // Define methods for each label's actions
        private void ExecuteChromeAction(int actionNumber)
        {
            switch (actionNumber)
            {
                case 1: OpenNewTab(); break;
                case 2: OpenLastVisitedWebsite(); break;
                case 3: BookmarkPage(); break;
                case 4: CloseTab(); break;
                case 5: OpenIncognitoWindow(); break;
                default: Console.WriteLine("Action not recognized for Chrome."); break;
            }
        }

        private void ExecuteFolderAction(int actionNumber)
        {
            switch (actionNumber)
            {
                //case 1: OpenFolder(); break;
                //case 2: RenameFolder(); break;
                //case 3: ViewFolderProperties(); break;
                //case 4: ShareFolder(); break;
                default: Console.WriteLine("Action not recognized for Folder."); break;
            }
        }

        private void ExecuteFileManagerAction(int actionNumber)
        {
            switch (actionNumber)
            {
                case 1: OpenDesktopFolder(); break;
                case 2: OpenDownloadsFolder(); break;
                case 3: OpenDocumentsFolder(); break;
                case 4: OpenVideosFolder(); break;
                case 5: OpenPicturesFolder(); break;
                default: Console.WriteLine("Action not recognized for File Manager."); break;
            }
        }

        private void ExecuteYouTubeAction(int actionNumber)
        {
            switch (actionNumber)
            {
                //case 1: PlayPauseVideo(); break;
                //case 2: LikeVideo(); break;
                //case 3: SubscribeToChannel(); break;
                //case 4: ViewComments(); break;
                //case 5: ShareVideoLink(); break;
                default: Console.WriteLine("Action not recognized for YouTube."); break;
            }
        }

        private void OpenDesktopFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            OpenFolderPath(path);
        }

        private void OpenDownloadsFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) + "\\Downloads";
            OpenFolderPath(path);
        }

        private void OpenDocumentsFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            OpenFolderPath(path);
        }

        private void OpenVideosFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyVideos);
            OpenFolderPath(path);
        }

        private void OpenPicturesFolder()
        {
            string path = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            OpenFolderPath(path);
        }

        private void OpenFolderPath(string folderPath)
        {
            try
            {
                if (Directory.Exists(folderPath))
                {
                    System.Diagnostics.Process.Start("explorer.exe", folderPath);
                }
                else
                {
                    Console.WriteLine($"The folder path does not exist: {folderPath}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error opening folder: {ex.Message}");
            }
        }


        public void OpenChrome()
        {
            ChromeOptions options = new ChromeOptions();

            options.AddExcludedArgument("enable-automation");

            driver = new ChromeDriver(options);
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
            camera_mouse = new CameraMouse();
            _tags = new List<Label>(); // Initialize the tag list
            ScalingFactor = GetScalingFactor();
            Show();
            StartZMQListener();
            TrackMouse();
        }

        private static (int X, int Y) GetMousePosition()
        {
            // Get the current mouse position using Cursor.Position
            var mousePosition = System.Windows.Forms.Cursor.Position;
            return (mousePosition.X, mousePosition.Y);
        }

        public double lastDirectionX = 0;
        public double lastDirectionY = 0;

        public double lastSpeed = 0;

        // TrackMouse method to update label position and direction arrow
        private void TrackMouse()
        {
            Thread trackingThread = new Thread(() =>
            {
                while (true)
                {
                    var currentMousePosition = GetMousePosition();

                    // Get the screen width and height
                    int screenWidth = (int)System.Windows.SystemParameters.PrimaryScreenWidth;
                    int screenHeight = (int)System.Windows.SystemParameters.PrimaryScreenHeight;

                    // Default offset
                    int offsetX = 15;  // You can adjust this for more fine-grained positioning
                    int offsetY = 50;

                    // Check if the label would exceed the screen boundaries
                    if (currentMousePosition.Y - offsetY < 0) // Top side
                    {
                        offsetY = -30; // Move the label below
                    }
                    else if (currentMousePosition.Y + offsetY > screenHeight) // Bottom side
                    {
                        offsetY = 50; // Move the label above
                    }

                    // Adjust label position based on mouse position and offset
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        Canvas.SetLeft(MouseActionLabel, currentMousePosition.X - offsetX);
                        Canvas.SetTop(MouseActionLabel, currentMousePosition.Y - offsetY);

                        // Draw arrow on transparent canvas
                        DrawArrowOnCanvas(new Point(currentMousePosition.X, currentMousePosition.Y), lastDirectionX, lastDirectionY, lastSpeed);
                    });

                    Thread.Sleep(50); // Sleep for 50 milliseconds
                }
            });

            trackingThread.IsBackground = true;
            trackingThread.Start();
        }

        public void ClearArrowDrawings()
        {
            Console.WriteLine($"Clearing {arrowElements.Count} arrow elements.");
            foreach (var element in arrowElements)
            {
                OverlayCanvas.Children.Remove(element);
            }
            arrowElements.Clear();
        }

        // Method to draw arrow on the transparent canvas
        // List to track arrow elements
        public static List<UIElement> arrowElements = new List<UIElement>();

        private void DrawArrowOnCanvas(Point mousePosition, double directionX, double directionY, double speed)
        {
            // Clear only the previous arrow visuals
            foreach (var element in arrowElements)
            {
                OverlayCanvas.Children.Remove(element);
            }
            arrowElements.Clear();

            // Normalize direction
            double magnitude = Math.Sqrt(directionX * directionX + directionY * directionY);
            if (magnitude > 0)
            {
                directionX /= magnitude;
                directionY /= magnitude;
            }

            // Stretch factor based on speed (the higher the speed, the longer the arrowhead)
            double stretchFactor = Math.Min(1 + speed * 0.1, 3); // Adjust the scaling factor as needed, capped at 3

            // Adjust arrow so it starts ahead of the cursor
            double cursorOffset = 30; // Distance between the cursor and the start of the arrow
            Point arrowStart = new Point(
                mousePosition.X + directionX * cursorOffset,  // Move the start ahead of the cursor
                mousePosition.Y + directionY * cursorOffset);

            // Calculate the stretched arrowhead points
            double arrowHeadLength = 5 * stretchFactor; // Base length of arrowhead stretched by speed
            Point arrowEnd = new Point(
                arrowStart.X + directionX * arrowHeadLength,
                arrowStart.Y + directionY * arrowHeadLength);

            // Draw stretched arrowhead (triangle)
            Polygon arrowHead = new Polygon
            {
                Points = new PointCollection
        {
            arrowEnd,
            new Point(arrowEnd.X - directionY * arrowHeadLength / 2 - directionX * arrowHeadLength / 2,
                      arrowEnd.Y + directionX * arrowHeadLength / 2 - directionY * arrowHeadLength / 2),
            new Point(arrowEnd.X + directionY * arrowHeadLength / 2 - directionX * arrowHeadLength / 2,
                      arrowEnd.Y - directionX * arrowHeadLength / 2 - directionY * arrowHeadLength / 2)
        },
                Fill = Brushes.Yellow
            };

            // Add the new arrowhead to the canvas
            OverlayCanvas.Children.Add(arrowHead);
            arrowElements.Add(arrowHead); // Keep track of the arrowhead
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
                IntPtr taskbarHandle = FindWindow("Shell_TrayWnd", null);

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

                    if (!isBrowser)
                    {
                        var clickableCondition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TreeItem),  // Include TreeItem for sidebar elements
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.ListItem)// Include Pane for potential container elements
                            );

                        var clickableElements = currentWindow.FindAll(TreeScope.Descendants, clickableCondition);

                        Dispatcher.Invoke(() => ProcessClickableElements(clickableElements));
                    }
                    else if (isBrowser)
                    {
                        Console.WriteLine("Staring browser scan");
                        StartScanning(driver);
                    }
                }

                if (taskbarHandle != IntPtr.Zero)
                {
                    var taskbarElement = AutomationElement.FromHandle(taskbarHandle);

                    if (taskbarElement != null)
                    {
                        var clickableCondition = new OrCondition(
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                            new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Hyperlink)
                        );

                        var taskbarItems = taskbarElement.FindAll(TreeScope.Subtree, clickableCondition);

                        Dispatcher.Invoke(() => ProcessClickableElements(taskbarItems, null));
                    }
                }
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

        public void StartScanning(IWebDriver driver)
        {
            try
            {
                if (driver == null)
                {
                    Console.WriteLine("Driver is null!");
                    return;
                }

                IReadOnlyCollection<IWebElement> linkElements = driver.FindElements(By.CssSelector("a"));
                if (linkElements == null || linkElements.Count == 0)
                {
                    Console.WriteLine("No links found.");
                    return;
                }

                int linkCount = 1;
                int viewportWidth = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerWidth;"));
                int viewportHeight = Convert.ToInt32(((IJavaScriptExecutor)driver).ExecuteScript("return window.innerHeight;"));
                var browserPosition = driver.Manage().Window.Position;

                foreach (IWebElement link in linkElements)
                {
                    try
                    {
                        if (link == null)
                        {
                            Console.WriteLine("Link is null, skipping...");
                            continue; // Skip if link is null
                        }

                        var location = link.Location;
                        var size = link.Size;

                        // Check if location or size is invalid
                        if (location == null || size == null || location.X < 0 || location.Y < 0 || size.Width <= 0 || size.Height <= 0)
                        {
                            Console.WriteLine($"Link {linkCount} has invalid location or size.");
                            continue;
                        }

                        if (IsInViewport(location.X, location.Y, size.Width, size.Height, viewportWidth, viewportHeight))
                        {
                            int adjustedX = location.X + browserPosition.X;
                            int adjustedY = location.Y + browserPosition.Y + 80;

                            // Create the bounding rectangle
                            Rect boundingRect = new Rect(adjustedX, adjustedY, size.Width, size.Height);

                            var clickableItem = new ClickableItem
                            {
                                Name = $"Link {linkCount}",
                                BoundingRectangle = boundingRect
                            };

                            // Ensure _clickableItems is initialized
                            if (_clickableItems == null)
                            {
                                Console.WriteLine("_clickableItems is null!");
                            }
                            else
                            {
                                _clickableItems.Add(clickableItem);
                            }

                            // Ensure _tags and OverlayCanvas are initialized
                            if (_tags == null)
                            {
                                Console.WriteLine("_tags list is null!");
                            }

                            if (OverlayCanvas == null)
                            {
                                Console.WriteLine("OverlayCanvas is null!");
                            }
                            else
                            {
                                // Ensure UI updates are marshaled to the UI thread using Dispatcher
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    // Add UI tag for visualization (as before)
                                    Label tag = new Label
                                    {
                                        Content = $"{globalCounter}",
                                        Background = Brushes.Yellow,
                                        Foreground = Brushes.Black,
                                        Padding = new Thickness(5),
                                        Opacity = 0.7
                                    };

                                    Canvas.SetLeft(tag, adjustedX);
                                    Canvas.SetTop(tag, adjustedY); // Position tag above the link
                                    OverlayCanvas.Children.Add(tag);

                                    _tags.Add(tag);
                                });
                            }
                        }

                        globalCounter++;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error processing link {linkCount}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during scanning: {ex.Message}");
            }
        }

        public bool detected = false;
        private void ProcessClickableElements(AutomationElementCollection clickableElements = null, AutomationElementCollection webClickables = null, bool isBrowser = false, AutomationElementCollection desktopIcons = null)
        {
            // Removed local counters, as we're now using globalCounter

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
                        Console.WriteLine($"Clickable Item {globalCounter}: {controlName}");

                        // UI updates must be done on the UI thread
                        Label tag = new Label
                        {
                            Content = globalCounter,
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

                        globalCounter++;  // Increment the global counter after processing each element
                    }
                }

                detected = true;
            }
            else if (isBrowser)
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
                                // Log the link data (optional)
                                Console.WriteLine($"Link {linkCount}:");
                                Console.WriteLine($"Bounding Box (Browser Coordinates) - X: {location.X}, Y: {location.Y}, Width: {size.Width}, Height: {size.Height}");
                                int adjustedX = location.X + browserPosition.X;
                                int adjustedY = location.Y + browserPosition.Y;
                                Console.WriteLine($"Adjusted Bounding Box (Screen Coordinates) - X: {adjustedX}, Y: {adjustedY}");
                                Console.WriteLine($"Link URL: {link.GetAttribute("href")}");
                                Console.WriteLine();

                                // Create a new ClickableItem with the bounding rectangle and name
                                var clickableItem = new ClickableItem
                                {
                                    Name = $"Link {linkCount}",
                                    BoundingRectangle = new Rect(adjustedX, adjustedY, size.Width, size.Height)
                                };

                                // Add the clickable item to the list
                                _clickableItems.Add(clickableItem);

                                // Create and store the clickable item (tag) to display in UI
                                Label tag = new Label
                                {
                                    Content = $"Link - {globalCounter}",
                                    Background = Brushes.Yellow,
                                    Foreground = Brushes.Black,
                                    Padding = new Thickness(5),
                                    Opacity = 0.7
                                };

                                // Set the position of the tag (above the link element)
                                Canvas.SetLeft(tag, adjustedX);
                                Canvas.SetTop(tag, adjustedY - 20); // Position tag above the link
                                OverlayCanvas.Children.Add(tag);  // Assuming OverlayCanvas is accessible here

                                // Add the tag to the list
                                _tags.Add(tag);  // Assuming _tags is globally accessible
                            }

                            linkCount++;
                            globalCounter++;
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

                detected = true;
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
                        Console.WriteLine($"Clickable Item {globalCounter}: {controlName}");

                        // UI updates must be done on the UI thread
                        Label tag = new Label
                        {
                            Content = globalCounter,
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

                        globalCounter++;  // Increment the global counter after processing each element
                    }
                }
                detected = true;;
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
                                    Console.WriteLine($"Taskbar Item {globalCounter}: {controlName}");

                                    // Create and store the taskbar item
                                    taskbarItems.Add(new ClickableItem
                                    {
                                        Name = controlName,
                                        BoundingRectangle = boundingRect
                                    });

                                    // Create a label (tag) for the taskbar item
                                    Label tag = new Label
                                    {
                                        Content = "T-" + globalCounter, // Prefix with 'T' for taskbar items
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

                                    globalCounter++;
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

            globalCounter = 1;
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

                globalCounter = 1;
            });
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