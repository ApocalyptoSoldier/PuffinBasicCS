//using It.Unimi.Dsi.Fastutil.Bytes;
using Org.Puffinbasic.File;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Runtime
{
    public class PrintBuffer
    {
        private static readonly byte SPACE = (byte)' ';
        private readonly List<byte> buffer;
        private int cursor;
        public PrintBuffer()
        {
            this.buffer = new List<byte>();
        }

        public virtual void AppendAtCursor(string value)
        {
            for (int i = buffer.Count; i < cursor + value.Length; i++)
            {
                buffer.Add(SPACE);
            }

            for (int i = 0; i < value.Length; i++)
            {
                buffer[cursor++] = (byte)value[i];
            }
        }

        public virtual void Flush(IPuffinBasicFile file)
        {
            for (int i = 0; i < buffer.Count; i++)
            {
                file.WriteByte(buffer.ElementAt(i));
            }

            for (int i = 0; i < buffer.Count; i++)
            {
                buffer[i] = SPACE;
            }

            buffer.Clear();
            cursor = 0;
        }
    }
}

