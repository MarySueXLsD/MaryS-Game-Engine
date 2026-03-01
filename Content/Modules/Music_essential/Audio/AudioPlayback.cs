namespace MarySGameEngine.Modules.Music_essential.Audio
{
    using System;
    using System.IO;
    using NAudio.Wave;
    using NAudio.Wave.SampleProviders;

    public class AudioPlayback : IDisposable
    {
        public event EventHandler<FftEventArgs> FftCalculated;
        public event EventHandler<MaxSampleEventArgs> MaximumCalculated;

        private IWavePlayer playbackDevice;
        private WaveStream fileStream;
        private IDisposable _streamOwner;
        private readonly int fftLength;
        private float _volume = 1f;

        public AudioPlayback(int fftLength = 8192)
        {
            this.fftLength = fftLength;
        }

        public void Load(string fileName)
        {
            Stop();
            CloseFile();
            EnsureDeviceCreated();
            OpenFile(fileName);
        }

        /// <summary>Load from a stream (e.g. HTTP). Playback can start immediately while data streams. Caller gives ownership of streamOwner; it is disposed when Stop/Load/Dispose is called.</summary>
        public void Load(Stream stream, IDisposable streamOwner)
        {
            Stop();
            CloseFile();
            if (stream == null) return;
            try
            {
                _streamOwner = streamOwner;
                EnsureDeviceCreated();
                OpenStream(stream);
            }
            catch (Exception)
            {
                _streamOwner?.Dispose();
                _streamOwner = null;
                throw;
            }
        }

        protected virtual void OnFftCalculated(FftEventArgs e) => FftCalculated?.Invoke(this, e);
        protected virtual void OnMaximumCalculated(MaxSampleEventArgs e) => MaximumCalculated?.Invoke(this, e);

        private void CloseFile()
        {
            if (fileStream != null)
            {
                fileStream.Dispose();
                fileStream = null;
            }
            if (_streamOwner != null)
            {
                _streamOwner.Dispose();
                _streamOwner = null;
            }
        }

        private void OpenFile(string fileName)
        {
            try
            {
                var inputStream = new AudioFileReader(fileName);
                fileStream = inputStream;
                var aggregator = new SampleAggregator(inputStream, fftLength)
                {
                    NotificationCount = inputStream.WaveFormat.SampleRate / 100,
                    PerformFFT = true
                };
                aggregator.FftCalculated += (s, a) => OnFftCalculated(a);
                aggregator.MaximumCalculated += (s, a) => OnMaximumCalculated(a);
                playbackDevice.Init(aggregator);
            }
            catch (Exception)
            {
                CloseFile();
                throw;
            }
        }

        private void OpenStream(Stream stream)
        {
            try
            {
                var readFully = new ReadFullyStream(stream);
                var mp3Reader = new Mp3FileReader(readFully);
                fileStream = mp3Reader;
                var sampleProvider = new Pcm16BitToSampleProvider(mp3Reader);
                var aggregator = new SampleAggregator(sampleProvider, fftLength)
                {
                    NotificationCount = mp3Reader.WaveFormat.SampleRate / 100,
                    PerformFFT = true
                };
                aggregator.FftCalculated += (s, a) => OnFftCalculated(a);
                aggregator.MaximumCalculated += (s, a) => OnMaximumCalculated(a);
                playbackDevice.Init(aggregator);
            }
            catch (Exception)
            {
                CloseFile();
                throw;
            }
        }

        private void EnsureDeviceCreated()
        {
            if (playbackDevice == null)
            {
                var wo = new WaveOut { DesiredLatency = 200 };
                wo.Volume = _volume;
                playbackDevice = wo;
            }
        }

        /// <summary>Volume 0.0 to 1.0.</summary>
        public float Volume
        {
            get => _volume;
            set
            {
                _volume = value < 0f ? 0f : value > 1f ? 1f : value;
                if (playbackDevice is WaveOut wo)
                    wo.Volume = _volume;
            }
        }

        public void Play()
        {
            if (playbackDevice != null && fileStream != null && playbackDevice.PlaybackState != PlaybackState.Playing)
                playbackDevice.Play();
        }

        public void Pause()
        {
            if (playbackDevice != null)
                playbackDevice.Pause();
        }

        public void Stop()
        {
            if (playbackDevice != null)
                playbackDevice.Stop();
            if (fileStream != null)
                fileStream.Position = 0;
        }

        public PlaybackState PlaybackState => playbackDevice?.PlaybackState ?? PlaybackState.Stopped;

        /// <summary>Current playback position.</summary>
        public TimeSpan CurrentTime
        {
            get
            {
                if (fileStream == null) return TimeSpan.Zero;
                try
                {
                    return TimeSpan.FromSeconds((double)fileStream.Position / fileStream.WaveFormat.AverageBytesPerSecond);
                }
                catch { return TimeSpan.Zero; }
            }
        }

        /// <summary>Total duration of the loaded file.</summary>
        public TimeSpan TotalTime
        {
            get
            {
                if (fileStream == null) return TimeSpan.Zero;
                try
                {
                    if (!fileStream.CanSeek || fileStream.Length <= 0) return TimeSpan.Zero;
                    return TimeSpan.FromSeconds((double)fileStream.Length / fileStream.WaveFormat.AverageBytesPerSecond);
                }
                catch { return TimeSpan.Zero; }
            }
        }

        /// <summary>Progress 0..1 for progress bar.</summary>
        public float Progress
        {
            get
            {
                if (fileStream == null) return 0f;
                try
                {
                    if (!fileStream.CanSeek || fileStream.Length <= 0) return 0f;
                    return (float)fileStream.Position / fileStream.Length;
                }
                catch { return 0f; }
            }
        }

        /// <summary>Seek to position by progress 0..1. Call from main thread. No-op for non-seekable streams (e.g. HTTP).</summary>
        public void SeekTo(float progress)
        {
            if (fileStream == null || !fileStream.CanSeek || fileStream.Length <= 0) return;
            progress = Math.Max(0f, Math.Min(1f, progress));
            try
            {
                long pos = (long)(progress * fileStream.Length);
                fileStream.Position = pos;
            }
            catch { /* ignore */ }
        }

        public void Dispose()
        {
            Stop();
            CloseFile();
            if (playbackDevice != null)
            {
                playbackDevice.Dispose();
                playbackDevice = null;
            }
        }
    }
}
