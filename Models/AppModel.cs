using System.Collections.ObjectModel;

namespace Mobius.Models
{
    public class AppModel
    {
        public string Name { get; set; }
        public string ExePath { get; set; }
        public string IconPath { get; set; }

        public bool IsSteam { get; set; }
        public int SteamAppId { get; set; }

        public bool VoiceEnabled { get; set; } = true;

        public ObservableCollection<string> Phrases { get; set; }
            = new ObservableCollection<string>();
    }
}
