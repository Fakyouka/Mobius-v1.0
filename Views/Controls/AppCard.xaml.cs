using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mobius.Models;

namespace Mobius.Views.Controls
{
    public partial class AppCard : UserControl
    {
        private string _backupName;

        public AppCard()
        {
            InitializeComponent();

            MouseLeftButtonDown += OnMouseLeftButtonDown;
            MouseRightButtonUp += OnMouseRightButtonUp;
        }

        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // double click anywhere on card -> edit title (design)
            if (e.ClickCount == 2)
            {
                EnterTitleEditMode();
                e.Handled = true;
            }
        }

        private void EnterTitleEditMode()
        {
            var model = DataContext as AppEntryModel;
            _backupName = model?.Name;

            if (TitleText != null) TitleText.Visibility = Visibility.Collapsed;
            if (TitleEdit != null)
            {
                TitleEdit.Visibility = Visibility.Visible;
                TitleEdit.Focus();
                TitleEdit.SelectAll();
            }
        }

        private void ExitTitleEditMode(bool commit)
        {
            var model = DataContext as AppEntryModel;

            if (!commit && model != null)
            {
                // отмена -> вернуть старое имя
                model.Name = _backupName ?? model.Name;
            }

            if (TitleEdit != null) TitleEdit.Visibility = Visibility.Collapsed;
            if (TitleText != null) TitleText.Visibility = Visibility.Visible;
        }

        private void TitleEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ExitTitleEditMode(commit: true);
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                ExitTitleEditMode(commit: false);
                e.Handled = true;
            }
        }

        private void TitleEdit_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            // потеряли фокус -> считаем что сохранили
            ExitTitleEditMode(commit: true);
        }

        private void OnMouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            // контекстное меню задается в LibraryPage.xaml
        }
    }
}
