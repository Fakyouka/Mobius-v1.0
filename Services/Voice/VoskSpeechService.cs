using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NAudio.Wave;

namespace Mobius.Services.Voice
{
    /// <summary>
    /// Сервис распознавания речи.
    /// ВАЖНО: Vosk подключается через reflection, чтобы проект собирался даже если NuGet Vosk не подтянулся.
    /// Если Vosk реально установлен/доступен — распознавание будет работать.
    ///
    /// Поддерживает Start(modelPath, microphoneName) — это нужно VoiceCoordinator'у.
    /// </summary>
    public sealed class VoskSpeechService : IDisposable
    {
        private readonly object _sync = new object();

        private string _modelPath = string.Empty;

        private WaveInEvent? _waveIn;

        private object? _voskModel;       // Vosk.Model
        private object? _voskRecognizer;  // Vosk.VoskRecognizer

        private bool _isRunning;
        private bool _isDisposed;

        // ----- То, что ожидает VoiceCoordinator -----

        public bool IsRunning
        {
            get { lock (_sync) return _isRunning; }
        }

        /// <summary>Промежуточный текст (partial).</summary>
        public event Action<string>? PartialText;

        /// <summary>Финальный текст (final).</summary>
        public event Action<string>? FinalText;

        /// <summary>Сырые JSON ответы Vosk (partial/final) — удобно для debug панели.</summary>
        public event Action<string>? VoskJson;

        // --------------------------------------------

        public void SetModelPath(string modelPath)
        {
            if (string.IsNullOrWhiteSpace(modelPath))
                throw new ArgumentException("modelPath is empty.", nameof(modelPath));

            if (!Directory.Exists(modelPath))
                throw new DirectoryNotFoundException($"Vosk model folder not found: {modelPath}");

            lock (_sync)
            {
                EnsureNotDisposed();
                EnsureNotRunning();
                _modelPath = modelPath;
            }
        }

        /// <summary>
        /// Синхронный запуск (старый вариант).
        /// </summary>
        public void Start(int deviceNumber = 0)
        {
            StartAsync(deviceNumber).GetAwaiter().GetResult();
        }

        /// <summary>
        /// НОВОЕ: запуск с путём к модели и именем микрофона.
        /// Это ровно под вызов из VoiceCoordinator: Start(modelPath, microphoneName)
        /// </summary>
        public void Start(string modelPath, string? microphoneName)
        {
            StartAsync(modelPath, microphoneName).GetAwaiter().GetResult();
        }

        /// <summary>
        /// Асинхронный запуск (старый вариант).
        /// </summary>
        public Task StartAsync(int deviceNumber = 0, CancellationToken ct = default)
        {
            lock (_sync)
            {
                EnsureNotDisposed();
                if (_isRunning) return Task.CompletedTask;

                if (string.IsNullOrWhiteSpace(_modelPath))
                    throw new InvalidOperationException("Model path is not set. Call SetModelPath() before Start().");

                // Создаём Vosk.Model и VoskRecognizer через reflection
                CreateVoskObjectsOrThrow();

                // Запускаем микрофон
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceNumber,
                    WaveFormat = new WaveFormat(16000, 1), // 16kHz mono (стандартно для Vosk моделей)
                    BufferMilliseconds = 50
                };

                _waveIn.DataAvailable += WaveInOnDataAvailable;
                _waveIn.RecordingStopped += WaveInOnRecordingStopped;

                _isRunning = true;
                _waveIn.StartRecording();
            }

            if (ct.CanBeCanceled)
            {
                ct.Register(() =>
                {
                    try { Stop(); } catch { /* ignore */ }
                });
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// НОВОЕ: асинхронный запуск с путём к модели и именем микрофона.
        /// </summary>
        public Task StartAsync(string modelPath, string? microphoneName, CancellationToken ct = default)
        {
            if (!string.IsNullOrWhiteSpace(modelPath))
                SetModelPath(modelPath);

            var deviceNumber = ResolveDeviceNumber(microphoneName);
            return StartAsync(deviceNumber, ct);
        }

        public void Stop()
        {
            object? recognizer;

            lock (_sync)
            {
                if (!_isRunning) return;
                _isRunning = false;

                if (_waveIn != null)
                {
                    try
                    {
                        _waveIn.DataAvailable -= WaveInOnDataAvailable;
                        _waveIn.RecordingStopped -= WaveInOnRecordingStopped;
                        _waveIn.StopRecording();
                    }
                    catch { /* ignore */ }

                    _waveIn.Dispose();
                    _waveIn = null;
                }

                recognizer = _voskRecognizer;
            }

            // Финальный flush вне lock
            if (recognizer != null)
            {
                try
                {
                    var finalJson = (string)Invoke(recognizer, "FinalResult")!;
                    EmitJson(finalJson);
                    EmitFinalTextFromJson(finalJson);
                }
                catch { /* ignore */ }
            }

            lock (_sync)
            {
                DisposeVoskObjects_NoLock();
            }
        }

        private void WaveInOnDataAvailable(object? sender, WaveInEventArgs e)
        {
            object? recognizer;

            lock (_sync)
            {
                if (!_isRunning) return;
                recognizer = _voskRecognizer;
            }

            if (recognizer == null) return;
            if (e.BytesRecorded <= 0) return;

            try
            {
                // bool AcceptWaveform(byte[] data, int length)
                var isFinal = (bool)Invoke(recognizer, "AcceptWaveform", e.Buffer, e.BytesRecorded)!;

                if (isFinal)
                {
                    var json = (string)Invoke(recognizer, "Result")!;
                    EmitJson(json);
                    EmitFinalTextFromJson(json);
                }
                else
                {
                    var json = (string)Invoke(recognizer, "PartialResult")!;
                    EmitJson(json);
                    EmitPartialTextFromJson(json);
                }
            }
            catch
            {
                // микрофон/драйвер/модель — пусть не валит приложение
            }
        }

        private void WaveInOnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            lock (_sync)
            {
                if (_waveIn != null)
                {
                    try
                    {
                        _waveIn.DataAvailable -= WaveInOnDataAvailable;
                        _waveIn.RecordingStopped -= WaveInOnRecordingStopped;
                    }
                    catch { /* ignore */ }

                    _waveIn.Dispose();
                    _waveIn = null;
                }

                _isRunning = false;
            }
        }

        private void CreateVoskObjectsOrThrow()
        {
            // Мы не используем "using Vosk;" и типы Vosk напрямую — чтобы не было CS0246.
            // Грузим типы по имени: "Vosk.Model" и "Vosk.VoskRecognizer".
            var modelType = FindType("Vosk.Model");
            var recognizerType = FindType("Vosk.VoskRecognizer");

            if (modelType == null || recognizerType == null)
            {
                throw new InvalidOperationException(
                    "Vosk types not found. Убедись, что NuGet пакет Vosk установлен именно в проект Mobius " +
                    "или что рядом лежит Vosk.dll. (Нужно: Vosk.Model и Vosk.VoskRecognizer).");
            }

            _voskModel = Activator.CreateInstance(modelType, _modelPath)
                        ?? throw new InvalidOperationException("Failed to create Vosk.Model instance.");

            _voskRecognizer = Activator.CreateInstance(recognizerType, _voskModel, 16000.0f)
                             ?? throw new InvalidOperationException("Failed to create VoskRecognizer instance.");

            // recognizer.SetWords(true)
            try
            {
                Invoke(_voskRecognizer, "SetWords", true);
            }
            catch
            {
                // если вдруг другая версия API — не падаем
            }
        }

        private static Type? FindType(string fullName)
        {
            // 1) Попробуем через уже загруженные сборки
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var t = asm.GetType(fullName, throwOnError: false, ignoreCase: false);
                    if (t != null) return t;
                }
                catch { /* ignore */ }
            }

            // 2) Попробуем загрузить сборку "Vosk"
            try
            {
                var asm = System.Reflection.Assembly.Load("Vosk");
                return asm.GetType(fullName, throwOnError: false, ignoreCase: false);
            }
            catch
            {
                return null;
            }
        }

        private static object? Invoke(object target, string methodName, params object[] args)
        {
            var mi = target.GetType().GetMethod(methodName);
            if (mi == null)
                throw new MissingMethodException(target.GetType().FullName, methodName);

            return mi.Invoke(target, args);
        }

        private void EmitJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            VoskJson?.Invoke(json);
        }

        // Мини-парсер без JSON либ: вытаскиваем "text" и "partial"
        private void EmitFinalTextFromJson(string json)
        {
            var text = ExtractJsonStringValue(json, "text");
            if (!string.IsNullOrWhiteSpace(text))
                FinalText?.Invoke(text);
        }

        private void EmitPartialTextFromJson(string json)
        {
            var text = ExtractJsonStringValue(json, "partial");
            if (!string.IsNullOrWhiteSpace(text))
                PartialText?.Invoke(text);
        }

        private static string? ExtractJsonStringValue(string json, string key)
        {
            try
            {
                var needle = $"\"{key}\":";
                var idx = json.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
                if (idx < 0) return null;

                idx += needle.Length;
                while (idx < json.Length && char.IsWhiteSpace(json[idx])) idx++;
                if (idx >= json.Length) return null;

                if (json[idx] != '\"') return null;
                idx++;

                var sb = new StringBuilder();
                while (idx < json.Length)
                {
                    char c = json[idx++];

                    if (c == '\\' && idx < json.Length)
                    {
                        char next = json[idx++];
                        sb.Append(next);
                        continue;
                    }

                    if (c == '\"') break;
                    sb.Append(c);
                }

                return sb.ToString();
            }
            catch
            {
                return null;
            }
        }

        private void DisposeVoskObjects_NoLock()
        {
            if (_voskRecognizer is IDisposable dRec)
            {
                try { dRec.Dispose(); } catch { /* ignore */ }
            }
            _voskRecognizer = null;

            if (_voskModel is IDisposable dModel)
            {
                try { dModel.Dispose(); } catch { /* ignore */ }
            }
            _voskModel = null;
        }

        private void EnsureNotRunning()
        {
            if (_isRunning)
                throw new InvalidOperationException("Service is running. Stop it before changing configuration.");
        }

        private void EnsureNotDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(VoskSpeechService));
        }

        /// <summary>
        /// Подбор номера устройства по имени микрофона (NAudio).
        /// Если имя пустое или не найдено — вернёт 0.
        /// </summary>
        private static int ResolveDeviceNumber(string? microphoneName)
        {
            if (string.IsNullOrWhiteSpace(microphoneName))
                return 0;

            try
            {
                for (int i = 0; i < WaveIn.DeviceCount; i++)
                {
                    var caps = WaveIn.GetCapabilities(i);
                    if (caps.ProductName != null &&
                        caps.ProductName.IndexOf(microphoneName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return i;
                    }
                }
            }
            catch
            {
                // ignore
            }

            return 0;
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            try { Stop(); } catch { /* ignore */ }
        }
    }
}
