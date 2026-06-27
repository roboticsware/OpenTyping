using System;
using System.IO;
using NAudio.Wave;
using OpenTyping.Properties;

namespace OpenTyping
{
    public static class SfxManager
    {
        private static MemoryStream pressedStream;
        private static WaveFileReader pressedReader;
        private static WaveOutEvent pressedPlayer;

        private static MemoryStream bellStream;
        private static WaveFileReader bellReader;
        private static WaveOutEvent bellPlayer;

        static SfxManager()
        {
            try
            {
                // Init Pressed Player
                using (Stream stream = OpenTyping.Properties.Resources.Pressed)
                {
                    if (stream != null)
                    {
                        var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        ms.Position = 0;
                        pressedStream = ms;
                        pressedReader = new WaveFileReader(pressedStream);

                        pressedPlayer = new WaveOutEvent();
                        pressedPlayer.Init(pressedReader);
                    }
                }

                // Init Bell Player
                using (Stream stream = OpenTyping.Properties.Resources.Bell)
                {
                    if (stream != null)
                    {
                        var ms = new MemoryStream();
                        stream.CopyTo(ms);
                        ms.Position = 0;
                        bellStream = ms;
                        bellReader = new WaveFileReader(bellStream);

                        bellPlayer = new WaveOutEvent();
                        bellPlayer.Init(bellReader);
                    }
                }
            }
            catch (Exception ex)
            {
                App.logger.Error("SfxManager initialization failed: " + ex.Message);
            }
        }

        public static void PlayPressed(Volume volume)
        {
            if (volume == Volume.Off || pressedPlayer == null || pressedReader == null) return;
            PlaySfx(pressedPlayer, pressedReader, volume);
        }

        public static void PlayBell(Volume volume)
        {
            if (volume == Volume.Off || bellPlayer == null || bellReader == null) return;
            PlaySfx(bellPlayer, bellReader, volume);
        }

        private static void PlaySfx(WaveOutEvent player, WaveFileReader reader, Volume volume)
        {
            try
            {
                float volumeFloat = (volume == Volume.Down) ? 0.5f : 1.0f;
                player.Volume = volumeFloat;

                if (player.PlaybackState == PlaybackState.Playing)
                {
                    reader.Position = 0;
                }
                else
                {
                    reader.Position = 0;
                    player.Play();
                }
            }
            catch (Exception ex)
            {
                App.logger.Error("SfxManager PlaySfx failed: " + ex.Message);
                try { player.Stop(); reader.Position = 0; player.Play(); } catch { }
            }
        }
    }
}
