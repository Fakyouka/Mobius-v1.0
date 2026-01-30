using System.Collections.ObjectModel;
using Mobius.Utils;

namespace Mobius.Models
{
    public sealed class AppEntryModel : ObservableObject
    {
        private string _name;
        private string _sourceText;
        private AppSourceType _sourceType;

        private string _exePath;
        private int _appId;
        private string _installDir;

        private string _iconPath;

        private bool _isRunning;
        private bool _speechEnabled = true;

        public string Name { get => _name; set => Set(ref _name, value); }
        public string SourceText { get => _sourceText; set => Set(ref _sourceText, value); }
        public AppSourceType SourceType { get => _sourceType; set => Set(ref _sourceType, value); }

        public string ExePath { get => _exePath; set => Set(ref _exePath, value); }

        public int AppId { get => _appId; set => Set(ref _appId, value); }
        public string InstallDir { get => _installDir; set => Set(ref _installDir, value); }

        public string IconPath { get => _iconPath; set => Set(ref _iconPath, value); }

        public bool IsRunning { get => _isRunning; set => Set(ref _isRunning, value); }
        public bool SpeechEnabled { get => _speechEnabled; set => Set(ref _speechEnabled, value); }

        // ✅ multi phrases
        public ObservableCollection<PhraseModel> Phrases { get; } = new ObservableCollection<PhraseModel>();
    }
}
