using Mobius.Models;
using Mobius.Services;
using Mobius.Services.Steam;
using Mobius.Services.Voice;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace Mobius.ViewModels
{
    public class LibraryViewModel : ViewModelBase
    {
        private readonly ConfigStore _config;
        private readonly SteamService _steam;
        private readonly SteamApiClient _steamApi;
        private readonly VoiceCoordinator _voice;

        private readonly DispatcherTimer _runningPollTimer;

        public ObservableCollection<AppModel> Apps { get; } = new ObservableCollection<AppModel>();

        private AppModel _selectedApp;
        public AppModel SelectedApp
        {
            get => _selectedApp;
            set
            {
                if (_selectedApp != value)
                {
                    _selectedApp = value;
                    OnPropertyChanged(nameof(SelectedApp));
                }
            }
        }

        private bool _speechMasterEnabled = true;
        public bool SpeechMasterEnabled
        {
            get => _speechMasterEnabled;
            set
            {
                if (_speechMasterEnabled != value)
                {
                    _speechMasterEnabled = value;
                    OnPropertyChanged(nameof(SpeechMasterEnabled));
                    _ = ApplySpeechStateAsync();
                }
            }
        }

        // ФАКТИЧЕСКОЕ состояние: движок реально запущен/не запущен
        private bool _speechIsRunning;
        public bool SpeechIsRunning
        {
            get => _speechIsRunning;
            set
            {
                if (_speechIsRunning != value)
                {
                    _speechIsRunning = value;
                    OnPropertyChanged(nameof(SpeechIsRunning));
                }
            }
        }

        public ICommand RefreshCommand { get; }
        public ICommand AddAppCommand { get; }
        public ICommand LaunchCommand { get; }
        public ICommand ChangeIconCommand { get; }
        public ICommand AddPhraseCommand { get; }
        public ICommand RemovePhraseCommand { get; }
        public ICommand ClearPhrasesCommand { get; }

        public LibraryViewModel()
        {
            _config = new ConfigStore();
            _steam = new SteamService();
            _steamApi = new SteamApiClient();
            _voice = new VoiceCoordinator();

            RefreshCommand = new RelayCommand(async _ => await RefreshAsync());
            AddAppCommand = new RelayCommand(_ => AddApp());
            LaunchCommand = new RelayCommand(_ => LaunchSelected());
            ChangeIconCommand = new RelayCommand(_ => ChangeIconForSelected());
            AddPhraseCommand = new RelayCommand(_ => AddPhraseToSelected());
            RemovePhraseCommand = new RelayCommand(p => RemovePhraseFromSelected(p as PhraseModel));
            ClearPhrasesCommand = new RelayCommand(_ => ClearPhrasesForSelected());

            // Периодическая проверка "приложение реально запущено?"
            _runningPollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _runningPollTimer.Tick += (_, __) =>
            {
                try { UpdateRunningState(); }
                catch { /* не валим UI */ }
            };
            _runningPollTimer.Start();

            // Подписка на обновления статуса голоса
            _voice.StateChanged += (_, __) =>
            {
                Application.Current?.Dispatcher?.Invoke(() =>
                {
                    SpeechIsRunning = _voice.IsRunning;
                });
            };

            // Стартовое состояние
            SpeechIsRunning = _voice.IsRunning;
        }

        public async Task RefreshAsync()
        {
            try
            {
                Debug.WriteLine("[LIBRARY] RefreshAsync CALLED");

                // 1) загрузка сохраненной библиотеки из конфига
                var saved = _config.LoadApps() ?? new List<AppModel>();

                Apps.Clear();
                foreach (var a in saved)
                {
                    NormalizeApp(a);
                    Apps.Add(a);
                }

                // 2) поиск установленных Steam игр
                var steamGames = _steam.GetInstalledGames()?.ToList() ?? new List<AppModel>();

                // 3) мёрдж: обновляем существующие, добавляем новые
                var byId = Apps
                    .Where(a => a.SourceType == AppSourceType.Steam && !string.IsNullOrWhiteSpace(a.SourceId))
                    .ToDictionary(a => a.SourceId, a => a);

                bool changed = false;

                foreach (var sg in steamGames)
                {
                    NormalizeApp(sg);

                    if (!string.IsNullOrWhiteSpace(sg.SourceId) && byId.TryGetValue(sg.SourceId, out var existing))
                    {
                        // обновляем только "технику", не трогаем фразы/переключатели
                        existing.Name = sg.Name;
                        existing.InstallDir = sg.InstallDir;
                        existing.ExePath = sg.ExePath;
                        existing.SourceText = sg.SourceText;

                        // если иконки нет — попробуем добыть
                        if (string.IsNullOrWhiteSpace(existing.IconPath))
                        {
                            existing.IconPath = await ResolveSteamIconAsync(existing);
                            changed = true;
                        }
                    }
                    else
                    {
                        // новая Steam игра
                        sg.IconPath = await ResolveSteamIconAsync(sg);

                        // дефолтные фразы (если модель их использует)
                        if (sg.Phrases == null)
                            sg.Phrases = new ObservableCollection<PhraseModel>();

                        if (sg.Phrases.Count == 0)
                        {
                            sg.Phrases.Add(new PhraseModel { Text = "запусти" });
                            sg.Phrases.Add(new PhraseModel { Text = "открой" });
                        }

                        Apps.Add(sg);
                        changed = true;
                    }
                }

                // 4) сохраняем обратно, если изменилось или если конфиг пустой и мы что-то нашли
                if (changed || saved.Count != Apps.Count)
                {
                    _config.SaveApps(Apps.ToList());
                }

                // обновим статусы запущенности
                UpdateRunningState();

                Debug.WriteLine($"[LIBRARY] Apps count = {Apps?.Count}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LIBRARY] RefreshAsync ERROR: " + ex);
            }

            await ApplySpeechStateAsync();
        }

        private void NormalizeApp(AppModel a)
        {
            if (a == null) return;

            if (a.Phrases == null)
                a.Phrases = new ObservableCollection<PhraseModel>();

            if (a.SourceType == 0)
            {
                // на всякий случай для старых конфигов
                if (!string.IsNullOrWhiteSpace(a.SourceText) && a.SourceText.Contains("steam", StringComparison.OrdinalIgnoreCase))
                    a.SourceType = AppSourceType.Steam;
                else
                    a.SourceType = AppSourceType.Local;
            }
        }

        private async Task<string> ResolveSteamIconAsync(AppModel app)
        {
            try
            {
                if (app == null || app.SourceType != AppSourceType.Steam || string.IsNullOrWhiteSpace(app.SourceId))
                    return app?.IconPath;

                // 1) локальный cache Steam
                var local = SteamIconResolver.TryGetLocalIconPath(app.SourceId);
                if (!string.IsNullOrWhiteSpace(local) && File.Exists(local))
                    return local;

                // 2) CDN по appid
                var downloaded = await _steamApi.DownloadIconAsync(app.SourceId);
                if (!string.IsNullOrWhiteSpace(downloaded) && File.Exists(downloaded))
                    return downloaded;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[STEAM] ResolveSteamIconAsync ERROR: " + ex);
            }

            return app?.IconPath;
        }

        private void AddApp()
        {
            // существующая логика "ручного" добавления (оставил как было в проекте)
            // Тут важно: после добавления - сохраняем.
            var app = new AppModel
            {
                Name = "Новое приложение",
                SourceType = AppSourceType.Local,
                Phrases = new ObservableCollection<PhraseModel>()
            };

            Apps.Add(app);
            SelectedApp = app;
            _config.SaveApps(Apps.ToList());
        }

        private void LaunchSelected()
        {
            if (SelectedApp == null) return;
            _ = LaunchAsync(SelectedApp);
        }

        private async Task LaunchAsync(AppModel app)
        {
            try
            {
                if (app == null) return;

                if (app.SourceType == AppSourceType.Steam && !string.IsNullOrWhiteSpace(app.SourceId))
                {
                    // Steam запуск
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = $"steam://run/{app.SourceId}",
                        UseShellExecute = true
                    });
                }
                else
                {
                    // Local запуск
                    if (!string.IsNullOrWhiteSpace(app.ExePath) && File.Exists(app.ExePath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = app.ExePath,
                            WorkingDirectory = Path.GetDirectoryName(app.ExePath),
                            UseShellExecute = true
                        });
                    }
                }

                await Task.Delay(500);
                UpdateRunningState();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[LAUNCH] ERROR: " + ex);
            }
        }

        private void ChangeIconForSelected()
        {
            if (SelectedApp == null) return;

            // оставляем вашу текущую логику выбора файла иконки (если есть)
            // ВАЖНО: после изменения сохраняем
            // Тут пример заглушки — если у тебя уже есть диалог, просто вызови SaveApps в конце.
            _config.SaveApps(Apps.ToList());
        }

        private void AddPhraseToSelected()
        {
            if (SelectedApp == null) return;
            if (SelectedApp.Phrases == null)
                SelectedApp.Phrases = new ObservableCollection<PhraseModel>();

            SelectedApp.Phrases.Add(new PhraseModel { Text = "новая фраза" });
            _config.SaveApps(Apps.ToList());
        }

        private void RemovePhraseFromSelected(PhraseModel phrase)
        {
            if (SelectedApp == null || phrase == null) return;
            SelectedApp.Phrases?.Remove(phrase);
            _config.SaveApps(Apps.ToList());
        }

        private void ClearPhrasesForSelected()
        {
            if (SelectedApp == null) return;
            SelectedApp.Phrases?.Clear();
            _config.SaveApps(Apps.ToList());
        }

        private async Task ApplySpeechStateAsync()
        {
            try
            {
                if (!SpeechMasterEnabled)
                {
                    await _voice.StopAsync();
                }
                else
                {
                    await _voice.StartAsync();
                }

                SpeechIsRunning = _voice.IsRunning;
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[VOICE] ApplySpeechStateAsync ERROR: " + ex);
            }
        }

        private void UpdateRunningState()
        {
            // ВАЖНО: зелёный только если реально запущено, а не по клику.
            foreach (var a in Apps)
                a.IsRunning = false;

            // 1) Local: по exe
            foreach (var a in Apps.Where(x => x.SourceType == AppSourceType.Local))
            {
                if (IsLocalAppRunning(a))
                    a.IsRunning = true;
            }

            // 2) Steam: по процессам в папке игры
            foreach (var a in Apps.Where(x => x.SourceType == AppSourceType.Steam))
            {
                if (IsSteamAppRunning(a))
                    a.IsRunning = true;
            }
        }

        private bool IsLocalAppRunning(AppModel a)
        {
            try
            {
                if (a == null) return false;

                if (!string.IsNullOrWhiteSpace(a.ExePath) && File.Exists(a.ExePath))
                {
                    var exeName = Path.GetFileNameWithoutExtension(a.ExePath);
                    return Process.GetProcessesByName(exeName).Any();
                }

                // fallback по имени
                if (!string.IsNullOrWhiteSpace(a.Name))
                    return Process.GetProcesses().Any(p => SafeEquals(p.ProcessName, a.Name));

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool IsSteamAppRunning(AppModel a)
        {
            try
            {
                if (a == null) return false;

                if (string.IsNullOrWhiteSpace(a.InstallDir) || !Directory.Exists(a.InstallDir))
                    return false;

                var root = Path.GetFullPath(a.InstallDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

                foreach (var p in Process.GetProcesses())
                {
                    try
                    {
                        // MainModule может падать без прав — игнорируем
                        var path = p.MainModule?.FileName;
                        if (string.IsNullOrWhiteSpace(path)) continue;

                        var full = Path.GetFullPath(path);
                        if (full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                    catch
                    {
                        // ignore
                    }
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        private bool SafeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.Trim(), b.Trim(), StringComparison.OrdinalIgnoreCase);
        }
    }

    // Простая реализация RelayCommand (если у тебя уже есть — оставь свою)
    public class RelayCommand : ICommand
    {
        private readonly Action<object> _execute;
        private readonly Predicate<object> _canExecute;

        public RelayCommand(Action<object> execute, Predicate<object> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public bool CanExecute(object parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object parameter) => _execute(parameter);

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
    }
}
