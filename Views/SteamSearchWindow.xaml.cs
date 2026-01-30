using System.Windows;
using Mobius.Services.Steam;
using Mobius.ViewModels;

namespace Mobius.Views
{
    public partial class SteamSearchWindow : Window
    {
        private readonly SteamSearchViewModel _vm;

        public SteamSearchWindow()
        {
            InitializeComponent();

            _vm = new SteamSearchViewModel();
            DataContext = _vm;

            _vm.RequestClose += () => Close();
        }

        public void SetAddHandler(System.Action<SteamGameResult> onAdd)
        {
            _vm.RequestAddSelected += r =>
            {
                if (r == null) return;
                onAdd?.Invoke(r);
            };
        }
    }
}
