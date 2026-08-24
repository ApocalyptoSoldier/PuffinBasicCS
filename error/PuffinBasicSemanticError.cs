using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Error
{
    public class PuffinBasicSemanticError : Exception
    {
        public enum ErrorCode
        {
            ARRAY_VARIABLE_CANNOT_STARTWITH_FN,
            SCALAR_VARIABLE_CANNOT_BE_INDEXED,
            BAD_NUMBER,
            BAD_ASSIGNMENT,
            DATA_TYPE_MISMATCH,
            INSUFFICIENT_UDF_ARGS,
            WEND_WITHOUT_WHILE,
            WHILE_WITHOUT_WEND,
            NEXT_WITHOUT_FOR,
            FOR_WITHOUT_NEXT,
            BAD_ARGUMENT,
            NOT_DEFINED,
            MISMATCHED_ENDIF,
            MISMATCHED_ELSEBEGIN,
            BAD_FUNCTION_DEF
        }

        public PuffinBasicSemanticError(ErrorCode errorCode, string line, string message) : base("[" + errorCode + "] " + message + Environment.NewLine + "LINE:" + Environment.NewLine + line)
        {
        }
    }
}

