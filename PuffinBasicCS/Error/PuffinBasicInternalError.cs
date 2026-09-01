namespace Org.Puffinbasic.Error
{
    using System;

    public class PuffinBasicInternalError : Exception
    {
        public PuffinBasicInternalError(string message) : base(message)
        {
        }
    }
}

