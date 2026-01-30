using System;
using NAudio.Wave;
using Newtonsoft.Json.Linq;
using Vosk;

namespace Mobius.Services.Voice
{
    public class VoskVoiceService : IDisposable
    {
        public event Action<string> PartialRecognized;
        public event Action<string> FinalRecognized;

        private Model _model;
        private VoskRecognizer _recognizer;
        private WaveInEvent _waveIn;

        public string ModelPath { get; set; } = "vosk-model";
        public int DeviceNumber { get; set; } = 0;
        public bool IsRunning { get; private set; }

        public void Start()
        {
            if (IsRunning)
                return;

            Vosk.Vosk.SetLogLevel(0);

            _model = new Model(ModelPath);
            _recognizer = new VoskRecognizer(_model, 16000.0f);

            _waveIn = new WaveInEvent();
            _waveIn.DeviceNumber = DeviceNumber;
            _waveIn.WaveFormat = new WaveFormat(16000, 1);
            _waveIn.DataAvailable += OnDataAvailable;
            _waveIn.StartRecording();

            IsRunning = true;
        }

        private void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            if (_recognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var json = _recognizer.Result();
                var text = JObject.Parse(json)["text"]?.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                    FinalRecognized?.Invoke(text);
            }
            else
            {
                var json = _recognizer.PartialResult();
                var text = JObject.Parse(json)["partial"]?.ToString();

                if (!string.IsNullOrWhiteSpace(text))
                    PartialRecognized?.Invoke(text);
            }
        }

        public void Stop()
        {
            if (!IsRunning)
                return;

            _waveIn.StopRecording();
            _waveIn.DataAvailable -= OnDataAvailable;
            _waveIn.Dispose();

            _recognizer.Dispose();
            _model.Dispose();

            _waveIn = null;
            _recognizer = null;
            _model = null;

            IsRunning = false;
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
