//using It.Unimi.Dsi.Fastutil.Ints;
namespace PuffinBasicCS.Runtime
{
    using PuffinBasicCS.Error;
    using static PuffinBasicCS.Error.PuffinBasicRuntimeError.ErrorCode;
    //using Javax.Sound.Sampled;
    //using Java.Io;
    //using Java.Util.Concurrent;
    //using Java.Util.Concurrent.Atomic;
    using System;
    using System.Media;

    using System.Collections.Concurrent;

    public class SoundState : IDisposable
    {
        private int counter = 0;
        private readonly ConcurrentDictionary<int, SoundPlayer> state = new ConcurrentDictionary<int, SoundPlayer>();

        private SoundPlayer Get(int id)
        {
            if (!state.TryGetValue(id, out SoundPlayer soundPlayer))
                throw new PuffinBasicRuntimeError(ILLEGAL_FUNCTION_PARAM, "Failed to get sound clip for id: " + id);

            return soundPlayer;
        }

        public virtual int Load(string file)
        {
            FileStream audioStream = null;
            try
            {
                audioStream = System.IO.File.OpenRead(file);
            }
            catch (Exception e)
            {
                throw new PuffinBasicRuntimeError(IO_ERROR, $"Failed to load audio file {file}, error: {e.Message}");
            }

            SoundPlayer soundPlayer = new SoundPlayer(file);

            var id = Interlocked.Increment(ref counter);
            state.TryAdd(id, soundPlayer);

            return id;
        }

        public virtual void Play(int id)
        {
            var soundPlayer = Get(id);

            soundPlayer.Stop();
            soundPlayer.Play();
        }

        public virtual void Stop(int id)
        {
            var soundPlayer = Get(id);
            soundPlayer.Stop();
        }

        public virtual void Loop(int id)
        {
            var soundPlayer = Get(id);

            soundPlayer.Stop();
            soundPlayer.PlayLooping();
        }

        public virtual void Dispose()
        {
            foreach (var soundPlayer in state.Values)
                soundPlayer.Dispose();
        }
    }
}

