using System.ComponentModel;
using Mobius.Services.Voice;
using Mobius.Utils;

namespace Mobius.ViewModels
{
    public sealed class MainViewModel : ObservableObject
    {
        private object _currentView;

        public MainViewModel()
        {
            Library = new LibraryViewModel(this);
            Settings = new SettingsViewModel(this);
            Voice = new VoiceCoordinator(this);

            CurrentView = Library;

            Library.PropertyChanged += OnLibraryChanged;
            Settings.PropertyChanged += OnSettingsChanged;

            Voice.UpdateState();
        }

        public LibraryViewModel Library { get; }
        public SettingsViewModel Settings { get; }
        public VoiceCoordinator Voice { get; }

        public object CurrentView
        {
            get => _currentView;
            set => Set(ref _currentView, value);
        }

        public void ShowLibrary() => CurrentView = Library;
        public void ShowSettings() => CurrentView = Settings;

        public void ToggleSettings()
        {
            if (ReferenceEquals(CurrentView, Settings)) ShowLibrary();
            else ShowSettings();
        }

        private void OnLibraryChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(LibraryViewModel.SpeechMasterEnabled))
                Voice.UpdateState();
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.SelectedMicrophone))
                Voice.UpdateState();
            if (e.PropertyName == nameof(SettingsViewModel.VoskModelPath))
                Voice.UpdateState();
        }
    }
}
