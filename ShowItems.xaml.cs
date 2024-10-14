using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace ATEDNIULI
{
    public partial class ShowItems : Window
    {
        private readonly List<TagItem> _items;

        public ShowItems()
        {
            InitializeComponent();

            // Set the size of the window to match the screen dimensions
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            // Center the window on the screen
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

            _items = new List<TagItem>
            {
                new TagItem { Tag = "1 - Item A", CenterX = 100, CenterY = 100 },
                new TagItem { Tag = "2 - Item B", CenterX = 150, CenterY = 110 },
                new TagItem { Tag = "3 - Item C", CenterX = 300, CenterY = 200 },
                new TagItem { Tag = "4 - Item D", CenterX = 100, CenterY = 100 }, // Overlapping item for testing
                new TagItem { Tag = "5 - Item E", CenterX = 250, CenterY = 250 }
            };

            AddTagsToCanvas();
        }

        private void AddTagsToCanvas()
        {
            foreach (var item in _items)
            {
                var textBlock = new TextBlock
                {
                    Text = item.Tag,
                    FontSize = 12,
                    Background = Brushes.LightGray,
                    Padding = new Thickness(5),
                    Cursor = Cursors.Hand,
                    Foreground = Brushes.Black // Ensure text is visible
                };

                // Set position on the Canvas
                Canvas.SetLeft(textBlock, item.CenterX - 50); // Adjust for centering
                Canvas.SetTop(textBlock, item.CenterY - 15);  // Adjust for centering

                // Add mouse event handlers
                textBlock.MouseDown += Tag_MouseDown;
                textBlock.MouseEnter += Tag_MouseEnter;
                textBlock.MouseLeave += Tag_MouseLeave;

                // Add the TextBlock to the Canvas
                OverlayCanvas.Children.Add(textBlock); // Add to the correct Canvas
            }
        }

        private void Tag_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var tag = ((TextBlock)sender).Text;
            MessageBox.Show($"You clicked on {tag}");
        }

        private void Tag_MouseEnter(object sender, MouseEventArgs e)
        {
            ((TextBlock)sender).Background = Brushes.Yellow;
        }

        private void Tag_MouseLeave(object sender, MouseEventArgs e)
        {
            ((TextBlock)sender).Background = Brushes.LightGray;
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Close or drag the window if needed
            if (e.ChangedButton == MouseButton.Left)
            {
                //
            }
        }
    }

    public class TagItem
    {
        public string Tag { get; set; }
        public int CenterX { get; set; }
        public int CenterY { get; set; }
    }
}
