using Mobius.Utils;

namespace Mobius.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private object _currentView;
        private readonly LibraryViewModel _library;
        private readonly SettingsViewModel _settings;

        public MainViewModel()
        {
            _library = new LibraryViewModel(this);
            _settings = new SettingsViewModel(this);
            CurrentView = _library;
        }

        public object CurrentView
        {
            get => _currentView;
            set => Set(ref _currentView, value);
        }

        public LibraryViewModel Library => _library;
        public SettingsViewModel Settings => _settings;

        public void ShowLibrary() => CurrentView = _library;
        public void ShowSettings() => CurrentView = _settings;

        // ✅ toggle: если уже Settings -> закрыть (вернуть Library)
        public void ToggleSettings()
        {
            if (ReferenceEquals(CurrentView, _settings)) ShowLibrary();
            else ShowSettings();
        }
    }
}
