namespace Org.Puffinbasic.Error
{
    using static Org.Puffinbasic.Parser.PuffinBasicIR;

    using System;

    public class PuffinBasicRuntimeError : Exception
    {
        public enum ErrorCode
        {
            UNKNOWN,
            ARRAY_INDEX_OUT_OF_BOUNDS,
            INDEX_OUT_OF_BOUNDS,
            DIVISION_BY_ZERO,
            ILLEGAL_FUNCTION_PARAM,
            DATA_OUT_OF_RANGE,
            IO_ERROR,
            DATA_TYPE_MISMATCH,
            ILLEGAL_FILE_ACCESS,
            OUT_OF_DATA,
            GRAPHICS_ERROR,
            INTERRUPTED_ERROR,
            NOT_INITIALIZED,
            DUPLICATE_LABEL,
            BAD_FIELD,
            MISSING_STRUCT,
            BAD_FUNCTION_CALL,
            IMPORT_ERROR
        }

        private readonly ErrorCode errorCode;
        public PuffinBasicRuntimeError(ErrorCode errorCode, string message) : base($"[{errorCode}] {message}")
        {
            this.errorCode = errorCode;
        }

        public PuffinBasicRuntimeError(PuffinBasicRuntimeError cause, Instruction instruction, string line) : base($"{cause.Message}{Environment.NewLine}Line: {instruction.inputRef}{Environment.NewLine}{line}", cause)
        {
            this.errorCode = cause.errorCode;
        }

        public PuffinBasicRuntimeError(Exception cause, Instruction instruction, string line) : base($"{cause.Message}{Environment.NewLine}Line: {instruction.inputRef}{Environment.NewLine}{line}", cause)
        {
            this.errorCode = ErrorCode.UNKNOWN;
        }
    }
}

