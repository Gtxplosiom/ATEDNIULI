using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;  // For Brush and Brushes

namespace ATEDNIULI
{
    public partial class UserGuide : Window
    {
        public UserGuide()
        {
            InitializeComponent();

            // Set initial state to State1
            var stateTemplate = FindResource($"State{state_now}") as DataTemplate;
            StateControl.Content = stateTemplate.LoadContent();

            this.Loaded += UserGuide_Loaded;
        }
        private void UserGuide_Loaded(object sender, RoutedEventArgs e)
        {
            // Display the window handle after the window is fully loaded
            IntPtr handle = GetWindowHandle();
            Console.WriteLine($"Window Handle: {handle}");
        }

        // Method to retrieve the current window's handle
        public IntPtr GetWindowHandle()
        {
            IntPtr handle = IntPtr.Zero;

            Application.Current.Dispatcher.Invoke(() =>
            {
                handle = new WindowInteropHelper(this).Handle;
                Console.WriteLine($"Obtained window handle: {handle}");
            });

            return handle;
        }

        public int state_now = 4;

        // Switch between states based on direction
        public void SwitchState(string direction)
        {
            if (direction == "next")
            {
                state_now++;
                UpdateState();
                ReturnState();
            }
            else if (direction == "previous")
            {
                if (state_now == 1)
                {
                    // Optional: You can show a message or take no action
                    Console.WriteLine("Already at the first state.");
                    return;
                }
                else if (state_now == 0)
                {
                    Console.WriteLine("No previous state");
                }

                state_now = state_now - 1;
                UpdateState();
                ReturnState();
            }
        }

        public string ReturnState()
        {
            return $"state{state_now}";
        }

        // Update the state dynamically based on the current state
        private void UpdateState()
        {
            // Find the appropriate DataTemplate resource by key
            var stateTemplate = FindResource($"State{state_now}") as DataTemplate;

            if (stateTemplate != null)
            {
                // Assign the content of the DataTemplate to ContentControl
                StateControl.Content = stateTemplate.LoadContent();
            }
            else
            {
                // Optional: Handle if a state is not found (e.g., out of bounds)
                Console.WriteLine("State not found!");
            }
        }

        public void UpdateClickedButtonText(string buttonTag)
        {
            var content = StateControl.Content as FrameworkElement;
            if (content != null)
            {
                var clickedButtonText = content.FindName("ClickedButtonText") as TextBlock;

                if (clickedButtonText != null)
                {
                    clickedButtonText.Text = $"Clicked Button: {buttonTag}";
                }
                else
                {
                    Console.WriteLine("ClickedButtonText not found in the current state.");
                }
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            SwitchState("next");
        }

        // Button click handler
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton && clickedButton.Tag != null)
            {
                string buttonTag = clickedButton.Tag.ToString();

                // Update the clicked button text
                UpdateClickedButtonText(buttonTag);

                // Change the border's background color based on the button tag
                ChangeBorderColor(clickedButton, buttonTag);
            }
        }

        // Change the border's background color
        private void ChangeBorderColor(Button clickedButton, string buttonTag)
        {
            // Map tag names to colors
            var colorMap = new Dictionary<string, Brush>
            {
                { "Chick", Brushes.DarkBlue },
                { "Dog", Brushes.LightBlue },
                { "Fox", Brushes.Orange },
                { "Panda", Brushes.Yellow },
                { "Bunny", Brushes.Purple },
                { "Cat", Brushes.Pink },
                { "Koala", Brushes.Orange },
                { "Wolf", Brushes.Green }
            };

            // Traverse the visual tree to find the ButtonContainerBorder
            DependencyObject parent = clickedButton;
            while (parent != null)
            {
                parent = VisualTreeHelper.GetParent(parent);

                if (parent is Border containerBorder && containerBorder.Name == "ButtonContainerBorder")
                {
                    if (colorMap.TryGetValue(buttonTag, out Brush newColor))
                    {
                        containerBorder.Background = newColor;
                    }
                    break;
                }
            }
        }


        // Placeholder button handler
        private void ShapeClickHandler(object sender, RoutedEventArgs e)
        {
            if (sender is Button clickedButton)
            {
                string tag = clickedButton.Tag?.ToString();

                UpdateClickedButtonText(tag);
                ChangeBorderColor(clickedButton, tag);
            }
        }

        public void UpdateTextBlocks(string letter)
        {
            // Access the loaded content of the StateControl
            var content = StateControl.Content as FrameworkElement;
            if (content != null)
            {
                Console.WriteLine($"Content successfully loaded for state {state_now}.");

                if (state_now == 1)
                {
                    Console.WriteLine($"In state 1 changing texts.... current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state1eText = content.FindName($"State1{letter}") as TextBlock;
                            if (state1eText != null)
                            {
                                Console.WriteLine("State1e found successfully!");
                                state1eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State1e not found!");
                            }
                            break;
                        case "f":
                            var state1fText = content.FindName($"State1{letter}") as TextBlock;
                            if (state1fText != null)
                            {
                                Console.WriteLine("State1f found successfully!");
                                state1fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State1f not found!");
                            }
                            break;
                    }
                }
                if (state_now == 2)
                {
                    Console.WriteLine($"In state 2 changing texts....current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state2eText = content.FindName($"State2{letter}") as TextBlock;
                            if (state2eText != null)
                            {
                                Console.WriteLine("State2e found successfully!");
                                state2eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State2e not found!");
                            }
                            break;
                        case "f":
                            var state2fText = content.FindName($"State2{letter}") as TextBlock;
                            if (state2fText != null)
                            {
                                Console.WriteLine("State2f found successfully!");
                                state2fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State2f not found!");
                            }
                            break;
                        case "g":
                            var state2gText = content.FindName($"State2{letter}") as TextBlock;
                            if (state2gText != null)
                            {
                                Console.WriteLine("State2g found successfully!");
                                state2gText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State2g not found!");
                            }
                            break;
                    }
                }
                if (state_now == 3)
                {
                    Console.WriteLine($"In state 3 changing texts.... current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state3eText = content.FindName($"State3{letter}") as TextBlock;
                            if (state3eText != null)
                            {
                                Console.WriteLine("State3e found successfully!");
                                state3eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State3e not found!");
                            }
                            break;
                        case "f":
                            var state3fText = content.FindName($"State3{letter}") as TextBlock;
                            if (state3fText != null)
                            {
                                Console.WriteLine("State3f found successfully!");
                                state3fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State3f not found!");
                            }
                            break;
                    }
                }
                if (state_now == 4)
                {
                    Console.WriteLine($"In state 4 changing texts.... current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state3eText = content.FindName($"State4{letter}") as TextBlock;
                            if (state3eText != null)
                            {
                                Console.WriteLine("State4e found successfully!");
                                state3eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State4e not found!");
                            }
                            break;
                        case "f":
                            var state3fText = content.FindName($"State4{letter}") as TextBlock;
                            if (state3fText != null)
                            {
                                Console.WriteLine("State4f found successfully!");
                                state3fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State4f not found!");
                            }
                            break;
                    }
                }
                if (state_now == 5)
                {
                    Console.WriteLine($"In state 5 changing texts.... current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state3eText = content.FindName($"State5{letter}") as TextBlock;
                            if (state3eText != null)
                            {
                                Console.WriteLine("State5e found successfully!");
                                state3eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State5e not found!");
                            }
                            break;
                        case "f":
                            var state3fText = content.FindName($"State5{letter}") as TextBlock;
                            if (state3fText != null)
                            {
                                Console.WriteLine("State5f found successfully!");
                                state3fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State5f not found!");
                            }
                            break;
                    }
                }
                if (state_now == 6)
                {
                    Console.WriteLine($"In state 6 changing texts.... current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state3eText = content.FindName($"State6{letter}") as TextBlock;
                            if (state3eText != null)
                            {
                                Console.WriteLine("State6e found successfully!");
                                state3eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State6e not found!");
                            }
                            break;
                        case "f":
                            var state3fText = content.FindName($"State6{letter}") as TextBlock;
                            if (state3fText != null)
                            {
                                Console.WriteLine("State6f found successfully!");
                                state3fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State6f not found!");
                            }
                            break;
                    }
                }
                if (state_now == 7)
                {
                    Console.WriteLine($"In state 7 changing texts.... current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state3eText = content.FindName($"State7{letter}") as TextBlock;
                            if (state3eText != null)
                            {
                                Console.WriteLine("State7e found successfully!");
                                state3eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State7e not found!");
                            }
                            break;
                        case "f":
                            var state3fText = content.FindName($"State7{letter}") as TextBlock;
                            if (state3fText != null)
                            {
                                Console.WriteLine("State7f found successfully!");
                                state3fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State7f not found!");
                            }
                            break;
                    }
                }
                if (state_now == 8)
                {
                    Console.WriteLine($"In state 8 changing texts.... current state {state_now}");
                    switch (letter)
                    {
                        case "e":
                            var state3eText = content.FindName($"State8{letter}") as TextBlock;
                            if (state3eText != null)
                            {
                                Console.WriteLine("State8e found successfully!");
                                state3eText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State8e not found!");
                            }
                            break;
                        case "f":
                            var state3fText = content.FindName($"State8{letter}") as TextBlock;
                            if (state3fText != null)
                            {
                                Console.WriteLine("State8f found successfully!");
                                state3fText.Visibility = Visibility.Visible;
                            }
                            else
                            {
                                Console.WriteLine("State8f not found!");
                            }
                            break;
                    }
                }
            }
            else
            {
                Console.WriteLine($"Content is null for state {state_now}.");
            }
        }
    }
}
