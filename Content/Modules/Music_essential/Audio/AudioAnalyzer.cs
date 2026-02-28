namespace MarySGameEngine.Modules.Music_essential.Audio
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using Microsoft.Xna.Framework;
    using NAudio.Dsp;

    public struct ComplexValue
    {
        public readonly float X;
        public readonly float Y;
        public ComplexValue(float x, float y) { X = x; Y = y; }
    }

    public struct AudioSample
    {
        public readonly float MaxSample;
        public readonly float MinSample;
        public AudioSample(float maxSample, float minSample) { MaxSample = maxSample; MinSample = minSample; }
    }

    public struct AnalyzedAudio
    {
        public readonly ComplexValue[] FFT;
        public readonly ComplexValue[] SmoothFFT;
        public readonly AudioSample[] Samples;

        public AnalyzedAudio(ComplexValue[] fft, ComplexValue[] smoothFft, AudioSample[] samples)
        {
            FFT = fft;
            SmoothFFT = smoothFft;
            Samples = samples;
        }
    }

    public class AudioAnalyzer : IDisposable
    {
        public AnalyzedAudio CurrentAnalyzedAudio { get; private set; }
        public readonly int SamplesHistory;
        public float FFTSmoothness;
        public TimeSpan ThreadTargetElapsedTime;

        private readonly AudioPlayback audioPlayback;
        private readonly Thread thread;
        private readonly object fftLock = new object();
        private bool threadRunning;
        private ConcurrentBag<AudioSample> lastSamples;
        private Complex[] lastFFT;
        private ComplexValue[] lastFFT2;
        private ComplexValue[] smoothFFT;

        public AudioAnalyzer(AudioPlayback audioPlayback, int samplesHistory = 3, float ffTSmoothness = 0.25f)
        {
            SamplesHistory = samplesHistory;
            FFTSmoothness = ffTSmoothness;
            ThreadTargetElapsedTime = TimeSpan.FromSeconds(1.0 / 60);
            CurrentAnalyzedAudio = new AnalyzedAudio(Array.Empty<ComplexValue>(), Array.Empty<ComplexValue>(), Array.Empty<AudioSample>());
            lastSamples = new ConcurrentBag<AudioSample>();
            this.audioPlayback = audioPlayback;
            this.audioPlayback.MaximumCalculated += OnMaximumCalculated;
            this.audioPlayback.FftCalculated += OnFftCalculated;
            threadRunning = true;
            thread = new Thread(ThreadRun);
            thread.Start();
        }

        private void OnMaximumCalculated(object sender, MaxSampleEventArgs e) => lastSamples.Add(new AudioSample(e.MaxSample, e.MinSample));

        private void OnFftCalculated(object sender, FftEventArgs e)
        {
            var result = e.Result;
            if (result == null || result.Length == 0) return;
            var copy = new Complex[result.Length];
            Array.Copy(result, copy, result.Length);
            lock (fftLock)
            {
                lastFFT = copy;
            }
        }

        private void ThreadRun()
        {
            var stopwatch = Stopwatch.StartNew();
            while (threadRunning)
            {
                if (stopwatch.ElapsedMilliseconds > ThreadTargetElapsedTime.TotalMilliseconds)
                {
                    stopwatch.Restart();

                    Complex[] fftSnapshot = null;
                    lock (fftLock)
                    {
                        if (lastFFT != null)
                        {
                            fftSnapshot = lastFFT;
                            lastFFT = null; // take ownership so next callback can set fresh data
                        }
                    }

                    if (fftSnapshot != null)
                    {
                        if (smoothFFT == null || smoothFFT.Length != fftSnapshot.Length)
                            smoothFFT = new ComplexValue[fftSnapshot.Length];
                        if (lastFFT2 == null || lastFFT2.Length != fftSnapshot.Length)
                            lastFFT2 = new ComplexValue[fftSnapshot.Length];

                        for (int i = 0; i < fftSnapshot.Length; i++)
                        {
                            float smoothX = MathHelper.Lerp(smoothFFT[i].X, fftSnapshot[i].X, FFTSmoothness);
                            float smoothY = MathHelper.Lerp(smoothFFT[i].Y, fftSnapshot[i].Y, FFTSmoothness);
                            smoothFFT[i] = new ComplexValue(smoothX, smoothY);
                            lastFFT2[i] = new ComplexValue(fftSnapshot[i].X, fftSnapshot[i].Y);
                        }
                    }

                    var samples = new List<AudioSample>(CurrentAnalyzedAudio.Samples);
                    samples.InsertRange(0, lastSamples);
                    var newBag = new ConcurrentBag<AudioSample>();
                    Interlocked.Exchange(ref lastSamples, newBag);
                    var sampleArr = samples.Take(SamplesHistory).ToArray();

                    // Pass copies to AnalyzedAudio so the main thread always sees a consistent snapshot
                    ComplexValue[] fftCopy = null;
                    ComplexValue[] smoothCopy = null;
                    if (lastFFT2 != null)
                    {
                        fftCopy = (ComplexValue[])lastFFT2.Clone();
                        smoothCopy = (ComplexValue[])smoothFFT.Clone();
                    }
                    CurrentAnalyzedAudio = new AnalyzedAudio(
                        fftCopy ?? Array.Empty<ComplexValue>(),
                        smoothCopy ?? Array.Empty<ComplexValue>(),
                        sampleArr);
                }
            }
        }

        public void Dispose()
        {
            threadRunning = false;
            thread.Join();
            audioPlayback.MaximumCalculated -= OnMaximumCalculated;
            audioPlayback.FftCalculated -= OnFftCalculated;
        }
    }
}
