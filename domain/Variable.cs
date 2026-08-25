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
using System.Runtime.Remoting.Messaging;

namespace Org.Puffinbasic.Domain
{
    public class Variable
    {
        public sealed class VariableName
        {
            internal readonly string varname;
            internal readonly string suffix;
            internal readonly PuffinBasicAtomTypeId dataType;
            public VariableName(string varname, string suffix, STObjects.PuffinBasicAtomTypeId dataType)
            {
                if (varname == null) throw new ArgumentNullException(nameof(varname));
                if (dataType == null) throw new ArgumentNullException(nameof(dataType));
                this.varname = varname;
                this.suffix = suffix == null ? "" : suffix;
                this.dataType = dataType;
            }

            public string GetVarname() => varname;

            public PuffinBasicAtomTypeId GetDataType() => dataType;

            public new string ToString() => varname + ":" + suffix + ":" + dataType;

            public bool Equals(object o)
            {
                if (this == o)
                    return true;
                if (o == null || GetType() != o.GetType())
                    return false;
                VariableName variable = (VariableName)o;
                return varname.Equals(variable.varname) && suffix == variable.suffix;
            }

            public int GetHashCode()
            {
                int hash = 17;
                hash = hash * 23 + varname.GetHashCode();
                hash = hash * 23 + suffix.GetHashCode();
                return hash;
            }
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
                    return new Variable(variableName, new ArrayType(variableName.GetDataType()));
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
                    return new Variable(variableName, new UDFType(variableName.GetDataType()));
                }
                else
                {
                    return new Variable(variableName, new ScalarType(variableName.GetDataType()));
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

        public virtual PuffinBasicType GetType() => type;

        public virtual bool IsScalar() => type.GetTypeId() == SCALAR;

        public virtual bool IsArray() => type.GetTypeId() == ARRAY;

        public virtual bool IsUDF() => type.GetTypeId() == UDF;

        public virtual string ToString() => variableName + ":" + type.GetTypeId();

        public virtual bool Equals(Variable o)
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
            int hash = 17;
            hash = hash * 23 + variableName.GetHashCode();
            hash = hash * 23 + type.GetHashCode();
            return hash;
        }
    }
}

