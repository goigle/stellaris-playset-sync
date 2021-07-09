using System.ComponentModel;
using System.Windows;

namespace StellarisPlaysetSync
{
    /// <summary>
    /// Interaction logic for PopupWindow.xaml
    /// </summary>
    public partial class PopupWindow : Window
    {
        public bool underControl = false;
        public bool mytimetodie = false;
        public PopupWindow()
        {
            InitializeComponent();
        }

        private void onClosing(object sender, CancelEventArgs e)
        {
            if (mytimetodie)
            {
                return; // do normal stuff
            }
            if (!underControl)
            {
                Hide();
            }
            e.Cancel = true;
        }
    }
}
