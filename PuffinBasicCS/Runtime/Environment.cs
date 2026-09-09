//using Java.Util;
namespace PuffinBasicCS.Runtime
{
    using System;
    using System.Collections.Generic;

    public interface IEnvironment
    {
        string Get(string key);
        void Set(string key, string value);

        public string this[string key] { get; set; }

        class SystemEnv : IEnvironment
        {
            private readonly Dictionary<string, string> overrides;

            string IEnvironment.this[string key] { get => Get(key) ?? "" ; set => overrides[key] = value; }

            public SystemEnv() => this.overrides = new Dictionary<string, string>();

            public virtual string? Get(string key)
            {
                if (overrides.TryGetValue(key, out string? result))
                    return result;
                else
                    return Environment.GetEnvironmentVariable(key);
            }

            public virtual void Set(string key, string value) => overrides.Add(key, value);
        }
    }
}

