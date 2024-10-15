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

        public ShowItems(List<TagItem> items)
        {
            InitializeComponent();

            // Set the size of the window to match the screen dimensions
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            // Center the window on the screen
            Left = (SystemParameters.PrimaryScreenWidth - Width) / 2;
            Top = (SystemParameters.PrimaryScreenHeight - Height) / 2;

            _items = items;

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
                OverlayCanvas.Children.Add(textBlock);
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
    }

    // Class to represent items with tags and coordinates
    public class TagItem
    {
        public string Tag { get; set; }
        public int CenterX { get; set; }
        public int CenterY { get; set; }
    }
}
