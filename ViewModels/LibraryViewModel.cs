using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using Mobius.Models;
using Mobius.Utils;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Mobius.ViewModels
{
    public sealed class LibraryViewModel : ObservableObject
    {
        private readonly MainViewModel _root;

        private bool _speechMasterEnabled = true;
        private bool _deleteMode;
        private bool _debugEnabled;
        private bool _debugPanelOpen;

        public LibraryViewModel(MainViewModel root)
        {
            _root = root;

            RefreshCommand = new RelayCommand(Refresh);
            ToggleSpeechCommand = new RelayCommand(ToggleSpeech);

            // ✅ toggle settings
            OpenSettingsCommand = new RelayCommand(() => _root.ToggleSettings());

            AddAppCommand = new RelayCommand(AddApp);
            EnterDeleteModeCommand = new RelayCommand(() => DeleteMode = true, () => Apps.Count > 0);
            ExitDeleteModeCommand = new RelayCommand(() => DeleteMode = false);

            ToggleDebugPanelCommand = new RelayCommand(() => DebugPanelOpen = !DebugPanelOpen);

            CardClickCommand = new RelayCommand<AppEntryModel>(LaunchOrSelect);
            ChangeIconCommand = new RelayCommand<AppEntryModel>(ChangeIcon);

            RunContextCommand = new RelayCommand<AppEntryModel>(LaunchOrSelect);
            AddPhraseContextCommand = new RelayCommand<AppEntryModel>(AddDefaultPhrase);
            RemovePhraseContextCommand = new RelayCommand<AppEntryModel>(RemoveLastPhrase);

            // demo
            Apps.Add(new AppEntryModel { Name = "Counter-Strike 2", SourceType = AppSourceType.Steam, SourceText = "Steam • AppID: 730" });
            Apps.Add(new AppEntryModel { Name = "Dota 2", SourceType = AppSourceType.Steam, SourceText = "Steam • AppID: 570" });
            Apps.Add(new AppEntryModel { Name = "Satisfactory", SourceType = AppSourceType.Steam, SourceText = "Steam • AppID: 526870" });

            foreach (var a in Apps)
            {
                a.Phrases.Add("запусти " + a.Name.ToLowerInvariant());
                a.Phrases.Add(a.Name.ToLowerInvariant());
            }
        }

        public ObservableCollection<AppEntryModel> Apps { get; } = new ObservableCollection<AppEntryModel>();
        public ObservableCollection<string> DebugLogs { get; } = new ObservableCollection<string>();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ToggleSpeechCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }

        public RelayCommand AddAppCommand { get; }
        public RelayCommand EnterDeleteModeCommand { get; }
        public RelayCommand ExitDeleteModeCommand { get; }

        public RelayCommand ToggleDebugPanelCommand { get; }

        public RelayCommand<AppEntryModel> CardClickCommand { get; }
        public RelayCommand<AppEntryModel> ChangeIconCommand { get; }

        public RelayCommand<AppEntryModel> RunContextCommand { get; }
        public RelayCommand<AppEntryModel> AddPhraseContextCommand { get; }
        public RelayCommand<AppEntryModel> RemovePhraseContextCommand { get; }

        public bool SpeechMasterEnabled
        {
            get => _speechMasterEnabled;
            set
            {
                if (Set(ref _speechMasterEnabled, value))
                {
                    foreach (var a in Apps) a.SpeechEnabled = value;
                    Raise(nameof(SpeechButtonLabel));
                    AddLog($"Speech master: {(value ? "ON" : "OFF")}");
                }
            }
        }

        public string SpeechButtonLabel => SpeechMasterEnabled ? "🎤" : "🔇";

        public bool DeleteMode
        {
            get => _deleteMode;
            set
            {
                if (Set(ref _deleteMode, value))
                    Raise(nameof(DeleteModeLabel));
            }
        }

        public string DeleteModeLabel => $"Режим удаления: {(DeleteMode ? "Включен" : "Выключен")}";

        public bool DebugEnabled
        {
            get => _debugEnabled;
            set
            {
                if (Set(ref _debugEnabled, value))
                {
                    if (!value) DebugPanelOpen = false;
                    AddLog($"Debug enabled: {(value ? "ON" : "OFF")}");
                }
            }
        }

        public bool DebugPanelOpen
        {
            get => _debugPanelOpen;
            set => Set(ref _debugPanelOpen, value);
        }

        public void AddLog(string text)
        {
            if (!DebugEnabled) return;
            DebugLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {text}");
        }

        private void Refresh() => AddLog("Refresh list");

        private void ToggleSpeech() => SpeechMasterEnabled = !SpeechMasterEnabled;

        private void AddApp()
        {
            var exeDlg = new Win32OpenFileDialog
            {
                Title = "Выбор exe-файла",
                Filter = "Executable (*.exe)|*.exe|All files (*.*)|*.*"
            };

            if (exeDlg.ShowDialog() != true) return;

            var iconDlg = new Win32OpenFileDialog
            {
                Title = "Выбор иконки (не обязательно)",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|All files (*.*)|*.*"
            };

            string icon = null;
            if (iconDlg.ShowDialog() == true) icon = iconDlg.FileName;

            var name = Path.GetFileNameWithoutExtension(exeDlg.FileName);

            var app = new AppEntryModel
            {
                Name = name,
                ExePath = exeDlg.FileName,
                IconPath = icon,
                SourceType = AppSourceType.Manual,
                SourceText = "Local • Manual"
            };
            app.Phrases.Add("запусти " + name.ToLowerInvariant());
            Apps.Insert(0, app);

            AddLog($"Added app: {name}");
        }

        public void LaunchOrSelect(AppEntryModel app)
        {
            if (app == null) return;

            if (DeleteMode)
            {
                Apps.Remove(app);
                AddLog($"Removed app: {app.Name}");
                return;
            }

            foreach (var a in Apps) a.IsRunning = false;
            app.IsRunning = true;

            AddLog($"Launch: {app.Name}");

            try
            {
                if (!string.IsNullOrWhiteSpace(app.ExePath) && File.Exists(app.ExePath))
                    Process.Start(app.ExePath);
            }
            catch (Exception ex)
            {
                AddLog("Launch error: " + ex.Message);
            }
        }

        public void ChangeIcon(AppEntryModel app)
        {
            if (app == null) return;

            var dlg = new Win32OpenFileDialog
            {
                Title = "Выбор иконки",
                Filter = "Images (*.png;*.jpg;*.jpeg;*.ico)|*.png;*.jpg;*.jpeg;*.ico|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            app.IconPath = dlg.FileName;
            AddLog($"Icon changed: {app.Name}");
        }

        private void AddDefaultPhrase(AppEntryModel app)
        {
            if (app == null) return;
            app.Phrases.Add("новая фраза");
            AddLog($"Phrase added for {app.Name}");
        }

        public void RemoveLastPhrase(AppEntryModel app)
        {
            if (app == null) return;
            if (app.Phrases.Count == 0) return;

            var phrase = app.Phrases[app.Phrases.Count - 1];
            app.Phrases.RemoveAt(app.Phrases.Count - 1);
            AddLog($"Phrase removed for {app.Name}: {phrase}");
        }
    }
}
