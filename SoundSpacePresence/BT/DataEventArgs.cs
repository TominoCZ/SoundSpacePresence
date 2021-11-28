using System;

namespace SoundSpacePresence
{
    public class DataEventArgs : EventArgs
    {
        public string Data { get; }

        public DataEventArgs(string data)
        {
            Data = data;
        }
    }
}