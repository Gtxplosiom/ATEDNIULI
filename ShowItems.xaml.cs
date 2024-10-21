using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System;
using System.Collections.Generic;
using System.Windows.Threading;

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
            SetupTaskbarAutomation();
        }

        private void SetupTaskbarAutomation()
        {
            var taskbarCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "Taskbar");
            _taskbarElement = AutomationElement.RootElement.FindFirst(TreeScope.Children, taskbarCondition);

            if (_taskbarElement != null)
            {
                // Subscribe to the property change event
                Automation.AddAutomationEventHandler(
                    WindowPattern.WindowOpenedEvent,
                    _taskbarElement,
                    TreeScope.Element,
                    (sender, e) => OnTaskbarChanged()
                );

                Automation.AddAutomationEventHandler(
                    WindowPattern.WindowClosedEvent,
                    _taskbarElement,
                    TreeScope.Element,
                    (sender, e) => OnTaskbarChanged()
                );

                // Initially list clickable items
                ListClickableItemsInTaskbar();
            }
            else
            {
                Console.WriteLine("Taskbar not found");
            }
        }

        private void OnTaskbarChanged()
        {
            // Clear existing clickable items and re-list them
            _clickableItems.Clear();
            ListClickableItemsInTaskbar();
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
                    ListClickableItemsInTaskbar();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        public void ListClickableItemsInTaskbar()
        {
            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Dispatcher.Invoke(() => ListClickableItemsInTaskbar());
                return;
            }

            try
            {
                // If taskbarElement is null, find it again
                if (_taskbarElement == null)
                {
                    var taskbarCondition = new PropertyCondition(AutomationElement.AutomationIdProperty, "Taskbar");
                    _taskbarElement = AutomationElement.RootElement.FindFirst(TreeScope.Children, taskbarCondition);
                }

                if (_taskbarElement != null)
                {
                    // Modify the clickable condition to target pinned items (buttons)
                    var clickableCondition = new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button);
                    var taskbarButtons = _taskbarElement.FindAll(TreeScope.Children, clickableCondition); // Search only in the immediate children of the taskbar

                    // Temporarily hold labels to add them all at once
                    List<Label> tagsToAdd = new List<Label>();
                    List<ClickableItem> itemsToAdd = new List<ClickableItem>();

                    foreach (AutomationElement button in taskbarButtons)
                    {
                        if (!button.Current.IsOffscreen)
                        {
                            var boundingRect = button.Current.BoundingRectangle;
                            if (!boundingRect.IsEmpty)
                            {
                                string controlName = button.Current.Name;

                                // Adjust for scaling
                                Rect adjustedBoundingRect = new Rect(
                                    boundingRect.Left / ScalingFactor,
                                    boundingRect.Top / ScalingFactor,
                                    boundingRect.Width / ScalingFactor,
                                    boundingRect.Height / ScalingFactor
                                );

                                Label tag = new Label
                                {
                                    Content = controlName,  // Use the control name for the tag
                                    Background = Brushes.Yellow,
                                    Foreground = Brushes.Black,
                                    Padding = new Thickness(5),
                                    Opacity = 0.7
                                };

                                Canvas.SetLeft(tag, adjustedBoundingRect.Left);
                                Canvas.SetTop(tag, adjustedBoundingRect.Top - 20);

                                tagsToAdd.Add(tag);  // Add the tag to the temporary list
                                _tags.Add(tag);  // Keep track of tags for later removal

                                itemsToAdd.Add(new ClickableItem
                                {
                                    Name = controlName,
                                    BoundingRectangle = boundingRect
                                });
                            }
                        }
                    }

                    // Add the tags to the canvas and update the clickable items list
                    foreach (var tag in tagsToAdd)
                    {
                        OverlayCanvas.Children.Add(tag);  // Add the tag to the canvas
                    }

                    foreach (var item in itemsToAdd)
                    {
                        _clickableItems.Add(item);  // Add the clickable item to the list
                    }
                }
                else
                {
                    Console.WriteLine("Taskbar not found");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }


        public List<ClickableItem> GetClickableItems()
        {
            return _clickableItems;
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
    }
}
