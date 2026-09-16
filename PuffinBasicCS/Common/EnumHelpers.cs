namespace PuffinBasicCS.Common
{
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Reflection;

    internal static class EnumHelpers
    {
        public static Dictionary<TEnum, string> GetEnumLabels<TEnum>() where TEnum : struct
        {
            Type t = typeof(TEnum);

            var dict = new Dictionary<TEnum, string>();

            foreach (TEnum val  in Enum.GetValues(t)) {
                #pragma warning disable CS8604 // Possible null reference argument.
                var memInfo = t.GetMember(val.ToString());

                dict.Add(val, memInfo[0].GetCustomAttribute<DescriptionAttribute>()?.Description ?? val.ToString());
                #pragma warning restore CS8604 // Possible null reference argument.
            }

            return dict;
        }
    }
}
