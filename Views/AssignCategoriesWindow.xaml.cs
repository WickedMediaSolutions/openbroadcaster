using System.Windows;

namespace OpenBroadcaster.Views
{
    public partial class AssignCategoriesWindow : Window
    {
        public AssignCategoriesWindow()
        {
            InitializeComponent();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}
