//using Java.Util;
namespace Org.Puffinbasic.Runtime
{
    using System;
    using System.Collections.Generic;

    public interface IEnvironment
    {
        string Get(string key);
        void Set(string key, string value);

        public string this[string key] { get; }

        class SystemEnv : IEnvironment
        {
            private readonly Dictionary<string, string> overrides;
            public SystemEnv() => this.overrides = new Dictionary<string, string>();

            public virtual string Get(string key)
            {
                if (overrides.TryGetValue(key, out string result))
                    return result;
                else
                    return Environment.GetEnvironmentVariable(key);
            }

            public string this[string key] => Get(key);

            public virtual void Set(string key, string value) => overrides.Add(key, value);
        }
    }
}

