using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Mobius.Models;
using Mobius.Services.Steam;
using Mobius.Utils;
using Win32OpenFileDialog = Microsoft.Win32.OpenFileDialog;

namespace Mobius.ViewModels
{
    public sealed class LibraryViewModel : ObservableObject
    {
        private readonly MainViewModel _root;

        private bool _speechMasterEnabled = true;
        private bool _debugEnabled;
        private bool _debugPanelOpen;

        private string _lastHeardPartial;
        private string _lastHeardFinal;

        // app settings modal
        private bool _isAppSettingsOpen;
        private AppEntryModel _selectedApp;

        public LibraryViewModel(MainViewModel root)
        {
            _root = root;

            RefreshCommand = new RelayCommand(async () => await RefreshAsync());
            ToggleSpeechCommand = new RelayCommand(() => SpeechMasterEnabled = !SpeechMasterEnabled);
            OpenSettingsCommand = new RelayCommand(() => _root.ToggleSettings());

            AddAppCommand = new RelayCommand(AddApp);

            CardClickCommand = new RelayCommand<AppEntryModel>(LaunchOrSelect);
            ChangeIconCommand = new RelayCommand<AppEntryModel>(ChangeIcon);

            OpenAppSettingsCommand = new RelayCommand<AppEntryModel>(OpenAppSettings);
            CloseAppSettingsCommand = new RelayCommand(() => IsAppSettingsOpen = false);

            AddPhraseCommand = new RelayCommand(AddPhrase, () => SelectedApp != null);
            RemovePhraseCommand = new RelayCommand<PhraseModel>(RemovePhrase);

            _ = RefreshAsync();
        }

        public ObservableCollection<AppEntryModel> Apps { get; } = new ObservableCollection<AppEntryModel>();
        public ObservableCollection<string> DebugLogs { get; } = new ObservableCollection<string>();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand ToggleSpeechCommand { get; }
        public RelayCommand OpenSettingsCommand { get; }

        public RelayCommand AddAppCommand { get; }

        public RelayCommand<AppEntryModel> CardClickCommand { get; }
        public RelayCommand<AppEntryModel> ChangeIconCommand { get; }

        public RelayCommand<AppEntryModel> OpenAppSettingsCommand { get; }
        public RelayCommand CloseAppSettingsCommand { get; }

        public RelayCommand AddPhraseCommand { get; }
        public RelayCommand<PhraseModel> RemovePhraseCommand { get; }

        /// <summary>
        /// То, что рисуется на кнопке включения/выключения распознавания речи (MainWindow.xaml).
        /// </summary>
        public string SpeechButtonLabel => SpeechMasterEnabled ? "🎤" : "🔇";

        public bool SpeechMasterEnabled
        {
            get => _speechMasterEnabled;
            set
            {
                if (Set(ref _speechMasterEnabled, value))
                {
                    // Обновить текст/иконку на кнопке
                    Raise(nameof(SpeechButtonLabel));

                    AddLog("Speech master: " + (value ? "ON" : "OFF"));
                }
            }
        }

        public bool DebugEnabled
        {
            get => _debugEnabled;
            set
            {
                if (Set(ref _debugEnabled, value))
                {
                    if (!value) DebugPanelOpen = false;
                }
            }
        }

        public bool DebugPanelOpen
        {
            get => _debugPanelOpen;
            set => Set(ref _debugPanelOpen, value);
        }

        public string LastHeardPartial
        {
            get => _lastHeardPartial;
            set => Set(ref _lastHeardPartial, value);
        }

        public string LastHeardFinal
        {
            get => _lastHeardFinal;
            set => Set(ref _lastHeardFinal, value);
        }

        public bool IsAppSettingsOpen
        {
            get => _isAppSettingsOpen;
            set => Set(ref _isAppSettingsOpen, value);
        }

        public AppEntryModel SelectedApp
        {
            get => _selectedApp;
            set
            {
                if (Set(ref _selectedApp, value))
                    AddPhraseCommand.RaiseCanExecuteChanged();
            }
        }

        public void AddLog(string text)
        {
            if (!DebugEnabled) return;
            DebugLogs.Insert(0, $"[{DateTime.Now:HH:mm:ss}] {text}");
        }

        private async Task RefreshAsync()
        {
            var steamPath = SteamService.TryGetSteamPath();
            var games = await Task.Run(() => SteamService.GetInstalledGames());

            var items = await Task.Run(() =>
            {
                return games.Select(g =>
                {
                    var icon = SteamIconResolver.TryResolveSquareIcon(steamPath, g.AppId);
                    var app = new AppEntryModel
                    {
                        Name = g.Name,
                        SourceType = AppSourceType.Steam,
                        SourceText = $"Steam • AppID: {g.AppId}",
                        AppId = g.AppId,
                        InstallDir = g.InstallDir,
                        IconPath = icon,
                        SpeechEnabled = true
                    };

                    // ✅ FIX: PhraseModel instead of string
                    app.Phrases.Add(new PhraseModel(g.Name.ToLowerInvariant()));
                    app.Phrases.Add(new PhraseModel("запусти " + g.Name.ToLowerInvariant()));

                    return app;
                }).ToList();
            });

            Apps.Clear();
            foreach (var i in items) Apps.Add(i);

            AddLog($"Steam scan: {Apps.Count} apps");
        }

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
                SourceText = "Local • Manual",
                SpeechEnabled = true
            };

            // ✅ FIX
            app.Phrases.Add(new PhraseModel(name.ToLowerInvariant()));
            app.Phrases.Add(new PhraseModel("запусти " + name.ToLowerInvariant()));

            Apps.Insert(0, app);
        }

        public void LaunchFromVoice(AppEntryModel app)
        {
            if (app == null) return;
            LaunchOrSelect(app);
        }

        public void LaunchOrSelect(AppEntryModel app)
        {
            if (app == null) return;

            foreach (var a in Apps) a.IsRunning = false;
            app.IsRunning = true;

            try
            {
                if (app.SourceType == AppSourceType.Steam && app.AppId > 0)
                {
                    Process.Start("steam://run/" + app.AppId);
                    return;
                }

                if (!string.IsNullOrWhiteSpace(app.ExePath) && File.Exists(app.ExePath))
                {
                    Process.Start(app.ExePath);
                }
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
        }

        private void OpenAppSettings(AppEntryModel app)
        {
            if (app == null) return;
            SelectedApp = app;
            IsAppSettingsOpen = true;
        }

        private void AddPhrase()
        {
            if (SelectedApp == null) return;
            SelectedApp.Phrases.Add(new PhraseModel("новая фраза"));
        }

        private void RemovePhrase(PhraseModel phrase)
        {
            if (SelectedApp == null || phrase == null) return;
            SelectedApp.Phrases.Remove(phrase);
        }
    }
}
