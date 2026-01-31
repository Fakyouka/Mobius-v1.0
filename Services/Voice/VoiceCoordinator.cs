using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Mobius.Services.Voice
{
    public class VoiceCoordinator
    {
        public event EventHandler StateChanged;

        public bool IsRunning { get; private set; }

        // Здесь должен быть твой реальный движок
        // Я оставил общую структуру: Start/Stop всегда обновляют IsRunning и вызывают StateChanged

        public async Task StartAsync()
        {
            try
            {
                // TODO: твой старт распознавания
                await Task.Delay(10);

                IsRunning = true;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[VOICE] StartAsync ERROR: " + ex);
                IsRunning = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public async Task StopAsync()
        {
            try
            {
                // TODO: твой стоп распознавания
                await Task.Delay(10);

                IsRunning = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                Debug.WriteLine("[VOICE] StopAsync ERROR: " + ex);
                IsRunning = false;
                StateChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }
}
