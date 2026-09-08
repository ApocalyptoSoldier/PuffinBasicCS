namespace PuffinBasicCS.Error
{
    using System;

    public class PuffinBasicInternalError : Exception
    {
        public PuffinBasicInternalError(string message) : base(message)
        {
        }
    }
}

