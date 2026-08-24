using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Error
{
    public class PuffinBasicSyntaxError : Exception
    {
        public PuffinBasicSyntaxError(string message) : base(message)
        {
        }
    }
}

