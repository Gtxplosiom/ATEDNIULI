using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;
using System;

namespace ATEDNIULI
{
    public partial class ShowItems : Window
    {
        public ShowItems()
        {
            InitializeComponent();
            Show();
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

        public void ListClickableItemsInCurrentWindow()
        {
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
                                Label tag = new Label
                                {
                                    Content = $"Tag {counter}",
                                    Background = Brushes.Yellow,
                                    Foreground = Brushes.Black,
                                    Padding = new Thickness(5),
                                    Opacity = 0.7
                                };

                                Canvas.SetLeft(tag, boundingRect.Left);
                                Canvas.SetTop(tag, boundingRect.Top - 20);
                                OverlayCanvas.Children.Add(tag);

                                counter++;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}");
            }
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
    }
}
