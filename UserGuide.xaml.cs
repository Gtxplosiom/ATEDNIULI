using System;
using System.Windows;

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
        }

        public int state_now = 0;

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
    }
}
