using System;
using System.Collections.ObjectModel;
using System.IO;
using Mobius.Utils;

// WinForms only for folder picker
using WinFormsFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;

// NAudio
using NAudio.CoreAudioApi;

namespace Mobius.ViewModels
{
    public sealed class SettingsViewModel : ObservableObject
    {
        private readonly MainViewModel _root;

        private MicDevice _selectedMicrophone;
        private string _saveFolder;
        private bool _debugEnabled;

        public SettingsViewModel(MainViewModel root)
        {
            _root = root;

            BackCommand = new RelayCommand(() => _root.ShowLibrary());
            ChooseSaveFolderCommand = new RelayCommand(ChooseSaveFolder);

            SaveToFileCommand = new RelayCommand(SaveToFile);
            LoadFromFileCommand = new RelayCommand(LoadFromFile);

            // default: рядом с exe (обычно CurrentDirectory)
            SaveFolder = Directory.GetCurrentDirectory();

            LoadMicrophones();
            if (Microphones.Count > 0)
                SelectedMicrophone = Microphones[0];
        }

        public ObservableCollection<MicDevice> Microphones { get; } = new ObservableCollection<MicDevice>();

        public MicDevice SelectedMicrophone
        {
            get => _selectedMicrophone;
            set
            {
                if (Set(ref _selectedMicrophone, value))
                {
                    // можно потом использовать value.Id в распознавании
                    _root.Library?.AddLog("Selected mic: " + (value?.Name ?? "(null)"));
                }
            }
        }

        public string SaveFolder
        {
            get => _saveFolder;
            set => Set(ref _saveFolder, value);
        }

        public bool DebugEnabled
        {
            get => _debugEnabled;
            set
            {
                if (Set(ref _debugEnabled, value))
                {
                    if (_root.Library != null)
                        _root.Library.DebugEnabled = value;
                }
            }
        }

        public RelayCommand BackCommand { get; }
        public RelayCommand ChooseSaveFolderCommand { get; }
        public RelayCommand SaveToFileCommand { get; }
        public RelayCommand LoadFromFileCommand { get; }

        private void LoadMicrophones()
        {
            Microphones.Clear();

            try
            {
                using (var enumerator = new MMDeviceEnumerator())
                {
                    var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                    foreach (var d in devices)
                        Microphones.Add(new MicDevice(d.ID, d.FriendlyName));
                }
            }
            catch (Exception ex)
            {
                // если NAudio не установлен/не доступен — не ломаем UI
                Microphones.Add(new MicDevice("default", "Default Microphone"));
                _root.Library?.AddLog("Mic enumerate failed: " + ex.Message);
            }
        }

        private void ChooseSaveFolder()
        {
            using (var dlg = new WinFormsFolderBrowserDialog())
            {
                dlg.Description = "Выберите папку для сохранений";
                dlg.SelectedPath = Directory.Exists(SaveFolder) ? SaveFolder : Directory.GetCurrentDirectory();

                if (dlg.ShowDialog() == WinFormsDialogResult.OK)
                    SaveFolder = dlg.SelectedPath;
            }
        }

        private void SaveToFile()
        {
            if (!Directory.Exists(SaveFolder))
                Directory.CreateDirectory(SaveFolder);

            var file = Path.Combine(SaveFolder, "mobius_config.json");

            // TODO: нормальная сериализация настроек/приложений
            File.WriteAllText(file, "{ \"todo\": \"serialize\" }");

            _root.Library?.AddLog("Saved config: " + file);
        }

        private void LoadFromFile()
        {
            // TODO: загрузка из SaveFolder (позже сделаем список файлов)
            _root.Library?.AddLog("Load: TODO (будет загрузка из папки сохранений)");
        }

        public sealed class MicDevice
        {
            public MicDevice(string id, string name)
            {
                Id = id;
                Name = name;
            }

            public string Id { get; }
            public string Name { get; }

            public override string ToString() => Name;
        }
    }
}
