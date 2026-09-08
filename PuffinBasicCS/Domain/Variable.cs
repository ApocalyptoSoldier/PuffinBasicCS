//using Com.Google.Common.Base;
//using Org.Jetbrains.Annotations;
namespace PuffinBasicCS.Domain
{
    using static PuffinBasicCS.Domain.STObjects;
    //using Java.Util;
    //using Java.Util.Function;
    using static PuffinBasicCS.Domain.STObjects.PuffinBasicTypeId;

    using System;

    using PuffinBasicCS.Error;

    public class Variable
    {
        public sealed class VariableName
        {
            internal readonly string varname;
            internal readonly string suffix;
            internal readonly PuffinBasicAtomTypeId dataType;
            public VariableName(string varname, string? suffix, PuffinBasicAtomTypeId? dataType)
            {
                ArgumentNullException.ThrowIfNull(varname);
                if (dataType == null) throw new ArgumentNullException(nameof(dataType));
                this.varname = varname;
                this.suffix = suffix ?? "";
                this.dataType = dataType.Value;
            }

            public string GetVarname() => varname;

            public PuffinBasicAtomTypeId DataType => dataType;

            public new string ToString() => varname + ":" + suffix + ":" + dataType;

            public override bool Equals(object? o)
            {
                if (this == o)
                    return true;
                if (o == null || o is not VariableName) 
                    return false;
                VariableName other = (VariableName)o;
                return this.varname == other.varname && this.suffix == other.suffix;
            }

            public override int GetHashCode() => HashCode.Combine(varname, suffix);
        }

        private static readonly string UDF_PREFIX = "FN";
        public enum VariableKindHint
        {
            ARRAY,
            DERIVE_FROM_NAME,
            UDF
        }

        public static Variable Of(VariableName variableName, VariableKindHint hint, string line)
        {
            if (hint == VariableKindHint.ARRAY)
            {
                if (!variableName.varname.StartsWith(UDF_PREFIX))
                {
                    return new Variable(variableName, new ArrayType(variableName.DataType));
                }
                else
                {
                    throw new PuffinBasicSemanticError(PuffinBasicSemanticError.ErrorCode.ARRAY_VARIABLE_CANNOT_STARTWITH_FN, line, "Array variable cannot start with " + UDF_PREFIX + ": " + variableName.varname);
                }
            }
            else
            {
                if ((hint == VariableKindHint.DERIVE_FROM_NAME && variableName.varname.StartsWith(UDF_PREFIX)) || hint == VariableKindHint.UDF)
                {
                    return new Variable(variableName, new UDFType(variableName.DataType));
                }
                else
                {
                    return new Variable(variableName, new ScalarType(variableName.DataType));
                }
            }
        }

        private readonly VariableName variableName;
        private readonly PuffinBasicType type;
        public Variable(VariableName variableName, STObjects.PuffinBasicType type)
        {
            if (type == null) throw new ArgumentNullException(nameof(type));
            if (variableName == null) throw new ArgumentNullException(nameof(variableName));
            this.variableName = variableName;
            this.type = type;
        }

        public virtual VariableName GetVariableName() => variableName;

        public virtual PuffinBasicType Type => type;

        public virtual bool IsScalar() => type.TypeId == SCALAR;

        public virtual bool IsArray() => type.TypeId == ARRAY;

        public virtual bool IsUDF() => type.TypeId == UDF;

        public override string ToString() => variableName + ":" + type.TypeId;

        public override bool Equals(object? o)
        {
            if (this == o)
                return true;
            if (o == null || !(o is Variable))
                return false;

            Variable other = (Variable)o;

            return this.variableName.Equals(other.variableName) 
                && this.type.TypeId == other.type.TypeId
                && this.type.AtomTypeId == other.type.AtomTypeId;

        }

        public override int GetHashCode() => HashCode.Combine(variableName, type);
    }
}

