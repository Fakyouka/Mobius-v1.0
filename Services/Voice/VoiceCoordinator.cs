using System;
using System.Linq;
using System.Text;
using Mobius.ViewModels;

namespace Mobius.Services.Voice
{
    public sealed class VoiceCoordinator
    {
        private readonly MainViewModel _root;
        private readonly VoskSpeechService _speech = new VoskSpeechService();

        public VoiceCoordinator(MainViewModel root)
        {
            _root = root;

            _speech.PartialText += t =>
            {
                _root.Library.LastHeardPartial = t;
                _root.Library.AddLog("partial: " + t);
            };

            _speech.FinalText += t =>
            {
                _root.Library.LastHeardFinal = t;
                _root.Library.AddLog("final: " + t);
                TryMatchAndLaunch(t);
            };
        }

        public void UpdateState()
        {
            var lib = _root.Library;
            var set = _root.Settings;

            if (!lib.SpeechMasterEnabled)
            {
                _speech.Stop();
                return;
            }

            if (!_speech.IsRunning)
            {
                try
                {
                    _speech.Start(set.VoskModelPath, set.SelectedMicrophoneName);
                    lib.AddLog("Vosk started");
                }
                catch (Exception ex)
                {
                    lib.AddLog("Vosk start error: " + ex.Message);
                }
            }
        }

        public void Stop() => _speech.Stop();

        private void TryMatchAndLaunch(string recognized)
        {
            var lib = _root.Library;
            if (string.IsNullOrWhiteSpace(recognized)) return;
            if (!lib.SpeechMasterEnabled) return;

            var normText = Normalize(recognized);

            foreach (var app in lib.Apps.ToList())
            {
                if (!app.SpeechEnabled) continue;

                foreach (var phrase in app.Phrases.ToList())
                {
                    var p = Normalize(phrase?.Text);
                    if (string.IsNullOrWhiteSpace(p)) continue;

                    if (ContainsPhrase(normText, p))
                    {
                        lib.AddLog($"MATCH [{app.Name}] phrase='{p}'");
                        lib.LaunchFromVoice(app);
                        return;
                    }
                }
            }
        }

        private static bool ContainsPhrase(string text, string phrase)
        {
            if (phrase.Contains(" "))
                return text.Contains(phrase);

            int idx = text.IndexOf(phrase, StringComparison.Ordinal);
            while (idx >= 0)
            {
                bool leftOk = idx == 0 || !IsWordChar(text[idx - 1]);
                int right = idx + phrase.Length;
                bool rightOk = right >= text.Length || !IsWordChar(text[right]);

                if (leftOk && rightOk) return true;
                idx = text.IndexOf(phrase, idx + 1, StringComparison.Ordinal);
            }
            return false;
        }

        private static bool IsWordChar(char c) => char.IsLetterOrDigit(c) || c == '_';

        private static string Normalize(string s)
        {
            if (string.IsNullOrWhiteSpace(s)) return "";
            s = s.Trim().ToLowerInvariant();

            var sb = new StringBuilder(s.Length);
            bool prevSpace = false;

            foreach (var ch in s)
            {
                if (char.IsLetterOrDigit(ch) || ch == '_')
                {
                    sb.Append(ch);
                    prevSpace = false;
                }
                else
                {
                    if (!prevSpace)
                    {
                        sb.Append(' ');
                        prevSpace = true;
                    }
                }
            }
            return sb.ToString().Trim();
        }
    }
}
