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

        public ShowItems()
        {
            InitializeComponent();
            _tags = new List<Label>(); // Initialize the tag list
            Show();
        }

        public class ClickableItem
        {
            public string Name { get; set; }
            public Rect BoundingRectangle { get; set; } // This holds the coordinates
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            // Set the window size to cover the entire screen
            var primaryScreenWidth = SystemParameters.PrimaryScreenWidth;
            var primaryScreenHeight = SystemParameters.PrimaryScreenHeight;

            // Set window position to the top-left corner (0, 0)
            this.Left = 0;
            this.Top = 0;

            // Set the window size to fill the entire screen
            this.Width = primaryScreenWidth;
            this.Height = primaryScreenHeight;

            // Make sure the canvas stretches to fill the window
            OverlayCanvas.Width = this.Width;
            OverlayCanvas.Height = this.Height;
        }

        private List<ClickableItem> _clickableItems; // List to store clickable items

        public void ListClickableItemsInCurrentWindow()
        {
            // Initialize the list of clickable items
            _clickableItems = new List<ClickableItem>();

            if (Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
            {
                Dispatcher.Invoke(() =>
                {
                    ListClickableItemsInCurrentWindow();
                });
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

                                Canvas.SetLeft(tag, boundingRect.Left);
                                Canvas.SetTop(tag, boundingRect.Top - 20);
                                OverlayCanvas.Children.Add(tag);

                                // Add the tag to the list
                                _tags.Add(tag);

                                // Create and store the clickable item
                                _clickableItems.Add(new ClickableItem
                                {
                                    Name = controlName,
                                    BoundingRectangle = boundingRect
                                });

                                counter++;
                            }
                        }
                    }

                    // Start the timer to remove tags after 10 seconds
                    //StartTagRemovalTimer();
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
    }
}
