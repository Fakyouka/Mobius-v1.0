using System.Windows;
using System.Windows.Input;
using Mobius.ViewModels;

namespace Mobius.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
            {
                // optional: maximize/restore (пока не просили)
                return;
            }
            DragMove();
        }

        private void HideFromAltTab_Click(object sender, RoutedEventArgs e)
        {
            // “чтобы не было видно в ALT+TAB”
            // делаем Hide + ShowInTaskbar false.
            // (можно потом добавить NotifyIcon — это уже не дизайн)
            ShowInTaskbar = false;
            Hide();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnMouseDown(MouseButtonEventArgs e)
        {
            base.OnMouseDown(e);

            // simple restore by click anywhere if hidden + show? (не всегда актуально)
            // user can bring back by clicking tray later — but for now add hotkey Esc? not requested
        }
    }
}
