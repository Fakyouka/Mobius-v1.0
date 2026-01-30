using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using Mobius.ViewModels;

namespace Mobius.Views
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;

        private DebugPanelWindow _debugWin;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            Loaded += (_, __) =>
            {
                HookDebugPanel();
                UpdateDebugPanel(force: true);
            };

            LocationChanged += (_, __) => UpdateDebugPanelPosition();
            SizeChanged += (_, __) => UpdateDebugPanelPosition();
            Closing += (_, __) => { try { _debugWin?.Close(); } catch { } };
        }

        private void HookDebugPanel()
        {
            if (_debugWin != null) return;

            _debugWin = new DebugPanelWindow
            {
                Owner = this,
                DataContext = _vm.Library,
                ShowInTaskbar = false
            };

            _vm.Library.PropertyChanged += OnLibraryPropertyChanged;
        }

        private void OnLibraryPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryViewModel.DebugPanelOpen))
                UpdateDebugPanel();
        }

        private void UpdateDebugPanel(bool force = false)
        {
            if (_debugWin == null) return;

            if (!_vm.Library.DebugEnabled)
            {
                HideDebugPanel(force);
                return;
            }

            if (_vm.Library.DebugPanelOpen)
                ShowDebugPanel(force);
            else
                HideDebugPanel(force);
        }

        private void ShowDebugPanel(bool force)
        {
            if (_debugWin.IsVisible == false)
                _debugWin.Show();

            UpdateDebugPanelPosition();

            // Visible position: прямо рядом справа (не накладывается на окно)
            double targetLeft = Left + Width + 10;
            double targetTop = Top + 12;

            _debugWin.Height = Math.Max(200, Height - 24);

            if (force)
            {
                _debugWin.Left = targetLeft;
                _debugWin.Top = targetTop;
                _debugWin.Opacity = 1;
                return;
            }

            AnimateWindow(_debugWin, targetLeft, targetTop, 1);
        }

        private void HideDebugPanel(bool force)
        {
            if (_debugWin == null) return;
            if (!_debugWin.IsVisible) return;

            // Hidden position: ещё дальше вправо (за экран/за границу)
            double targetLeft = Left + Width + _debugWin.Width + 40;
            double targetTop = Top + 12;

            if (force)
            {
                _debugWin.Left = targetLeft;
                _debugWin.Top = targetTop;
                _debugWin.Opacity = 0;
                _debugWin.Hide();
                return;
            }

            AnimateWindow(_debugWin, targetLeft, targetTop, 0, onDone: () =>
            {
                try { _debugWin.Hide(); } catch { }
            });
        }

        private void UpdateDebugPanelPosition()
        {
            if (_debugWin == null) return;
            if (!_debugWin.IsVisible) return;

            _debugWin.Top = Top + 12;
            _debugWin.Height = Math.Max(200, Height - 24);
        }

        private static void AnimateWindow(Window w, double left, double top, double opacity, Action onDone = null)
        {
            var dur = new Duration(TimeSpan.FromMilliseconds(180));

            var aLeft = new DoubleAnimation(left, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var aTop = new DoubleAnimation(top, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
            var aOp = new DoubleAnimation(opacity, dur) { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };

            int done = 0;
            void Finish()
            {
                done++;
                if (done >= 3) onDone?.Invoke();
            }

            aLeft.Completed += (_, __) => Finish();
            aTop.Completed += (_, __) => Finish();
            aOp.Completed += (_, __) => Finish();

            w.BeginAnimation(LeftProperty, aLeft);
            w.BeginAnimation(TopProperty, aTop);
            w.BeginAnimation(OpacityProperty, aOp);
        }

        private void TopBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2) return;
            DragMove();
        }

        private void HideFromAltTab_Click(object sender, RoutedEventArgs e)
        {
            ShowInTaskbar = false;
            Hide();
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
