using System;
using System.Collections.ObjectModel;
using System.IO;
using Mobius.Utils;
using WinFormsFolderBrowserDialog = System.Windows.Forms.FolderBrowserDialog;
using WinFormsDialogResult = System.Windows.Forms.DialogResult;
using NAudio.CoreAudioApi;

namespace Mobius.ViewModels
{
    public sealed class SettingsViewModel : ObservableObject
    {
        private readonly MainViewModel _root;

        private MicDevice _selectedMicrophone;
        private string _saveFolder;
        private bool _debugEnabled;
        private string _voskModelPath;

        public SettingsViewModel(MainViewModel root)
        {
            _root = root;

            BackCommand = new RelayCommand(() => _root.ShowLibrary());
            ChooseSaveFolderCommand = new RelayCommand(ChooseSaveFolder);
            SaveToFileCommand = new RelayCommand(SaveToFile);
            LoadFromFileCommand = new RelayCommand(LoadFromFile);

            SaveFolder = Directory.GetCurrentDirectory();
            VoskModelPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "vosk-model");

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
                    _root.Voice.UpdateState();
            }
        }

        public string SelectedMicrophoneName => _selectedMicrophone?.Name;

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
                    _root.Library.DebugEnabled = value;
                    _root.Library.DebugPanelOpen = value;
                }
            }
        }

        public string VoskModelPath
        {
            get => _voskModelPath;
            set
            {
                if (Set(ref _voskModelPath, value))
                    _root.Voice.UpdateState();
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
            catch
            {
                Microphones.Add(new MicDevice("default", "Default Microphone"));
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
            File.WriteAllText(file, "{ \"todo\": \"serialize\" }");
        }

        private void LoadFromFile()
        {
            // TODO
        }

        public sealed class MicDevice
        {
            public MicDevice(string id, string name) { Id = id; Name = name; }
            public string Id { get; }
            public string Name { get; }
            public override string ToString() => Name;
        }
    }
}
