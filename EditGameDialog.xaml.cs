using System;
using System.Windows;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace PS4RichPresence
{
    public partial class EditGameDialog : Window
    {
        public GameInfo GameInfo { get; private set; }

        public EditGameDialog(GameInfo gameInfo)
        {
            InitializeComponent();
            GameInfo = new GameInfo
            {
                TitleId = gameInfo.TitleId,
                Name = gameInfo.Name,
                ImageUrl = gameInfo.ImageUrl
            };

            // Load current values
            TitleIDTextBox.Text = GameInfo.TitleId;
            GameNameTextBox.Text = GameInfo.Name;
            ImageUrlTextBox.Text = GameInfo.ImageUrl;

            // Update preview
            UpdatePreview();

            // Add event handler for image URL changes
            ImageUrlTextBox.TextChanged += (s, e) => UpdatePreview();
        }

        private void UpdatePreview()
        {
            try
            {
                if (Uri.TryCreate(ImageUrlTextBox.Text, UriKind.Absolute, out Uri uri))
                {
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = uri;
                    bitmap.EndInit();
                    PreviewImage.Source = bitmap;
                }
                else
                {
                    PreviewImage.Source = null;
                }
            }
            catch
            {
                PreviewImage.Source = null;
            }
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Image Files|*.png;*.jpg;*.jpeg",
                Title = "Select Game Image"
            };

            if (dialog.ShowDialog() == true)
            {
                ImageUrlTextBox.Text = dialog.FileName;
            }
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(GameNameTextBox.Text))
            {
                MessageBox.Show("Please enter a game name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            GameInfo.Name = GameNameTextBox.Text;
            GameInfo.ImageUrl = ImageUrlTextBox.Text;
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
} 