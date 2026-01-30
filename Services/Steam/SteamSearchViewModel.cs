using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using Mobius.Services.Steam;
using Mobius.Utils;

namespace Mobius.ViewModels
{
    public sealed class SteamSearchViewModel : ObservableObject
    {
        private readonly SteamApiClient _api = new SteamApiClient();

        private string _query;
        private bool _isBusy;
        private string _status;

        public SteamSearchViewModel()
        {
            SearchCommand = new RelayCommand(async () => await SearchAsync(), () => !IsBusy && !string.IsNullOrWhiteSpace(Query));
            AddSelectedCommand = new RelayCommand(() => RequestAddSelected?.Invoke(Selected), () => Selected != null);
            CloseCommand = new RelayCommand(() => RequestClose?.Invoke());
        }

        public ObservableCollection<SteamGameResult> Results { get; } = new ObservableCollection<SteamGameResult>();

        public SteamGameResult Selected
        {
            get => _selected;
            set
            {
                if (Set(ref _selected, value))
                    AddSelectedCommand.RaiseCanExecuteChanged();
            }
        }
        private SteamGameResult _selected;

        public string Query
        {
            get => _query;
            set
            {
                if (Set(ref _query, value))
                    SearchCommand.RaiseCanExecuteChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            private set
            {
                if (Set(ref _isBusy, value))
                    SearchCommand.RaiseCanExecuteChanged();
            }
        }

        public string Status
        {
            get => _status;
            private set => Set(ref _status, value);
        }

        public RelayCommand SearchCommand { get; }
        public RelayCommand AddSelectedCommand { get; }
        public RelayCommand CloseCommand { get; }

        public event Action<SteamGameResult> RequestAddSelected;
        public event Action RequestClose;

        private async Task SearchAsync()
        {
            if (IsBusy) return;
            if (string.IsNullOrWhiteSpace(Query)) return;

            IsBusy = true;
            Status = "Ищу в Steam…";
            Results.Clear();
            Selected = null;

            try
            {
                var q = Query.Trim();

                var items = await Task.Run(() => _api.Search(q));
                foreach (var it in items)
                    Results.Add(it);

                Status = items.Count == 0 ? "Ничего не найдено" : $"Найдено: {items.Count}";
            }
            catch (Exception ex)
            {
                Status = "Ошибка поиска: " + ex.Message;
            }
            finally
            {
                IsBusy = false;
            }
        }
    }
}
