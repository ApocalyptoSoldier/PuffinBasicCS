namespace Org.Puffinbasic.Error
{
    using System;

    public class PuffinBasicSyntaxError : Exception
    {
        public PuffinBasicSyntaxError(string message) : base(message)
        {
        }
    }
}

