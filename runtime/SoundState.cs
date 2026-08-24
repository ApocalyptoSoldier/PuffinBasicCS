using It.Unimi.Dsi.Fastutil.Ints;
using Org.Puffinbasic.Error;
using Javax.Sound.Sampled;
using Java.Io;
using Java.Util.Concurrent;
using Java.Util.Concurrent.Atomic;
using Org.Puffinbasic.Error.PuffinBasicRuntimeError.ErrorCode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public class SoundState : IDisposable
    {
        private sealed class ClipState : IDisposable
        {
            readonly AudioInputStream stream;
            readonly Clip clip;
            ClipState(AudioInputStream stream, Clip clip)
            {
                this.stream = stream;
                this.clip = clip;
            }

            public void Dispose()
            {
                clip.Dispose();
                stream.Dispose();
            }
        }

        private readonly ExecutorService executor;
        private readonly AtomicInteger counter;
        private readonly Dictionary<int, ClipState> state;
        SoundState()
        {
            this.executor = Executors.NewSingleThreadExecutor();
            this.counter = new AtomicInteger();
            this.state = new Int2ObjectOpenHashMap();
        }

        public virtual int Load(string file)
        {
            var future = executor.Submit(() =>
            {
                var audioFile = new File(file);
                AudioInputStream audioStream;
                try
                {
                    audioStream = AudioSystem.GetAudioInputStream(audioFile);
                }
                catch (Exception e)
                {
                    throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to load audio file: " + file + ", error: " + e.GetMessage());
                }

                var format = audioStream.GetFormat();
                var info = new Info(typeof(Clip), format);
                Clip clip;
                try
                {
                    clip = (Clip)AudioSystem.GetLine(info);
                    clip.Open(audioStream);
                }
                catch (Exception e)
                {
                    throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to get/open line from audio: " + file + ", error: " + e.GetMessage());
                }

                var id = counter.IncrementAndGet();
                state.Put(id, new ClipState(audioStream, clip));
                return id;
            });
            try
            {
                return future.Get();
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, "Failed to get id from loaded audio: " + file + ", error: " + e.GetMessage());
            }
        }

        private ClipState Get(int id)
        {
            var s = state[id];
            if (s == null)
            {
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Failed to get sound clip for id: " + id);
            }

            return s;
        }

        public virtual void Play(int id)
        {
            executor.Submit(() =>
            {
                var clip = Get(id).clip;
                if (clip.IsRunning())
                {
                    clip.Stop();
                }

                clip.SetFramePosition(0);
                clip.Start();
            });
        }

        public virtual void Stop(int id)
        {
            executor.Submit(() =>
            {
                var clip = Get(id).clip;
                if (clip.IsRunning())
                {
                    clip.Stop();
                }
            });
        }

        public virtual void Loop(int id)
        {
            executor.Submit(() =>
            {
                var clip = Get(id).clip;
                if (clip.IsRunning())
                {
                    clip.Stop();
                }

                clip.SetFramePosition(0);
                clip.Loop(Clip.LOOP_CONTINUOUSLY);
            });
        }

        public virtual void Dispose()
        {
            state.Values().ForEach((s) => s.clip.Dispose());
            executor.ShutdownNow();
        }
    }
}

