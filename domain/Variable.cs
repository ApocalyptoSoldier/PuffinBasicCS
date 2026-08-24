//using Com.Google.Common.Base;
//using Org.Jetbrains.Annotations;
using static Org.Puffinbasic.Domain.STObjects;
using Org.Puffinbasic.Error;
//using Java.Util;
//using Java.Util.Function;
using static Org.Puffinbasic.Domain.STObjects.PuffinBasicTypeId;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;

namespace Org.Puffinbasic.Domain
{
    public class Variable
    {
        public sealed class VariableName
        {
            private readonly string varname;
            private readonly string suffix;
            private readonly PuffinBasicAtomTypeId dataType;
            public VariableName(string varname, string suffix, STObjects.PuffinBasicAtomTypeId dataType)
            {
                this.varname = Preconditions.CheckNotNull(varname);
                this.suffix = suffix == null ? "" : suffix;
                this.dataType = Preconditions.CheckNotNull(dataType);
            }

            public string GetVarname()
            {
                return varname;
            }

            public PuffinBasicAtomTypeId GetDataType()
            {
                return dataType;
            }

            public string ToString()
            {
                return varname + ":" + suffix + ":" + dataType;
            }

            public bool Equals(object o)
            {
                if (this == o)
                    return true;
                if (o == null || GetType() != o.GetType())
                    return false;
                VariableName variable = (VariableName)o;
                return varname.Equals(variable.varname) && Objects.Equals(suffix, variable.suffix);
            }

            public int GetHashCode()
            {
                return Objects.Hash(varname, suffix);
            }
        }

        private static readonly string UDF_PREFIX = "FN";
        public enum VariableKindHint
        {
            ARRAY,
            DERIVE_FROM_NAME,
            UDF
        }

        public static Variable Of(VariableName variableName, VariableKindHint hint, Supplier<string> lineSupplier)
        {
            if (hint == VariableKindHint.ARRAY)
            {
                if (!variableName.varname.StartsWith(UDF_PREFIX))
                {
                    return new Variable(variableName, new ArrayType(variableName.GetDataType()));
                }
                else
                {
                    throw new PuffinBasicSemanticError(PuffinBasicSemanticError.ErrorCode.ARRAY_VARIABLE_CANNOT_STARTWITH_FN, lineSupplier.Get(), "Array variable cannot start with " + UDF_PREFIX + ": " + variableName.varname);
                }
            }
            else
            {
                if ((hint == VariableKindHint.DERIVE_FROM_NAME && variableName.varname.StartsWith(UDF_PREFIX)) || hint == VariableKindHint.UDF)
                {
                    return new Variable(variableName, new UDFType(variableName.GetDataType()));
                }
                else
                {
                    return new Variable(variableName, new ScalarType(variableName.GetDataType()));
                }
            }
        }

        private readonly VariableName variableName;
        private readonly IPuffinBasicType type;
        public Variable(VariableName variableName, STObjects.PuffinBasicType type)
        {
            this.variableName = Preconditions.CheckNotNull(variableName);
            this.type = Preconditions.CheckNotNull(type);
        }

        public virtual VariableName GetVariableName()
        {
            return variableName;
        }

        public virtual IPuffinBasicType GetType()
        {
            return type;
        }

        public virtual bool IsScalar()
        {
            return type.GetTypeId() == SCALAR;
        }

        public virtual bool IsArray()
        {
            return type.GetTypeId() == ARRAY;
        }

        public virtual bool IsUDF()
        {
            return type.GetTypeId() == UDF;
        }

        public virtual string ToString()
        {
            return variableName + ":" + type.GetTypeId();
        }

        public virtual bool Equals(object o)
        {
            if (this == o)
                return true;
            if (o == null || GetType() != o.GetType())
                return false;
            Variable variable = (Variable)o;
            return variableName.Equals(variable.variableName) && type.Equals(variable.type);
        }

        public virtual int GetHashCode()
        {
            return Objects.Hash(variableName, type);
        }
    }
}

