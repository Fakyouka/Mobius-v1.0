using Mobius.Utils;

namespace Mobius.ViewModels
{
    public sealed class AddAppViewModel : ObservableObject
    {
        private string _name;
        private string _exePath;
        private string _iconPath;

        public string Name { get => _name; set => Set(ref _name, value); }
        public string ExePath { get => _exePath; set => Set(ref _exePath, value); }
        public string IconPath { get => _iconPath; set => Set(ref _iconPath, value); }
    }
}
